namespace Reconciliation.Data.Processing;

public enum ProcessOutcome
{
    Completed = 1,
    Failed = 2,
    AlreadyProcessed = 3,
    NotFound = 4,
}