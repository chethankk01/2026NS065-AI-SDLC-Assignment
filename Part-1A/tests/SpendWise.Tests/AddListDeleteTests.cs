using SpendWise.Core;

namespace SpendWise.Tests;

// Spec: User Story 1 (FR-001, FR-006), User Story 3 (FR-007)
public class AddListDeleteTests
{
    [Fact]
    public void AddStoresExpenseAndPrintsConfirmationUS11()
    {
        var h = new Harness();
        var r = h.Run("add", "--amount", "12.50", "--category", "food", "--date", "2026-11-10", "--note", "Lunch");

        Assert.Equal(0, r.Code);
        Assert.Equal("Added expense #1: 12.50 Food 2026-11-10", r.Out);
        var e = Assert.Single(h.Store.Data.Expenses);
        Assert.Equal((1, 12.50m, Category.Food, "Lunch"), (e.Id, e.Amount, e.Category, e.Note));
    }

    [Fact]
    public void AddDefaultsDateToTodayFR004()
    {
        var h = new Harness();
        h.Run("add", "--amount", "5", "--category", "Health");
        Assert.Equal(Harness.Today, h.Store.Data.Expenses[0].Date);
    }

    [Fact]
    public void ListOrdersByDateThenIdUS12()
    {
        var h = new Harness();
        h.Add("1.00", "Food", "2026-11-12");
        h.Add("2.00", "Food", "2026-11-03");
        h.Add("3.00", "Food", "2026-11-03");

        var lines = h.Run("list").Out.Split('\n', StringSplitOptions.TrimEntries);
        Assert.StartsWith("#2", lines[0], StringComparison.Ordinal);
        Assert.StartsWith("#3", lines[1], StringComparison.Ordinal);
        Assert.StartsWith("#1", lines[2], StringComparison.Ordinal);
    }

    [Fact]
    public void ListFiltersByCategoryUS13()
    {
        var h = new Harness();
        h.Add("1.00", "Food", "2026-11-01");
        h.Add("900.00", "Rent", "2026-11-01");

        var r = h.Run("list", "--category", "rent");
        Assert.Contains("Rent", r.Out, StringComparison.Ordinal);
        Assert.DoesNotContain("Food", r.Out, StringComparison.Ordinal);
    }

    [Fact]
    public void ListFiltersByMonthUS14()
    {
        var h = new Harness();
        h.Add("1.00", "Food", "2026-10-31");
        h.Add("2.00", "Food", "2026-11-01");

        var r = h.Run("list", "--month", "2026-11");
        Assert.Contains("2026-11-01", r.Out, StringComparison.Ordinal);
        Assert.DoesNotContain("2026-10-31", r.Out, StringComparison.Ordinal);
    }

    [Fact]
    public void ListCombinesFiltersFR006()
    {
        var h = new Harness();
        h.Add("1.00", "Food", "2026-11-01");
        h.Add("2.00", "Rent", "2026-11-02");
        h.Add("3.00", "Food", "2026-10-02");

        var r = h.Run("list", "--category", "Food", "--month", "2026-11");
        Assert.Single(r.Out.Split('\n'));
        Assert.Contains("2026-11-01", r.Out, StringComparison.Ordinal);
    }

    [Fact]
    public void ListEmptyPrintsNoExpensesFoundUS15()
    {
        var r = new Harness().Run("list");
        Assert.Equal((0, "No expenses found."), (r.Code, r.Out));
    }

    [Fact]
    public void DeleteRemovesExpenseUS31()
    {
        var h = new Harness();
        h.Add("1.00", "Food", "2026-11-01");
        h.Add("2.00", "Food", "2026-11-02");

        var r = h.Run("delete", "--id", "1");
        Assert.Equal((0, "Deleted expense #1"), (r.Code, r.Out));
        Assert.Equal(2, Assert.Single(h.Store.Data.Expenses).Id);
    }

    [Fact]
    public void DeleteNeverReusesIdsUS32()
    {
        var h = new Harness();
        h.Add("1.00", "Food", "2026-11-01");
        h.Add("2.00", "Food", "2026-11-02");
        h.Run("delete", "--id", "2");
        h.Add("3.00", "Food", "2026-11-03");

        Assert.Equal(3, h.Store.Data.Expenses[^1].Id);
    }

    [Fact]
    public void DeleteUnknownIdFailsUS33()
    {
        var r = new Harness().Run("delete", "--id", "99");
        Assert.Equal(1, r.Code);
        Assert.Contains("Expense not found", r.Err, StringComparison.Ordinal);
    }

    [Fact]
    public void NoteIsStoredVerbatimEdgeCase()
    {
        var h = new Harness();
        h.Add("1.00", "Food", "2026-11-01", "=SUM(A1) +x @y -z");
        Assert.Equal("=SUM(A1) +x @y -z", h.Store.Data.Expenses[0].Note);
    }
}
