using System.Globalization;

namespace SpendWise.Core;

public static class InputValidator
{
    public const decimal MaxAmount = 1_000_000.00m;
    public const int MaxNoteLength = 200;
    public static readonly DateOnly MinDate = new(2000, 1, 1);

    public static decimal ParseAmount(string? text)
    {
        if (text is null
            || !decimal.TryParse(text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value)
            || DecimalScale(value) > 2
            || value < 0.01m
            || value > MaxAmount)
        {
            throw new ValidationException("Invalid amount");
        }

        return value;
    }

    public static Category ParseCategory(string? text)
    {
        foreach (var candidate in Enum.GetValues<Category>())
        {
            if (string.Equals(candidate.ToString(), text, StringComparison.OrdinalIgnoreCase))
            {
                return candidate;
            }
        }

        throw new ValidationException("Unknown category");
    }

    public static DateOnly ParseDate(string? text, DateOnly today)
    {
        if (text is null
            || !DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            || date < MinDate
            || date > today)
        {
            throw new ValidationException("Invalid date");
        }

        return date;
    }

    public static string ParseMonth(string? text)
    {
        if (text is null
            || !DateOnly.TryParseExact(text + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
        {
            throw new ValidationException("Invalid month");
        }

        return text;
    }

    public static string ParseNote(string? text)
    {
        if (text is not null && text.Length > MaxNoteLength)
        {
            throw new ValidationException("Note too long");
        }

        return text ?? string.Empty;
    }

    public static int ParseId(string? text)
    {
        if (text is null || !int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var id))
        {
            throw new ValidationException("Invalid id");
        }

        return id;
    }

    private static int DecimalScale(decimal value) => (decimal.GetBits(value)[3] >> 16) & 0xFF;
}
