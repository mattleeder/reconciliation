namespace Reconciliation.Domain.Entities;

public class ReconciliationRun
{
    public int Id { get; set; }
    public DateTime CreatedUtc { get; set; }
    public string BankFileName { get; set; } = string.Empty;
    public string LedgerFileName { get; set; } = string.Empty;
    public RunStatus Status { get; set; }
    public DateTime? CompletedUtc { get; set; }
    public string? FailureReason { get; set; }
}