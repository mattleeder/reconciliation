using Reconciliation.Domain.Matching;
using Reconciliation.Domain.Entities;

namespace Reconciliation.Tests;

public class MatchingEngineTests
{

    private static Transaction Tx(TransactionSource source, string date, decimal amount, string description = "", int id = 0)
    {
        return new Transaction
        {
            Id = id,
            Source = source,
            Date = DateOnly.Parse(date),
            Amount = amount,
            Description = description
        };
    }

    [Fact]
    public void Run_SameAmountAndDate_ReturnsOneExactMatch()
    {
        var bank = new List<Transaction> { Tx(TransactionSource.Bank, "2026-01-05", 10m) };
        var ledger = new List<Transaction> { Tx(TransactionSource.Ledger, "2026-01-05", 10m) };

        var result = MatchingEngine.Run(bank, ledger);

        var match = Assert.Single(result.Matches);
        Assert.Equal(MatchKind.ExactAmountAndDate, match.Kind);
        Assert.Equal(1.0, match.Confidence);
    }

    [Fact]
    public void Run_SameAmountDifferentDate_ReturnsNoMatches()
    {
        var bank = new List<Transaction> { Tx(TransactionSource.Bank, "2026-01-05", 10m) };
        var ledger = new List<Transaction> { Tx(TransactionSource.Ledger, "2026-01-01", 10m) };

        var result = MatchingEngine.Run(bank, ledger);

        Assert.Empty(result.Matches);
        Assert.Contains(bank[0], result.UnmatchedBank);
        Assert.Contains(ledger[0], result.UnmatchedLedger);
    }

    [Fact]
    public void Run_SameDateDifferentAmount_ReturnsNoMatches()
    {
        var bank = new List<Transaction> { Tx(TransactionSource.Bank, "2026-01-01", 100m) };
        var ledger = new List<Transaction> { Tx(TransactionSource.Ledger, "2026-01-01", 10m) };

        var result = MatchingEngine.Run(bank, ledger);

        Assert.Empty(result.Matches);
        Assert.Single(result.UnmatchedBank);
        Assert.Single(result.UnmatchedLedger);
    }

    [Fact]
    public void Run_TwoBankRowsOneLedgerRow_ReturnsOneExactMatch()
    {
        var bank = new List<Transaction> 
        { 
            Tx(TransactionSource.Bank, "2026-01-01", 10m),
            Tx(TransactionSource.Bank, "2026-01-01", 10m),
        };
        var ledger = new List<Transaction> { Tx(TransactionSource.Ledger, "2026-01-01", 10m) };

        var result = MatchingEngine.Run(bank, ledger);

        Assert.Single(result.Matches);
        Assert.Single(result.UnmatchedBank);
        Assert.Empty(result.UnmatchedLedger);
    }

    [Fact]
    public void Run_IdenticalBankRows_MatchesLowerIdFirst()
    {
        var bank = new List<Transaction> 
        { 
            Tx(TransactionSource.Bank, "2026-01-01", 10m, id: 2),
            Tx(TransactionSource.Bank, "2026-01-01", 10m, id: 1),
        };
        var ledger = new List<Transaction> { Tx(TransactionSource.Ledger, "2026-01-01", 10m, id: 1) };

        var result = MatchingEngine.Run(bank, ledger);

        var matched = Assert.Single(result.Matches);
        var unmatched = Assert.Single(result.UnmatchedBank);
        Assert.Empty(result.UnmatchedLedger);

        Assert.Equal(1, matched.LeftTransaction!.Id);
        Assert.Equal(2, unmatched.Id);
    }

    [Fact]
    public void Run_TwoBankRowsTwoLedgerRows_ReturnsTwoMatchesAndNoRepeatLedgerRows()
    {
        var bank1 = Tx(TransactionSource.Bank, "2026-01-01", 10m);
        var bank2 = Tx(TransactionSource.Bank, "2026-01-01", 10m);
        var ledger1 = Tx(TransactionSource.Ledger, "2026-01-01", 10m);
        var ledger2 = Tx(TransactionSource.Ledger, "2026-01-01", 10m);

        var result = MatchingEngine.Run(
            new List<Transaction> { bank1, bank2 },
            new List<Transaction> { ledger1, ledger2 });

        Assert.Equal(2, result.Matches.Count);

        var matchedBank = result.Matches.Select(m => m.LeftTransaction);
        var matchedLedger = result.Matches.Select(m => m.RightTransaction);
        Assert.Contains(bank1, matchedBank);
        Assert.Contains(bank2, matchedBank);
        Assert.Contains(ledger1, matchedLedger);
        Assert.Contains(ledger2, matchedLedger);

        Assert.Empty(result.UnmatchedBank);
        Assert.Empty(result.UnmatchedLedger);
    }

    [Fact]
    public void Run_BothListsEmpty_ReturnsNoMatchWithoutException()
    {
        var result = MatchingEngine.Run(
            new List<Transaction> { },
            new List<Transaction> { });

        Assert.Empty(result.Matches);
        Assert.Empty(result.UnmatchedBank);
        Assert.Empty(result.UnmatchedLedger);
    }


    [Fact]
    public void Run_SameAmountAndDate_LeftTransactionIsBankRowRightTransactionIsLedgerRow()
    {
        var bank = new List<Transaction> { Tx(TransactionSource.Bank, "2026-01-05", 10m) };
        var ledger = new List<Transaction> { Tx(TransactionSource.Ledger, "2026-01-05", 10m) };

        var result = MatchingEngine.Run(bank, ledger);

        var match = Assert.Single(result.Matches);
        Assert.Equal(MatchKind.ExactAmountAndDate, match.Kind);
        Assert.Same(bank[0], match.LeftTransaction);
        Assert.Same(ledger[0], match.RightTransaction);
    }

    [Fact]
    public void Run_LedgerRowInBankList_ThrowsArgumentException()
    {
        var bank = new List<Transaction> { Tx(TransactionSource.Ledger, "2026-01-01", 10m) };
        var ledger = new List<Transaction>();

        var exception = Assert.Throws<ArgumentException>(() => MatchingEngine.Run(bank, ledger));
        Assert.Equal("bank", exception.ParamName);
    }

    [Fact]
    public void Run_BankRowInLedgerList_ThrowsArgumentException()
    {
        var bank = new List<Transaction>();
        var ledger = new List<Transaction> { Tx(TransactionSource.Bank, "2026-01-01", 10m) };

        var exception = Assert.Throws<ArgumentException>(() => MatchingEngine.Run(bank, ledger));
        Assert.Equal("ledger", exception.ParamName);
    }

    [Fact]
    public void Run_SameAmountOneDayApartIdenticalDescription_ReturnsFuzzyMatch()
    {
        var bank = new List<Transaction> { Tx(TransactionSource.Bank, "2026-01-05", 10m, "Tesco Stores") };
        var ledger = new List<Transaction> { Tx(TransactionSource.Ledger, "2026-01-06", 10m, "Tesco Stores") };

        var result = MatchingEngine.Run(bank, ledger);

        var match = Assert.Single(result.Matches);
        Assert.Equal(MatchKind.FuzzyDescription, match.Kind);
        Assert.Equal(1.0, match.Confidence);
        Assert.Same(bank[0], match.LeftTransaction);
        Assert.Same(ledger[0], match.RightTransaction);
        Assert.Empty(result.UnmatchedBank);
        Assert.Empty(result.UnmatchedLedger);
    }

    [Fact]
    public void Run_SameAmountWithinWindowUnrelatedDescriptions_ReturnsNoMatch()
    {
        var bank = new List<Transaction> { Tx(TransactionSource.Bank, "2026-01-05", 10m, "Tesco") };
        var ledger = new List<Transaction> { Tx(TransactionSource.Ledger, "2026-01-06", 10m, "Amazon") };

        var result = MatchingEngine.Run(bank, ledger);

        Assert.Empty(result.Matches);
        Assert.Same(bank[0], Assert.Single(result.UnmatchedBank));
        Assert.Same(ledger[0], Assert.Single(result.UnmatchedLedger));
    }

    [Fact]
    public void Run_SimilarDescriptionBeyondDateWindow_ReturnsNoMatch()
    {
        var bank = new List<Transaction> { Tx(TransactionSource.Bank, "2026-01-01", 10m, "Tesco Stores") };
        var ledger = new List<Transaction> { Tx(TransactionSource.Ledger, "2026-01-05", 10m, "Tesco Stores") };

        var result = MatchingEngine.Run(bank, ledger);

        Assert.Empty(result.Matches);
        Assert.Single(result.UnmatchedBank);
        Assert.Single(result.UnmatchedLedger);
    }

    [Fact]
    public void Run_DifferentAmountIdenticalDescription_ReturnsNoMatch()
    {
        var bank = new List<Transaction> { Tx(TransactionSource.Bank, "2026-01-05", 10m, "Tesco Stores") };
        var ledger = new List<Transaction> { Tx(TransactionSource.Ledger, "2026-01-05", 20m, "Tesco Stores") };

        var result = MatchingEngine.Run(bank, ledger);

        Assert.Empty(result.Matches);
        Assert.Single(result.UnmatchedBank);
        Assert.Single(result.UnmatchedLedger);
    }

    [Fact]
    public void Run_ExactCandidateAndFuzzyCandidate_ExactTakesPriority()
    {
        var bankRow = Tx(TransactionSource.Bank, "2026-01-05", 10m, "Tesco");
        var exactLedger = Tx(TransactionSource.Ledger, "2026-01-05", 10m, "Amazon");
        var fuzzyLedger = Tx(TransactionSource.Ledger, "2026-01-06", 10m, "Tesco");

        var result = MatchingEngine.Run(
            new List<Transaction> { bankRow },
            new List<Transaction> { exactLedger, fuzzyLedger });

        var match = Assert.Single(result.Matches);
        Assert.Equal(MatchKind.ExactAmountAndDate, match.Kind);
        Assert.Same(exactLedger, match.RightTransaction);
        Assert.Same(fuzzyLedger, Assert.Single(result.UnmatchedLedger));
    }

    [Fact]
    public void Run_TwoFuzzyCandidates_ChoosesHigherScore()
    {
        var bankRow = Tx(TransactionSource.Bank, "2026-01-05", 10m, "Tesco Stores");
        var lowerScore = Tx(TransactionSource.Ledger, "2026-01-06", 10m, "Tesco Stores Express Extra", id: 1);
        var higherScore = Tx(TransactionSource.Ledger, "2026-01-06", 10m, "Tesco Stores Express", id: 2);

        var result = MatchingEngine.Run(
            new List<Transaction> { bankRow },
            new List<Transaction> { lowerScore, higherScore });

        var match = Assert.Single(result.Matches);
        Assert.Same(higherScore, match.RightTransaction);
        Assert.Same(lowerScore, Assert.Single(result.UnmatchedLedger));
    }

    [Fact]
    public void Run_TwoFuzzyCandidatesEqualScore_ChoosesEarlierDate()
    {
        var bankRow = Tx(TransactionSource.Bank, "2026-01-05", 10m, "Tesco Stores");
        var later = Tx(TransactionSource.Ledger, "2026-01-07", 10m, "Tesco Stores", id: 1);
        var earlier = Tx(TransactionSource.Ledger, "2026-01-06", 10m, "Tesco Stores", id: 2);

        var result = MatchingEngine.Run(
            new List<Transaction> { bankRow },
            new List<Transaction> { later, earlier });

        var match = Assert.Single(result.Matches);
        Assert.Same(earlier, match.RightTransaction);
    }

    [Fact]
    public void Run_TwoFuzzyCandidatesEqualScoreAndDate_ChoosesLowerId()
    {
        var bankRow = Tx(TransactionSource.Bank, "2026-01-05", 10m, "Tesco Stores");
        var higherId = Tx(TransactionSource.Ledger, "2026-01-06", 10m, "Tesco Stores Alpha", id: 5);
        var lowerId = Tx(TransactionSource.Ledger, "2026-01-06", 10m, "Tesco Stores Beta", id: 3);

        var result = MatchingEngine.Run(
            new List<Transaction> { bankRow },
            new List<Transaction> { higherId, lowerId });

        var match = Assert.Single(result.Matches);
        Assert.Same(lowerId, match.RightTransaction);
    }

    [Fact]
    public void Run_FuzzyMatch_ConfidenceEqualsDescriptionScore()
    {
        var bank = new List<Transaction> { Tx(TransactionSource.Bank, "2026-01-05", 10m, "Tesco Stores") };
        var ledger = new List<Transaction> { Tx(TransactionSource.Ledger, "2026-01-06", 10m, "Tesco Stores 4521") };

        var result = MatchingEngine.Run(bank, ledger);

        var match = Assert.Single(result.Matches);
        Assert.Equal(2.0 / 3.0, match.Confidence, precision: 5);
    }

    [Fact]
    public void Run_DescriptionScoreExactlyAtThreshold_ReturnsFuzzyMatch()
    {
        var bank = new List<Transaction> { Tx(TransactionSource.Bank, "2026-01-05", 10m, "a b") };
        var ledger = new List<Transaction> { Tx(TransactionSource.Ledger, "2026-01-06", 10m, "a b c d") };

        var result = MatchingEngine.Run(bank, ledger);

        var match = Assert.Single(result.Matches);
        Assert.Equal(0.5, match.Confidence);
    }

    [Theory]
    [InlineData("2026-01-08", 1)]
    [InlineData("2026-01-02", 1)]
    [InlineData("2026-01-09", 0)]
    [InlineData("2026-01-01", 0)]
    public void Run_LedgerDateAtWindowEdge_MatchesOnlyWithinThreeDays(string ledgerDate, int expectedMatches)
    {
        var bank = new List<Transaction> { Tx(TransactionSource.Bank, "2026-01-05", 10m, "Tesco Stores") };
        var ledger = new List<Transaction> { Tx(TransactionSource.Ledger, ledgerDate, 10m, "Tesco Stores") };

        var result = MatchingEngine.Run(bank, ledger);

        Assert.Equal(expectedMatches, result.Matches.Count);
    }

    [Fact]
    public void Run_TwoBankRowsOneFuzzyCandidate_LedgerRowIsNotReused()
    {
        var bank1 = Tx(TransactionSource.Bank, "2026-01-05", 10m, "Tesco Stores", id: 1);
        var bank2 = Tx(TransactionSource.Bank, "2026-01-05", 10m, "Tesco Stores Metro", id: 2);
        var ledgerRow = Tx(TransactionSource.Ledger, "2026-01-06", 10m, "Tesco Stores");

        var result = MatchingEngine.Run(
            new List<Transaction> { bank1, bank2 },
            new List<Transaction> { ledgerRow });

        var match = Assert.Single(result.Matches);
        Assert.Same(bank1, match.LeftTransaction);
        Assert.Same(bank2, Assert.Single(result.UnmatchedBank));
        Assert.Empty(result.UnmatchedLedger);
    }

    [Fact]
    public void Run_TwoIdenticalBankRows_ReturnsDuplicateMatch()
    {
        var original = Tx(TransactionSource.Bank, "2026-01-05", 10m, "Tesco Stores", id: 1);
        var copy = Tx(TransactionSource.Bank, "2026-01-05", 10m, "Tesco Stores", id: 2);

        var result = MatchingEngine.Run(
            new List<Transaction> { original, copy },
            new List<Transaction>());

        var match = Assert.Single(result.Matches);
        Assert.Equal(MatchKind.PossibleDuplicate, match.Kind);
        Assert.Equal(1.0, match.Confidence);
        Assert.Same(original, match.LeftTransaction);
        Assert.Same(copy, match.RightTransaction);
        Assert.Equal(2, result.UnmatchedBank.Count);
        Assert.Empty(result.UnmatchedLedger);
    }

    [Fact]
    public void Run_TwoIdenticalLedgerRows_ReturnsDuplicateMatch()
    {
        var original = Tx(TransactionSource.Ledger, "2026-01-05", 10m, "Tesco Stores", id: 1);
        var copy = Tx(TransactionSource.Ledger, "2026-01-05", 10m, "Tesco Stores", id: 2);

        var result = MatchingEngine.Run(
            new List<Transaction>(),
            new List<Transaction> { original, copy });

        var match = Assert.Single(result.Matches);
        Assert.Equal(MatchKind.PossibleDuplicate, match.Kind);
        Assert.Same(original, match.LeftTransaction);
        Assert.Same(copy, match.RightTransaction);
        Assert.Empty(result.UnmatchedBank);
        Assert.Equal(2, result.UnmatchedLedger.Count);
    }

    [Fact]
    public void Run_SameDateAndAmountUnrelatedDescriptions_ReturnsNoDuplicate()
    {
        var bank = new List<Transaction>
        {
            Tx(TransactionSource.Bank, "2026-01-05", 10m, "Tesco", id: 1),
            Tx(TransactionSource.Bank, "2026-01-05", 10m, "Amazon", id: 2),
        };

        var result = MatchingEngine.Run(bank, new List<Transaction>());

        Assert.Empty(result.Matches);
    }

    [Fact]
    public void Run_SameAmountAndDescriptionDifferentDates_ReturnsNoDuplicate()
    {
        var bank = new List<Transaction>
        {
            Tx(TransactionSource.Bank, "2026-01-05", 10m, "Tesco Stores", id: 1),
            Tx(TransactionSource.Bank, "2026-01-06", 10m, "Tesco Stores", id: 2),
        };

        var result = MatchingEngine.Run(bank, new List<Transaction>());

        Assert.Empty(result.Matches);
    }

    [Fact]
    public void Run_SameDateAndDescriptionDifferentAmounts_ReturnsNoDuplicate()
    {
        var bank = new List<Transaction>
        {
            Tx(TransactionSource.Bank, "2026-01-05", 10m, "Tesco Stores", id: 1),
            Tx(TransactionSource.Bank, "2026-01-05", 20m, "Tesco Stores", id: 2),
        };

        var result = MatchingEngine.Run(bank, new List<Transaction>());

        Assert.Empty(result.Matches);
    }

    [Fact]
    public void Run_DescriptionsDifferOnlyByCaseAndPunctuation_ReturnsDuplicate()
    {
        var bank = new List<Transaction>
        {
            Tx(TransactionSource.Bank, "2026-01-05", 10m, "Tesco-Stores!", id: 1),
            Tx(TransactionSource.Bank, "2026-01-05", 10m, "tesco stores", id: 2),
        };

        var result = MatchingEngine.Run(bank, new List<Transaction>());

        var match = Assert.Single(result.Matches);
        Assert.Equal(MatchKind.PossibleDuplicate, match.Kind);
    }

    [Fact]
    public void Run_ThreeIdenticalBankRows_PairsEachCopyWithTheFirstRow()
    {
        var first = Tx(TransactionSource.Bank, "2026-01-05", 10m, "Tesco Stores", id: 1);
        var second = Tx(TransactionSource.Bank, "2026-01-05", 10m, "Tesco Stores", id: 2);
        var third = Tx(TransactionSource.Bank, "2026-01-05", 10m, "Tesco Stores", id: 3);

        var result = MatchingEngine.Run(
            new List<Transaction> { first, second, third },
            new List<Transaction>());

        Assert.Equal(2, result.Matches.Count);
        Assert.All(result.Matches, m => Assert.Same(first, m.LeftTransaction));
        var copies = result.Matches.Select(m => m.RightTransaction);
        Assert.Contains(second, copies);
        Assert.Contains(third, copies);
    }

    [Fact]
    public void Run_IdenticalBankAndLedgerRows_ReturnsExactMatchAndNoDuplicate()
    {
        var bank = new List<Transaction> { Tx(TransactionSource.Bank, "2026-01-05", 10m, "Tesco Stores") };
        var ledger = new List<Transaction> { Tx(TransactionSource.Ledger, "2026-01-05", 10m, "Tesco Stores") };

        var result = MatchingEngine.Run(bank, ledger);

        var match = Assert.Single(result.Matches);
        Assert.Equal(MatchKind.ExactAmountAndDate, match.Kind);
    }

    [Fact]
    public void Run_TwoIdenticalBankRowsOneIdenticalLedgerRow_ReturnsExactMatchAndDuplicate()
    {
        var original = Tx(TransactionSource.Bank, "2026-01-05", 10m, "Tesco Stores", id: 1);
        var copy = Tx(TransactionSource.Bank, "2026-01-05", 10m, "Tesco Stores", id: 2);
        var ledgerRow = Tx(TransactionSource.Ledger, "2026-01-05", 10m, "Tesco Stores");

        var result = MatchingEngine.Run(
            new List<Transaction> { original, copy },
            new List<Transaction> { ledgerRow });

        Assert.Equal(2, result.Matches.Count);

        var exact = Assert.Single(result.Matches, m => m.Kind == MatchKind.ExactAmountAndDate);
        Assert.Same(original, exact.LeftTransaction);
        Assert.Same(ledgerRow, exact.RightTransaction);

        var duplicate = Assert.Single(result.Matches, m => m.Kind == MatchKind.PossibleDuplicate);
        Assert.Same(original, duplicate.LeftTransaction);
        Assert.Same(copy, duplicate.RightTransaction);

        Assert.Same(copy, Assert.Single(result.UnmatchedBank));
        Assert.Empty(result.UnmatchedLedger);
    }

    [Fact]
    public void Run_IdenticalBankRowsInReverseOrder_OriginalIsTheLowerId()
    {
        var original = Tx(TransactionSource.Bank, "2026-01-05", 10m, "Tesco Stores", id: 1);
        var copy = Tx(TransactionSource.Bank, "2026-01-05", 10m, "Tesco Stores", id: 2);

        var result = MatchingEngine.Run(
            new List<Transaction> { copy, original },
            new List<Transaction>());

        var match = Assert.Single(result.Matches);
        Assert.Same(original, match.LeftTransaction);
        Assert.Same(copy, match.RightTransaction);
    }

    [Fact]
    public void Run_IdenticalRowsWithEmptyDescriptions_AreNotFlaggedAsDuplicates()
    {
        var bank = new List<Transaction>
        {
            Tx(TransactionSource.Bank, "2026-01-05", 10m, "", id: 1),
            Tx(TransactionSource.Bank, "2026-01-05", 10m, "", id: 2),
        };

        var result = MatchingEngine.Run(bank, new List<Transaction>());

        Assert.Empty(result.Matches);
    }
}