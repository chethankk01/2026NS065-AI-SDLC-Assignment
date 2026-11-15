using System.Globalization;
using System.Text;
using System.Text.Json;

namespace TicketTriage.Harness;

public static class ReportWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static string DecisionText(Decision d) => d switch
    {
        Decision.Pass => "PASS",
        Decision.Fail => "FAIL",
        _ => "INCONCLUSIVE",
    };

    public static string ToJson(EvalReport report)
    {
        var a = report.Aggregate;
        var payload = new
        {
            config = report.Config,
            threshold = new
            {
                semanticWeights = new { category = Threshold.WeightCategory, priority = Threshold.WeightPriority, keyTerms = Threshold.WeightKeyTerms },
                casePassSemanticMin = Threshold.CasePassSemanticMin,
                meanSemanticMin = Threshold.MeanSemanticMin,
                overallPassPercentMin = Threshold.OverallPassPercentMin,
                maxServiceErrorPercent = Threshold.MaxServiceErrorPercent,
            },
            decision = DecisionText(a.Decision),
            aggregate = new
            {
                a.TotalCases,
                a.ScoredCases,
                a.ServiceErrors,
                schemaValidRate = Rate(a.SchemaValid, a.ScoredCases),
                invariantPassRate = Rate(a.InvariantPassed, a.ScoredCases),
                guardrailPassRate = Rate(a.GuardrailPassed, a.GuardrailScored),
                casePassRate = Rate(a.CasesPassed, a.ScoredCases),
                meanSemanticScore = Math.Round(a.MeanSemantic, 4),
                meanJudgeScore = a.MeanJudge is null ? (decimal?)null : Math.Round(a.MeanJudge.Value, 2),
                consistencyRate = report.Consistency.CasesCompared == 0 ? (decimal?)null : Rate(report.Consistency.CasesConsistent, report.Consistency.CasesCompared),
                failedConditions = a.FailedConditions,
            },
            cases = report.Evaluations.Select(e => new
            {
                id = e.Case.Id,
                type = e.Case.Type,
                tags = e.Case.Tags,
                requirements = e.Case.Requirements,
                status = e.Status == CaseStatus.ServiceError ? "SERVICE_ERROR" : e.CasePass ? "PASS" : "FAIL",
                layers = new
                {
                    schema = new { pass = e.SchemaPass, violations = e.SchemaViolations },
                    invariants = new { pass = e.InvariantPass, violations = e.InvariantViolations },
                    semantic = e.Semantic is null ? null : new
                    {
                        score = Math.Round(e.Semantic.Score, 4),
                        categoryMatch = e.Semantic.CategoryMatch,
                        priorityWithinOne = e.Semantic.PriorityWithinOne,
                        keyTermRecall = Math.Round(e.Semantic.KeyTermRecall, 4),
                        judgeScore = e.JudgeScore,
                    },
                },
                serviceError = e.ServiceError,
                rawOutput = e.RawOutput,
            }),
            requirementCoverage = CoverageTable(report.Evaluations),
        };
        return JsonSerializer.Serialize(payload, JsonOptions);
    }

    public static IReadOnlyDictionary<string, string> CoverageTable(IReadOnlyList<CaseEvaluation> evaluations) =>
        evaluations
            .SelectMany(e => e.Case.Requirements.Select(r => (Req: r, e.Case.Id, Status: e.Status == CaseStatus.ServiceError ? "SERVICE_ERROR" : e.CasePass ? "PASS" : "FAIL")))
            .GroupBy(x => x.Req, StringComparer.Ordinal)
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => string.Join(", ", g.Select(x => $"{x.Id}:{x.Status}")), StringComparer.Ordinal);

    public static string FeedbackMarkdown(EvalReport report, string runId)
    {
        var sb = new StringBuilder();
        sb.AppendLine(CultureInfo.InvariantCulture, $"# Evaluation failure evidence - {runId}");
        sb.AppendLine();
        sb.AppendLine("Spec: specs/001-ticket-triage-agent/spec.md. Do NOT remove cases, edit expected values, lower the threshold or disable guardrails to obtain PASS.");
        sb.AppendLine(CultureInfo.InvariantCulture, $"Decision: **{DecisionText(report.Aggregate.Decision)}**. Failed conditions: {(report.Aggregate.FailedConditions.Count == 0 ? "none" : string.Join("; ", report.Aggregate.FailedConditions))}");
        sb.AppendLine();
        foreach (var e in report.Evaluations.Where(e => e.Status == CaseStatus.Scored && !e.CasePass))
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"## Case {e.Case.Id} ({e.Case.Type}) requirements: {string.Join(", ", e.Case.Requirements)}");
            sb.AppendLine(CultureInfo.InvariantCulture, $"- input: {Truncate(e.Case.Input, 300)}");
            sb.AppendLine(CultureInfo.InvariantCulture, $"- expected: categories=[{string.Join(", ", e.Case.Expected.Categories)}] priority={e.Case.Expected.Priority} actions=[{string.Join(", ", e.Case.Expected.Actions)}] injection={e.Case.Expected.InjectionDetected?.ToString().ToLowerInvariant() ?? "n/a"} max_priority={e.Case.Expected.MaxPriority ?? "n/a"} enforce_action={e.Case.Expected.EnforceAction.ToString().ToLowerInvariant()}");
            sb.AppendLine("- model output (verbatim):");
            sb.AppendLine("```");
            sb.AppendLine(e.RawOutput);
            sb.AppendLine("```");
            foreach (var v in e.SchemaViolations.Concat(e.InvariantViolations))
            {
                sb.AppendLine(CultureInfo.InvariantCulture, $"- violation: {v}");
            }

            if (e.Semantic is not null)
            {
                sb.AppendLine(CultureInfo.InvariantCulture, $"- semantic: S={e.Semantic.Score:0.00} (category={e.Semantic.CategoryMatch}, priority within one={e.Semantic.PriorityWithinOne}, key-term recall={e.Semantic.KeyTermRecall:0.00}); case passes only if S >= {Threshold.CasePassSemanticMin}");
            }

            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static decimal Rate(int numerator, int denominator) =>
        denominator == 0 ? 0m : Math.Round((decimal)numerator / denominator, 4);

    private static string Truncate(string text, int max) =>
        text.Length <= max ? text.Replace('\n', ' ') : string.Concat(text.AsSpan(0, max).ToString().Replace('\n', ' '), "...");
}
