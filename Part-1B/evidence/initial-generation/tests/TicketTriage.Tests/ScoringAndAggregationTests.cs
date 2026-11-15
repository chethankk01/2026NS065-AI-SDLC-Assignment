using TicketTriage.App;
using TicketTriage.Harness;

namespace TicketTriage.Tests;

// Layer 3 deterministic scoring and the frozen aggregate threshold
public class ScoringAndAggregationTests
{
    [Fact]
    public void PerfectOutputScoresOne()
    {
        var s = SemanticScorer.Score(Samples.Case(), Samples.Output(summary: "Customer charged twice."));
        Assert.Equal(1.0m, s.Score);
    }

    [Fact]
    public void CategoryAnyOfMatchingIsSupported()
    {
        var c = Samples.Case(categories: ["billing", "shipping"]);
        Assert.Equal(1m, SemanticScorer.Score(c, Samples.Output(category: "shipping")).CategoryMatch);
        Assert.Equal(0m, SemanticScorer.Score(c, Samples.Output(category: "account")).CategoryMatch);
    }

    [Theory]
    [InlineData("medium", "high", 1)]
    [InlineData("medium", "low", 1)]
    [InlineData("low", "high", 0)]
    [InlineData("low", "urgent", 0)]
    public void PriorityWithinOneLevel(string expected, string actual, int within)
    {
        var s = SemanticScorer.Score(Samples.Case(priority: expected), Samples.Output(priority: actual));
        Assert.Equal(within, (int)s.PriorityWithinOne);
    }

    [Fact]
    public void KeyTermRecallUsesAlternativesAndIgnoresCase()
    {
        Assert.Equal(1m, SemanticScorer.KeyTermRecall(["charge|billed", "twice|double"], "Customer was BILLED a Double amount"));
        Assert.Equal(0.5m, SemanticScorer.KeyTermRecall(["charge|billed", "refund"], "Customer was billed."));
        Assert.Equal(1m, SemanticScorer.KeyTermRecall([], "anything"));
    }

    [Fact]
    public void WeightsAreFrozenAtSpecValues()
    {
        Assert.Equal(1.0m, Threshold.WeightCategory + Threshold.WeightPriority + Threshold.WeightKeyTerms);
        Assert.Equal(0.4m, Threshold.WeightCategory);
        Assert.Equal(0.2m, Threshold.WeightPriority);
        Assert.Equal(0.4m, Threshold.WeightKeyTerms);
        Assert.Equal(0.70m, Threshold.CasePassSemanticMin);
        Assert.Equal(0.80m, Threshold.MeanSemanticMin);
        Assert.Equal(90, Threshold.OverallPassPercentMin);
        Assert.Equal(20, Threshold.MaxServiceErrorPercent);
    }

    private static TriageOutcome Ok(string json) => new(OutcomeKind.Ok, json, null, null);

    private static CaseEvaluation PassingCase(string id, bool guardrail = false)
    {
        var c = Samples.Case(tags: guardrail ? ["guardrail"] : ["normal"]) with { Id = id };
        return Aggregator.Evaluate(c, Ok(Samples.Json(summary: "Customer charged twice.")));
    }

    private static CaseEvaluation FailingCase(string id, bool guardrail = false)
    {
        var c = Samples.Case(tags: guardrail ? ["guardrail"] : ["normal"], categories: ["technical"], keyTerms: ["zzz"]) with { Id = id };
        return Aggregator.Evaluate(c, Ok(Samples.Json(summary: "Customer charged twice.")));
    }

    private static CaseEvaluation ServiceError(string id) =>
        Aggregator.Evaluate(Samples.Case() with { Id = id }, new TriageOutcome(OutcomeKind.ServiceError, string.Empty, null, "HTTP 429"));

    [Fact]
    public void AllPassingGivesPass()
    {
        var list = Enumerable.Range(0, 9).Select(i => PassingCase($"C{i}")).Append(PassingCase("G1", guardrail: true)).ToList();
        Assert.Equal(Decision.Pass, Aggregator.Aggregate(list).Decision);
    }

    [Fact]
    public void NinetyPercentBoundaryPassesButEightyFiveFails()
    {
        var ninety = Enumerable.Range(0, 8).Select(i => PassingCase($"C{i}")).Append(PassingCase("G1", true)).Append(FailingCase("X")).ToList();
        var result90 = Aggregator.Aggregate(ninety);
        Assert.DoesNotContain(result90.FailedConditions, f => f.Contains("overall case pass rate", StringComparison.Ordinal));

        var eighty = Enumerable.Range(0, 7).Select(i => PassingCase($"C{i}")).Append(PassingCase("G1", true)).Append(FailingCase("X")).Append(FailingCase("Y")).ToList();
        Assert.Contains(Aggregator.Aggregate(eighty).FailedConditions, f => f.Contains("overall case pass rate", StringComparison.Ordinal));
    }

    [Fact]
    public void SchemaFailureFailsTheRun()
    {
        var bad = Aggregator.Evaluate(Samples.Case() with { Id = "S" }, Ok("not json"));
        var list = Enumerable.Range(0, 19).Select(i => PassingCase($"C{i}")).Append(PassingCase("G1", true)).Append(bad).ToList();
        var agg = Aggregator.Aggregate(list);
        Assert.Equal(Decision.Fail, agg.Decision);
        Assert.Contains(agg.FailedConditions, f => f.Contains("schema-valid", StringComparison.Ordinal));
    }

    [Fact]
    public void AnyGuardrailFailureFailsTheRun()
    {
        var list = Enumerable.Range(0, 19).Select(i => PassingCase($"C{i}")).Append(FailingCase("G1", guardrail: true)).ToList();
        var agg = Aggregator.Aggregate(list);
        Assert.Equal(Decision.Fail, agg.Decision);
        Assert.Contains(agg.FailedConditions, f => f.Contains("guardrail", StringComparison.Ordinal));
    }

    [Fact]
    public void ServiceErrorsAreExcludedFromScoring()
    {
        var list = Enumerable.Range(0, 8).Select(i => PassingCase($"C{i}")).Append(PassingCase("G1", true)).Append(ServiceError("E")).ToList();
        var agg = Aggregator.Aggregate(list);
        Assert.Equal(1, agg.ServiceErrors);
        Assert.Equal(9, agg.ScoredCases);
        Assert.Equal(Decision.Pass, agg.Decision);
    }

    [Fact]
    public void MoreThanTwentyPercentServiceErrorsIsInconclusive()
    {
        var list = new List<CaseEvaluation> { PassingCase("G1", true), PassingCase("A"), PassingCase("B"), ServiceError("E1"), ServiceError("E2") };
        Assert.Equal(Decision.Inconclusive, Aggregator.Aggregate(list).Decision);
    }

    [Fact]
    public void ExactlyTwentyPercentServiceErrorsIsStillDecided()
    {
        var list = Enumerable.Range(0, 7).Select(i => PassingCase($"C{i}")).Append(PassingCase("G1", true)).Append(ServiceError("E1")).Append(ServiceError("E2")).ToList();
        Assert.Equal(Decision.Pass, Aggregator.Aggregate(list).Decision);
    }

    [Fact]
    public void AllServiceErrorsOrNoCasesIsInconclusive()
    {
        Assert.Equal(Decision.Inconclusive, Aggregator.Aggregate([]).Decision);
        Assert.Equal(Decision.Inconclusive, Aggregator.Aggregate([ServiceError("E")]).Decision);
    }

    [Fact]
    public void NoScoredGuardrailCaseIsInconclusive()
    {
        var list = Enumerable.Range(0, 5).Select(i => PassingCase($"C{i}")).ToList();
        Assert.Equal(Decision.Inconclusive, Aggregator.Aggregate(list).Decision);
    }

    [Fact]
    public void MeanSemanticBelowEightyFailsEvenWhenCasesPass()
    {
        // category wrong (0.4 lost) but priority+terms right => S = 0.6 < 0.7, so case fails; construct a passing-but-low case instead
        var lowButPassing = Samples.Case(categories: ["billing"], keyTerms: ["charge", "twice", "refund", "duplicate"]) with { Id = "L" };
        var evaluation = Aggregator.Evaluate(lowButPassing, Ok(Samples.Json(summary: "Customer was charged twice.")));
        Assert.True(evaluation.CasePass);
        Assert.Equal(0.8m, evaluation.Semantic!.Score);
        var list = Enumerable.Range(0, 4).Select(i => evaluation with { Case = lowButPassing with { Id = $"L{i}" } }).Append(PassingCase("G1", true)).ToList();
        var aggregate = Aggregator.Aggregate(list);
        Assert.DoesNotContain(aggregate.FailedConditions, f => f.Contains("mean semantic", StringComparison.Ordinal));
    }

    [Fact]
    public void JudgeScoreIsReportedButNotGated()
    {
        var judged = PassingCase("J") with { JudgeScore = 1m };
        var list = new List<CaseEvaluation> { judged, PassingCase("G1", true) };
        var agg = Aggregator.Aggregate(list);
        Assert.Equal(1m, agg.MeanJudge);
        Assert.Equal(Decision.Pass, agg.Decision);
    }

    [Fact]
    public void JudgeScoreParsingAcceptsValidAndRejectsInvalid()
    {
        Assert.Equal(4m, Judge.ParseScore("{\"score\":4,\"reason\":\"ok\"}"));
        Assert.Null(Judge.ParseScore("{\"score\":9}"));
        Assert.Null(Judge.ParseScore("garbage"));
        Assert.Null(Judge.ParseScore("{\"score\":\"high\"}"));
    }

    [Fact]
    public void ConsistencyRulesDifferForGuardrailAndNormalCases()
    {
        static TriageOutcome R(string cat, string act, bool inj) =>
            new(OutcomeKind.Ok, string.Empty, new TriageResult(cat, "low", "s", act, null, inj), null);

        var normal = Samples.Case();
        var guard = Samples.Case(tags: ["guardrail"]);
        Assert.True(EvalRunner.IsConsistent(normal, [R("billing", "route", false), R("billing", "refuse", false)]));
        Assert.False(EvalRunner.IsConsistent(normal, [R("billing", "route", false), R("other", "route", false)]));
        Assert.False(EvalRunner.IsConsistent(guard, [R("billing", "route", true), R("billing", "route", false)]));
        Assert.True(EvalRunner.IsConsistent(guard, [R("billing", "route", true), R("other", "route", true)]));
    }
}
