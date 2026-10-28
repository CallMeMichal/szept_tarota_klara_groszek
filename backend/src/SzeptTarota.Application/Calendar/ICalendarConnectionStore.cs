namespace SzeptTarota.Application.Calendar;

/// <summary>Jedno połączenie strony z Google Calendar Klary; refresh token leży w bazie zaszyfrowany.</summary>
public interface ICalendarConnectionStore
{
    /// <summary>Zapisane połączenie albo <c>null</c>, gdy Klara jeszcze nigdy się nie połączyła.</summary>
    Task<CalendarConnection?> FindAsync(CancellationToken cancellationToken);

    /// <summary>Zapisuje dostęp z nowej zgody Klary w miejsce poprzedniego; wybrany Kalendarz Sesji zostaje.</summary>
    Task SaveAccessAsync(string refreshToken, DateTimeOffset connectedAt, CancellationToken cancellationToken);

    /// <summary>Zapisuje Kalendarz Sesji (z blokami „Dostępna”); połączenie musi już istnieć.</summary>
    Task SaveSessionCalendarAsync(string calendarId, CancellationToken cancellationToken);
}

/// <param name="RefreshToken">
/// Odszyfrowany refresh token; <c>null</c>, gdy nie da się go odszyfrować (np. po utracie kluczy Data Protection),
/// czyli połączenie nie działa.
/// </param>
/// <param name="SessionCalendarId">Kalendarz z blokami „Dostępna” (Kalendarz Sesji), o ile Klara go wybrała.</param>
public sealed record CalendarConnection(string? RefreshToken, string? SessionCalendarId, DateTimeOffset ConnectedAt);
