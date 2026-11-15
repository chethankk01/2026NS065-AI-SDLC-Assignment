# Tasks: TicketTriage Agent

**Input**: [spec.md](spec.md), [plan.md](plan.md)

## Phase 1: Setup
- [ ] T001 Solution, projects, `Directory.Build.props`, `.editorconfig`, `.gitignore`, `global.json`
- [ ] T002 [P] `prompts/system-prompt.v1.txt` implementing schema, GR-001..005, INV rules (FR-002, FR-004)

## Phase 2: Application
- [ ] T003 Models, `IModelClient`, `ResponseParser` with fence stripping (FR-001, FR-003)
- [ ] T004 `GeminiClient`: env key/model, retries, service-error classification, no key in messages (FR-005, FR-007)
- [ ] T005 `TriageAgent` incl. empty-input short-circuit (FR-001, INV-006)
- [ ] T006 CLI `triage` command (FR-008)

## Phase 3: Evaluation harness
- [ ] T007 `SchemaValidator` (INV-001, schema contract)
- [ ] T008 `InvariantChecker` (INV-002..006, GR expectations)
- [ ] T009 `SemanticScorer` (BP-001..003, S formula)
- [ ] T010 `Judge` (BP-005, optional)
- [ ] T011 `Aggregator` + frozen `Threshold` (scoring section, FR-007 INCONCLUSIVE)
- [ ] T012 Runner, `eval-results.json`, `feedback.md`, requirement coverage table, consistency (FR-006, BP-004)
- [ ] T013 `evals/dataset.json` >= 15 cases, 6 case types, all INV/GR covered (SC-001)

## Phase 4: Tests and evidence
- [ ] T014 Offline xUnit tests for evaluators, parser, agent, Gemini client, dataset integrity (SC-002)
- [ ] T015 Preserve first generation to `evidence/initial-generation/` BEFORE the first evaluation
- [ ] T016 Run evaluation; capture failures; feed to coding agent; repair prompt/parser/guardrail; re-evaluate with same dataset and threshold
- [ ] T017 Preserve `evidence/final-generation/`, README, secret scan, human decision

## Traceability

| Requirement | Tasks | Test group |
|---|---|---|
| INV-001 | T007 | SchemaValidatorTests |
| INV-002..006 | T008 | InvariantCheckerTests |
| BP-001..003 | T009 | SemanticScorerTests |
| Threshold / decision | T011 | AggregatorTests |
| FR-001, FR-003, INV-006 | T003, T005 | AgentTests, ResponseParserTests |
| FR-005, FR-007 | T004 | GeminiClientTests |
| SC-001 | T013 | DatasetTests |
