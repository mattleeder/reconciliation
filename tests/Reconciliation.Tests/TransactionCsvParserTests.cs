using Reconciliation.Domain.Entities;
using Reconciliation.Domain.Import;

namespace Reconciliation.Tests;

public class TransactionCsvParserTests
{
    private const string Header = "Date,Amount,Description";

    private static CsvParseResult ParseBank(params string[] lines)
    {
        return TransactionCsvParser.Parse(string.Join("\n", lines), TransactionSource.Bank);
    }

    [Fact]
    public void Parse_ValidRow_ReturnsTransaction()
    {
        var result = ParseBank(Header, "2026-01-05,-12.50,Coffee");

        Assert.Empty(result.Errors);
        var transaction = Assert.Single(result.Transactions);
        Assert.Equal(new DateOnly(2026, 1, 5), transaction.Date);
        Assert.Equal(-12.50m, transaction.Amount);
        Assert.Equal("Coffee", transaction.Description);
        Assert.Equal(TransactionSource.Bank, transaction.Source);
    }

    [Fact]
    public void Parse_LedgerSource_IsAppliedToTransactions()
    {
        var result = TransactionCsvParser.Parse($"{Header}\n2026-01-05,10.00,Rent", TransactionSource.Ledger);

        var transaction = Assert.Single(result.Transactions);
        Assert.Equal(TransactionSource.Ledger, transaction.Source);
    }

    [Fact]
    public void Parse_DescriptionContainsComma_KeepsWholeDescription()
    {
        var result = ParseBank(Header, "2026-01-05,10.00,Salary, January");

        Assert.Empty(result.Errors);
        var transaction = Assert.Single(result.Transactions);
        Assert.Equal("Salary, January", transaction.Description);
    }

    [Fact]
    public void Parse_EmptyDescription_IsValid()
    {
        var result = ParseBank(Header, "2026-01-05,10.00,");

        Assert.Empty(result.Errors);
        var transaction = Assert.Single(result.Transactions);
        Assert.Equal(string.Empty, transaction.Description);
    }

    [Fact]
    public void Parse_BadDate_ReturnsErrorAndSkipsRow()
    {
        var result = ParseBank(Header, "05/01/2026,10.00,x");

        Assert.Empty(result.Transactions);
        var error = Assert.Single(result.Errors);
        Assert.Equal(2, error.LineNumber);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("10.001")]
    [InlineData("£5.00")]
    [InlineData("")]
    public void Parse_BadAmount_ReturnsErrorAndSkipsRow(string amount)
    {
        var result = ParseBank(Header, $"2026-01-05,{amount},x");

        Assert.Empty(result.Transactions);
        var error = Assert.Single(result.Errors);
        Assert.Equal(2, error.LineNumber);
    }

    [Fact]
    public void Parse_TwoColumnsOnly_ReturnsError()
    {
        var result = ParseBank(Header, "2026-01-05,10.00");

        Assert.Empty(result.Transactions);
        var error = Assert.Single(result.Errors);
        Assert.Equal(2, error.LineNumber);
    }

    [Fact]
    public void Parse_GoodAndBadRows_ReturnsGoodRowsAndErrorWithCorrectLine()
    {
        var result = ParseBank(
            Header,
            "2026-01-05,10.00,First",
            "not-a-date,10.00,Second",
            "2026-01-07,30.00,Third");

        Assert.Equal(2, result.Transactions.Count);
        Assert.Equal("First", result.Transactions[0].Description);
        Assert.Equal("Third", result.Transactions[1].Description);
        var error = Assert.Single(result.Errors);
        Assert.Equal(3, error.LineNumber);
    }

    [Theory]
    [InlineData("Date , Amount , Description")]
    [InlineData("date,amount,description")]
    public void Parse_HeaderWithSpacesOrDifferentCase_IsAccepted(string header)
    {
        var result = ParseBank(header, "2026-01-05,10.00,Coffee");

        Assert.Empty(result.Errors);
        Assert.Single(result.Transactions);
    }

    [Theory]
    [InlineData("Foo,Bar,Baz")]
    [InlineData("Date,Amount,Description,Extra")]
    [InlineData("Date,Amount")]
    [InlineData("Amount,Date,Description")]
    [InlineData("")]
    public void Parse_WrongHeader_ReturnsSingleErrorOnLineOneAndNoTransactions(string header)
    {
        var result = ParseBank(header, "2026-01-05,10.00,Coffee");

        Assert.Empty(result.Transactions);
        var error = Assert.Single(result.Errors);
        Assert.Equal(1, error.LineNumber);
    }

    [Fact]
    public void Parse_EmptyText_ReturnsHeaderError()
    {
        var result = TransactionCsvParser.Parse(string.Empty, TransactionSource.Bank);

        Assert.Empty(result.Transactions);
        var error = Assert.Single(result.Errors);
        Assert.Equal(1, error.LineNumber);
    }

    [Fact]
    public void Parse_HeaderOnly_ReturnsNothing()
    {
        var result = ParseBank(Header);

        Assert.Empty(result.Transactions);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Parse_WindowsLineEndings_ParsesTheSameAsUnix()
    {
        var csv = string.Join("\r\n", Header, "2026-01-05,10.00,First", "2026-01-06,20.00,Second");

        var result = TransactionCsvParser.Parse(csv, TransactionSource.Bank);

        Assert.Empty(result.Errors);
        Assert.Equal(2, result.Transactions.Count);
        Assert.Equal("First", result.Transactions[0].Description);
        Assert.Equal("Second", result.Transactions[1].Description);
    }

    [Fact]
    public void Parse_BlankLineInMiddle_IsSkippedAndLaterErrorKeepsItsLineNumber()
    {
        var result = ParseBank(
            Header,
            "2026-01-05,10.00,Good",
            "",
            "bad-date,10.00,Bad");

        Assert.Single(result.Transactions);
        var error = Assert.Single(result.Errors);
        Assert.Equal(4, error.LineNumber);
    }

    [Fact]
    public void Parse_TrailingNewline_IsNotAnError()
    {
        var result = TransactionCsvParser.Parse($"{Header}\n2026-01-05,10.00,Coffee\n", TransactionSource.Bank);

        Assert.Empty(result.Errors);
        Assert.Single(result.Transactions);
    }
}