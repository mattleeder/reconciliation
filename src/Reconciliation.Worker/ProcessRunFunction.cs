using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Reconciliation.Data.Processing;

namespace Reconciliation.Worker;

public class ProcessRunFunction(ILogger<ProcessRunFunction> logger)
{
    [Function(nameof(ProcessRunFunction))]
    public void Run([QueueTrigger(RunQueueNames.ReconcileRuns)] string message)
    {
        logger.LogInformation("Received queue message: {Message}", message);
    }
}