using TicketTriage.App;

namespace TicketTriage.Harness;

public sealed record EvalConfig(
    string Model,
    double Temperature,
    int MaxOutputTokens,
    string PromptFile,
    string PromptHash,
    string DatasetFile,
    string DatasetHash,
    int Repeat,
    bool Judge,
    int DelayMs,
    string TimestampUtc);

public sealed record ConsistencyResult(int CasesCompared, int CasesConsistent);

public sealed record EvalReport(
    EvalConfig Config,
    IReadOnlyList<CaseEvaluation> Evaluations,
    AggregateResult Aggregate,
    ConsistencyResult Consistency);

public sealed class EvalRunner(TriageAgent agent, IModelClient judgeClient)
{
    public async Task<EvalReport> RunAsync(
        IReadOnlyList<EvalCase> cases,
        EvalConfig config,
        Func<int, Task> delay,
        Action<string> log,
        CancellationToken cancellationToken = default)
    {
        var evaluations = new List<CaseEvaluation>();
        var compared = 0;
        var consistent = 0;

        foreach (var evalCase in cases)
        {
            var usesModel = !string.IsNullOrWhiteSpace(evalCase.Input);
            var first = await agent.TriageAsync(evalCase.Input, cancellationToken).ConfigureAwait(false);
            if (usesModel)
            {
                await delay(config.DelayMs).ConfigureAwait(false);
            }

            var evaluation = Aggregator.Evaluate(evalCase, first);
            if (config.Judge && evaluation.Status == CaseStatus.Scored && evaluation.SchemaPass && first.Result is not null)
            {
                var judged = await Judge.ScoreAsync(judgeClient, evalCase.Input, first.Result.Summary, cancellationToken).ConfigureAwait(false);
                await delay(config.DelayMs).ConfigureAwait(false);
                evaluation = evaluation with { JudgeScore = judged };
            }

            evaluations.Add(evaluation);
            log($"{evalCase.Id,-5} {Describe(evaluation)}");

            if (config.Repeat > 1 && usesModel && first.Kind == OutcomeKind.Ok)
            {
                var repeats = new List<TriageOutcome> { first };
                for (var i = 1; i < config.Repeat; i++)
                {
                    repeats.Add(await agent.TriageAsync(evalCase.Input, cancellationToken).ConfigureAwait(false));
                    await delay(config.DelayMs).ConfigureAwait(false);
                }

                if (repeats.All(r => r.Kind == OutcomeKind.Ok && r.Result is not null))
                {
                    compared++;
                    consistent += IsConsistent(evalCase, repeats) ? 1 : 0;
                }
            }
        }

        return new EvalReport(config, evaluations, Aggregator.Aggregate(evaluations), new ConsistencyResult(compared, consistent));
    }

    public static bool IsConsistent(EvalCase evalCase, IReadOnlyList<TriageOutcome> runs)
    {
        var results = runs.Select(r => r.Result!).ToList();
        var sameAction = results.Select(r => r.Action).Distinct(StringComparer.Ordinal).Count() == 1;
        var sameInjection = results.Select(r => r.InjectionDetected).Distinct().Count() == 1;
        var sameCategory = results.Select(r => r.Category).Distinct(StringComparer.Ordinal).Count() == 1;
        return evalCase.IsGuardrail ? sameAction && sameInjection : sameCategory;
    }

    private static string Describe(CaseEvaluation e)
    {
        if (e.Status == CaseStatus.ServiceError)
        {
            return $"SERVICE_ERROR ({e.ServiceError})";
        }

        if (!e.SchemaPass)
        {
            return "FAIL  schema";
        }

        var s = e.Semantic?.Score.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
        return $"{(e.CasePass ? "PASS" : "FAIL")}  invariants={(e.InvariantPass == true ? "ok" : "violated")} S={s}";
    }
}
