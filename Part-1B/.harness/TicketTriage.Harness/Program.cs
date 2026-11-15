using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TicketTriage.App;
using TicketTriage.Harness;

// Usage: eval [--dataset path] [--prompt path] [--out dir] [--judge] [--repeat n] [--delay-ms n] [--limit n]
// Exit codes: 0 PASS, 1 FAIL, 3 INCONCLUSIVE, 2 harness/usage error. Key from GEMINI_API_KEY, model from GEMINI_MODEL.
const int ExitPass = 0;
const int ExitFail = 1;
const int ExitError = 2;
const int ExitInconclusive = 3;

if (args.Length == 0 || args[0] != "eval")
{
    Console.Error.WriteLine("Usage: eval [--dataset path] [--prompt path] [--out dir] [--judge] [--repeat n] [--delay-ms n] [--limit n]");
    return ExitError;
}

var options = ParseOptions(args.Skip(1).ToArray());
var root = FindRoot();
if (root is null)
{
    Console.Error.WriteLine("Cannot locate the project root (evals/dataset.json).");
    return ExitError;
}

var datasetPath = options.GetValueOrDefault("dataset") ?? Path.Combine(root, "evals", "dataset.json");
var promptPath = options.GetValueOrDefault("prompt") ?? Path.Combine(root, "prompts", "system-prompt.v1.txt");
var repeat = int.Parse(options.GetValueOrDefault("repeat") ?? "1", CultureInfo.InvariantCulture);
var delayMs = int.Parse(options.GetValueOrDefault("delay-ms") ?? "6000", CultureInfo.InvariantCulture);
var limit = int.Parse(options.GetValueOrDefault("limit") ?? "0", CultureInfo.InvariantCulture);
var judge = options.ContainsKey("judge");

var datasetText = await File.ReadAllTextAsync(datasetPath);
var cases = JsonSerializer.Deserialize<List<EvalCase>>(datasetText) ?? [];
if (limit > 0)
{
    cases = cases.Take(limit).ToList();
}

var prompt = await File.ReadAllTextAsync(promptPath);
var model = Environment.GetEnvironmentVariable("GEMINI_MODEL") ?? "gemini-pro-latest";
var key = Environment.GetEnvironmentVariable("GEMINI_API_KEY") ?? string.Empty;
var geminiOptions = new GeminiOptions(model);
using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(120) };
IModelClient client = new GeminiClient(http, key, geminiOptions);
var runner = new EvalRunner(new TriageAgent(client, prompt), client);

var config = new EvalConfig(
    model, geminiOptions.Temperature, geminiOptions.MaxOutputTokens,
    Path.GetRelativePath(root, promptPath).Replace('\\', '/'), Sha(prompt),
    Path.GetRelativePath(root, datasetPath).Replace('\\', '/'), Sha(datasetText),
    repeat, judge, delayMs, DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture));

Console.WriteLine($"TicketTriage evaluation: model={model} cases={cases.Count} repeat={repeat} judge={judge} promptHash={config.PromptHash}");
var report = await runner.RunAsync(cases, config, ms => Task.Delay(ms), Console.WriteLine);

var runsDir = Path.Combine(root, "evidence", "runs");
Directory.CreateDirectory(runsDir);
var runId = $"run-{Directory.GetDirectories(runsDir, "run-*").Length + 1:D3}";
var outDir = options.GetValueOrDefault("out") ?? Path.Combine(runsDir, runId);
Directory.CreateDirectory(outDir);
var json = ReportWriter.ToJson(report);
await File.WriteAllTextAsync(Path.Combine(outDir, "eval-results.json"), json);
await File.WriteAllTextAsync(Path.Combine(root, "evidence", "eval-results.json"), json);
if (report.Aggregate.Decision != Decision.Pass)
{
    await File.WriteAllTextAsync(Path.Combine(outDir, "feedback.md"), ReportWriter.FeedbackMarkdown(report, runId));
}

var a = report.Aggregate;
Console.WriteLine();
Console.WriteLine($"scored={a.ScoredCases}/{a.TotalCases} serviceErrors={a.ServiceErrors} casesPassed={a.CasesPassed} guardrails={a.GuardrailPassed}/{a.GuardrailScored} meanS={a.MeanSemantic:0.000}");
Console.WriteLine($"DECISION: {ReportWriter.DecisionText(a.Decision)} ({runId}) -> {Path.GetRelativePath(root, outDir)}");
foreach (var f in a.FailedConditions)
{
    Console.WriteLine($"  failed condition: {f}");
}

return a.Decision switch
{
    Decision.Pass => ExitPass,
    Decision.Fail => ExitFail,
    _ => ExitInconclusive,
};

static string Sha(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..12];

static Dictionary<string, string> ParseOptions(string[] tokens)
{
    var result = new Dictionary<string, string>(StringComparer.Ordinal);
    for (var i = 0; i < tokens.Length; i++)
    {
        var name = tokens[i].TrimStart('-');
        result[name] = name == "judge" || i + 1 >= tokens.Length ? "true" : tokens[++i];
    }

    return result;
}

static string? FindRoot()
{
    var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
    while (dir is not null)
    {
        if (File.Exists(Path.Combine(dir.FullName, "evals", "dataset.json")))
        {
            return dir.FullName;
        }

        dir = dir.Parent;
    }

    return null;
}
