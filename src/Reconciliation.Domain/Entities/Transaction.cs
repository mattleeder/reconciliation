namespace Reconciliation.Domain.Entities;

public class Transaction
{
    public int Id { get; set; }
    public TransactionSource Source { get; set; }
    public DateOnly Date { get; set; }

    /// <summary>
    /// This project assumes a single currency (pound sterling) with two decimal places.
    /// </summary>
    public decimal Amount { get; set; }
    public string Description { get; set; } = string.Empty;
    public int RunId { get; set; }
    public ReconciliationRun? Run { get; set; }
}