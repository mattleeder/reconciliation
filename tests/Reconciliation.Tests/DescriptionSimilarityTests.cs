namespace Reconciliation.Tests;

using Reconciliation.Domain.Matching;

public class DescriptionSimilarityTests
{
    [Theory]
    [InlineData("TESCO STORES 4521", "Tesco Stores", 2.0/3.0)]
    [InlineData("Tesco Stores", "Tesco Stores", 1.0)]
    [InlineData("Tesco", "Amazon", 0.0)]
    [InlineData("Tesco-Stores!", "tesco stores", 1.0)]
    [InlineData("", "", 0.0)]
    [InlineData("", "Tesco", 0.0)]
    [InlineData("a a b", "a b", 1.0)]
    [InlineData("Tesco 4521", "Tesco 4522", 1.0/3.0)]
    public void Score_TwoStrings_ReturnsScore(string a, string b, double expectedScore)
    {
        var actualScore = DescriptionSimilarity.Score(a, b);
        Assert.Equal(expectedScore, actualScore, precision: 5);
    }
}