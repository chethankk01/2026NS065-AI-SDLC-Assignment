using System.Globalization;
using System.Text;
using System.Text.Json;

namespace SpendWise.Core;

public static class OutputFormatter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    public static string Money(decimal value) => value.ToString("F2", CultureInfo.InvariantCulture);

    public static string StatusText(BudgetStatus status) => status switch
    {
        BudgetStatus.Ok => "OK",
        BudgetStatus.Warning => "WARNING",
        BudgetStatus.Exceeded => "EXCEEDED",
        _ => "NO BUDGET",
    };

    public static string Added(Expense e) =>
        $"Added expense #{e.Id}: {Money(e.Amount)} {e.Category} {e.Date:yyyy-MM-dd}";

    public static string BudgetSet(Budget b) => $"Budget set: {b.Category} {b.Month} {Money(b.Limit)}";

    public static string ListText(IReadOnlyList<Expense> expenses)
    {
        if (expenses.Count == 0)
        {
            return "No expenses found.";
        }

        var sb = new StringBuilder();
        foreach (var e in expenses)
        {
            sb.Append(CultureInfo.InvariantCulture, $"#{e.Id}  {e.Date:yyyy-MM-dd}  {e.Category,-13}  {Money(e.Amount),12}  {e.Note}").AppendLine();
        }

        return sb.ToString().TrimEnd();
    }

    public static string ListJson(IReadOnlyList<Expense> expenses) =>
        JsonSerializer.Serialize(
            expenses.Select(e => new
            {
                id = e.Id,
                amount = e.Amount,
                category = e.Category.ToString(),
                date = e.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                note = e.Note,
            }),
            JsonOptions);

    public static string SummaryText(MonthlySummary summary, bool hasExpenses)
    {
        if (!hasExpenses)
        {
            return $"No expenses for {summary.Month}.";
        }

        var sb = new StringBuilder();
        sb.AppendLine(CultureInfo.InvariantCulture, $"Summary {summary.Month}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"{"Category",-14}{"Spent",12}{"Budget",12}{"Percent",10}  Status");
        foreach (var r in summary.Rows)
        {
            var budget = r.Limit is null ? "-" : Money(r.Limit.Value);
            var percent = r.Percent is null ? "-" : Money(r.Percent.Value) + "%";
            sb.AppendLine(CultureInfo.InvariantCulture, $"{r.Category,-14}{Money(r.Spent),12}{budget,12}{percent,10}  {StatusText(r.Status)}");
        }

        sb.Append(CultureInfo.InvariantCulture, $"Total: {Money(summary.Total)}");
        return sb.ToString();
    }

    public static string SummaryJson(MonthlySummary summary) =>
        JsonSerializer.Serialize(
            new
            {
                month = summary.Month,
                rows = summary.Rows.Select(r => new
                {
                    category = r.Category.ToString(),
                    spent = r.Spent,
                    budget = r.Limit,
                    percent = r.Percent,
                    status = StatusText(r.Status),
                }),
                total = summary.Total,
            },
            JsonOptions);
}
