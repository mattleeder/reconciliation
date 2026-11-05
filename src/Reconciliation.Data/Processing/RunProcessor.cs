using Microsoft.EntityFrameworkCore;
using Reconciliation.Domain.Entities;
using Reconciliation.Domain.Import;
using Reconciliation.Domain.Matching;

namespace Reconciliation.Data.Processing;

public class RunProcessor(ReconciliationDbContext dbContext)
{
    private const int MaxFailureReasonLength = 1000; // must match HasMaxLength in ReconciliationRunConfiguration

    private static string BuildFailureReason(IReadOnlyList<CsvRowError> bankErrors, IReadOnlyList<CsvRowError> ledgerErrors)
    {
        var header = $"Bank: {bankErrors.Count} error(s); Ledger: {ledgerErrors.Count} error(s). ";
        var details = bankErrors.Select(e => $"Bank line {e.LineNumber}: {e.Message}")
            .Concat(ledgerErrors.Select(e => $"Ledger line {e.LineNumber}: {e.Message}"));
        var reason = header + string.Join("; ", details);

        return reason.Length <= MaxFailureReasonLength
            ? reason
            : reason[..(MaxFailureReasonLength - 1)] + "…"; // Unicode ellipsis is 1 char
    }

    private async Task MarkFailed(ReconciliationRun run, string failureReason, CancellationToken cancellationToken)
    {
        // This function trusts that the failureReason is below the length cap
        run.Status = RunStatus.Failed;
        run.FailureReason = failureReason;
        run.CompletedUtc = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
    }
    public async Task<ProcessOutcome> ProcessAsync(int runId, CancellationToken cancellationToken = default)
    {
        var run = await dbContext.Runs.FirstOrDefaultAsync(r => r.Id == runId, cancellationToken);

        if (run == null)
        {
            return ProcessOutcome.NotFound;
        }

        if (run.Status != RunStatus.Pending)
        {
            return ProcessOutcome.AlreadyProcessed;
        }

        var runInput = await dbContext.RunInputs.FirstOrDefaultAsync(r => r.RunId == runId, cancellationToken);

        if (runInput == null)
        {
            await MarkFailed(run, "Run input is missing", cancellationToken);
            return ProcessOutcome.Failed;
        }

        var bankResult = TransactionCsvParser.Parse(runInput.BankCsv, TransactionSource.Bank);
        var ledgerResult = TransactionCsvParser.Parse(runInput.LedgerCsv, TransactionSource.Ledger);

        if (bankResult.Errors.Count > 0 || ledgerResult.Errors.Count > 0)
        {
            var failureReason = BuildFailureReason(bankResult.Errors, ledgerResult.Errors);
            await MarkFailed(run, failureReason, cancellationToken);
            return ProcessOutcome.Failed;
        }

        foreach (var tx in bankResult.Transactions)
        {
            tx.Run = run;
        }

        foreach (var tx in ledgerResult.Transactions)
        {
            tx.Run = run;
        }

        var matchingResult = MatchingEngine.Run(bankResult.Transactions, ledgerResult.Transactions);

        dbContext.Transactions.AddRange(bankResult.Transactions);
        dbContext.Transactions.AddRange(ledgerResult.Transactions);
        dbContext.Matches.AddRange(matchingResult.Matches);
        run.Status = RunStatus.Complete;
        run.CompletedUtc = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        return ProcessOutcome.Completed;
    }
}