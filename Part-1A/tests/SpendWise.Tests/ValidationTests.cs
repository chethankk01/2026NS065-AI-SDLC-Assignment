namespace SpendWise.Tests;

// Spec: User Story 2 (FR-002..FR-005, FR-012)
public class ValidationTests
{
    [Theory]
    [InlineData("0")]
    [InlineData("-5")]
    [InlineData("abc")]
    [InlineData("1000000.01")]
    [InlineData("10.999")]
    [InlineData("12,50")]
    [InlineData("1e3")]
    [InlineData(" 5")]
    [InlineData("")]
    public void AddRejectsInvalidAmountUS21(string amount)
    {
        var h = new Harness();
        var r = h.Run("add", "--amount", amount, "--category", "Food", "--date", "2026-11-01");

        Assert.Equal(1, r.Code);
        Assert.Contains("Invalid amount", r.Err, StringComparison.Ordinal);
        Assert.Equal(0, h.Store.SaveCount);
    }

    [Theory]
    [InlineData("0.01")]
    [InlineData("1000000.00")]
    public void AddAcceptsAmountBoundariesEdgeCase(string amount)
    {
        var r = new Harness().Run("add", "--amount", amount, "--category", "Food", "--date", "2026-11-01");
        Assert.Equal(0, r.Code);
    }

    [Fact]
    public void AddRejectsUnknownCategoryUS22()
    {
        var r = new Harness().Run("add", "--amount", "5", "--category", "Pets");
        Assert.Equal(1, r.Code);
        Assert.Contains("Unknown category", r.Err, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("2026-11-16")]
    [InlineData("1999-12-31")]
    [InlineData("2026-02-30")]
    [InlineData("2026-1-5")]
    [InlineData("15/11/2026")]
    public void AddRejectsInvalidDateUS23(string date)
    {
        var r = new Harness().Run("add", "--amount", "5", "--category", "Food", "--date", date);
        Assert.Equal(1, r.Code);
        Assert.Contains("Invalid date", r.Err, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("2026-11-15")]
    [InlineData("2000-01-01")]
    public void AddAcceptsDateBoundariesFR004(string date)
    {
        var r = new Harness().Run("add", "--amount", "5", "--category", "Food", "--date", date);
        Assert.Equal(0, r.Code);
    }

    [Fact]
    public void AddRejectsLongNoteUS24()
    {
        var r = new Harness().Run("add", "--amount", "5", "--category", "Food", "--note", new string('x', 201));
        Assert.Equal(1, r.Code);
        Assert.Contains("Note too long", r.Err, StringComparison.Ordinal);
    }

    [Fact]
    public void AddAcceptsNoteOfExactly200FR005()
    {
        var r = new Harness().Run("add", "--amount", "5", "--category", "Food", "--note", new string('x', 200));
        Assert.Equal(0, r.Code);
    }

    [Fact]
    public void AddMissingRequiredOptionUS25()
    {
        var r = new Harness().Run("add", "--category", "Food");
        Assert.Equal(1, r.Code);
        Assert.Contains("Missing required option", r.Err, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("2026-13")]
    [InlineData("2026-1")]
    [InlineData("nov")]
    public void ListRejectsInvalidMonthEdgeCase(string month)
    {
        var r = new Harness().Run("list", "--month", month);
        Assert.Equal(1, r.Code);
        Assert.Contains("Invalid month", r.Err, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("nothing")]
    [InlineData("")]
    public void UnknownCommandOrMissingExitsWithValidationCodeFR012(string command)
    {
        var args = command.Length == 0 ? [] : new[] { command };
        Assert.Equal(1, new Harness().Run(args).Code);
    }

    [Fact]
    public void UnknownOptionAndMissingValueAreRejectedFR012()
    {
        var h = new Harness();
        Assert.Equal(1, h.Run("list", "--bogus", "1").Code);
        Assert.Equal(1, h.Run("list", "--month").Code);
    }

    [Fact]
    public void DeleteRejectsNonNumericIdFR007()
    {
        var r = new Harness().Run("delete", "--id", "abc");
        Assert.Equal(1, r.Code);
        Assert.Contains("Invalid id", r.Err, StringComparison.Ordinal);
    }
}
