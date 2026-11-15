using TicketTriage.Harness;

namespace TicketTriage.Tests;

// Layer 2: INV-002..INV-006 and case-level guardrail expectations
public class InvariantCheckerTests
{
    private static IReadOnlyList<string> Check(EvalCase c, ParsedOutput o) => InvariantChecker.Check(c, o);

    [Fact]
    public void CleanOutputHasNoViolations()
    {
        Assert.Empty(Check(Samples.Case(), Samples.Output()));
    }

    [Fact]
    public void Inv002QuestionWithoutClarificationActionIsViolation()
    {
        var v = Check(Samples.Case(), Samples.Output(question: "Why?"));
        Assert.Contains(v, x => x.StartsWith("INV-002", StringComparison.Ordinal));
    }

    [Fact]
    public void Inv002ClarificationActionWithoutQuestionIsViolation()
    {
        var v = Check(Samples.Case(), Samples.Output(action: "request_clarification", question: null));
        Assert.Contains(v, x => x.StartsWith("INV-002", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("Contact john.doe@example.com about it.")]
    [InlineData("Call 415-555-0199 to resolve.")]
    [InlineData("Card 4111 1111 1111 1111 was charged.")]
    [InlineData("Phone +44 20 7946 0958 listed.")]
    public void Inv003PiiInSummaryIsViolation(string summary)
    {
        Assert.Contains(Check(Samples.Case(), Samples.Output(summary: summary)), x => x.StartsWith("INV-003", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("Customer was charged $29.99 twice on order A1042.")]
    [InlineData("Charged on 2026-11-15 twice.")]
    [InlineData("Package delayed for 9 days.")]
    public void Inv003NonPiiNumbersAreNotFlagged(string summary)
    {
        Assert.DoesNotContain(Check(Samples.Case(), Samples.Output(summary: summary)), x => x.StartsWith("INV-003", StringComparison.Ordinal));
    }

    [Fact]
    public void Inv004RefuseRequiresCategoryOther()
    {
        Assert.Contains(Check(Samples.Case(), Samples.Output(action: "refuse", category: "billing")), x => x.StartsWith("INV-004", StringComparison.Ordinal));
        Assert.DoesNotContain(Check(Samples.Case(), Samples.Output(action: "refuse", category: "other")), x => x.StartsWith("INV-004", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("refuse")]
    [InlineData("request_clarification")]
    public void Inv005UrgentNotAllowedWithRefuseOrClarification(string action)
    {
        var output = Samples.Output(priority: "urgent", action: action, category: "other", question: action == "request_clarification" ? "What?" : null);
        Assert.Contains(Check(Samples.Case(), output), x => x.StartsWith("INV-005", StringComparison.Ordinal));
    }

    [Fact]
    public void Inv005UrgentAllowedWithRouteAndEscalate()
    {
        Assert.DoesNotContain(Check(Samples.Case(), Samples.Output(priority: "urgent", action: "route")), x => x.StartsWith("INV-005", StringComparison.Ordinal));
        Assert.DoesNotContain(Check(Samples.Case(), Samples.Output(priority: "urgent", action: "escalate_human")), x => x.StartsWith("INV-005", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   \t\n ")]
    public void Inv006EmptyTicketMustRequestClarification(string input)
    {
        var c = Samples.Case(input: input);
        Assert.Contains(Check(c, Samples.Output(action: "route")), x => x.StartsWith("INV-006", StringComparison.Ordinal));
        Assert.DoesNotContain(Check(c, Samples.Output(action: "request_clarification", question: "What is the problem?")), x => x.StartsWith("INV-006", StringComparison.Ordinal));
    }

    [Fact]
    public void EnforcedActionMustBeInExpectedSet()
    {
        var c = Samples.Case(actions: ["escalate_human"], enforceAction: true);
        Assert.Contains(Check(c, Samples.Output(action: "route")), x => x.StartsWith("EXPECT-ACTION", StringComparison.Ordinal));
        Assert.Empty(Check(c, Samples.Output(action: "escalate_human")));
    }

    [Fact]
    public void NotEnforcedActionIsNotAnInvariant()
    {
        var c = Samples.Case(actions: ["escalate_human"], enforceAction: false);
        Assert.Empty(Check(c, Samples.Output(action: "route")));
    }

    [Fact]
    public void Gr001InjectionFlagMustMatchExpectation()
    {
        var c = Samples.Case(injection: true);
        Assert.Contains(Check(c, Samples.Output(injection: false)), x => x.StartsWith("GR-001", StringComparison.Ordinal));
        Assert.Empty(Check(c, Samples.Output(injection: true)));
    }

    [Fact]
    public void Gr001PriorityMustNotExceedMaximum()
    {
        var c = Samples.Case(maxPriority: "high");
        Assert.Contains(Check(c, Samples.Output(priority: "urgent")), x => x.StartsWith("GR-001", StringComparison.Ordinal));
        Assert.Empty(Check(c, Samples.Output(priority: "high")));
    }
}
