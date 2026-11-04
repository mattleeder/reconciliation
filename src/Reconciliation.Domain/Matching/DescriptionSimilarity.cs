using System.Text.RegularExpressions;

namespace Reconciliation.Domain.Matching;

public static class DescriptionSimilarity
{
    private static readonly Regex WordPattern = new Regex(@"[\p{L}\p{Nd}]+");
    private static HashSet<string> GetWordSet(string text)
    {
        var words = WordPattern.Matches(text.ToLowerInvariant()).Select(m => m.Value);
        return new HashSet<string>(words);
    }

    /// <summary>
    /// Scores how similar two descriptions are, from 0 (nothing in common) to 1 (identical).
    /// Case and punctuation are ignored. Two empty descriptions score 0.
    /// </summary>
    public static double Score(string a, string b)
    {
        var hashSetA = GetWordSet(a);
        var hashSetB = GetWordSet(b);

        var intersection = new HashSet<string>(hashSetA);
        intersection.IntersectWith(hashSetB);

        var unionCount = hashSetA.Count + hashSetB.Count - intersection.Count;

        if (unionCount == 0)
        {
            return 0;
        }

        return (double)intersection.Count / unionCount;
    }
}