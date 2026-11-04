namespace Reconciliation.Domain.Matching;

using Reconciliation.Domain.Entities;
using System.Diagnostics.CodeAnalysis;

public static class MatchingEngine
{
    private const int FuzzyDateWindowDays = 3;
    private const double FuzzyThreshold = 0.5;

    private static bool TryScoreFuzzy(Transaction bankTx, Transaction ledgerTx, out double score)
    {
        score = 0;
        if (bankTx.Amount != ledgerTx.Amount)
        {
            return false;
        }

        var descriptionSimilarityScore = DescriptionSimilarity.Score(bankTx.Description, ledgerTx.Description);
        var dateDifferenceInDays = Math.Abs(bankTx.Date.DayNumber - ledgerTx.Date.DayNumber);

        if (descriptionSimilarityScore < FuzzyThreshold 
            || dateDifferenceInDays > FuzzyDateWindowDays)
        {
            return false;
        }

        score = descriptionSimilarityScore;
        return true;
    }

    private static bool TryFindBestCandidate(
        Transaction bankTx, IEnumerable<Transaction> availableLedger,
        [NotNullWhen(true)] out Transaction? best, out double score)
    {
        // Candidates scoring below the threshold are already rejected by TryScoreFuzzy,
        // so starting at the threshold lets the first valid candidate become the best.
        // The '>' check must come before the '>=' check: a better score clears the ties first.
        var bestScore = FuzzyThreshold;
        var tied = new List<Transaction>();

        foreach (var ledgerTx in availableLedger)
        {
            if (!TryScoreFuzzy(bankTx, ledgerTx, out var candidateScore))
            {
                continue;
            }

            if (candidateScore > bestScore)
            {
                // New best score found so clear previous candidates.
                bestScore = candidateScore;
                tied.Clear();
            }

            if (candidateScore >= bestScore)
            {
                tied.Add(ledgerTx);
            }
        }

        if (tied.Count == 0)
        {
            best = null;
            score = 0;
            return false;
        }

        best = tied.OrderBy(t => t.Date).ThenBy(t => t.Id).First();
        score = bestScore;
        return true;
    }
    
    private static MatchingResult MatchExact(IReadOnlyList<Transaction> sortedBank, IReadOnlyList<Transaction> sortedLedger)
    {
        var ledgerByKey = sortedLedger
            .GroupBy(t => (t.Amount, t.Date))
            .ToDictionary(g => g.Key, g => new Queue<Transaction>(g));

        var matches = new List<Match>();
        var unmatchedBank = new List<Transaction>();
        var usedLedger = new HashSet<Transaction>();

        foreach (var bankTx in sortedBank)
        {
            if (ledgerByKey.TryGetValue((bankTx.Amount, bankTx.Date), out var candidates)
                && candidates.TryDequeue(out var ledgerTx))
            {
                matches.Add(new Match
                {
                    LeftTransaction = bankTx,
                    RightTransaction = ledgerTx,
                    Kind = MatchKind.ExactAmountAndDate,
                    Confidence = 1.0,
                });
                usedLedger.Add(ledgerTx);
            }
            else
            {
                unmatchedBank.Add(bankTx);
            }
        }

        var unmatchedLedger = sortedLedger.Where(t => !usedLedger.Contains(t)).ToList();

        return new MatchingResult(matches, unmatchedBank, unmatchedLedger);
    }

    private static MatchingResult MatchFuzzy(IReadOnlyList<Transaction> unmatchedBank, IReadOnlyList<Transaction> unmatchedLedger)
    {
        var matches = new List<Match>();
        var unmatchedBankSecondPass = new List<Transaction>();
        var usedLedger = new HashSet<Transaction>();

        foreach (var bankTx in unmatchedBank)
        {
            var available = unmatchedLedger.Where(t => !usedLedger.Contains(t));

            if (TryFindBestCandidate(bankTx, available, out var best, out var score))
            {
                matches.Add(new Match
                {
                    LeftTransaction = bankTx,
                    RightTransaction = best,
                    Kind = MatchKind.FuzzyDescription,
                    Confidence = score,
                });
                usedLedger.Add(best);
            }
            else
            {
                unmatchedBankSecondPass.Add(bankTx);
            }
        }

        List<Transaction> unmatchedLedgerSecondPass = [.. unmatchedLedger.Where(t => !usedLedger.Contains(t))];

        return new MatchingResult(matches, unmatchedBankSecondPass, unmatchedLedgerSecondPass);
    }

    private static List<Match> FindDuplicates(IReadOnlyList<Transaction> sortedRows)
    {
        var duplicates = new List<Match>();

        foreach (var group in sortedRows.GroupBy(t => (t.Amount, t.Date)))
        {
            var originals = new List<Transaction>();

            foreach (var row in group)
            {
                // A perfect score means the word sets are identical. It is exactly 1.0
                // (whole number / whole number), so == is safe here. Empty descriptions score 0,
                // so blank memos are never flagged.
                var original = originals.FirstOrDefault(o =>
                    DescriptionSimilarity.Score(o.Description, row.Description) == 1.0);

                if (original is null)
                {
                    originals.Add(row);
                    continue;
                }

                duplicates.Add(new Match
                {
                    LeftTransaction = original,
                    RightTransaction = row,
                    Kind = MatchKind.PossibleDuplicate,
                    Confidence = 1.0,
                });
            }
        }

        return duplicates;
    }

    /// <summary>
    /// 3 passes, exact then fuzzy then duplicates. Exact consumes all exact matches and leaves unmatched bank and ledger transactions.
    /// Fuzzy consumes all fuzzy matches and leaves unmatched bank and ledger transactions.
    /// Duplicates consumes nothing, but duplicate pairs are returned in Matches with Kind = PossibleDuplicate. 
    /// Duplicates doesn't remove rows from unmatched lists.
    /// </summary>
    /// <exception cref="ArgumentException">A transaction's Source doesn't match the list it was passed in.</exception>
    public static MatchingResult Run(IReadOnlyList<Transaction> bank, IReadOnlyList<Transaction> ledger)
    {
        if (bank.Any(t => t.Source != TransactionSource.Bank))
        {
            throw new ArgumentException(
                "The bank list contains a transaction that is not from the bank.", nameof(bank));
        }

        if (ledger.Any(t => t.Source != TransactionSource.Ledger))
        {
            throw new ArgumentException(
                "The ledger list contains a transaction that is not from the ledger.", nameof(ledger));
        }

        var sortedBank = bank.OrderBy(t => t.Date).ThenBy(t => t.Id).ToList();
        var sortedLedger = ledger.OrderBy(t => t.Date).ThenBy(t => t.Id).ToList();

        var exactPass = MatchExact(sortedBank, sortedLedger);
        var fuzzyPass = MatchFuzzy(exactPass.UnmatchedBank, exactPass.UnmatchedLedger);
        var duplicates = FindDuplicates(sortedBank).Concat(FindDuplicates(sortedLedger));

        return new MatchingResult(
            [.. exactPass.Matches, .. fuzzyPass.Matches, .. duplicates],
            fuzzyPass.UnmatchedBank,
            fuzzyPass.UnmatchedLedger);
    }
}