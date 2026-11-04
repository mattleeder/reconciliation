namespace Reconciliation.Domain.Entities;

public class ReconciliationRun
{
    public int Id { get; set; }
    public DateTime CreatedUtc { get; set; }
    public string BankFileName { get; set; } = string.Empty;
    public string LedgerFileName { get; set; } = string.Empty;
}