namespace TicketTriage.App;

public sealed class TriageAgent(IModelClient client, string systemPrompt)
{
    private const string EmptyInputReply =
        "{\"category\":\"other\",\"priority\":\"low\",\"summary\":\"The ticket was empty.\"," +
        "\"action\":\"request_clarification\",\"clarification_question\":\"What problem do you need help with?\"," +
        "\"injection_detected\":false}";

    public async Task<TriageOutcome> TriageAsync(string ticket, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(ticket))
        {
            // INV-006: the model API rejects empty content, so this case is decided in code.
            var empty = ResponseParser.TryParse(EmptyInputReply, out _);
            return new TriageOutcome(OutcomeKind.Ok, EmptyInputReply, empty, null);
        }

        var reply = await client.GenerateAsync(systemPrompt, ticket, cancellationToken).ConfigureAwait(false);
        if (!reply.Success)
        {
            return new TriageOutcome(OutcomeKind.ServiceError, reply.Text, null, reply.Error);
        }

        var parsed = ResponseParser.TryParse(reply.Text, out var error);
        return parsed is null
            ? new TriageOutcome(OutcomeKind.ParseError, reply.Text, null, error)
            : new TriageOutcome(OutcomeKind.Ok, reply.Text, parsed, null);
    }
}
