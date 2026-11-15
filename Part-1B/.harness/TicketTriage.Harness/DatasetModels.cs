using System.Text.Json.Serialization;

namespace TicketTriage.Harness;

public sealed record Expected(
    [property: JsonPropertyName("categories")] IReadOnlyList<string> Categories,
    [property: JsonPropertyName("priority")] string Priority,
    [property: JsonPropertyName("actions")] IReadOnlyList<string> Actions,
    [property: JsonPropertyName("injection_detected")] bool? InjectionDetected,
    [property: JsonPropertyName("max_priority")] string? MaxPriority,
    [property: JsonPropertyName("enforce_action")] bool EnforceAction);

public sealed record EvalCase(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("tags")] IReadOnlyList<string> Tags,
    [property: JsonPropertyName("requirements")] IReadOnlyList<string> Requirements,
    [property: JsonPropertyName("input")] string Input,
    [property: JsonPropertyName("expected")] Expected Expected,
    [property: JsonPropertyName("key_terms")] IReadOnlyList<string> KeyTerms)
{
    public bool IsGuardrail => Tags.Contains("guardrail", StringComparer.Ordinal);
}

public static class Levels
{
    public static readonly IReadOnlyList<string> Priorities = ["low", "medium", "high", "urgent"];
    public static readonly IReadOnlyList<string> Categories = ["billing", "technical", "account", "shipping", "other"];
    public static readonly IReadOnlyList<string> Actions = ["route", "request_clarification", "escalate_human", "refuse"];

    public static int PriorityRank(string priority)
    {
        for (var i = 0; i < Priorities.Count; i++)
        {
            if (string.Equals(Priorities[i], priority, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }
}
