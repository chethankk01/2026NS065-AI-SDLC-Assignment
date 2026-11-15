# Part 1A - SpendWise Expense & Budget Tracker (C# / .NET 8)

Deterministic CLI built with the GitHub Spec Kit workflow and validated by an independent local harness.

## Layout

| Path | Purpose |
|---|---|
| `.specify/memory/constitution.md` | Project constitution (measurable engineering constraints) |
| `specs/001-expense-budget-tracker/` | `spec.md` (with clarifications + change history), `plan.md`, `tasks.md`, `analysis.md` |
| `src/SpendWise.Core` | Domain, validation, command handling, formatting (no file/console access) |
| `src/SpendWise.Infrastructure` | JSON file store (atomic write), system clock, store-path validation, `CliHost` |
| `src/SpendWise.Cli` | `Program.cs` entry point |
| `tests/SpendWise.Tests` | Spec-derived xUnit tests (names carry the acceptance-scenario / FR id) |
| `.harness/` | `run-harness.ps1` (runner), `CodeMetricsConfig.txt`, `baseline.json` (integrity baseline) |
| `evidence/` | `initial-generation/`, `final-generation/`, `harness-results/`, `harness-log.md` |
| `.claude/skills/speckit-*` | Spec Kit commands used to drive the workflow |

## Prerequisites

.NET SDK 8.0.x (`global.json` pins 8.0.307, roll-forward allowed), PowerShell 5.1+ (Windows) and internet access for NuGet restore and the vulnerable-package check.

## Run the application

```powershell
dotnet run --project src/SpendWise.Cli -- add --amount 12.50 --category food --date 2026-11-10 --note "Lunch"
dotnet run --project src/SpendWise.Cli -- list --month 2026-11
dotnet run --project src/SpendWise.Cli -- budget set --category food --month 2026-11 --limit 200
dotnet run --project src/SpendWise.Cli -- summary --month 2026-11
dotnet run --project src/SpendWise.Cli -- summary --month 2026-11 --json
dotnet run --project src/SpendWise.Cli -- delete --id 1
```

Options: `--store <file>` (default `spendwise.json`, must be inside the working directory), `--json` on `list`/`summary`.
Exit codes: `0` success, `1` validation error, `2` storage error. Categories: Food, Transport, Rent, Utilities, Entertainment, Health, Other.

## Run the harness

```powershell
powershell -ExecutionPolicy Bypass -File .harness\run-harness.ps1
```

| Gate | What it checks |
|---|---|
| G1 Build | compiles with warnings-as-errors (analyzers off, isolates compile errors) |
| G2 Lint | `dotnet format --verify-no-changes` against `.editorconfig` |
| G3 SAST | full analyzer build: security-category rules and CA1502 complexity (<= 10) are errors; `dotnet list package --vulnerable` |
| G4 Tests | all xUnit tests pass, none skipped |
| G5 Coverage | >= 85% line coverage on Core + Infrastructure |
| G6 Integrity | SHA-256 of tests and gate configuration unchanged vs `.harness/baseline.json` |

Results are written only by the runner to `evidence/harness-results/run-NNN/` (`result.json`, raw stdout/stderr per gate, `feedback.md` on failure). Do not edit them by hand.

## Notes

- No secrets or API keys are used or stored. The store file `spendwise.json` is local, unencrypted data.
- See `evidence/harness-log.md` for the generation/repair history and the human ACCEPT/REJECT decision.
