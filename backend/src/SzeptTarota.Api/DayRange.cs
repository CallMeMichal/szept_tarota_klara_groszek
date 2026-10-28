using System.Globalization;

namespace SzeptTarota.Api;

/// <summary>Zakres dni z zapytania: <c>from</c> i <c>to</c> (RRRR-MM-DD, włącznie) to dni w czasie polskim.</summary>
public static class DayRange
{
    /// <summary>
    /// Najdłuższy zakres w jednym zapytaniu (kalendarz pokazuje miesiąc); ogranicza tylko wielkość zapytania do Google,
    /// nie to, na ile do przodu Klara przyjmuje Terminy.
    /// </summary>
    public const int MaxDays = 62;

    /// <summary>Dni z zapytania albo błędy po nazwach parametrów.</summary>
    public static (DateOnly From, DateOnly To)? Parse(string? from, string? to, out Dictionary<string, string[]> errors)
    {
        errors = [];
        var fromDay = ParseDay(from, "from", errors);
        var toDay = ParseDay(to, "to", errors);
        if (fromDay > toDay || toDay?.DayNumber - fromDay?.DayNumber >= MaxDays)
        {
            errors["to"] = [$"Zakres może mieć od 1 do {MaxDays} dni."];
        }

        return fromDay is { } f && toDay is { } t && errors.Count == 0 ? (f, t) : null;
    }

    private static DateOnly? ParseDay(string? value, string key, Dictionary<string, string[]> errors)
    {
        if (DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day))
        {
            return day;
        }

        errors[key] = ["Podaj datę w formacie RRRR-MM-DD."];
        return null;
    }
}
