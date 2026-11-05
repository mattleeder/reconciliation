namespace Reconciliation.Data.Processing;

public interface IRunQueue
{
    Task EnqueueAsync(int runId, CancellationToken cancellationToken = default);
}