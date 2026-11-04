namespace Reconciliation.Domain.Entities;

public class Match
{
    public int Id { get; set; }
    public int LeftTransactionId { get; set; }
    public int RightTransactionId { get; set; }
    public Transaction? LeftTransaction { get; set; }
    public Transaction? RightTransaction { get; set; }
    public MatchKind Kind { get; set; }
    public double Confidence { get; set; }
    public ReviewDecision? Decision { get; set; }
}