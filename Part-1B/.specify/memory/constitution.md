# TicketTriage AI Agent (Part 1B) Constitution

## Core Principles

### I. Specification Is Authoritative (NON-NEGOTIABLE)
Implementation, prompts, evaluators and the dataset follow the approved `spec.md`, `plan.md` and `tasks.md`. Every evaluation case and check traces to a requirement ID (FR-xxx / INV-xxx / GR-xxx / SC-xxx). A change to expected results or the threshold is made only by amending the spec with a dated change note.

### II. Non-Determinism Is Bounded, Not Ignored
Model wording may vary; structure, safety and decisions may not. The agent output MUST satisfy a fixed JSON schema, structural invariants and guardrail rules regardless of wording. Exact string equality MUST NOT be used to judge free-text fields.

### III. Layered Independent Evaluation (NON-NEGOTIABLE)
One harness evaluates every output in three layers: (1) schema, (2) invariants/properties, (3) semantic quality. An LLM judge MAY contribute to layer 3 but MUST NOT replace layers 1-2, and layers 1-2 MUST be pure deterministic code that does not call a model.

### IV. Threshold Declared Before Evaluation
The pass threshold and scoring formula in the spec are frozen before the first final evaluation. They MUST NOT be lowered, nor cases removed or expected values edited, to obtain PASS.

### V. Safety Guardrails Cannot Be Disabled
Guardrails (prompt-injection resistance, PII non-leakage, human escalation for safety/legal threats, refusal of out-of-scope requests) are mandatory and have a 100% pass requirement. Disabling or loosening a guardrail to obtain PASS is prohibited.

### VI. Honest Failure Attribution
Model/service failures (HTTP 429/5xx, timeout, network, missing key) are reported as `SERVICE_ERROR` and are never counted as application failures or as passes. Too many service errors make the run `INCONCLUSIVE`, not PASS.

### VII. Secrets Never Committed
API keys are read only from environment variables. No key, `.env` file or credential is committed, logged or written to evidence files.

## Measurable Engineering Constraints

- Language/runtime: C# 12 on .NET 8, nullable enabled, warnings-as-errors.
- Model access behind an `IModelClient` interface so evaluators and tests can run with a recorded/fake client; model name from `GEMINI_MODEL`, key from `GEMINI_API_KEY`.
- Generation settings recorded in evidence: model, temperature, max output tokens, prompt version hash.
- Evaluation dataset >= 15 cases with all six required case types.
- Unit tests of the deterministic evaluators (schema, invariants, scoring) run without network access and pass at 100%.

## Development Workflow

1. Spec Kit flow: constitution -> specify -> clarify -> plan -> tasks -> analyze -> implement.
2. First generation preserved in `evidence/initial-generation/` before any evaluation.
3. Harness failure evidence (case, output, failed requirement, score) is fed to the coding agent; repair may change prompt, parser, validator or guardrail; then re-evaluate with the same dataset and threshold.
4. A human records the final ACCEPT/REJECT decision after the automated result.

## Governance

This constitution supersedes other practices. Amendments need a dated entry, a version bump and a list of impacted artifacts.

**Amendment log**: 2026-11-15 v1.0.0 initial ratification.

**Version**: 1.0.0 | **Ratified**: 2026-11-15 | **Last Amended**: 2026-11-15
