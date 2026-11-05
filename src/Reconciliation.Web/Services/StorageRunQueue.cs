using System.Globalization;
using Azure.Storage.Queues;
using Reconciliation.Data.Processing;

namespace Reconciliation.Web.Services;

public class StorageRunQueue(QueueClient queueClient) : IRunQueue
{
    public async Task EnqueueAsync(int runId, CancellationToken cancellationToken = default)
    {
        await queueClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

        // the base64 encoding is configured in QueueClient in Program.cs because azure functions expect base64 messages
        await queueClient.SendMessageAsync(runId.ToString(CultureInfo.InvariantCulture), cancellationToken);
    }
}