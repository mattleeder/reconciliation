using Microsoft.EntityFrameworkCore;
using Reconciliation.Data.Processing;
using Reconciliation.Domain.Entities;

namespace Reconciliation.Tests;

[Collection("Database")]
public class RunProcessorTests : IAsyncLifetime
{
    private const string Header = "Date,Amount,Description\n";

    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }

    public async Task InitializeAsync()
    {
        await TestDatabase.ResetAsync();
    }

    private static async Task<int> SaveRunAsync(RunStatus status, string bankCsv, string ledgerCsv)
    {
        var run = new ReconciliationRun { CreatedUtc = DateTime.UtcNow, Status = status };

        await using var dbContext = TestDatabase.CreateContext();
        dbContext.RunInputs.Add(new RunInput { Run = run, BankCsv = bankCsv, LedgerCsv = ledgerCsv });
        await dbContext.SaveChangesAsync();

        return run.Id;
    }

    private static async Task<ProcessOutcome> ProcessAsync(int runId)
    {
        await using var dbContext = TestDatabase.CreateContext();
        return await new RunProcessor(dbContext).ProcessAsync(runId);
    }

    [Fact]
    public async Task ProcessAsync_PendingRunWithValidCsvs_CompletesAndSavesMatches()
    {
        var bankCsv = Header + "2026-01-01,10.00,Local Shop\n2026-02-01,99.00,Something Else\n";
        var ledgerCsv = Header + "2026-01-01,10.00,Local Shop\n";
        var runId = await SaveRunAsync(RunStatus.Pending, bankCsv, ledgerCsv);

        var outcome = await ProcessAsync(runId);

        Assert.Equal(ProcessOutcome.Completed, outcome);

        await using var assertContext = TestDatabase.CreateContext();
        var run = await assertContext.Runs.SingleAsync(r => r.Id == runId);
        Assert.Equal(RunStatus.Complete, run.Status);
        Assert.NotNull(run.CompletedUtc);
        Assert.Null(run.FailureReason);
        Assert.Equal(3, await assertContext.Transactions.CountAsync(t => t.RunId == runId));
        var match = await assertContext.Matches.SingleAsync();
        Assert.Equal(MatchKind.ExactAmountAndDate, match.Kind);
    }

    [Fact]
    public async Task ProcessAsync_BadBankRow_FailsWithLineNumberAndSavesNoTransactions()
    {
        var bankCsv = Header + "2026-01-01,10.00,Local Shop\n2026-01-02,abc,Bad Row\n";
        var ledgerCsv = Header + "2026-01-01,10.00,Local Shop\n";
        var runId = await SaveRunAsync(RunStatus.Pending, bankCsv, ledgerCsv);

        var outcome = await ProcessAsync(runId);

        Assert.Equal(ProcessOutcome.Failed, outcome);

        await using var assertContext = TestDatabase.CreateContext();
        var run = await assertContext.Runs.SingleAsync(r => r.Id == runId);
        Assert.Equal(RunStatus.Failed, run.Status);
        Assert.NotNull(run.CompletedUtc);
        Assert.NotNull(run.FailureReason);
        Assert.Contains("Bank line 3", run.FailureReason);
        Assert.Equal(0, await assertContext.Transactions.CountAsync());
        Assert.Equal(0, await assertContext.Matches.CountAsync());
    }

    [Fact]
    public async Task ProcessAsync_ErrorsInBothFiles_ReasonMentionsBoth()
    {
        var bankCsv = Header + "2026-01-02,abc,Bad Bank Row\n";
        var ledgerCsv = Header + "not-a-date,10.00,Bad Ledger Row\n";
        var runId = await SaveRunAsync(RunStatus.Pending, bankCsv, ledgerCsv);

        var outcome = await ProcessAsync(runId);

        Assert.Equal(ProcessOutcome.Failed, outcome);

        await using var assertContext = TestDatabase.CreateContext();
        var run = await assertContext.Runs.SingleAsync(r => r.Id == runId);
        Assert.NotNull(run.FailureReason);
        Assert.Contains("Bank line 2", run.FailureReason);
        Assert.Contains("Ledger line 2", run.FailureReason);
    }

    [Fact]
    public async Task ProcessAsync_ManyBadRows_FailureReasonIsCappedAtColumnLength()
    {
        var badRows = string.Join("\n", Enumerable.Range(1, 500).Select(_ => "2026-01-01,abc,Bad Row"));
        var bankCsv = Header + badRows + "\n";
        var ledgerCsv = Header + "2026-01-01,10.00,Local Shop\n";
        var runId = await SaveRunAsync(RunStatus.Pending, bankCsv, ledgerCsv);

        var outcome = await ProcessAsync(runId);

        Assert.Equal(ProcessOutcome.Failed, outcome);

        await using var assertContext = TestDatabase.CreateContext();
        var run = await assertContext.Runs.SingleAsync(r => r.Id == runId);
        Assert.NotNull(run.FailureReason);
        Assert.Equal(1000, run.FailureReason.Length);
        Assert.EndsWith("…", run.FailureReason);
    }

    [Theory]
    [InlineData(RunStatus.Complete)]
    [InlineData(RunStatus.Failed)]
    public async Task ProcessAsync_RunNotPending_IsAlreadyProcessedAndChangesNothing(RunStatus status)
    {
        var bankCsv = Header + "2026-01-01,10.00,Local Shop\n";
        var ledgerCsv = Header + "2026-01-01,10.00,Local Shop\n";
        var runId = await SaveRunAsync(status, bankCsv, ledgerCsv);

        var outcome = await ProcessAsync(runId);

        Assert.Equal(ProcessOutcome.AlreadyProcessed, outcome);

        await using var assertContext = TestDatabase.CreateContext();
        var run = await assertContext.Runs.SingleAsync(r => r.Id == runId);
        Assert.Equal(status, run.Status);
        Assert.Null(run.CompletedUtc);
        Assert.Equal(0, await assertContext.Transactions.CountAsync());
        Assert.Equal(0, await assertContext.Matches.CountAsync());
    }

    [Fact]
    public async Task ProcessAsync_CalledTwice_SecondCallDoesNothing()
    {
        var bankCsv = Header + "2026-01-01,10.00,Local Shop\n";
        var ledgerCsv = Header + "2026-01-01,10.00,Local Shop\n";
        var runId = await SaveRunAsync(RunStatus.Pending, bankCsv, ledgerCsv);

        var first = await ProcessAsync(runId);
        var second = await ProcessAsync(runId);

        Assert.Equal(ProcessOutcome.Completed, first);
        Assert.Equal(ProcessOutcome.AlreadyProcessed, second);

        await using var assertContext = TestDatabase.CreateContext();
        Assert.Equal(2, await assertContext.Transactions.CountAsync());
        Assert.Single(await assertContext.Matches.ToListAsync());
    }

    [Fact]
    public async Task ProcessAsync_UnknownRun_ReturnsNotFound()
    {
        var outcome = await ProcessAsync(9999);

        Assert.Equal(ProcessOutcome.NotFound, outcome);
    }

    [Fact]
    public async Task ProcessAsync_PendingRunWithoutInput_Fails()
    {
        var run = new ReconciliationRun { CreatedUtc = DateTime.UtcNow, Status = RunStatus.Pending };
        await using (var saveContext = TestDatabase.CreateContext())
        {
            saveContext.Runs.Add(run);
            await saveContext.SaveChangesAsync();
        }

        var outcome = await ProcessAsync(run.Id);

        Assert.Equal(ProcessOutcome.Failed, outcome);

        await using var assertContext = TestDatabase.CreateContext();
        var loaded = await assertContext.Runs.SingleAsync(r => r.Id == run.Id);
        Assert.Equal(RunStatus.Failed, loaded.Status);
        Assert.Equal("Run input is missing", loaded.FailureReason);
        Assert.NotNull(loaded.CompletedUtc);
    }
}