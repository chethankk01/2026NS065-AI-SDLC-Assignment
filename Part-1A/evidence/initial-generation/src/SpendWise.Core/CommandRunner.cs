namespace SpendWise.Core;

public sealed class CommandRunner
{
    public const int ExitOk = 0;
    public const int ExitValidation = 1;
    public const int ExitStorage = 2;

    private readonly ExpenseService _service;
    private readonly Dictionary<string, Func<ParsedArgs, string>> _handlers;

    public CommandRunner(IExpenseStore store, IClock clock)
    {
        _service = new ExpenseService(store, clock);
        _handlers = new Dictionary<string, Func<ParsedArgs, string>>(StringComparer.Ordinal)
        {
            ["add"] = HandleAdd,
            ["list"] = HandleList,
            ["delete"] = HandleDelete,
            ["budget set"] = HandleBudgetSet,
            ["summary"] = HandleSummary,
        };
    }

    public int Run(IReadOnlyList<string> args, TextWriter stdout, TextWriter stderr)
    {
        try
        {
            var parsed = ArgumentParser.Parse(args);
            if (!_handlers.TryGetValue(parsed.Command, out var handler))
            {
                throw new ValidationException($"Unknown command {parsed.Command}");
            }

            stdout.WriteLine(handler(parsed));
            return ExitOk;
        }
        catch (ValidationException ex)
        {
            stderr.WriteLine(ex.Message);
            return ExitValidation;
        }
        catch (StorageException ex)
        {
            stderr.WriteLine($"Storage error: {ex.Message}");
            return ExitStorage;
        }
    }

    private string HandleAdd(ParsedArgs a) =>
        OutputFormatter.Added(_service.Add(a.Require("amount"), a.Require("category"), a.Get("date"), a.Get("note")));

    private string HandleList(ParsedArgs a)
    {
        var expenses = _service.List(a.Get("category"), a.Get("month"));
        return a.Json ? OutputFormatter.ListJson(expenses) : OutputFormatter.ListText(expenses);
    }

    private string HandleDelete(ParsedArgs a) => $"Deleted expense #{_service.Delete(a.Require("id"))}";

    private string HandleBudgetSet(ParsedArgs a) =>
        OutputFormatter.BudgetSet(_service.SetBudget(a.Require("category"), a.Require("month"), a.Require("limit")));

    private string HandleSummary(ParsedArgs a)
    {
        var summary = _service.Summarize(a.Require("month"));
        if (a.Json)
        {
            return OutputFormatter.SummaryJson(summary);
        }

        var spentRows = summary.Rows.Any(r => r.Spent > 0m);
        return OutputFormatter.SummaryText(summary, spentRows);
    }
}
