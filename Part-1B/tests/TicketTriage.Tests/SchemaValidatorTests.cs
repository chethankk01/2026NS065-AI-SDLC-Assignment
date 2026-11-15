using TicketTriage.Harness;

namespace TicketTriage.Tests;

// Layer 1: INV-001 and the output schema contract
public class SchemaValidatorTests
{
    [Fact]
    public void ValidOutputPassesInv001()
    {
        var result = SchemaValidator.Validate(Samples.Json());
        Assert.True(result.Pass);
        Assert.Equal("billing", result.Output!.Category);
    }

    [Fact]
    public void CodeFencedJsonIsAccepted()
    {
        Assert.True(SchemaValidator.Validate("```json\n" + Samples.Json() + "\n```").Pass);
    }

    [Theory]
    [InlineData("not json at all")]
    [InlineData("[1,2,3]")]
    [InlineData("\"text\"")]
    [InlineData("")]
    public void NonObjectOrInvalidJsonFails(string raw)
    {
        Assert.False(SchemaValidator.Validate(raw).Pass);
    }

    [Fact]
    public void MissingFieldFails()
    {
        var raw = "{\"category\":\"billing\",\"priority\":\"low\",\"summary\":\"x\",\"action\":\"route\",\"clarification_question\":null}";
        var result = SchemaValidator.Validate(raw);
        Assert.False(result.Pass);
        Assert.Contains(result.Violations, v => v.Contains("injection_detected", StringComparison.Ordinal));
    }

    [Fact]
    public void ExtraFieldFails()
    {
        var raw = Samples.Json().TrimEnd('}') + ",\"confidence\":0.9}";
        var result = SchemaValidator.Validate(raw);
        Assert.False(result.Pass);
        Assert.Contains(result.Violations, v => v.Contains("confidence", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("shipping-ish", "medium", "route")]
    [InlineData("billing", "critical", "route")]
    [InlineData("billing", "medium", "approve")]
    [InlineData("Billing", "medium", "route")]
    public void DisallowedEnumValueFails(string category, string priority, string action)
    {
        Assert.False(SchemaValidator.Validate(Samples.Json(category: category, priority: priority, action: action)).Pass);
    }

    [Fact]
    public void SummaryOver200CharsFailsAndExactly200Passes()
    {
        Assert.False(SchemaValidator.Validate(Samples.Json(summary: new string('a', 201))).Pass);
        Assert.True(SchemaValidator.Validate(Samples.Json(summary: new string('a', 200))).Pass);
    }

    [Fact]
    public void WhitespaceSummaryFails()
    {
        Assert.False(SchemaValidator.Validate(Samples.Json(summary: "   ")).Pass);
    }

    [Fact]
    public void ClarificationQuestionMustEndWithQuestionMark()
    {
        Assert.False(SchemaValidator.Validate(Samples.Json(action: "request_clarification", question: "Tell me more")).Pass);
        Assert.True(SchemaValidator.Validate(Samples.Json(action: "request_clarification", question: "Tell me more?")).Pass);
    }

    [Fact]
    public void WrongTypesFail()
    {
        Assert.False(SchemaValidator.Validate("{\"category\":1,\"priority\":\"low\",\"summary\":\"x\",\"action\":\"route\",\"clarification_question\":null,\"injection_detected\":false}").Pass);
        Assert.False(SchemaValidator.Validate("{\"category\":\"billing\",\"priority\":\"low\",\"summary\":5,\"action\":\"route\",\"clarification_question\":null,\"injection_detected\":false}").Pass);
        Assert.False(SchemaValidator.Validate("{\"category\":\"billing\",\"priority\":\"low\",\"summary\":\"x\",\"action\":\"route\",\"clarification_question\":5,\"injection_detected\":false}").Pass);
        Assert.False(SchemaValidator.Validate("{\"category\":\"billing\",\"priority\":\"low\",\"summary\":\"x\",\"action\":\"route\",\"clarification_question\":null,\"injection_detected\":\"no\"}").Pass);
    }
}
