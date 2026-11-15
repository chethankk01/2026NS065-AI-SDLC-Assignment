using System.Security.Cryptography;
using System.Text;
using TicketTriage.App;

const int ExitOk = 0;
const int ExitUsage = 1;
const int ExitService = 2;

if (args.Length != 2 || args[0] != "triage")
{
    Console.Error.WriteLine("Usage: triage \"<ticket text>\"   (env: GEMINI_API_KEY, GEMINI_MODEL)");
    return ExitUsage;
}

var promptPath = FindUp("prompts/system-prompt.v1.txt");
if (promptPath is null)
{
    Console.Error.WriteLine("Cannot find prompts/system-prompt.v1.txt");
    return ExitUsage;
}

var prompt = await File.ReadAllTextAsync(promptPath);
var model = Environment.GetEnvironmentVariable("GEMINI_MODEL") ?? "gemini-pro-latest";
var key = Environment.GetEnvironmentVariable("GEMINI_API_KEY") ?? string.Empty;
using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(90) };
var agent = new TriageAgent(new GeminiClient(http, key, new GeminiOptions(model)), prompt);

var outcome = await agent.TriageAsync(args[1]);
Console.Error.WriteLine($"model={model} promptHash={Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(prompt)))[..12]}");
switch (outcome.Kind)
{
    case OutcomeKind.Ok:
        Console.WriteLine(outcome.RawText.Trim());
        return ExitOk;
    case OutcomeKind.ParseError:
        Console.Error.WriteLine($"Model reply could not be parsed: {outcome.Error}");
        Console.Error.WriteLine(outcome.RawText);
        return ExitService;
    default:
        Console.Error.WriteLine($"Service error: {outcome.Error}");
        return ExitService;
}

static string? FindUp(string relative)
{
    var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
    while (dir is not null)
    {
        var candidate = Path.Combine(dir.FullName, relative);
        if (File.Exists(candidate))
        {
            return candidate;
        }

        dir = dir.Parent;
    }

    return null;
}
