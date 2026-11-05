using Microsoft.EntityFrameworkCore;
using Reconciliation.Domain.Entities;

namespace Reconciliation.Data.Processing;


public class RunSubmitter(IDbContextFactory<ReconciliationDbContext> dbFactory, IRunQueue runQueue)
{
    public async Task<RunSubmitResult> SubmitAsync(string bankFileName, string bankCsv, string ledgerFileName, string ledgerCsv, CancellationToken cancellationToken = default)
    {
        var run = new ReconciliationRun
        {
            BankFileName = bankFileName,
            LedgerFileName = ledgerFileName,
            CreatedUtc = DateTime.UtcNow,
            Status = RunStatus.Pending,
        };

        var runInput = new RunInput
        {
            BankCsv = bankCsv,
            LedgerCsv = ledgerCsv,
            Run = run,
        };

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        db.Runs.Add(run);
        db.RunInputs.Add(runInput);
        await db.SaveChangesAsync(cancellationToken);

        try
        {
            await runQueue.EnqueueAsync(run.Id, cancellationToken);
            return new RunSubmitResult(run.Id, true);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            run.Status = RunStatus.Failed;
            run.FailureReason = "Could not queue the run for processing.";
            run.CompletedUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
            return new RunSubmitResult(run.Id, false);
        }
    }
}