using System.Globalization;
using System.Text;
using System.Text.Json;

namespace TicketTriage.App;

public sealed record GeminiOptions(string Model, double Temperature = 0.2, int MaxOutputTokens = 4096, int MaxRetries = 2);

public sealed class GeminiClient(HttpClient http, string apiKey, GeminiOptions options) : IModelClient
{
    private static readonly int[] RetryDelaysMs = [2000, 6000];

    public Func<int, Task> Delay { get; init; } = ms => Task.Delay(ms);

    public async Task<ModelReply> GenerateAsync(string systemPrompt, string userText, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return new ModelReply(false, string.Empty, "GEMINI_API_KEY is not set", null);
        }

        var body = BuildBody(systemPrompt, userText);
        ModelReply last = new(false, string.Empty, "no attempt made", null);
        for (var attempt = 0; attempt <= options.MaxRetries; attempt++)
        {
            last = await SendOnceAsync(body, cancellationToken).ConfigureAwait(false);
            if (last.Success || !IsRetryable(last.HttpStatus) || attempt == options.MaxRetries)
            {
                return last;
            }

            await Delay(RetryDelaysMs[Math.Min(attempt, RetryDelaysMs.Length - 1)]).ConfigureAwait(false);
        }

        return last;
    }

    private static bool IsRetryable(int? status) => status is null or 429 or >= 500;

    private string BuildBody(string systemPrompt, string userText) => JsonSerializer.Serialize(new
    {
        systemInstruction = new { parts = new[] { new { text = systemPrompt } } },
        contents = new[] { new { role = "user", parts = new[] { new { text = userText } } } },
        generationConfig = new
        {
            temperature = options.Temperature,
            maxOutputTokens = options.MaxOutputTokens,
            responseMimeType = "application/json",
        },
    });

    private async Task<ModelReply> SendOnceAsync(string body, CancellationToken cancellationToken)
    {
        var model = options.Model.StartsWith("models/", StringComparison.Ordinal) ? options.Model[7..] : options.Model;
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            new Uri($"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent"))
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("x-goog-api-key", apiKey);

        try
        {
            using var response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            var text = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var status = (int)response.StatusCode;
            return response.IsSuccessStatusCode
                ? ExtractText(text, status)
                : new ModelReply(false, string.Empty, $"HTTP {status.ToString(CultureInfo.InvariantCulture)}", status);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return new ModelReply(false, string.Empty, $"Network or timeout error: {ex.GetType().Name}", null);
        }
    }

    internal static ModelReply ExtractText(string responseJson, int status)
    {
        try
        {
            using var doc = JsonDocument.Parse(responseJson);
            if (!doc.RootElement.TryGetProperty("candidates", out var candidates) || candidates.GetArrayLength() == 0)
            {
                var reason = doc.RootElement.TryGetProperty("promptFeedback", out var feedback)
                    && feedback.TryGetProperty("blockReason", out var block)
                    ? block.GetString()
                    : "no candidates";
                return new ModelReply(false, string.Empty, $"Blocked or empty reply: {reason}", status);
            }

            var sb = new StringBuilder();
            if (candidates[0].TryGetProperty("content", out var content) && content.TryGetProperty("parts", out var parts))
            {
                foreach (var part in parts.EnumerateArray())
                {
                    if (part.TryGetProperty("text", out var t))
                    {
                        sb.Append(t.GetString());
                    }
                }
            }

            return sb.Length == 0
                ? new ModelReply(false, string.Empty, "Blocked or empty reply: no text", status)
                : new ModelReply(true, sb.ToString(), null, status);
        }
        catch (JsonException)
        {
            return new ModelReply(false, string.Empty, "Unreadable HTTP envelope", status);
        }
    }
}
