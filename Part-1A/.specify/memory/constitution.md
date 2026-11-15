# SpendWise Expense Tracker (Part 1A) Constitution

## Core Principles

### I. Specification Is Authoritative (NON-NEGOTIABLE)
Implementation MUST follow the approved `spec.md`, `plan.md` and `tasks.md`. Every source file and test traces to a requirement ID (FR-xxx / SC-xxx). Ambiguity discovered later is resolved by amending the upstream Spec Kit artifact (with a dated change note), never by silently changing code or tests.

### II. Deterministic Money Handling
All monetary values use C# `decimal` (never `float`/`double`), rounded with `MidpointRounding.AwayFromZero` to 2 decimal places. Time is injected through an `IClock` abstraction; the code MUST NOT call `DateTime.Now`/`UtcNow` outside the single system-clock adapter. The same input MUST always produce byte-identical output.

### III. Test-First and Spec-Derived Tests
Tests are derived from the acceptance scenarios in `spec.md` and are written before or independently of the implementation. Each acceptance scenario maps to at least one binary (pass/fail) test. The coding agent MUST NOT weaken assertions, delete failing tests, or skip tests to obtain a pass.

### IV. Layered, Library-First Design
Domain logic lives in `SpendWise.Core` (no I/O, no console). Persistence is behind `IExpenseStore`. The CLI (`SpendWise.Cli`) is a thin adapter: arguments in, stdout for results, stderr for errors, documented exit codes (0 success, 1 validation error, 2 storage error).

### V. Mandatory Harness Gates (NON-NEGOTIABLE)
A change is acceptable only when ALL gates pass: (1) build with warnings-as-errors, (2) `dotnet format --verify-no-changes` lint, (3) security analysis (Roslyn security analyzers + vulnerable-package scan), (4) unit and contract tests, (5) line coverage >= 85% on `SpendWise.Core`. Gates MUST NOT be disabled, suppressed or have thresholds lowered to obtain PASS. Harness results are machine-written and never hand-edited.

## Measurable Engineering Constraints

- Language/runtime: C# 12 on .NET 8. Nullable reference types enabled.
- Static analysis: 0 build warnings, 0 analyzer errors, 0 formatting diffs.
- Security: 0 high/critical findings; no secrets in source; no unvalidated path input; no dependency with known vulnerabilities (`dotnet list package --vulnerable` reports none).
- Quality: >= 85% line coverage on Core; cyclomatic complexity of any method <= 10.
- Performance: any CLI command completes in < 500 ms for 10,000 stored expenses.
- Persistence: JSON file written atomically (write temp file then replace) so a crash never leaves a corrupt file.

## Development Workflow

1. Spec Kit flow: constitution -> specify -> clarify -> plan -> tasks -> analyze -> implement.
2. The first AI-generated implementation is preserved in `evidence/initial-generation/` before any harness-driven repair.
3. Each harness run is logged to `evidence/` by the runner; failures are fed back to the coding agent as structured evidence and repaired, then the SAME gates re-run.
4. A human records the final ACCEPT/REJECT decision after the automated result.
5. Secrets, API keys and `.env` files are never committed.

## Governance

This constitution supersedes other practices. Amendments require a dated entry below, a version bump, and a note of impacted artifacts. Specification changes follow the same rule.

**Amendment log**: 2026-11-15 v1.0.0 initial ratification.

**Version**: 1.0.0 | **Ratified**: 2026-11-15 | **Last Amended**: 2026-11-15
