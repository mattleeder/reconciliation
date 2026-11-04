using Reconciliation.Domain.Entities;

namespace Reconciliation.Domain.Import;

public record CsvParseResult(IReadOnlyList<Transaction> Transactions, IReadOnlyList<CsvRowError> Errors);