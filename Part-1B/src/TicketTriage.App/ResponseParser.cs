using System.Text.Json;

namespace TicketTriage.App;

public static class ResponseParser
{
    public static string StripCodeFences(string text)
    {
        var trimmed = text.Trim();
        if (!trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            return trimmed;
        }

        var firstNewline = trimmed.IndexOf('\n', StringComparison.Ordinal);
        var lastFence = trimmed.LastIndexOf("```", StringComparison.Ordinal);
        if (firstNewline < 0 || lastFence <= firstNewline)
        {
            return trimmed;
        }

        return trimmed[(firstNewline + 1)..lastFence].Trim();
    }

    public static TriageResult? TryParse(string text, out string? error)
    {
        try
        {
            using var doc = JsonDocument.Parse(StripCodeFences(text));
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                error = "Reply is not a JSON object";
                return null;
            }

            var result = new TriageResult(
                RequiredString(root, "category"),
                RequiredString(root, "priority"),
                RequiredString(root, "summary"),
                RequiredString(root, "action"),
                OptionalString(root, "clarification_question"),
                root.GetProperty("injection_detected").GetBoolean());
            error = null;
            return result;
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            error = $"Reply does not match the expected shape: {ex.Message}";
            return null;
        }
    }

    private static string RequiredString(JsonElement root, string name) => root.GetProperty(name).GetString()
        ?? throw new InvalidOperationException($"{name} is null");

    private static string? OptionalString(JsonElement root, string name)
    {
        var element = root.GetProperty(name);
        return element.ValueKind == JsonValueKind.Null ? null : element.GetString();
    }
}
