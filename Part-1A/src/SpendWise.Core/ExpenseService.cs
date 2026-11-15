using System.Globalization;

namespace SpendWise.Core;

public sealed class ExpenseService(IExpenseStore store, IClock clock)
{
    public Expense Add(string? amount, string? category, string? date, string? note)
    {
        var parsedAmount = InputValidator.ParseAmount(amount);
        var parsedCategory = InputValidator.ParseCategory(category);
        var parsedDate = date is null ? clock.Today : InputValidator.ParseDate(date, clock.Today);
        var parsedNote = InputValidator.ParseNote(note);

        var data = store.Load();
        var expense = new Expense(data.NextId, parsedAmount, parsedCategory, parsedDate, parsedNote);
        data.Expenses.Add(expense);
        data.NextId++;
        store.Save(data);
        return expense;
    }

    public IReadOnlyList<Expense> List(string? category, string? month)
    {
        Category? categoryFilter = category is null ? null : InputValidator.ParseCategory(category);
        var monthFilter = month is null ? null : InputValidator.ParseMonth(month);

        return store.Load().Expenses
            .Where(e => categoryFilter is null || e.Category == categoryFilter)
            .Where(e => monthFilter is null || MonthOf(e.Date) == monthFilter)
            .OrderBy(e => e.Date)
            .ThenBy(e => e.Id)
            .ToList();
    }

    public int Delete(string? id)
    {
        var parsedId = InputValidator.ParseId(id);
        var data = store.Load();
        if (data.Expenses.RemoveAll(e => e.Id == parsedId) == 0)
        {
            throw new ValidationException("Expense not found");
        }

        store.Save(data);
        return parsedId;
    }

    public Budget SetBudget(string? category, string? month, string? limit)
    {
        var budget = new Budget(
            InputValidator.ParseCategory(category),
            InputValidator.ParseMonth(month),
            InputValidator.ParseAmount(limit));

        var data = store.Load();
        data.Budgets.RemoveAll(b => b.Category == budget.Category && b.Month == budget.Month);
        data.Budgets.Add(budget);
        store.Save(data);
        return budget;
    }

    public MonthlySummary Summarize(string? month)
    {
        var parsedMonth = InputValidator.ParseMonth(month);
        var data = store.Load();
        var expenses = data.Expenses.Where(e => MonthOf(e.Date) == parsedMonth).ToList();
        var budgets = data.Budgets.Where(b => b.Month == parsedMonth).ToList();

        var categories = expenses.Select(e => e.Category)
            .Union(budgets.Select(b => b.Category))
            .OrderBy(c => c.ToString(), StringComparer.Ordinal);

        var rows = categories
            .Select(c => BuildRow(c, expenses.Where(e => e.Category == c).Sum(e => e.Amount), budgets.FirstOrDefault(b => b.Category == c)))
            .ToList();

        return new MonthlySummary(parsedMonth, rows, expenses.Sum(e => e.Amount));
    }

    internal static SummaryRow BuildRow(Category category, decimal spent, Budget? budget)
    {
        if (budget is null)
        {
            return new SummaryRow(category, spent, null, null, BudgetStatus.NoBudget);
        }

        var percent = Math.Round(spent / budget.Limit * 100m, 2, MidpointRounding.AwayFromZero);
        return new SummaryRow(category, spent, budget.Limit, percent, StatusFor(spent, budget.Limit));
    }

    internal static BudgetStatus StatusFor(decimal spent, decimal limit)
    {
        if (spent * 100m < limit * 80m)
        {
            return BudgetStatus.Ok;
        }

        return spent <= limit ? BudgetStatus.Warning : BudgetStatus.Exceeded;
    }

    private static string MonthOf(DateOnly date) => date.ToString("yyyy-MM", CultureInfo.InvariantCulture);
}
