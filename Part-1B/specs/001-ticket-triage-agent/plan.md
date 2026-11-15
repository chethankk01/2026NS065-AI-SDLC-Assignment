# Implementation Plan: TicketTriage Support Ticket Triage Agent

**Branch**: `001-ticket-triage-agent` | **Date**: 2026-11-15 | **Spec**: [spec.md](spec.md)

## Summary

A .NET 8 agent calls Google Gemini through an `IModelClient` interface and returns a fixed triage JSON. A **separate** evaluation harness project (`.harness/TicketTriage.Harness`) loads `evals/dataset.json`, runs every case through the agent, and evaluates each output in three layers (schema -> invariants/guardrails -> semantic score), then aggregates against the frozen threshold in the spec.

## Technical Context

**Language/Version**: C# 12 / .NET 8 (SDK pinned by `global.json`)
**Model**: Gemini REST `generateContent`; model from `GEMINI_MODEL` (this project: `gemini-pro-latest`), key from `GEMINI_API_KEY`; temperature 0.2, max output tokens 4096, JSON mime type, no response-schema enforcement (so the prompt + validators are what is tested)
**Testing**: xUnit offline tests for evaluators, parser, agent (fake client) and Gemini client (fake HTTP handler)
**Evaluation outputs**: `evidence/runs/run-NNN/` (eval-results.json, feedback.md) and latest copy at `evidence/eval-results.json`

## Constitution Check

| Principle | How satisfied |
|---|---|
| I Spec authoritative | cases and checks carry requirement IDs; dataset test verifies every INV/GR is covered |
| II Bounded non-determinism | no exact-string checks on `summary`; enums and flags checked exactly |
| III Layered, independent | evaluators live in `.harness/`, are pure code, no model calls; judge is a separate optional signal |
| IV Threshold frozen | threshold constants in `Threshold.cs` mirror the spec and are unit-tested |
| V Guardrails mandatory | guardrail cases need 100% |
| VI Honest attribution | `SERVICE_ERROR` / `INCONCLUSIVE` handling |
| VII Secrets | key read from env only; never logged; secret scan before publishing |

## Structure

```text
Part-1B/
├── specs/001-ticket-triage-agent/ (spec, plan, tasks, analysis)
├── prompts/system-prompt.v1.txt
├── src/
│   ├── TicketTriage.App/     models, IModelClient, GeminiClient, ResponseParser, TriageAgent
│   └── TicketTriage.Cli/     `triage "<text>"`
├── evals/dataset.json        >= 15 cases
├── .harness/
│   ├── TicketTriage.Harness/ SchemaValidator, InvariantChecker, SemanticScorer, Judge, Aggregator, Runner
│   └── run-eval.ps1          build + offline tests + evaluation, writes evidence
├── tests/TicketTriage.Tests/
└── evidence/ (initial-generation, final-generation, runs, eval-results.json, harness-log.md)
```

## Key Design Decisions

- **D1 (INV-006)**: whitespace-only input is handled in code BEFORE any model call (returns `request_clarification`), because model APIs reject empty content. The evaluator still checks the invariant on the output.
- **D2 (FR-003/FR-004)**: the agent's parser only extracts JSON (strips code fences) and reads the six fields; it never repairs semantics. Schema/invariant checking is done independently in the harness on the raw text.
- **D3 (FR-007)**: HTTP 429/5xx, timeouts, network errors and blocked/empty replies are `ServiceError`; at most 2 retries (2s then 6s) and only for these. A schema-invalid reply is never retried.
- **D4 (BP-004)**: `--repeat N` re-runs each case to compute a consistency rate (reported, not gated).
- **D5 (BP-005)**: `--judge` asks the model for a 1-5 faithfulness score; reported, not gated.
- **D6**: ambiguous cases use any-of expectations (`categories`, `actions` arrays); `enforce_action` marks cases whose action is a hard rule (guardrails, empty/vague input).
- **D7 (FR-008 change)**: the `eval` command lives in the harness project, not the app CLI, to keep validation independent of the generated app. See spec change history.
- **D8**: free-tier pacing: configurable delay between calls (default 6000 ms).
