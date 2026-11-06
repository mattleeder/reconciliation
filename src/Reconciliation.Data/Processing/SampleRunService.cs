using Microsoft.EntityFrameworkCore;

namespace Reconciliation.Data.Processing;

public class SampleRunService(RunSubmitter submitter, IDbContextFactory<ReconciliationDbContext> dbFactory)
{
    public const string BankFileName = "sample-bank.csv";
    public const string LedgerFileName = "sample-ledger.csv";

    // There is no authentication and no way to delete runs, so the public demo caps how many
    // sample runs can pile up in the free database. Past the cap the latest sample run is reused.
    public const int MaxSampleRuns = 20;

    public async Task<RunSubmitResult> SubmitAsync(string bankCsv, string ledgerCsv, CancellationToken cancellationToken = default)
    {
        await using (var db = await dbFactory.CreateDbContextAsync(cancellationToken))
        {
            var sampleRuns = db.Runs.Where(r => r.BankFileName == BankFileName && r.LedgerFileName == LedgerFileName);

            // Check then act, so concurrent clicks can overshoot the cap slightly. That is fine for a soft cap.
            if (await sampleRuns.CountAsync(cancellationToken) >= MaxSampleRuns)
            {
                var latestId = await sampleRuns
                    .OrderByDescending(r => r.CreatedUtc)
                    .ThenByDescending(r => r.Id)
                    .Select(r => r.Id)
                    .FirstAsync(cancellationToken);

                return new RunSubmitResult(latestId, true);
            }
        }

        return await submitter.SubmitAsync(BankFileName, bankCsv, LedgerFileName, ledgerCsv, cancellationToken);
    }
}
