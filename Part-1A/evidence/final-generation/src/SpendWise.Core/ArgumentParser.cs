namespace SpendWise.Core;

public sealed record ParsedArgs(string Command, IReadOnlyDictionary<string, string> Options, bool Json)
{
    public string? Get(string name) => Options.TryGetValue(name, out var value) ? value : null;

    public string Require(string name) =>
        Get(name) ?? throw new ValidationException($"Missing required option --{name}");
}

public static class ArgumentParser
{
    private static readonly HashSet<string> KnownOptions =
        ["amount", "category", "date", "note", "month", "id", "limit"];

    public static ParsedArgs Parse(IReadOnlyList<string> args)
    {
        if (args.Count == 0)
        {
            throw new ValidationException("Missing command");
        }

        var command = args[0];
        var start = 1;
        if (command == "budget" && args.Count > 1 && args[1] == "set")
        {
            command = "budget set";
            start = 2;
        }

        var options = new Dictionary<string, string>(StringComparer.Ordinal);
        var json = false;
        for (var i = start; i < args.Count; i++)
        {
            if (args[i] == "--json")
            {
                json = true;
                continue;
            }

            var name = OptionName(args[i]);
            if (i + 1 >= args.Count)
            {
                throw new ValidationException($"Missing value for option --{name}");
            }

            options[name] = args[++i];
        }

        return new ParsedArgs(command, options, json);
    }

    private static string OptionName(string token)
    {
        if (!token.StartsWith("--", StringComparison.Ordinal) || !KnownOptions.Contains(token[2..]))
        {
            throw new ValidationException($"Unknown option {token}");
        }

        return token[2..];
    }
}
