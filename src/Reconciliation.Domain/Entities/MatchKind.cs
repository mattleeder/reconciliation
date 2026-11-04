namespace Reconciliation.Domain.Entities;

public enum MatchKind
{
    ExactAmountAndDate = 1,
    FuzzyDescription = 2,
    PossibleDuplicate = 3,
}