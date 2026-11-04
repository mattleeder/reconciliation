namespace Reconciliation.Domain.Matching;

using Reconciliation.Domain.Entities;

public record MatchingResult(IReadOnlyList<Match> Matches, IReadOnlyList<Transaction> UnmatchedBank, IReadOnlyList<Transaction> UnmatchedLedger);