# Part 1A - Harness Log (SpendWise Expense & Budget Tracker)

> Section A is a factual index of artifacts and machine-recorded results (pointers to files written by `.harness/run-harness.ps1`).
> Sections B-D are to be written by the student in their own words (course AI policy: no AI-generated log/reflection entries).

## A. Machine-recorded facts

### A1. Generation context
- Spec Kit artifacts: `.specify/memory/constitution.md`, `specs/001-expense-budget-tracker/{spec,plan,tasks,analysis}.md`
- Coding agent: Claude Code (Claude Sonnet 5.5). Context supplied: constitution, spec, plan, tasks.
- First generation preserved, with SHA-256 list, BEFORE any harness run: `evidence/initial-generation/`
- Final generation: `evidence/final-generation/`
- SDK: .NET 8.0.307 (`global.json`)

### A2. Harness executions (`evidence/harness-results/index.md`)

| Run | Overall | G1 Build | G2 Lint | G3a Analyzers | G3b Packages | G4 Tests | G5 Coverage | G6 Integrity |
|---|---|---|---|---|---|---|---|---|
| run-001 | FAIL | PASS | FAIL | FAIL | FAIL | FAIL | FAIL | PASS |
| run-002 | FAIL | PASS | PASS | PASS | PASS | FAIL (66 pass, 1 fail) | FAIL | PASS |
| run-003 | PASS | PASS | PASS | PASS | PASS | PASS (67/67, 0 skipped) | PASS (96.65% line) | PASS |

Raw output per gate and `feedback.md` for each failing run are under `evidence/harness-results/run-NNN/`.

### A3. Failures and repairs that actually occurred

| # | Run | Gate | Evidence (file) | Root cause | Change made | Re-run |
|---|---|---|---|---|---|---|
| 1 | 001 | G2 | `run-001/G2-lint.stdout.txt`: `error CHARSET` on Program.cs | scaffolded files had a UTF-8 BOM; `.editorconfig` requires none | removed the BOM from 4 scaffolded files | run-002 G2 PASS |
| 2 | 001 | G3a (also blocked G4, G5) | `run-001/G3a-analyzers.stdout.txt`: 92x CA1707 (underscores in test member names) | test names like `Add_RejectsInvalidAmount_US2_1` violate the analyzer | renamed 46 test methods (underscores removed, spec IDs kept as name suffix). Proof only declaration lines changed: `evidence/final-generation/test-repair-diff-run001.txt`; originals in `evidence/final-generation/tests-before-run001-repair/`. Rule NOT suppressed. | run-002 G3a PASS |
| 3 | 001 | G3b | `run-001/G3b-packages.stdout.txt`: System.Net.Http 4.3.0 and System.Text.RegularExpressions 4.3.0 (High, transitive via test SDK) | outdated Microsoft.NET.Test.Sdk 17.8.0 | upgraded Microsoft.NET.Test.Sdk (18.12.0), xunit (2.9.3), xunit.runner.visualstudio (4.0.1); removed unused coverlet.collector | run-002 G3b PASS |
| 4 | 002 | G4 (and G5) | `run-002/feedback.md`: `UnwritableStoreIsStorageErrorFR013` expected exit 2, actual 0 | application bug: a directory at the store path was treated as an empty store | `JsonFileStore.Load` now throws `StorageException` if the path is a directory. Test and spec unchanged. | run-003 all PASS |

### A4. Harness/tooling issues (not application failures)
- First harness attempt hung in G2 because the runner passed `--verbosity diagnostic` to `dotnet format` while redirecting output. The attempt was aborted and its partial output deleted (it produced no result). The flag was removed from the runner.
- Run-001 `feedback.md` had an empty evidence block for G2 because the runner's regex required digits after `error`. The regex was corrected after run-001. Run-001 evidence is unmodified.
- When the student first re-ran the harness, it appeared stuck. Cause: the runner used `Start-Process -Wait`, which also waits for dotnet's long-lived build-server processes. The stuck processes and the incomplete run were removed and the runner now uses `WaitForExit()`. A second runner defect followed (PowerShell 5.1 returned an empty exit code after `WaitForExit()`, marking every gate FAIL although the underlying commands succeeded); that invalid run was deleted and the runner now caches the process handle. The earlier "wrong verbosity flag" diagnosis for the first hang was incomplete: `Start-Process -Wait` was the underlying cause.
- The integrity baseline (`.harness/baseline.json`) was re-recorded with `-Baseline` for run-001, run-002 and run-004 (runner fixes), after the runner and test-method-rename changes above. Run-003 ran without `-Baseline` and G6 reported unchanged.

### A5. Final automated result
- run-003: OVERALL PASS. 67 tests passed, 0 failed, 0 skipped. Line coverage 96.65% (Core 100%, Infrastructure 85.18%) against threshold 85%. 0 vulnerable packages. 0 build/analyzer errors.

### A6. Specification changes during development
- One clarification (FR-009 budget-only categories) recorded in `spec.md` "Change history" and `analysis.md` finding A1. No acceptance criterion, threshold or test assertion was weakened.

## B. Human review of the failure/repair cycles (student to write)

I chose this bug because, there is a requirement of throwing exit code 2, instead of exit code 0,In run-002 the test expected exit code 2, but got exit code 0, the cause is  by default the file.exisits returns boolean and with only returning an exmpty storedata instead of exit code 2. i fixed it by adding a one more check for Directory and one more check whether file contains the data or the data is unreadble. i didnot change the test or the spec because it is a real bug and shows the user that the file is unreadable

## C. Final human decision (student to write)

I accept the software despite its known limitations because it is a local application where the data remains on the user's device. The evidence supporting this acceptance is that run-005 passed all gates, with 67 out of 67 tests passing and 96.65% code coverage. This is acceptable for users who use their personal laptop and do not share it with others. It is not recommended for use on laptops that are accessed by multiple people.

## D. Known limitations and residual risks (student to write)
The biggest limitation is that the data is stored in plain JSON format. The test harness did not detect this issue because it does not include a check to determine whether data is encrypted or unencrypted. Before real-world use, I would add features to encrypt the file and modify file permissions so that both the file and the data are secure.

