# Tasks: SpendWise Expense & Budget Tracker CLI

**Input**: [spec.md](spec.md), [plan.md](plan.md)

## Phase 1: Setup
- [ ] T001 Create solution, 3 projects + test project, `global.json`, `.editorconfig`, analyzer settings (NFR-001..003, NFR-006)
- [ ] T002 [P] Create harness scripts in `.harness/` (G1-G6) and evidence folders

## Phase 2: Foundation
- [ ] T003 Models, `IClock`, `IExpenseStore`, exceptions in Core (FR-014)
- [ ] T004 Input validation: amount, category, date, month, note (FR-002..005, US2)
- [ ] T005 `JsonFileStore` atomic write, corrupt-file handling, `StorePathValidator` (FR-013, FR-015, SC-005)

## Phase 3: US1 Record and list (P1)
- [ ] T006 `ExpenseService.Add` + `add` command (FR-001, US1.1)
- [ ] T007 `ExpenseService.List` with filters and ordering + `list` command (FR-006, US1.2-1.5)

## Phase 4: US2 Validation (P1)
- [ ] T008 Error mapping and exit codes, missing/unknown options (FR-012, US2.1-2.6)

## Phase 5: US3 Delete (P2)
- [ ] T009 `Delete` + id monotonic persistence (FR-007, US3.1-3.3)

## Phase 6: US4 Budgets and summary (P2)
- [ ] T010 `SetBudget` upsert (FR-008, US4.1)
- [ ] T011 `Summarize`, status rules, percent rounding, grand total (FR-009, FR-010, US4.2-4.6, SC-003)
- [ ] T012 `--json` output for list and summary (FR-011, US4.7)

## Phase 7: Tests and Harness
- [ ] T013 Spec-derived tests, one group per acceptance scenario, edge cases (SC-001)
- [ ] T014 Determinism (SC-004) and performance (NFR-005) tests
- [ ] T015 Preserve first generation to `evidence/initial-generation/` BEFORE any harness run
- [ ] T016 Run harness; capture failures; feed to coding agent; repair; re-run same gates
- [ ] T017 Preserve `evidence/final-generation/`, write README, human ACCEPT/REJECT in harness-log

## Traceability Matrix (requirement -> task -> test group)

| Requirement | Tasks | Test group |
|---|---|---|
| FR-001 | T006 | AddTests |
| FR-002..005 | T004 | ValidationTests |
| FR-006 | T007 | ListTests |
| FR-007 | T009 | DeleteTests |
| FR-008 | T010 | BudgetTests |
| FR-009, FR-010 | T011 | SummaryTests |
| FR-011 | T012 | JsonOutputTests |
| FR-012 | T008 | ExitCodeTests |
| FR-013, FR-015, SC-005 | T005 | StoreTests |
| FR-014 | T003 | DeterminismTests |
| NFR-001..006 | T001, T002 | harness gates G1-G5 |
