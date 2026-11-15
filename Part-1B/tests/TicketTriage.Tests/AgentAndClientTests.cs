using System.Net;
using System.Text.Json;
using TicketTriage.App;

namespace TicketTriage.Tests;

// FR-001, FR-003, FR-005, FR-007, INV-006 (agent side)
public sealed class AgentAndClientTests
{
    private static ModelReply Reply(string text) => new(true, text, null, 200);

    [Theory]
    [InlineData("")]
    [InlineData("   \t\n")]
    public async Task EmptyTicketIsHandledWithoutCallingTheModel(string ticket)
    {
        var client = new FakeModelClient(_ => Reply("should not be used"));
        var outcome = await new TriageAgent(client, "p").TriageAsync(ticket);

        Assert.Equal(0, client.Calls);
        Assert.Equal(OutcomeKind.Ok, outcome.Kind);
        Assert.Equal("request_clarification", outcome.Result!.Action);
        Assert.NotNull(outcome.Result.ClarificationQuestion);
    }

    [Fact]
    public async Task ValidReplyIsParsed()
    {
        var outcome = await new TriageAgent(new FakeModelClient(_ => Reply(Samples.Json())), "p").TriageAsync("I was charged twice");
        Assert.Equal(OutcomeKind.Ok, outcome.Kind);
        Assert.Equal("billing", outcome.Result!.Category);
    }

    [Fact]
    public async Task CodeFencedReplyIsParsed()
    {
        var outcome = await new TriageAgent(new FakeModelClient(_ => Reply("```json\n" + Samples.Json() + "\n```")), "p").TriageAsync("x");
        Assert.Equal(OutcomeKind.Ok, outcome.Kind);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("{\"category\":\"billing\"}")]
    public async Task UnparseableReplyIsParseErrorAndKeepsRawText(string raw)
    {
        var outcome = await new TriageAgent(new FakeModelClient(_ => Reply(raw)), "p").TriageAsync("x");
        Assert.Equal(OutcomeKind.ParseError, outcome.Kind);
        Assert.Equal(raw, outcome.RawText);
    }

    [Fact]
    public async Task ServiceFailureIsServiceErrorNotParseError()
    {
        var client = new FakeModelClient(_ => new ModelReply(false, string.Empty, "HTTP 429", 429));
        var outcome = await new TriageAgent(client, "p").TriageAsync("x");
        Assert.Equal(OutcomeKind.ServiceError, outcome.Kind);
        Assert.Equal("HTTP 429", outcome.Error);
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public int Requests { get; private set; }

        public string? LastApiKeyHeader { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests++;
            LastApiKeyHeader = request.Headers.TryGetValues("x-goog-api-key", out var v) ? v.First() : null;
            return Task.FromResult(respond(request));
        }
    }

    private static string Envelope(string text) =>
        JsonSerializer.Serialize(new { candidates = new[] { new { content = new { parts = new[] { new { text } } } } } });

    private static GeminiClient NewClient(StubHandler handler, string key = "test-key") =>
        new(new HttpClient(handler), key, new GeminiOptions("gemini-pro-latest")) { Delay = _ => Task.CompletedTask };

    [Fact]
    public async Task SuccessfulHttpResponseReturnsModelText()
    {
        using var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Envelope("hello")) });
        var reply = await NewClient(handler).GenerateAsync("sys", "user", CancellationToken.None);

        Assert.True(reply.Success);
        Assert.Equal("hello", reply.Text);
        Assert.Equal("test-key", handler.LastApiKeyHeader);
    }

    [Fact]
    public async Task RateLimitIsRetriedTwiceThenReportedAsServiceFailure()
    {
        using var handler = new StubHandler(_ => new HttpResponseMessage((HttpStatusCode)429));
        var reply = await NewClient(handler).GenerateAsync("s", "u", CancellationToken.None);

        Assert.False(reply.Success);
        Assert.Equal(429, reply.HttpStatus);
        Assert.Equal(3, handler.Requests);
    }

    [Fact]
    public async Task ClientErrorIsNotRetried()
    {
        using var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.BadRequest));
        var reply = await NewClient(handler).GenerateAsync("s", "u", CancellationToken.None);

        Assert.False(reply.Success);
        Assert.Equal(1, handler.Requests);
    }

    [Fact]
    public async Task TransientFailureThenSuccessIsRecovered()
    {
        using var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Envelope("ok")) });
        var calls = 0;
        using var flaky = new StubHandler(r => ++calls == 1
            ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Envelope("ok")) });
        var reply = await NewClient(flaky).GenerateAsync("s", "u", CancellationToken.None);

        Assert.True(reply.Success);
        Assert.Equal(2, flaky.Requests);
        Assert.Equal(0, handler.Requests);
    }

    [Fact]
    public async Task BlockedReplyIsServiceFailure()
    {
        using var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"promptFeedback\":{\"blockReason\":\"SAFETY\"}}"),
        });
        var reply = await NewClient(handler).GenerateAsync("s", "u", CancellationToken.None);

        Assert.False(reply.Success);
        Assert.Contains("SAFETY", reply.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MissingKeyIsServiceFailureWithoutAnyHttpCall()
    {
        using var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var reply = await NewClient(handler, key: string.Empty).GenerateAsync("s", "u", CancellationToken.None);

        Assert.False(reply.Success);
        Assert.Equal(0, handler.Requests);
    }

    [Fact]
    public async Task ErrorMessagesNeverContainTheApiKey()
    {
        const string key = "SECRET-KEY-VALUE-123";
        using var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.Forbidden));
        var reply = await NewClient(handler, key).GenerateAsync("s", "u", CancellationToken.None);

        Assert.DoesNotContain(key, reply.Error ?? string.Empty, StringComparison.Ordinal);
        Assert.DoesNotContain(key, reply.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ModelPrefixInNameIsAccepted()
    {
        Uri? seen = null;
        using var handler = new StubHandler(r =>
        {
            seen = r.RequestUri;
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Envelope("ok")) };
        });
        var client = new GeminiClient(new HttpClient(handler), "k", new GeminiOptions("models/gemini-pro-latest"));
        await client.GenerateAsync("s", "u", CancellationToken.None);

        Assert.Contains("/models/gemini-pro-latest:generateContent", seen!.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("models/models", seen.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnreadableEnvelopeIsServiceFailure()
    {
        using var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("<html>") });
        var reply = await NewClient(handler).GenerateAsync("s", "u", CancellationToken.None);
        Assert.False(reply.Success);
    }
}
