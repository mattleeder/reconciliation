using System.Globalization;
using Microsoft.Azure.Functions.Worker;
using Reconciliation.Data.Processing;

namespace Reconciliation.Worker;

public class SweepStaleRunsFunction(RunProcessor processor, ILogger<SweepStaleRunsFunction> logger)
{
    private static readonly TimeSpan StaleAfter = TimeSpan.FromMinutes(10);
    private const int MaxRunsPerSweep = 50;

    [Function(nameof(SweepStaleRunsFunction))]
    [QueueOutput(RunQueueNames.ReconcileRuns)]
    public async Task<string[]> Run(
        [TimerTrigger("0 */5 * * * *")] TimerInfo timer,
        CancellationToken cancellationToken)
    {
        var runIds = await processor.GetStalePendingRunIdsAsync(DateTime.UtcNow - StaleAfter, MaxRunsPerSweep, cancellationToken);

        if (runIds.Count > 0)
        {
            logger.LogInformation("Re-queuing {Count} stale pending run(s).", runIds.Count);
        }

        return runIds.Select(id => id.ToString(CultureInfo.InvariantCulture)).ToArray();
    }
}