using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Reconciliation.Data;
using Reconciliation.Data.Processing;
using Reconciliation.Domain.Entities;

namespace Reconciliation.Tests;

[Collection("Database")]
public class SampleRunServiceTests : IAsyncLifetime
{
    private const string BankCsv = "Date,Amount,Description\n2026-01-01,10.00,Local Shop\n";
    private const string LedgerCsv = "Date,Amount,Description\n2026-01-01,10.00,Local Shop Ledger\n";

    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }

    public async Task InitializeAsync()
    {
        await TestDatabase.ResetAsync();
    }

    private sealed class TestDbContextFactory : IDbContextFactory<ReconciliationDbContext>
    {
        public ReconciliationDbContext CreateDbContext()
        {
            return TestDatabase.CreateContext();
        }
    }

    private sealed class FakeRunQueue(List<int> queuedIds) : IRunQueue
    {
        public Task EnqueueAsync(int runId, CancellationToken cancellationToken = default)
        {
            queuedIds.Add(runId);
            return Task.CompletedTask;
        }
    }

    private static SampleRunService CreateService(List<int> queuedIds)
    {
        var dbFactory = new TestDbContextFactory();
        var submitter = new RunSubmitter(dbFactory, new FakeRunQueue(queuedIds), NullLogger<RunSubmitter>.Instance);
        return new SampleRunService(submitter, dbFactory);
    }

    [Fact]
    public async Task SubmitAsync_UnderCap_SavesAndQueuesANewSampleRun()
    {
        var queuedIds = new List<int>();

        var result = await CreateService(queuedIds).SubmitAsync(BankCsv, LedgerCsv);

        Assert.True(result.Queued);
        Assert.Equal([result.RunId], queuedIds);

        await using var db = TestDatabase.CreateContext();
        var run = await db.Runs.SingleAsync(r => r.Id == result.RunId);
        Assert.Equal(SampleRunService.BankFileName, run.BankFileName);
        Assert.Equal(SampleRunService.LedgerFileName, run.LedgerFileName);
        Assert.Equal(RunStatus.Pending, run.Status);
    }

    [Fact]
    public async Task SubmitAsync_AtCap_ReusesTheLatestSampleRun()
    {
        await using (var db = TestDatabase.CreateContext())
        {
            var start = DateTime.UtcNow.AddHours(-1);

            for (var i = 0; i < SampleRunService.MaxSampleRuns; i++)
            {
                db.Runs.Add(new ReconciliationRun
                {
                    BankFileName = SampleRunService.BankFileName,
                    LedgerFileName = SampleRunService.LedgerFileName,
                    CreatedUtc = start.AddMinutes(i),
                    Status = RunStatus.Complete,
                });
            }

            await db.SaveChangesAsync();
        }

        int latestId;
        await using (var db = TestDatabase.CreateContext())
        {
            latestId = await db.Runs.OrderByDescending(r => r.CreatedUtc).Select(r => r.Id).FirstAsync();
        }

        var queuedIds = new List<int>();

        var result = await CreateService(queuedIds).SubmitAsync(BankCsv, LedgerCsv);

        Assert.Equal(latestId, result.RunId);
        Assert.Empty(queuedIds);

        await using var check = TestDatabase.CreateContext();
        Assert.Equal(SampleRunService.MaxSampleRuns, await check.Runs.CountAsync());
    }

    [Fact]
    public async Task SubmitAsync_OtherRunsDoNotCountTowardsTheCap()
    {
        await using (var db = TestDatabase.CreateContext())
        {
            for (var i = 0; i < SampleRunService.MaxSampleRuns; i++)
            {
                db.Runs.Add(new ReconciliationRun
                {
                    BankFileName = "bank.csv",
                    LedgerFileName = "ledger.csv",
                    CreatedUtc = DateTime.UtcNow,
                    Status = RunStatus.Complete,
                });
            }

            await db.SaveChangesAsync();
        }

        var queuedIds = new List<int>();

        var result = await CreateService(queuedIds).SubmitAsync(BankCsv, LedgerCsv);

        Assert.Equal([result.RunId], queuedIds);
    }
}
