- `decimal` value used as microsoft documentation lists it as appropriate for financial values. This avoids having to use a `long` with minor units.
- `Money.TryParse` accepts plain decimals with no symbols or separators and for simplicity and at most 2 decimal places to avoid rounding issues.
- `MatchingEngine.Run` runs an exact pass first, then a fuzzy pass then finds duplicates. This ensures a perfect match is never stolen by a fuzzy one.
- `MatchingEngine.FindDuplicates` finds transactions that have the same date, amount and an identical word set, per source. Empty descriptions are never flagged.
- `MatchingEngine.Run` throws `ArgumentException` on a mismatched transaction source as this is a caller bug.
- Unmatched rows are the exception list.
- Duplicates sit in Matches because a Match is just “two transactions the engine thinks are related, and why.” The Kind field (MatchKind.PossibleDuplicate) carries the why.

# Persistence
- **Enums as strings** Rows are readable in SSMS, and reordering the enum later can’t silently change what stored rows mean.
- **Restricted deletes on transactions** A transaction that has matches is not allowed to be deleted. SQL Server rejects two cascade paths to the same table, which is what `Match` would have.
- **One decision per match** A unique index on `MatchId` means this is enforced by the db, not just code.
- **Test Database** Testing uses a separate `Reconciliation_Tests` db, forced by `SqlConnectionStringBuilder` so they can never wipe dev data. Each test starts from a fresh migrated database which also tests migration.

# Known Limitations
- Single Currency (GBP) used for simplicity to avoid having to deal with parsing various currency forms.
- `MatchingEngine.TryScoreFuzzy` Uses fixed thresholds of Jaccard Scoring >= 0.5 for the scoring threshold and 3 days for the window.
- `MatchingEngine.MatchFuzzy` is a greedy algorithm that uses a tie-break on score, then date, then id. It is deterministic but not globally optimal; a later bank row may be a better match for a transaction.
- We have pinned a patched version of System.Security.Cryptography.Pkcs due to security vulnerabilities.
- As description is the last column in the CV and the split is limited to 3 parts, a thousands separator like `1,000.00` in the amount column won't fail. It will cause the row to be mistead as `amount = 1`, `description = 000.00,actual_description`. This means amounts must not contain thousands separators.