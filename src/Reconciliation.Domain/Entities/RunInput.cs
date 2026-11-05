namespace Reconciliation.Domain.Entities;

public class RunInput
{
    public int RunId { get; set; }
    public string BankCsv { get; set; } = string.Empty;
    public string LedgerCsv { get; set; } = string.Empty;
    public ReconciliationRun? Run { get; set; }
}