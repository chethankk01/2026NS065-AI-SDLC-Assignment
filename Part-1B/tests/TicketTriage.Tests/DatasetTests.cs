using System.Text.Json;
using System.Text.RegularExpressions;
using TicketTriage.Harness;

namespace TicketTriage.Tests;

// SC-001: dataset size, coverage of case types and requirement IDs, traceability to the spec
public sealed class DatasetTests
{
    private static List<EvalCase> Load()
    {
        var path = Path.Combine(Samples.RepoRoot(), "evals", "dataset.json");
        return JsonSerializer.Deserialize<List<EvalCase>>(File.ReadAllText(path)) ?? [];
    }

    [Fact]
    public void DatasetHasAtLeastFifteenCasesWithUniqueIds()
    {
        var cases = Load();
        Assert.True(cases.Count >= 15, $"only {cases.Count} cases");
        Assert.Equal(cases.Count, cases.Select(c => c.Id).Distinct(StringComparer.Ordinal).Count());
    }

    [Theory]
    [InlineData("normal", 4)]
    [InlineData("boundary", 2)]
    [InlineData("ambiguous", 2)]
    [InlineData("missing_info", 2)]
    [InlineData("malformed", 2)]
    [InlineData("adversarial", 5)]
    public void EachRequiredCaseTypeHasMinimumCount(string type, int minimum)
    {
        Assert.True(Load().Count(c => c.Type == type) >= minimum);
    }

    [Fact]
    public void EmptyAndWhitespaceTicketsArePresent()
    {
        var inputs = Load().Select(c => c.Input).ToList();
        Assert.Contains(inputs, i => i.Length == 0);
        Assert.Contains(inputs, i => i.Length > 0 && string.IsNullOrWhiteSpace(i));
    }

    [Fact]
    public void EveryInvariantAndGuardrailIsExercisedByAtLeastOneCase()
    {
        var covered = Load().SelectMany(c => c.Requirements).ToHashSet(StringComparer.Ordinal);
        foreach (var id in new[] { "INV-001", "INV-002", "INV-003", "INV-004", "INV-005", "INV-006", "GR-001", "GR-002", "GR-003", "GR-004", "GR-005" })
        {
            Assert.Contains(id, covered);
        }
    }

    [Fact]
    public void EveryRequirementIdInTheDatasetExistsInTheSpec()
    {
        var spec = File.ReadAllText(Path.Combine(Samples.RepoRoot(), "specs", "001-ticket-triage-agent", "spec.md"));
        foreach (var id in Load().SelectMany(c => c.Requirements).Distinct(StringComparer.Ordinal))
        {
            Assert.Contains(id, spec, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void EveryCaseDefinesGroundTruthAndValidExpectedValues()
    {
        foreach (var c in Load())
        {
            Assert.False(string.IsNullOrWhiteSpace(c.Id));
            Assert.NotEmpty(c.Requirements);
            Assert.NotEmpty(c.Expected.Categories);
            Assert.All(c.Expected.Categories, x => Assert.Contains(x, Levels.Categories));
            Assert.All(c.Expected.Actions, x => Assert.Contains(x, Levels.Actions));
            Assert.Contains(c.Expected.Priority, Levels.Priorities);
            if (c.Expected.MaxPriority is { } max)
            {
                Assert.Contains(max, Levels.Priorities);
            }

            if (c.Expected.EnforceAction)
            {
                Assert.NotEmpty(c.Expected.Actions);
            }
        }
    }

    [Fact]
    public void GuardrailCasesEnforceTheirAction()
    {
        Assert.All(Load().Where(c => c.IsGuardrail), c => Assert.True(c.Expected.EnforceAction, c.Id));
    }

    [Fact]
    public void SpecThresholdTextMatchesCodeConstants()
    {
        var spec = File.ReadAllText(Path.Combine(Samples.RepoRoot(), "specs", "001-ticket-triage-agent", "spec.md"));
        Assert.Contains("overall case pass rate >= 90%", spec, StringComparison.Ordinal);
        Assert.Contains("mean semantic score `S` >= 0.80", spec, StringComparison.Ordinal);
        Assert.Contains("0.4*category_match + 0.2*priority_within_one + 0.4*key_term_recall", spec, StringComparison.Ordinal);
        Assert.Contains("S >= 0.70", spec, StringComparison.Ordinal);
        Assert.Matches(new Regex("service errors <= 20%", RegexOptions.None, TimeSpan.FromSeconds(1)), spec);
    }

    [Fact]
    public void PromptFileExistsAndMentionsEveryGuardrailTopic()
    {
        var prompt = File.ReadAllText(Path.Combine(Samples.RepoRoot(), "prompts", "system-prompt.v1.txt"));
        foreach (var word in new[] { "injection", "personal data", "escalate_human", "refuse", "request_clarification" })
        {
            Assert.Contains(word, prompt, StringComparison.OrdinalIgnoreCase);
        }
    }
}
