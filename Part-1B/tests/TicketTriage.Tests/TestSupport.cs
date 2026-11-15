using TicketTriage.App;
using TicketTriage.Harness;

namespace TicketTriage.Tests;

internal sealed class FakeModelClient(Func<string, ModelReply> reply) : IModelClient
{
    public int Calls { get; private set; }

    public Task<ModelReply> GenerateAsync(string systemPrompt, string userText, CancellationToken cancellationToken)
    {
        Calls++;
        return Task.FromResult(reply(userText));
    }
}

internal static class Samples
{
    public static string Json(
        string category = "billing",
        string priority = "medium",
        string summary = "Customer was charged twice and wants a refund.",
        string action = "route",
        string? question = null,
        bool injection = false)
    {
        var q = question is null ? "null" : $"\"{question}\"";
        return $"{{\"category\":\"{category}\",\"priority\":\"{priority}\",\"summary\":\"{summary}\",\"action\":\"{action}\",\"clarification_question\":{q},\"injection_detected\":{(injection ? "true" : "false")}}}";
    }

    public static EvalCase Case(
        string input = "I was charged twice",
        string[]? categories = null,
        string priority = "medium",
        string[]? actions = null,
        bool? injection = null,
        string? maxPriority = null,
        bool enforceAction = false,
        string[]? keyTerms = null,
        string[]? tags = null) =>
        new(
            "T01",
            "normal",
            tags ?? ["normal"],
            ["FR-001"],
            input,
            new Expected(categories ?? ["billing"], priority, actions ?? ["route"], injection, maxPriority, enforceAction),
            keyTerms ?? ["charge", "twice"]);

    public static ParsedOutput Output(string category = "billing", string priority = "medium", string summary = "Charged twice.", string action = "route", string? question = null, bool injection = false) =>
        new(category, priority, summary, action, question, injection);

    public static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "evals", "dataset.json")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("repo root not found");
    }
}
