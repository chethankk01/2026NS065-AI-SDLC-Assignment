using System.Diagnostics;
using System.Security.Cryptography;
using SpendWise.Core;
using SpendWise.Infrastructure;

namespace SpendWise.Tests;

// Spec: FR-013, FR-014, FR-015, SC-004, SC-005, NFR-005
public sealed class InfrastructureTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "spendwise-" + Guid.NewGuid().ToString("N"));

    public InfrastructureTests() => Directory.CreateDirectory(_dir);

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private (int Code, string Out, string Err) Cli(params string[] args)
    {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var code = CliHost.Run(args, _dir, new FakeClock(Harness.Today), stdout, stderr);
        return (code, stdout.ToString().TrimEnd(), stderr.ToString().TrimEnd());
    }

    [Fact]
    public void Data_PersistsAcrossRuns_FR013()
    {
        Cli("add", "--amount", "12.50", "--category", "Food", "--date", "2026-11-10");
        var r = Cli("list");

        Assert.Equal(0, r.Code);
        Assert.Contains("12.50", r.Out, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(_dir, "spendwise.json.tmp")));
    }

    [Fact]
    public void SecondSave_ReplacesExistingFile_FR013()
    {
        Cli("add", "--amount", "1.00", "--category", "Food", "--date", "2026-11-10");
        Cli("add", "--amount", "2.00", "--category", "Food", "--date", "2026-11-11");

        Assert.Equal(2, Cli("list").Out.Split('\n').Length);
    }

    [Fact]
    public void IdsAreNotReusedAcrossProcesses_FR007()
    {
        Cli("add", "--amount", "1.00", "--category", "Food", "--date", "2026-11-10");
        Cli("delete", "--id", "1");
        var r = Cli("add", "--amount", "1.00", "--category", "Food", "--date", "2026-11-10");

        Assert.StartsWith("Added expense #2", r.Out, StringComparison.Ordinal);
    }

    [Fact]
    public void CorruptStore_ExitsWith2AndIsNotModified_US2_6_SC005()
    {
        var file = Path.Combine(_dir, "spendwise.json");
        File.WriteAllText(file, "{ this is not json");
        var before = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file)));

        foreach (var command in new[] { new[] { "list" }, ["add", "--amount", "5", "--category", "Food"] })
        {
            var r = Cli(command);
            Assert.Equal(2, r.Code);
            Assert.Contains("Storage error", r.Err, StringComparison.Ordinal);
        }

        Assert.Equal(before, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file))));
    }

    [Fact]
    public void NullJsonStore_IsStorageError_FR013()
    {
        File.WriteAllText(Path.Combine(_dir, "spendwise.json"), "null");
        Assert.Equal(2, Cli("list").Code);
    }

    [Fact]
    public void StoreOption_UsesGivenFile_FR015()
    {
        Cli("add", "--amount", "1.00", "--category", "Food", "--date", "2026-11-10", "--store", "other.json");

        Assert.True(File.Exists(Path.Combine(_dir, "other.json")));
        Assert.Equal("No expenses found.", Cli("list").Out);
    }

    [Theory]
    [InlineData("../escape.json")]
    [InlineData("sub/../../escape.json")]
    public void StoreOutsideWorkingDirectory_IsRejected_FR015(string path)
    {
        var r = Cli("list", "--store", path);
        Assert.Equal(1, r.Code);
        Assert.Contains("Invalid store path", r.Err, StringComparison.Ordinal);
    }

    [Fact]
    public void StoreOptionWithoutValue_IsRejected_FR015()
    {
        Assert.Equal(1, Cli("list", "--store").Code);
    }

    [Fact]
    public void UnwritableStore_IsStorageError_FR013()
    {
        Directory.CreateDirectory(Path.Combine(_dir, "spendwise.json"));
        Assert.Equal(2, Cli("list").Code);
    }

    [Fact]
    public void SameCommandSequence_ProducesIdenticalOutputAndStore_SC004()
    {
        string Sequence(string store)
        {
            Cli("add", "--amount", "12.50", "--category", "Food", "--date", "2026-11-10", "--store", store);
            Cli("budget", "set", "--category", "Food", "--month", "2026-11", "--limit", "100", "--store", store);
            var summary = Cli("summary", "--month", "2026-11", "--store", store).Out;
            return summary + File.ReadAllText(Path.Combine(_dir, store));
        }

        Assert.Equal(Sequence("a.json"), Sequence("b.json"));
    }

    [Fact]
    public void ListWith10000Expenses_CompletesUnder500ms_NFR005()
    {
        var store = new JsonFileStore(Path.Combine(_dir, "big.json"));
        var data = new StoreData { NextId = 10_001 };
        for (var i = 1; i <= 10_000; i++)
        {
            data.Expenses.Add(new Expense(i, 1.00m, Category.Food, new DateOnly(2026, 11, 1), "n"));
        }

        store.Save(data);
        var sw = Stopwatch.StartNew();
        var r = Cli("summary", "--month", "2026-11", "--store", "big.json");
        sw.Stop();

        Assert.Equal(0, r.Code);
        Assert.Contains("10000.00", r.Out, StringComparison.Ordinal);
        Assert.True(sw.ElapsedMilliseconds < 500, $"took {sw.ElapsedMilliseconds} ms");
    }

    [Fact]
    public void SystemClock_ReturnsCurrentDate_FR014()
    {
        var delta = Math.Abs(new SystemClock().Today.DayNumber - DateOnly.FromDateTime(DateTime.Now).DayNumber);
        Assert.True(delta <= 1);
    }
}
