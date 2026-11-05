using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Reconciliation.Data;
using Reconciliation.Data.Processing;
using Reconciliation.Domain.Entities;

namespace Reconciliation.Tests;

[Collection("Database")]
public class RunSubmitterTests : IAsyncLifetime
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

    private sealed class FakeRunQueue(Func<int, Task> onEnqueue) : IRunQueue
    {
        public Task EnqueueAsync(int runId, CancellationToken cancellationToken = default)
        {
            return onEnqueue(runId);
        }
    }

    private static Task<RunSubmitResult> SubmitAsync(Func<int, Task> onEnqueue)
    {
        var submitter = new RunSubmitter(new TestDbContextFactory(), new FakeRunQueue(onEnqueue), NullLogger<RunSubmitter>.Instance);
        return submitter.SubmitAsync("bank.csv", BankCsv, "ledger.csv", LedgerCsv);
    }

    private static async Task<(ReconciliationRun Run, RunInput? Input)> LoadAsync(int runId)
    {
        await using var dbContext = TestDatabase.CreateContext();
        var run = await dbContext.Runs.SingleAsync(r => r.Id == runId);
        var input = await dbContext.RunInputs.SingleOrDefaultAsync(i => i.RunId == runId);
        return (run, input);
    }

    [Fact]
    public async Task SubmitAsync_QueueSucceeds_SavesPendingRunAndQueuesItsId()
    {
        var queuedIds = new List<int>();

        var result = await SubmitAsync(runId =>
        {
            queuedIds.Add(runId);
            return Task.CompletedTask;
        });

        Assert.True(result.Queued);
        Assert.Equal(result.RunId, Assert.Single(queuedIds));

        var (run, input) = await LoadAsync(result.RunId);
        Assert.Equal(RunStatus.Pending, run.Status);
        Assert.Equal("bank.csv", run.BankFileName);
        Assert.Equal("ledger.csv", run.LedgerFileName);
        Assert.Null(run.CompletedUtc);
        Assert.NotNull(input);
        Assert.Equal(BankCsv, input.BankCsv);
        Assert.Equal(LedgerCsv, input.LedgerCsv);
    }

    [Fact]
    public async Task SubmitAsync_QueueThrows_MarksRunFailedAndKeepsInput()
    {
        var result = await SubmitAsync(_ => throw new InvalidOperationException("queue is down"));

        Assert.False(result.Queued);

        var (run, input) = await LoadAsync(result.RunId);
        Assert.Equal(RunStatus.Failed, run.Status);
        Assert.Equal("Could not queue the run for processing.", run.FailureReason);
        Assert.NotNull(run.CompletedUtc);
        Assert.NotNull(input);
    }

    [Fact]
    public async Task SubmitAsync_RunIsAlreadySavedWhenMessageIsSent()
    {
        // If an assertion in here fails, RunSubmitter catches the exception and marks the run
        // Failed, so the Queued check at the end is what makes a failure here visible.
        var result = await SubmitAsync(async runId =>
        {
            var (run, input) = await LoadAsync(runId);
            Assert.Equal(RunStatus.Pending, run.Status);
            Assert.NotNull(input);
        });

        Assert.True(result.Queued);
    }

    [Fact]
    public async Task SubmitAsync_Cancelled_PropagatesAndLeavesRunPending()
    {
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => SubmitAsync(_ => throw new OperationCanceledException()));

        await using var dbContext = TestDatabase.CreateContext();
        var run = await dbContext.Runs.SingleAsync();
        Assert.Equal(RunStatus.Pending, run.Status);
    }
}