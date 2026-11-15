using SpendWise.Core;

namespace SpendWise.Infrastructure;

public sealed class SystemClock : IClock
{
    public DateOnly Today => DateOnly.FromDateTime(DateTime.Now);
}

public static class StorePathValidator
{
    public static string Resolve(string path, string baseDirectory)
    {
        var root = Path.GetFullPath(baseDirectory);
        var full = Path.GetFullPath(path, root);
        var rootWithSeparator = root.EndsWith(Path.DirectorySeparatorChar) ? root : root + Path.DirectorySeparatorChar;
        if (!full.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
        {
            throw new ValidationException("Invalid store path");
        }

        return full;
    }
}

public static class CliHost
{
    public const string DefaultStore = "spendwise.json";

    public static int Run(IReadOnlyList<string> args, string workingDirectory, IClock clock, TextWriter stdout, TextWriter stderr)
    {
        try
        {
            var (remaining, storePath) = ExtractStoreOption(args);
            var resolved = StorePathValidator.Resolve(storePath, workingDirectory);
            return new CommandRunner(new JsonFileStore(resolved), clock).Run(remaining, stdout, stderr);
        }
        catch (ValidationException ex)
        {
            stderr.WriteLine(ex.Message);
            return CommandRunner.ExitValidation;
        }
    }

    private static (List<string> Remaining, string StorePath) ExtractStoreOption(IReadOnlyList<string> args)
    {
        var remaining = new List<string>();
        var store = DefaultStore;
        for (var i = 0; i < args.Count; i++)
        {
            if (args[i] == "--store")
            {
                store = i + 1 < args.Count ? args[++i] : throw new ValidationException("Missing value for option --store");
            }
            else
            {
                remaining.Add(args[i]);
            }
        }

        return (remaining, store);
    }
}
