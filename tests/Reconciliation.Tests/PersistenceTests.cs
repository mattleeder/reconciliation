using Microsoft.EntityFrameworkCore;
using Reconciliation.Data;
using Microsoft.Data.SqlClient;
using Reconciliation.Domain.Entities;
using Reconciliation.Domain.Matching;

namespace Reconciliation.Tests;

[Collection("Database")]
public class PersistenceTests : IAsyncLifetime
{

    private static ReconciliationRun NewRun()
    {
        return new ReconciliationRun
        {
            Id = 0,
            CreatedUtc = DateTime.UtcNow,
        };
    }

    private static Transaction Tx(TransactionSource source, string date, decimal amount, ReconciliationRun run, string description = "", int id = 0)
    {
        return new Transaction
        {
            Id = id,
            Source = source,
            Date = DateOnly.Parse(date),
            Amount = amount,
            Description = description,
            Run = run,
        };
    }
    
    private static ReconciliationDbContext CreateDbContext()
    {
        var connectionString = Environment.GetEnvironmentVariable("RECONCILIATION_CONNECTION");

        if (string.IsNullOrEmpty(connectionString))
        {
            Assert.Fail("Missing environment variable 'RECONCILIATION_CONNECTION'.");
        }

        SqlConnectionStringBuilder connectionStringBuilder = new(connectionString)
        {
            InitialCatalog = "Reconciliation_Tests"
        };

        var optionsBuilder = new DbContextOptionsBuilder<ReconciliationDbContext>();
        optionsBuilder.UseSqlServer(connectionStringBuilder.ConnectionString);

        return new ReconciliationDbContext(optionsBuilder.Options);
    }

    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }

    public async Task InitializeAsync()
    {
        await using var dbContext = CreateDbContext();
        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.MigrateAsync();
    }

    [Fact]
    public async Task SaveAndLoad_EngineMatch_RoundTripsWithTransactions()
    {
        var run = NewRun();
        var bank = new List<Transaction> { Tx(TransactionSource.Bank, "2026-01-01", 10m, run, "Local Shop Bank") };
        var ledger = new List<Transaction> { Tx(TransactionSource.Ledger, "2026-01-01", 10m, run, "Local Shop Ledger") };

        var result = MatchingEngine.Run(bank, ledger);

        await using var actContext = CreateDbContext();
        actContext.Matches.AddRange(result.Matches);
        await actContext.SaveChangesAsync();

        await using var assertContext = CreateDbContext();
        var query = await assertContext.Matches.Include(m => m.LeftTransaction).Include(m => m.RightTransaction).ToListAsync();
        var match = Assert.Single(query);
        Assert.Equal(MatchKind.ExactAmountAndDate, match.Kind);
        Assert.NotNull(match.LeftTransaction);
        Assert.NotNull(match.RightTransaction);
        Assert.Equal(TransactionSource.Bank, match.LeftTransaction.Source);
        Assert.Equal(TransactionSource.Ledger, match.RightTransaction.Source);
        Assert.InRange(match.LeftTransaction.Id, 1, int.MaxValue);
        Assert.InRange(match.RightTransaction.Id, 1, int.MaxValue);
        Assert.Equal(10m, match.LeftTransaction.Amount);
        Assert.Equal(10m, match.RightTransaction.Amount);
        Assert.Equal("Local Shop Bank", match.LeftTransaction.Description);
        Assert.Equal("Local Shop Ledger", match.RightTransaction.Description);
    }

    [Fact]
    public async Task SaveAndLoad_ReviewDecision_RoundTripsAndIsUniquePerMatch()
    {
        var run = NewRun();
        var bank = new List<Transaction> { Tx(TransactionSource.Bank, "2026-01-01", 10m, run, "Local Shop Bank") };
        var ledger = new List<Transaction> { Tx(TransactionSource.Ledger, "2026-01-01", 10m, run, "Local Shop Ledger") };
        var result = MatchingEngine.Run(bank, ledger);

        await using var saveMatchContext = CreateDbContext();
        saveMatchContext.Matches.AddRange(result.Matches);
        await saveMatchContext.SaveChangesAsync();
        var matchId = result.Matches[0].Id;

        await using var decideContext = CreateDbContext();
        var decideMatch = await decideContext.Matches.SingleAsync(m => m.Id == matchId);
        decideMatch.Decision = new ReviewDecision
        {
            Outcome = ReviewOutcome.Approved,
            DecidedAtUtc = DateTime.UtcNow,
            Note = "Looks right"
        };
        await decideContext.SaveChangesAsync();

        await using var assertContext = CreateDbContext();
        var assertMatch = await assertContext.Matches
            .Include(m => m.Decision)
            .SingleAsync(m => m.Id == matchId);

        Assert.NotNull(assertMatch.Decision);
        Assert.Equal(matchId, assertMatch.Decision.MatchId);
        Assert.Equal(ReviewOutcome.Approved, assertMatch.Decision.Outcome);
        Assert.Equal("Looks right", assertMatch.Decision.Note);

        await using var duplicateContext = CreateDbContext();
        duplicateContext.ReviewDecisions.Add(new ReviewDecision
        {
            MatchId = matchId,
            Outcome = ReviewOutcome.Rejected,
            DecidedAtUtc = DateTime.UtcNow
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => duplicateContext.SaveChangesAsync());
    }

    [Fact]
    public async Task SaveAndLoad_PendingRunWithInput_RoundTrips()
    {
        const string bankCsv = "Date,Amount,Description\n2026-01-01,10.00,Shop, with a comma\n";
        const string ledgerCsv = "Date,Amount,Description\n2026-01-01,10.00,Shop\n";
        var run = NewRun();
        run.Status = RunStatus.Pending;

        await using var actContext = CreateDbContext();
        actContext.RunInputs.Add(new RunInput { Run = run, BankCsv = bankCsv, LedgerCsv = ledgerCsv });
        await actContext.SaveChangesAsync();
        var runId = run.Id;

        await using var assertContext = CreateDbContext();
        var loadedRun = await assertContext.Runs.SingleAsync(r => r.Id == runId);
        var loadedInput = await assertContext.RunInputs.SingleAsync(i => i.RunId == runId);

        Assert.Equal(RunStatus.Pending, loadedRun.Status);
        Assert.Null(loadedRun.CompletedUtc);
        Assert.Null(loadedRun.FailureReason);
        Assert.Equal(bankCsv, loadedInput.BankCsv);
        Assert.Equal(ledgerCsv, loadedInput.LedgerCsv);
    }

    [Fact]
    public async Task Save_RunWithoutStatus_IsStoredAsComplete()
    {
        var run = NewRun();

        await using var actContext = CreateDbContext();
        actContext.Runs.Add(run);
        await actContext.SaveChangesAsync();
        var runId = run.Id;

        await using var assertContext = CreateDbContext();
        var loaded = await assertContext.Runs.SingleAsync(r => r.Id == runId);
        Assert.Equal(RunStatus.Complete, loaded.Status);
    }

    [Fact]
    public async Task SaveAndLoad_FailedRun_RoundTripsFailureReason()
    {
        var run = NewRun();
        run.Status = RunStatus.Failed;
        run.CompletedUtc = DateTime.UtcNow;
        run.FailureReason = "Line 3: bad amount";

        await using var actContext = CreateDbContext();
        actContext.Runs.Add(run);
        await actContext.SaveChangesAsync();
        var runId = run.Id;

        await using var assertContext = CreateDbContext();
        var loaded = await assertContext.Runs.SingleAsync(r => r.Id == runId);
        Assert.Equal(RunStatus.Failed, loaded.Status);
        Assert.Equal("Line 3: bad amount", loaded.FailureReason);
        Assert.NotNull(loaded.CompletedUtc);
    }

    [Fact]
    public async Task Save_PendingRun_StoresStatusAsText()
    {
        var run = NewRun();
        run.Status = RunStatus.Pending;

        await using var actContext = CreateDbContext();
        actContext.Runs.Add(run);
        await actContext.SaveChangesAsync();
        var runId = run.Id;

        await using var assertContext = CreateDbContext();
        var rawStatus = await assertContext.Database
            .SqlQuery<string>($"SELECT Status AS Value FROM Runs WHERE Id = {runId}")
            .SingleAsync();
        Assert.Equal("Pending", rawStatus);
    }

    [Fact]
    public async Task Delete_RunThatHasInput_IsRejected()
    {
        var run = NewRun();

        await using var actContext = CreateDbContext();
        actContext.RunInputs.Add(new RunInput { Run = run, BankCsv = "b", LedgerCsv = "l" });
        await actContext.SaveChangesAsync();
        var runId = run.Id;

        await using var deleteContext = CreateDbContext();
        var toDelete = await deleteContext.Runs.SingleAsync(r => r.Id == runId);
        deleteContext.Runs.Remove(toDelete);

        await Assert.ThrowsAsync<DbUpdateException>(() => deleteContext.SaveChangesAsync());
    }

    [Fact]
    public async Task Save_InputForMissingRun_IsRejected()
    {
        await using var dbContext = CreateDbContext();
        dbContext.RunInputs.Add(new RunInput { RunId = 9999, BankCsv = "b", LedgerCsv = "l" });

        await Assert.ThrowsAsync<DbUpdateException>(() => dbContext.SaveChangesAsync());
    }
}