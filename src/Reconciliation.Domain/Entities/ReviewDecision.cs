namespace Reconciliation.Domain.Entities;

public class ReviewDecision
{
    public int Id { get; set; }
    public int MatchId { get; set; }
    public ReviewOutcome Outcome { get; set; }
    public DateTime DecidedAtUtc { get; set; }
    public string? Note { get; set; }
}