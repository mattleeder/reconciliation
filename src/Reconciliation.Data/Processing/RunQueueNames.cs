namespace Reconciliation.Data.Processing;

public static class RunQueueNames
{
    public const string ReconcileRuns = "reconcile-runs";
    public const string ReconcileRunsPoison = ReconcileRuns + "-poison"; // the Functions host moves a message here after 5 failed attempts
}