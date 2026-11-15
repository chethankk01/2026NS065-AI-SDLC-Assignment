using System.Text.Json;

namespace SpendWise.Tests;

// Spec: User Story 4 (FR-008..FR-011, SC-003)
public class BudgetSummaryTests
{
    private static Harness WithFoodBudget(string limit = "200.00")
    {
        var h = new Harness();
        Assert.Equal(0, h.Run("budget", "set", "--category", "food", "--month", "2026-11", "--limit", limit).Code);
        return h;
    }

    [Fact]
    public void BudgetSet_PrintsAndOverwrites_US4_1()
    {
        var h = new Harness();
        var first = h.Run("budget", "set", "--category", "food", "--month", "2026-11", "--limit", "200.00");
        h.Run("budget", "set", "--category", "Food", "--month", "2026-11", "--limit", "300.00");

        Assert.Equal("Budget set: Food 2026-11 200.00", first.Out);
        Assert.Equal(300.00m, Assert.Single(h.Store.Data.Budgets).Limit);
    }

    [Fact]
    public void Summary_ShowsSpentPercentAndStatus_US4_2()
    {
        var h = WithFoodBudget();
        h.Add("50.00", "Food", "2026-11-02");
        h.Add("30.25", "Food", "2026-11-03");

        var r = h.Run("summary", "--month", "2026-11");
        Assert.Equal(0, r.Code);
        Assert.Contains("80.25", r.Out, StringComparison.Ordinal);
        Assert.Contains("40.13%", r.Out, StringComparison.Ordinal);
        Assert.Contains("OK", r.Out, StringComparison.Ordinal);
        Assert.Contains("Total: 80.25", r.Out, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("159.99", "OK")]
    [InlineData("160.00", "WARNING")]
    [InlineData("200.00", "WARNING")]
    [InlineData("200.01", "EXCEEDED")]
    public void Summary_StatusBoundaries_US4_3_SC003(string spent, string status)
    {
        var h = WithFoodBudget();
        h.Add(spent, "Food", "2026-11-02");

        using var doc = JsonDocument.Parse(h.Run("summary", "--month", "2026-11", "--json").Out);
        Assert.Equal(status, doc.RootElement.GetProperty("rows")[0].GetProperty("status").GetString());
    }

    [Fact]
    public void Summary_StatusUsesExactRatioNotRoundedPercent_FR010()
    {
        // 159.999.. cannot be entered, but 15.9999 of limit 20.00 style rounding is avoided:
        // 79.995 percent would round to 80.00; use limit 2000.00 and spent 1599.90 => 79.995%.
        var h = WithFoodBudget("2000.00");
        h.Add("1599.90", "Food", "2026-11-02");

        var r = h.Run("summary", "--month", "2026-11");
        Assert.Contains("80.00%", r.Out, StringComparison.Ordinal);
        Assert.Contains("OK", r.Out, StringComparison.Ordinal);
    }

    [Fact]
    public void Summary_NoBudgetRow_US4_4()
    {
        var h = new Harness();
        h.Add("10.00", "Rent", "2026-11-02");

        var r = h.Run("summary", "--month", "2026-11");
        Assert.Contains("NO BUDGET", r.Out, StringComparison.Ordinal);
        Assert.Contains("-", r.Out, StringComparison.Ordinal);
    }

    [Fact]
    public void Summary_EmptyMonth_US4_5()
    {
        var h = WithFoodBudget();
        var r = h.Run("summary", "--month", "2026-11");
        Assert.Equal((0, "No expenses for 2026-11."), (r.Code, r.Out));
    }

    [Fact]
    public void Summary_TotalIsExactDecimalSumAndRowsAlphabetical_US4_6()
    {
        var h = new Harness();
        h.Add("0.10", "Transport", "2026-11-01");
        h.Add("0.20", "Food", "2026-11-01");

        using var doc = JsonDocument.Parse(h.Run("summary", "--month", "2026-11", "--json").Out);
        Assert.Equal(0.30m, doc.RootElement.GetProperty("total").GetDecimal());
        Assert.Equal("Food", doc.RootElement.GetProperty("rows")[0].GetProperty("category").GetString());
        Assert.Equal("Transport", doc.RootElement.GetProperty("rows")[1].GetProperty("category").GetString());
    }

    [Fact]
    public void Summary_BudgetOnlyCategoryAppearsWithZeroSpent_ChangeHistory()
    {
        var h = WithFoodBudget();
        h.Add("10.00", "Rent", "2026-11-02");

        var r = h.Run("summary", "--month", "2026-11");
        Assert.Contains("Food", r.Out, StringComparison.Ordinal);
        Assert.Contains("0.00", r.Out, StringComparison.Ordinal);
    }

    [Fact]
    public void Summary_JsonIsValidAndStable_US4_7()
    {
        var h = WithFoodBudget();
        h.Add("50.00", "Food", "2026-11-02");

        var first = h.Run("summary", "--month", "2026-11", "--json").Out;
        var second = h.Run("summary", "--month", "2026-11", "--json").Out;
        using var doc = JsonDocument.Parse(first);

        Assert.Equal(first, second);
        Assert.Equal("2026-11", doc.RootElement.GetProperty("month").GetString());
        Assert.Equal(25.00m, doc.RootElement.GetProperty("rows")[0].GetProperty("percent").GetDecimal());
    }

    [Fact]
    public void ListJson_IsValid_FR011()
    {
        var h = new Harness();
        h.Add("12.50", "Food", "2026-11-01", "Lunch");

        using var doc = JsonDocument.Parse(h.Run("list", "--json").Out);
        var item = doc.RootElement[0];
        Assert.Equal(12.50m, item.GetProperty("amount").GetDecimal());
        Assert.Equal("Food", item.GetProperty("category").GetString());
    }

    [Fact]
    public void BudgetSet_ValidatesInputs_FR008()
    {
        var h = new Harness();
        Assert.Contains("Invalid amount", h.Run("budget", "set", "--category", "Food", "--month", "2026-11", "--limit", "0").Err, StringComparison.Ordinal);
        Assert.Contains("Invalid month", h.Run("budget", "set", "--category", "Food", "--month", "x", "--limit", "5").Err, StringComparison.Ordinal);
        Assert.Contains("Missing required option", h.Run("budget", "set", "--category", "Food").Err, StringComparison.Ordinal);
    }
}
