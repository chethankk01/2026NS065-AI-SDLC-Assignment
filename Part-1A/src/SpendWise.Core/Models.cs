namespace SpendWise.Core;

public enum Category
{
    Food,
    Transport,
    Rent,
    Utilities,
    Entertainment,
    Health,
    Other,
}

public enum BudgetStatus
{
    Ok,
    Warning,
    Exceeded,
    NoBudget,
}

public sealed record Expense(int Id, decimal Amount, Category Category, DateOnly Date, string Note);

public sealed record Budget(Category Category, string Month, decimal Limit);

public sealed class StoreData
{
    public int NextId { get; set; } = 1;

    public List<Expense> Expenses { get; set; } = [];

    public List<Budget> Budgets { get; set; } = [];
}

public sealed record SummaryRow(Category Category, decimal Spent, decimal? Limit, decimal? Percent, BudgetStatus Status);

public sealed record MonthlySummary(string Month, IReadOnlyList<SummaryRow> Rows, decimal Total);
