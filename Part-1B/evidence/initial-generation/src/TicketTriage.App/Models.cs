using System.Text.Json.Serialization;

namespace TicketTriage.App;

public sealed record TriageResult(
    [property: JsonPropertyName("category")] string Category,
    [property: JsonPropertyName("priority")] string Priority,
    [property: JsonPropertyName("summary")] string Summary,
    [property: JsonPropertyName("action")] string Action,
    [property: JsonPropertyName("clarification_question")] string? ClarificationQuestion,
    [property: JsonPropertyName("injection_detected")] bool InjectionDetected);

public enum OutcomeKind
{
    Ok,
    ServiceError,
    ParseError,
}

public sealed record TriageOutcome(OutcomeKind Kind, string RawText, TriageResult? Result, string? Error);

public sealed record ModelReply(bool Success, string Text, string? Error, int? HttpStatus);

public interface IModelClient
{
    Task<ModelReply> GenerateAsync(string systemPrompt, string userText, CancellationToken cancellationToken);
}
