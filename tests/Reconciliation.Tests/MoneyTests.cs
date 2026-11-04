using Reconciliation.Domain.Matching;

namespace Reconciliation.Tests;

public class MoneyTests
{
    [Theory]
    [InlineData("12.34", 12.34)]
    [InlineData("-3.00", -3.00)]
    [InlineData("12", 12.00)]
    [InlineData("0.05", 0.05)]
    [InlineData("0", 0.00)]
    [InlineData("12.50 ", 12.50)]
    [InlineData(" 10.50 ", 10.50)]
    [InlineData(" 100.5 ", 100.50)]
    [InlineData("+5", 5)]
    [InlineData("-0.05", -0.05)]
    public void TryParse_ValidText_ReturnsAmount(string text, decimal expected)
    {
        Assert.True(Money.TryParse(text, out decimal actual));
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("£12.34")]
    [InlineData("12,34.00")]
    [InlineData("12,34")]
    [InlineData("12.505")]
    [InlineData("$100")]
    [InlineData("€100")]
    [InlineData("GBP 100")]
    [InlineData("")]
    [InlineData("    ")]
    [InlineData("abc")]
    [InlineData("1,234.56")]
    [InlineData(null)]
    public void TryParse_InvalidText_ReturnsFalse(string? text)
    {
        Assert.False(Money.TryParse(text, out decimal amount));
        Assert.Equal(0m, amount);
    }

    [Theory]
    [InlineData(12.34, "12.34")]
    [InlineData(12, "12.00")]
    [InlineData(0.00, "0.00")]
    [InlineData(-12.34, "-12.34")]
    public void Format_Amount_ReturnsTwoDecimalString(decimal amount, string expected)
    {
        var actual = Money.Format(amount);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(1234)]
    [InlineData(12.34)]
    [InlineData(12)]
    [InlineData(0.00)]
    [InlineData(-12.34)]
    [InlineData(-3.00)]
    [InlineData(13.00)]
    [InlineData(0.05)]
    [InlineData(12.50)]
    [InlineData(10.50)]
    [InlineData(100.50)]
    [InlineData(5)]
    [InlineData(-0.05)]
    [InlineData(1234567.89)]
    public void Format_RoundTrip(decimal amount)
    {
        var formatted = Money.Format(amount);
        Assert.True(Money.TryParse(formatted, out var parsedAmount));
        Assert.Equal(amount, parsedAmount);
    }
}