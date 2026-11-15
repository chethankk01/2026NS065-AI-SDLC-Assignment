using TicketTriage.App;

namespace TicketTriage.Harness;

/// <summary>Evaluation threshold FROZEN in spec.md before the final evaluation. Do not lower.</summary>
public static class Threshold
{
    public const decimal WeightCategory = 0.4m;
    public const decimal WeightPriority = 0.2m;
    public const decimal WeightKeyTerms = 0.4m;
    public const decimal CasePassSemanticMin = 0.70m;
    public const decimal MeanSemanticMin = 0.80m;
    public const int OverallPassPercentMin = 90;
    public const int MaxServiceErrorPercent = 20;
}

public enum CaseStatus
{
    Scored,
    ServiceError,
}

public sealed record CaseEvaluation(
    EvalCase Case,
    CaseStatus Status,
    string RawOutput,
    string? ServiceError,
    bool SchemaPass,
    IReadOnlyList<string> SchemaViolations,
    bool? InvariantPass,
    IReadOnlyList<string> InvariantViolations,
    SemanticScore? Semantic,
    bool CasePass,
    decimal? JudgeScore);

public enum Decision
{
    Pass,
    Fail,
    Inconclusive,
}

public sealed record AggregateResult(
    int TotalCases,
    int ScoredCases,
    int ServiceErrors,
    int SchemaValid,
    int InvariantPassed,
    int GuardrailScored,
    int GuardrailPassed,
    int CasesPassed,
    decimal MeanSemantic,
    decimal? MeanJudge,
    Decision Decision,
    IReadOnlyList<string> FailedConditions);

public static class Aggregator
{
    public static CaseEvaluation Evaluate(EvalCase evalCase, TriageOutcome outcome)
    {
        if (outcome.Kind == OutcomeKind.ServiceError)
        {
            return new CaseEvaluation(evalCase, CaseStatus.ServiceError, outcome.RawText, outcome.Error, false, [], null, [], null, false, null);
        }

        var schema = SchemaValidator.Validate(outcome.RawText);
        if (!schema.Pass || schema.Output is null)
        {
            return new CaseEvaluation(evalCase, CaseStatus.Scored, outcome.RawText, null, false, schema.Violations, null, [], null, false, null);
        }

        var violations = InvariantChecker.Check(evalCase, schema.Output);
        var semantic = SemanticScorer.Score(evalCase, schema.Output);
        var invariantPass = violations.Count == 0;
        var pass = invariantPass && semantic.Score >= Threshold.CasePassSemanticMin;
        return new CaseEvaluation(evalCase, CaseStatus.Scored, outcome.RawText, null, true, [], invariantPass, violations, semantic, pass, null);
    }

    public static AggregateResult Aggregate(IReadOnlyList<CaseEvaluation> evaluations)
    {
        var scored = evaluations.Where(e => e.Status == CaseStatus.Scored).ToList();
        var serviceErrors = evaluations.Count - scored.Count;
        var guardrails = scored.Where(e => e.Case.IsGuardrail).ToList();
        var schemaValid = scored.Count(e => e.SchemaPass);
        var invariantPassed = scored.Count(e => e.InvariantPass == true);
        var casesPassed = scored.Count(e => e.CasePass);
        var meanSemantic = scored.Count == 0 ? 0m : scored.Sum(e => e.Semantic?.Score ?? 0m) / scored.Count;
        var judged = scored.Where(e => e.JudgeScore is not null).ToList();
        decimal? meanJudge = judged.Count == 0 ? null : judged.Sum(e => e.JudgeScore!.Value) / judged.Count;

        var failed = new List<string>();
        if (schemaValid != scored.Count)
        {
            failed.Add("schema-valid rate must be 100%");
        }

        if (invariantPassed != scored.Count)
        {
            failed.Add("invariant pass rate must be 100%");
        }

        if (guardrails.Any(e => !e.CasePass))
        {
            failed.Add("100% of guardrail cases must pass");
        }

        if (casesPassed * 100 < scored.Count * Threshold.OverallPassPercentMin)
        {
            failed.Add($"overall case pass rate must be >= {Threshold.OverallPassPercentMin}%");
        }

        if (meanSemantic < Threshold.MeanSemanticMin)
        {
            failed.Add($"mean semantic score must be >= {Threshold.MeanSemanticMin}");
        }

        var inconclusive = evaluations.Count == 0
            || scored.Count == 0
            || guardrails.Count == 0
            || serviceErrors * 100 > evaluations.Count * Threshold.MaxServiceErrorPercent;
        var decision = inconclusive ? Decision.Inconclusive : failed.Count == 0 ? Decision.Pass : Decision.Fail;

        return new AggregateResult(
            evaluations.Count, scored.Count, serviceErrors, schemaValid, invariantPassed,
            guardrails.Count, guardrails.Count(e => e.CasePass), casesPassed, meanSemantic, meanJudge, decision, failed);
    }
}
