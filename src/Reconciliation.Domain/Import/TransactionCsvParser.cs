using System.Globalization;
using Reconciliation.Domain.Entities;
using Reconciliation.Domain.Matching;

namespace Reconciliation.Domain.Import;

public static class TransactionCsvParser
{
    public static CsvParseResult Parse(string csvText, TransactionSource source)
    {
        var lines = csvText.Split("\n");

        var actualHeaders = lines[0].Split(',').Select(h => h.Trim());
        string[] expectedHeaders = ["Date", "Amount", "Description"];
        if (!actualHeaders.SequenceEqual(expectedHeaders, StringComparer.OrdinalIgnoreCase))
        {
            var csvError = new CsvRowError(1, $"Expected header 'Date,Amount,Description' but found '{lines[0].Trim()}'");
            return new CsvParseResult([], [csvError]);
        }

        var transactions = new List<Transaction>();
        var errors = new List<CsvRowError>();

        for(int index = 1; index < lines.Length; index++)
        {
            var lineNumber = index + 1;
            var line = lines[index];

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var trimmedLine = line.Trim();
            var cols = trimmedLine.Split(",", 3);

            if (cols.Length != 3)
            {
                var csvError = new CsvRowError(lineNumber, $"Expected 3 columns but found {cols.Length}");
                errors.Add(csvError);
                continue;
            }

            var dateText = cols[0].Trim();
            var amountText = cols[1].Trim();
            var descriptionText = cols[2].Trim();

            if (!DateOnly.TryParseExact(dateText, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly date))
            {
                var csvError = new CsvRowError(lineNumber, $"Could not parse date '{dateText}'");
                errors.Add(csvError);
                continue;
            }

            if (!Money.TryParse(amountText, out decimal amount))
            {
                var csvError = new CsvRowError(lineNumber, $"Could not parse amount '{amountText}'");
                errors.Add(csvError);
                continue;
            }

            transactions.Add(new Transaction
            {
                Source = source,
                Date = date,
                Amount = amount,
                Description = descriptionText, 
            });
            
        }

        return new CsvParseResult(transactions, errors);
    }
}