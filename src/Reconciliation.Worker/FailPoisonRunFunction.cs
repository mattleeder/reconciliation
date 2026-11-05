using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Reconciliation.Data.Processing;

namespace Reconciliation.Worker;

public class FailPoisonRunFunction(RunProcessor processor, ILogger<FailPoisonRunFunction> logger)
{
    [Function(nameof(FailPoisonRunFunction))]
    public async Task Run(
        [QueueTrigger(RunQueueNames.ReconcileRunsPoison)] string message,
        CancellationToken cancellationToken)
    {
        if (!int.TryParse(message, out var runId))
        {
            logger.LogWarning("Ignoring poison message that is not a run id: {Message}", message);
            return;
        }

        var outcome = await processor.MarkFailedAsync(runId, "Processing failed repeatedly and was abandoned.", cancellationToken);

        logger.LogWarning("Run {RunId} abandoned after repeated failures: {Outcome}", runId, outcome);
    }
}