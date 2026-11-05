using Microsoft.Azure.Functions.Worker;
using Reconciliation.Data.Processing;

namespace Reconciliation.Worker;

public class ProcessRunFunction(RunProcessor processor, ILogger<ProcessRunFunction> logger)
{
    [Function(nameof(ProcessRunFunction))]
    public async Task Run(
        [QueueTrigger(RunQueueNames.ReconcileRuns)] string message,
        CancellationToken cancellationToken)
    {
        if (!int.TryParse(message, out var runId))
        {
            logger.LogWarning("Ignoring queue message that is not a run id: {Message}", message);
            return;
        }

        var outcome = await processor.ProcessAsync(runId, cancellationToken);

        logger.LogInformation("Run {RunId} processed: {Outcome}", runId, outcome);
    }
}