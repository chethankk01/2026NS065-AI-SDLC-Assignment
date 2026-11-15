using System.Text.Json;

namespace TicketTriage.Harness;

public sealed record ParsedOutput(
    string Category,
    string Priority,
    string Summary,
    string Action,
    string? ClarificationQuestion,
    bool InjectionDetected);

public sealed record SchemaResult(bool Pass, IReadOnlyList<string> Violations, ParsedOutput? Output);

/// <summary>Layer 1 (INV-001): independent, deterministic schema check of the raw model text.</summary>
public static class SchemaValidator
{
    private static readonly string[] Fields =
        ["category", "priority", "summary", "action", "clarification_question", "injection_detected"];

    public static SchemaResult Validate(string raw)
    {
        var violations = new List<string>();
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(Unfence(raw));
        }
        catch (JsonException ex)
        {
            return new SchemaResult(false, [$"INV-001: output is not valid JSON ({ex.Message})"], null);
        }

        using (doc)
        {
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return new SchemaResult(false, ["INV-001: output is not a JSON object"], null);
            }

            CheckFieldSet(root, violations);
            var category = EnumField(root, "category", Levels.Categories, violations);
            var priority = EnumField(root, "priority", Levels.Priorities, violations);
            var action = EnumField(root, "action", Levels.Actions, violations);
            var summary = SummaryField(root, violations);
            var question = QuestionField(root, violations);
            var injection = BoolField(root, "injection_detected", violations);

            if (violations.Count > 0 || category is null || priority is null || action is null || summary is null || injection is null)
            {
                return new SchemaResult(false, violations, null);
            }

            return new SchemaResult(true, violations, new ParsedOutput(category, priority, summary, action, question, injection.Value));
        }
    }

    private static string Unfence(string raw)
    {
        var text = raw.Trim();
        if (!text.StartsWith("```", StringComparison.Ordinal))
        {
            return text;
        }

        var open = text.IndexOf('\n', StringComparison.Ordinal);
        var close = text.LastIndexOf("```", StringComparison.Ordinal);
        return open >= 0 && close > open ? text[(open + 1)..close].Trim() : text;
    }

    private static void CheckFieldSet(JsonElement root, List<string> violations)
    {
        var present = root.EnumerateObject().Select(p => p.Name).ToList();
        violations.AddRange(Fields.Where(f => !present.Contains(f, StringComparer.Ordinal)).Select(f => $"INV-001: missing field '{f}'"));
        violations.AddRange(present.Where(p => !Fields.Contains(p, StringComparer.Ordinal)).Select(p => $"INV-001: unexpected field '{p}'"));
    }

    private static string? EnumField(JsonElement root, string name, IReadOnlyList<string> allowed, List<string> violations)
    {
        if (!root.TryGetProperty(name, out var element))
        {
            return null;
        }

        if (element.ValueKind != JsonValueKind.String)
        {
            violations.Add($"INV-001: '{name}' must be a string");
            return null;
        }

        var value = element.GetString() ?? string.Empty;
        if (!allowed.Contains(value, StringComparer.Ordinal))
        {
            violations.Add($"INV-001: '{name}' has disallowed value '{value}'");
            return null;
        }

        return value;
    }

    private static string? SummaryField(JsonElement root, List<string> violations)
    {
        if (!root.TryGetProperty("summary", out var element))
        {
            return null;
        }

        if (element.ValueKind != JsonValueKind.String)
        {
            violations.Add("INV-001: 'summary' must be a string");
            return null;
        }

        var value = element.GetString() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(value) || value.Length > 200)
        {
            violations.Add($"INV-001: 'summary' must be 1-200 non-whitespace characters (length {value.Length})");
            return null;
        }

        return value;
    }

    private static string? QuestionField(JsonElement root, List<string> violations)
    {
        if (!root.TryGetProperty("clarification_question", out var element) || element.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (element.ValueKind != JsonValueKind.String)
        {
            violations.Add("INV-001: 'clarification_question' must be a string or null");
            return null;
        }

        var value = element.GetString() ?? string.Empty;
        if (value.Length is < 1 or > 200 || !value.EndsWith('?'))
        {
            violations.Add("INV-001: 'clarification_question' must be 1-200 characters and end with '?'");
        }

        return value;
    }

    private static bool? BoolField(JsonElement root, string name, List<string> violations)
    {
        if (!root.TryGetProperty(name, out var element))
        {
            return null;
        }

        if (element.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            return element.GetBoolean();
        }

        violations.Add($"INV-001: '{name}' must be a boolean");
        return null;
    }
}
