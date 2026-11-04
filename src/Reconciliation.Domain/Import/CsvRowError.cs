namespace Reconciliation.Domain.Import;

public record CsvRowError(int LineNumber, string Message);