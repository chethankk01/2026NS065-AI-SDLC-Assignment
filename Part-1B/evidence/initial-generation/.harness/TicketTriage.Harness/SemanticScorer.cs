namespace TicketTriage.Harness;

public sealed record SemanticScore(decimal CategoryMatch, decimal PriorityWithinOne, decimal KeyTermRecall, decimal Score);

/// <summary>Layer 3 (deterministic part): S = 0.4*category + 0.2*priority(+-1) + 0.4*key-term recall.</summary>
public static class SemanticScorer
{
    public static SemanticScore Score(EvalCase evalCase, ParsedOutput output)
    {
        var category = evalCase.Expected.Categories.Contains(output.Category, StringComparer.Ordinal) ? 1m : 0m;
        var priorityDiff = Math.Abs(Levels.PriorityRank(output.Priority) - Levels.PriorityRank(evalCase.Expected.Priority));
        var priority = priorityDiff <= 1 ? 1m : 0m;
        var recall = KeyTermRecall(evalCase.KeyTerms, output.Summary);
        return new SemanticScore(category, priority, recall, (Threshold.WeightCategory * category) + (Threshold.WeightPriority * priority) + (Threshold.WeightKeyTerms * recall));
    }

    public static decimal KeyTermRecall(IReadOnlyList<string> keyTerms, string summary)
    {
        if (keyTerms.Count == 0)
        {
            return 1m;
        }

        var matched = keyTerms.Count(term => term
            .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(alt => summary.Contains(alt, StringComparison.OrdinalIgnoreCase)));
        return (decimal)matched / keyTerms.Count;
    }
}
