using System.Globalization;

namespace Reconciliation.Domain.Matching;

public static class Money
{
    public static bool TryParse(string? text, out decimal amount)
    {
        var style = NumberStyles.AllowLeadingWhite
            | NumberStyles.AllowTrailingWhite
            | NumberStyles.AllowLeadingSign
            | NumberStyles.AllowDecimalPoint;
        var formatProvider = CultureInfo.InvariantCulture;
        var parseSuccess = Decimal.TryParse(text, style, formatProvider, out amount);

        if (!parseSuccess || amount != Decimal.Round(amount, 2))
        {
            amount = 0;
            return false;
        }

        return true;
    }

    public static string Format(decimal amount)
    {
        var formatProvider = CultureInfo.InvariantCulture;
        return amount.ToString("F2", formatProvider);
    }
}