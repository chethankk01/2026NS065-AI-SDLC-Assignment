# Implementation Plan: SpendWise Expense & Budget Tracker CLI

**Branch**: `001-expense-budget-tracker` | **Date**: 2026-11-15 | **Spec**: [spec.md](spec.md)

## Summary

A layered .NET 8 CLI. Domain rules and command handling live in `SpendWise.Core` (no file or console access); file persistence, the system clock and path validation live in `SpendWise.Infrastructure`; `SpendWise.Cli` is a 5-line `Program.cs`. All behaviour is exercised by spec-derived xUnit tests, and a local harness runs build, format, security, test and coverage gates.

## Technical Context

**Language/Version**: C# 12 / .NET 8 (SDK pinned in `global.json`), nullable enabled
**Primary Dependencies**: none at runtime (System.Text.Json only). Dev: xunit, coverlet.msbuild, Microsoft.NET.Test.Sdk, Microsoft.CodeAnalysis.NetAnalyzers
**Storage**: single JSON file, atomic replace (temp file + `File.Replace`/`File.Move`)
**Testing**: xUnit; tests black-box the `CommandRunner` and `CliHost`
**Target Platform**: Windows/Linux console
**Project Type**: CLI + libraries
**Performance Goals**: < 500 ms per command with 10,000 expenses (NFR-005)
**Constraints**: decimal money, injected clock, 0 warnings, coverage >= 85%, complexity <= 10

## Constitution Check

| Principle | How the plan satisfies it |
|---|---|
| I Spec authoritative | tasks/tests carry FR/SC IDs; see traceability in tasks.md |
| II Deterministic money | `decimal`, `AwayFromZero`, `IClock`, invariant culture |
| III Spec-derived tests | one test group per acceptance scenario |
| IV Layered | Core / Infrastructure / Cli split; `IExpenseStore`, `IClock` |
| V Harness gates | `.harness/run-harness.ps1` runs all gates; analyzers set to error |

## Project Structure

```text
Part-1A/
├── specs/001-expense-budget-tracker/ (spec, plan, tasks, analysis)
├── src/
│   ├── SpendWise.Core/            models, validation, ExpenseService, CommandRunner, formatters
│   ├── SpendWise.Infrastructure/  JsonFileStore, SystemClock, StorePathValidator, CliHost
│   └── SpendWise.Cli/             Program.cs
├── tests/SpendWise.Tests/
├── .harness/                      run-harness.ps1, gate scripts, CodeMetricsConfig
└── evidence/                      initial-generation, final-generation, harness-results, harness-log.md
```

## Key Design Decisions

- **D1 (FR-002, clarification)**: Amount text is parsed with `NumberStyles.AllowDecimalPoint` + invariant culture; sign, exponent, comma, whitespace are rejected; decimal scale > 2 is rejected, never rounded.
- **D2 (FR-010)**: Status uses exact comparison (`spent*100 < limit*80` => OK; `spent <= limit` => WARNING; else EXCEEDED); the rounded percent is display-only.
- **D3 (FR-013)**: The store loads before any mutation; a parse failure raises `StorageException` (exit 2) and nothing is written.
- **D4 (FR-007)**: `NextId` is persisted in the store file so IDs are never reused after deletion.
- **D5 (FR-015)**: `StorePathValidator` resolves the full path and requires it to be inside the working directory.
- **D6 (FR-009 change)**: summary rows = categories with an expense or a budget in the month.
- **D7 (NFR-006)**: command handlers are small methods registered in a dictionary to keep complexity <= 10.

## Harness Design

| Gate | Command | Pass condition |
|---|---|---|
| G1 Build | `dotnet build -warnaserror` | exit 0 |
| G2 Lint | `dotnet format --verify-no-changes` | exit 0 |
| G3 SAST | analyzers (CA2xxx/CA3xxx/CA5xxx, CA1502) as errors in G1 + `dotnet list package --vulnerable --include-transitive` | no findings |
| G4 Tests | `dotnet test` | all pass, none skipped |
| G5 Coverage | coverlet threshold 85% line | threshold met |
| G6 Integrity | SHA-256 of test files and `.editorconfig` vs recorded baseline | unchanged without logged reason |

The runner writes `evidence/harness-results/run-NNN.json` and raw stdout/stderr per gate; it is the only writer of those files.
