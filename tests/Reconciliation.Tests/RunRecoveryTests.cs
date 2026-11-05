using Microsoft.EntityFrameworkCore;
using Reconciliation.Data.Processing;
using Reconciliation.Domain.Entities;

namespace Reconciliation.Tests;

[Collection("Database")]
public class RunRecoveryTests : IAsyncLifetime
{
    private static readonly DateTime Now = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }

    public async Task InitializeAsync()
    {
        await TestDatabase.ResetAsync();
    }

    private static async Task<int> SaveRunAsync(RunStatus status, DateTime createdUtc)
    {
        var run = new ReconciliationRun { CreatedUtc = createdUtc, Status = status };

        await using var dbContext = TestDatabase.CreateContext();
        dbContext.Runs.Add(run);
        await dbContext.SaveChangesAsync();

        return run.Id;
    }

    private static async Task<ProcessOutcome> MarkFailedAsync(int runId, string reason)
    {
        await using var dbContext = TestDatabase.CreateContext();
        return await new RunProcessor(dbContext).MarkFailedAsync(runId, reason);
    }

    private static async Task<List<int>> GetStaleAsync(DateTime createdBeforeUtc, int maxCount)
    {
        await using var dbContext = TestDatabase.CreateContext();
        return await new RunProcessor(dbContext).GetStalePendingRunIdsAsync(createdBeforeUtc, maxCount);
    }

    [Fact]
    public async Task MarkFailedAsync_PendingRun_FailsWithReason()
    {
        var runId = await SaveRunAsync(RunStatus.Pending, Now);

        var outcome = await MarkFailedAsync(runId, "gave up");

        Assert.Equal(ProcessOutcome.Failed, outcome);
        await using var dbContext = TestDatabase.CreateContext();
        var run = await dbContext.Runs.SingleAsync(r => r.Id == runId);
        Assert.Equal(RunStatus.Failed, run.Status);
        Assert.Equal("gave up", run.FailureReason);
        Assert.NotNull(run.CompletedUtc);
    }

    [Fact]
    public async Task MarkFailedAsync_CompleteRun_IsLeftAlone()
    {
        var runId = await SaveRunAsync(RunStatus.Complete, Now);

        var outcome = await MarkFailedAsync(runId, "gave up");

        Assert.Equal(ProcessOutcome.AlreadyProcessed, outcome);
        await using var dbContext = TestDatabase.CreateContext();
        var run = await dbContext.Runs.SingleAsync(r => r.Id == runId);
        Assert.Equal(RunStatus.Complete, run.Status);
        Assert.Null(run.FailureReason);
    }

    [Fact]
    public async Task MarkFailedAsync_UnknownRun_ReturnsNotFound()
    {
        Assert.Equal(ProcessOutcome.NotFound, await MarkFailedAsync(999, "gave up"));
    }

    [Fact]
    public async Task MarkFailedAsync_ReasonOverCap_Throws()
    {
        var runId = await SaveRunAsync(RunStatus.Pending, Now);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => MarkFailedAsync(runId, new string('x', 1001)));
    }

    [Fact]
    public async Task GetStalePendingRunIdsAsync_ReturnsOnlyOldPendingRuns_OldestFirst_UpToMax()
    {
        var newerStale = await SaveRunAsync(RunStatus.Pending, Now.AddMinutes(-20));
        var oldestStale = await SaveRunAsync(RunStatus.Pending, Now.AddMinutes(-30));
        await SaveRunAsync(RunStatus.Pending, Now.AddMinutes(-1));   // too new
        await SaveRunAsync(RunStatus.Complete, Now.AddMinutes(-30)); // old but not pending
        await SaveRunAsync(RunStatus.Failed, Now.AddMinutes(-30));

        var all = await GetStaleAsync(Now.AddMinutes(-10), 50);
        var capped = await GetStaleAsync(Now.AddMinutes(-10), 1);

        Assert.Equal([oldestStale, newerStale], all);
        Assert.Equal([oldestStale], capped);
    }
}