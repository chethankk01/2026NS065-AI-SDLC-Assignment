# Feature Specification: SpendWise Expense & Budget Tracker CLI

**Feature Branch**: `001-expense-budget-tracker`
**Created**: 2026-11-15
**Status**: Draft - pending human review
**Input**: "A deterministic C# command-line expense tracker: record expenses, categorise them, set monthly category budgets, and produce a monthly summary with budget status. Data persists in a local JSON file."

## Clarifications

### Session 2026-11-15 (recommended answers, to be confirmed by reviewer)

- Q: Are categories free-text or fixed? -> A: Fixed set: Food, Transport, Rent, Utilities, Entertainment, Health, Other. Input is case-insensitive and stored in canonical casing.
- Q: How are amounts with more than 2 decimals handled? -> A: Rejected with a validation error (no silent rounding of user input). Rounding applies only to computed values (percentages).
- Q: Is an expense dated in the future allowed? -> A: No. Date must be <= today (from `IClock`) and >= 2000-01-01.
- Q: What is the budget status boundary? -> A: spent/limit < 80% = OK; 80% <= ratio <= 100% = WARNING; ratio > 100% = EXCEEDED. Exactly 100% is WARNING.
- Q: Are deleted IDs reused? -> A: No. IDs increase monotonically and are never reused.
- Q: One budget per what? -> A: One budget per (category, month). Setting it again overwrites the previous limit.

### Change history

- 2026-11-15 (during /speckit.plan review): FR-009 clarified - the summary lists every category that has an expense OR a budget in the month (a budget-only category shows spent `0.00`, status `OK`). The "No expenses for <month>." message applies when the month has no expenses, regardless of budgets. No acceptance criteria were weakened.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Record and list expenses (Priority: P1)

A user records spending and views it, filtered and sorted.

**Why this priority**: Core value; every other feature depends on stored expenses.

**Independent Test**: Run `add` several times then `list`; verify stored data and output order.

**Acceptance Scenarios**:

1. **Given** an empty store, **When** `add --amount 12.50 --category food --date 2026-11-10 --note "Lunch"` runs, **Then** exit code is 0, stdout is `Added expense #1: 12.50 Food 2026-11-10`, and the store contains exactly one expense with id 1, amount 12.50, category `Food`.
2. **Given** expenses on dates 2026-11-12, 2026-11-03, 2026-11-03, **When** `list` runs, **Then** output is ordered by date ascending, ties broken by id ascending.
3. **Given** expenses in Food and Rent, **When** `list --category rent`, **Then** only Rent expenses are shown.
4. **Given** expenses in October and November 2026, **When** `list --month 2026-11`, **Then** only November expenses are shown.
5. **Given** no matching expenses, **When** `list` runs, **Then** stdout is `No expenses found.` and exit code is 0.

### User Story 2 - Validation and safe failure (Priority: P1)

Invalid input never corrupts data and always gives a clear error.

**Independent Test**: Run `add` with each invalid input; verify exit code 1, message on stderr, store unchanged.

**Acceptance Scenarios**:

1. **Given** any store, **When** amount is `0`, `-5`, `abc`, `1000000.01`, or `10.999`, **Then** exit code is 1, stderr contains `Invalid amount`, and the store is unchanged.
2. **Given** any store, **When** category is not in the fixed set, **Then** exit code is 1 and stderr contains `Unknown category`.
3. **Given** clock = 2026-11-15, **When** date is `2026-11-16`, `1999-12-31`, or `2026-02-30`, **Then** exit code is 1 and stderr contains `Invalid date`.
4. **Given** a note longer than 200 characters, **Then** exit code is 1 and stderr contains `Note too long`.
5. **Given** a required option is missing, **Then** exit code is 1 and stderr contains `Missing required option`.
6. **Given** a corrupt or unreadable store file, **When** any command runs, **Then** exit code is 2, stderr contains `Storage error`, and the file is not overwritten.

### User Story 3 - Delete an expense (Priority: P2)

**Independent Test**: Add then delete, verify removal and non-reuse of IDs.

**Acceptance Scenarios**:

1. **Given** expenses #1 and #2, **When** `delete --id 1`, **Then** exit code 0, stdout `Deleted expense #1`, and only #2 remains.
2. **Given** expense #2 was the highest id and was deleted, **When** a new expense is added, **Then** it receives id 3 (ids are never reused).
3. **Given** no expense with id 99, **When** `delete --id 99`, **Then** exit code 1 and stderr contains `Expense not found`.

### User Story 4 - Monthly budgets and summary (Priority: P2)

**Independent Test**: Set budgets, add expenses, run `summary --month`, verify totals and status.

**Acceptance Scenarios**:

1. **Given** `budget set --category food --month 2026-11 --limit 200.00`, **Then** exit code 0 and stdout `Budget set: Food 2026-11 200.00`; setting it again overwrites the limit.
2. **Given** Food expenses 50.00 and 30.25 in 2026-11 with limit 200.00, **When** `summary --month 2026-11`, **Then** Food row shows spent `80.25`, percent `40.13%`, status `OK`.
3. **Given** Food limit 200.00, **When** spent is exactly `160.00`, **Then** status is `WARNING` (80.00%); at `200.00` status is `WARNING` (100.00%); at `200.01` status is `EXCEEDED`; at `159.99` status is `OK`.
4. **Given** spending in a category with no budget, **Then** that row shows budget `-`, percent `-`, status `NO BUDGET`.
5. **Given** a month with no expenses, **Then** stdout is `No expenses for 2026-11.` and the total is not printed.
6. **Given** any month, **Then** the grand total equals the exact decimal sum of the month's expenses, and category rows are ordered alphabetically.
7. **Given** `--json`, **Then** summary output is valid JSON with the same values and a stable property order.

### Edge Cases

- Amount `0.01` (minimum) and `1000000.00` (maximum) are accepted.
- Amount written with a comma (`12,50`) is rejected as `Invalid amount` (culture-invariant parsing only).
- Month `2026-13` or `2026-1` is rejected as `Invalid month`.
- Sum of 0.1 + 0.2 expenses equals exactly `0.30` (decimal arithmetic).
- Concurrent/interrupted write must not leave a partially written file (atomic replace).
- Path of the store file comes from `--store` or defaults to `./spendwise.json`; paths containing `..` that escape the working directory are rejected.
- Option values beginning with `=`, `+`, `-`, `@` in notes are stored verbatim (no formula injection concerns in JSON, but they must round-trip unchanged).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: `add` MUST create an expense with auto-assigned monotonically increasing id, amount, category, date, optional note.
- **FR-002**: Amount MUST satisfy 0.01 <= amount <= 1,000,000.00 with at most 2 decimals, parsed with invariant culture.
- **FR-003**: Category MUST be one of the fixed set, case-insensitive, stored in canonical casing.
- **FR-004**: Date MUST be `yyyy-MM-dd`, a real calendar date, >= 2000-01-01 and <= today per `IClock`; default is today.
- **FR-005**: Note MUST be <= 200 characters and stored verbatim.
- **FR-006**: `list` MUST support `--category` and `--month yyyy-MM` filters (combinable), sorted by date asc then id asc.
- **FR-007**: `delete --id` MUST remove one expense; IDs are never reused.
- **FR-008**: `budget set` MUST store one limit per (category, month), overwriting prior value; limit must satisfy the amount rule.
- **FR-009**: `summary --month` MUST report per-category spent, budget, percent (2 dp, MidpointRounding.AwayFromZero), status, and the grand total.
- **FR-010**: Status rules: ratio < 80% `OK`; 80% <= ratio <= 100% `WARNING`; > 100% `EXCEEDED`; no budget `NO BUDGET`. Comparison MUST use exact decimal arithmetic, not the rounded percent.
- **FR-011**: `--json` MUST be supported on `list` and `summary` and produce valid JSON.
- **FR-012**: Exit codes: 0 success, 1 validation error, 2 storage error. Errors go to stderr, results to stdout.
- **FR-013**: Persistence MUST be a JSON file written atomically; a corrupt file MUST cause exit code 2 and MUST NOT be overwritten.
- **FR-014**: All monetary values MUST be `decimal`; all "now" access MUST go through `IClock`.
- **FR-015**: Store path MUST be validated (no escaping the working directory via `..`).

### Non-Functional / Engineering Requirements

- **NFR-001**: Build has 0 warnings with warnings-as-errors.
- **NFR-002**: `dotnet format --verify-no-changes` exits 0.
- **NFR-003**: Security analyzers report 0 high/critical findings and no vulnerable packages.
- **NFR-004**: Line coverage on `SpendWise.Core` >= 85%.
- **NFR-005**: Each command completes in < 500 ms with 10,000 stored expenses.
- **NFR-006**: No method has cyclomatic complexity > 10.

### Key Entities

- **Expense**: id, amount, category, date, note.
- **Budget**: category, month, limit.
- **MonthlySummary**: month, rows (category, spent, budget, percent, status), grand total.

## Success Criteria *(mandatory)*

- **SC-001**: 100% of acceptance scenarios above have a passing automated test (traceability matrix in `evidence/`).
- **SC-002**: All harness gates (build, format, security, tests, coverage) report PASS in a single harness run.
- **SC-003**: Boundary cases (159.99 / 160.00 / 200.00 / 200.01) produce the specified statuses.
- **SC-004**: Running the same command sequence twice yields byte-identical stdout and store file.
- **SC-005**: A corrupt store file is never modified (verified by hash before/after).

## Assumptions

- Single user, single process; no concurrent writers.
- Currency is a single unspecified currency (no conversion).
- No authentication; the data file is local and not encrypted (disclosed limitation).
