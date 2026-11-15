using SpendWise.Core;

namespace SpendWise.Tests;

internal sealed class FakeClock(DateOnly today) : IClock
{
    public DateOnly Today { get; } = today;
}

internal sealed class InMemoryStore : IExpenseStore
{
    public StoreData Data { get; set; } = new();

    public int SaveCount { get; private set; }

    public StoreData Load() => Data;

    public void Save(StoreData data)
    {
        Data = data;
        SaveCount++;
    }
}

internal sealed record RunResult(int Code, string Out, string Err);

internal sealed class Harness
{
    public static readonly DateOnly Today = new(2026, 11, 15);

    public InMemoryStore Store { get; } = new();

    public RunResult Run(params string[] args)
    {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var code = new CommandRunner(Store, new FakeClock(Today)).Run(args, stdout, stderr);
        return new RunResult(code, stdout.ToString().TrimEnd(), stderr.ToString().TrimEnd());
    }

    public void Add(string amount, string category, string date, string note = "")
    {
        var result = Run("add", "--amount", amount, "--category", category, "--date", date, "--note", note);
        Assert.Equal(0, result.Code);
    }
}
