# Domain
- `decimal` value used as microsoft documentation lists it as appropriate for financial values. This avoids having to use a `long` with minor units.
- `Money.TryParse` accepts plain decimals with no symbols or separators and for simplicity and at most 2 decimal places to avoid rounding issues.
- `MatchingEngine.Run` runs an exact pass first, then a fuzzy pass then finds duplicates. This ensures a perfect match is never stolen by a fuzzy one.
- `MatchingEngine.FindDuplicates` finds transactions that have the same date, amount and an identical word set, per source. Empty descriptions are never flagged.
- `MatchingEngine.Run` throws `ArgumentException` on a mismatched transaction source as this is a caller bug.
- Unmatched rows are the exception list.
- Duplicates sit in Matches because a Match is just “two transactions the engine thinks are related, and why.” The Kind field (MatchKind.PossibleDuplicate) carries the why.
- `RunStatus` currently has no initialiser. It defaults to 0 which is not defined. The EF treats 0 as not set and leaves it out of the insert.

# Persistence
- **Enums as strings** Rows are readable in SSMS, and reordering the enum later can’t silently change what stored rows mean.
- **Restricted deletes on transactions** A transaction that has matches is not allowed to be deleted. SQL Server rejects two cascade paths to the same table, which is what `Match` would have.
- **One decision per match** A unique index on `MatchId` means this is enforced by the db, not just code.
- **Test Database** Testing uses a separate `Reconciliation_Tests` db, forced by `SqlConnectionStringBuilder` so they can never wipe dev data. Each test starts from a fresh migrated database which also tests migration.

# Import CSV
- **Format** `Date,Amount,Description` The header matching is case-insensitive and with spaces ignored. Dates are `yyyy-MM-dd` only. Amounts are processed by `Money.TryParse`
- **Description** This is the last column. We split on the first 2 commas so description can have commas.
- **Bad rows are collected** Users see every problem.
- **Reconciliation** This is only enabled when both files parse without error. This means a half-imported file can't produce misleading results.
- **Parsing** Lives in Domain (`TransactionCsvParser`) so it can be unit tested.

# Runs
- **`ReconciliationRun`** Every import is a run. Each transaction belongs to one run so importing the same files multiple times creates separate runs and doesn't duplicate or overwrite data. History is kept.
- **`SaveChangesAsync`** This saves a whole run in one go, a failure saves nothing. EF assigns the Ids and sets `RunId`.
- **`Match` has no `RunId`** A match's run is found through transactions, this avoids storing the same information twice which could result in disagreements. The cost is a join.
- **`MatchingEngine` knows nothing about runs**
- **`Restrict` on `Transaction` to `Run`** Deleting a run is a deliberate feature to add later, not something that should happen by accident.

# Review
- **Approve and reject** These mean different things per kind. Exact and fuzzy means a correct pairing, possible duplicate approve means this is a duplicate.
- **One decision per match** Enforced by the database, UI handles the failure case with a message.
- **Unmatched rows** These don't get decisions. A transaction is unmatched if it isn't an exact or fuzzy match. Duplicate pairs don't count as matched.

# Web App
- **Blazor Web App** Interactive server is set per page. No separate API, pages called the databasse directly. Pages without events are static.
- **IDbContextFactory** Short lived.
- **Connection String** This is stored in user secrets locally, or an App Service setting in Azure.

# Data
- **RunInpurConfiguration** We set `.HasMaxLength(1024 * 1024)` this is not enforced here but is enforced in the app. The schema just records it.

# Processing
- **`RunSubmitter`** If the `catch (Exception)` rethrows then the`DbUpdateException` will bubble up to the caller. The page handles `DbUpdateException` and the run remains `Pending`

# Known Limitations
-**Single Currency (GBP)**used for simplicity to avoid having to deal with parsing various currency forms.
- **`MatchingEngine.TryScoreFuzzy`** Uses fixed thresholds of Jaccard Scoring >= 0.5 for the scoring threshold and 3 days for the window.
- **`MatchingEngine.MatchFuzzy`** is a greedy algorithm that uses a tie-break on score, then date, then id. It is deterministic but not globally optimal; a later bank row may be a better match for a transaction.
- **Pinned Version** We have pinned a patched version of `System.Security.Cryptography.Pkcs` due to security vulnerabilities.
- **3 Commas** As description is the last column in the CSV and the split is limited to 3 parts, a thousands separator like `1,000.00` in the amount column won't fail. It will cause the row to be misread as `amount = 1`, `description = 000.00,actual_description`. This means amounts must not contain thousands separators.
- **Persistence Tests** These need a running SQL Server and `RECONCILIATION_CONNECTION`.
- **Row Id** Freshly imported rows all have `Id` 0 while matching. This means the tie-break by Id has no effect on an import, the order of rows decides ties instead.
- **No delete** Runs cannot be deleted, review decisions cannot be undone and unmatched rows cannot be matched by hand.
- **No authentication**
- **Cancellation** On cancellation the run stays `Pending`. Could later be fixed by a timer triggered sweep.