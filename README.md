# AI-Augmented SDLC - Assignment 1: Spec-Driven Development & Harness Engineering

Student: Chethan K K (2026NS93065) - BITS Pilani WILP, SEZG534

Two small C# (.NET 8) applications built with the GitHub Spec Kit workflow and validated by independent harnesses.

| Part | Application | Type | Status |
|---|---|---|---|
| [Part-1A](Part-1A/) | SpendWise Expense & Budget Tracker CLI | Deterministic | Implemented. Harness gates G1-G6 PASS (run-005): 67 tests, 96.65% line coverage. Failure/repair history in `Part-1A/evidence/harness-log.md`. |
| [Part-1B](Part-1B/) | TicketTriage support-ticket triage agent (Gemini) | Non-deterministic AI | Specification, agent, evaluation harness and 21-case dataset implemented; 88 offline tests pass. **The live model evaluation has NOT been run yet** (see below), so `evidence/eval-results.json` and the Part-1B harness log do not exist yet. |

## Part 1B status (honest disclosure)

The live evaluation calls the Gemini API. From the development machine the API returned HTTP 403 for generation calls (the network/key combination was blocked), so no real model output has been evaluated. No results have been fabricated or mocked. The evaluation, any failures and the repair cycle will be added after it is run from a working environment.

## Repository layout

```text
Part-1A/   Spec Kit artifacts (.specify, specs/), src/, tests/, .harness/, evidence/
Part-1B/   Spec Kit artifacts (.specify, specs/), src/, prompts/, evals/, .harness/, tests/, evidence/
```

## How to run

- Part 1A: see [Part-1A/README.md](Part-1A/README.md). Harness: `powershell -ExecutionPolicy Bypass -File .harness\run-harness.ps1` (from `Part-1A`).
- Part 1B (offline tests): from `Part-1B`, `dotnet test tests/TicketTriage.Tests`.
- Part 1B (live evaluation): set the environment variable `GEMINI_API_KEY` (and optionally `GEMINI_MODEL`, default `gemini-pro-latest`), then from `Part-1B` run `powershell -ExecutionPolicy Bypass -File .harness\run-eval.ps1`. Results are written to `evidence/runs/run-NNN/` and `evidence/eval-results.json`.

## Security notes

- No API keys, credentials or `.env` files are committed. Keys are read only from environment variables.
- Part 1A's `spendwise.json` data file is local and unencrypted (documented limitation).

## Tools and AI use

Spec Kit workflow, code and harness were produced with the AI coding agent Claude Code (Claude Sonnet 5.5) under the student's direction and review. The student's own review and decisions are recorded in `Part-1A/evidence/harness-log.md` (sections B-D). Raw harness output files contain local file paths.
