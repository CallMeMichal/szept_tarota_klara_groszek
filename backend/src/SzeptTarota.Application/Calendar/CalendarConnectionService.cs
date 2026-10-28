using Microsoft.Extensions.Options;

namespace SzeptTarota.Application.Calendar;

/// <summary>
/// Połączenie strony z Google Calendar Klary w panelu (ADR 0002): Klara zgadza się w Google na dostęp, strona
/// zapisuje refresh token, a Klara wybiera kalendarz z blokami „Dostępna” (Kalendarz Sesji). Jej główny kalendarz
/// jest zawsze odczytywany jako zajętość, więc nie może być kalendarzem Sesji.
/// </summary>
public sealed class CalendarConnectionService(
    IGoogleCalendar google,
    ICalendarConnectionStore store,
    IOptions<ShopOptions> shop,
    TimeProvider time)
{
    /// <summary>Ścieżka API, na którą Google odsyła przeglądarkę po zgodzie Klary.</summary>
    public const string CallbackPath = "/api/panel/calendar/callback";

    /// <summary>Strona Google ze zgodą na dostęp do kalendarzy.</summary>
    /// <exception cref="GoogleCalendarNotConfiguredException">Gdy brakuje kluczy klienta OAuth.</exception>
    /// <exception cref="ShopNotConfiguredException">Gdy brakuje <c>Shop:SiteUrl</c>.</exception>
    public Uri ConsentUrl(string state) => google.ConsentUrl(RedirectUri(), state);

    /// <summary>Zapisuje dostęp za kod z powrotu ze zgody; <c>false</c>, gdy Google go nie dał.</summary>
    public async Task<bool> ConnectAsync(string code, CancellationToken cancellationToken)
    {
        string refreshToken;
        try
        {
            refreshToken = await google.ExchangeCodeAsync(code, RedirectUri(), cancellationToken);
        }
        catch (Exception e) when (e is GoogleCalendarAuthorizationException or GoogleCalendarNotConfiguredException
                                       or ShopNotConfiguredException or HttpRequestException)
        {
            return false;
        }

        await store.SaveAccessAsync(refreshToken, time.GetUtcNow(), cancellationToken);
        return true;
    }

    /// <summary>Stan połączenia sprawdzony na żywo w Google, z kalendarzami Klary, gdy działa.</summary>
    public async Task<CalendarConnectionStatus> GetStatusAsync(CancellationToken cancellationToken)
    {
        if (await store.FindAsync(cancellationToken) is not { } connection)
        {
            return new CalendarConnectionStatus(CalendarConnectionState.NotConnected, [], null);
        }

        if (await ListCalendarsOrNullAsync(connection, cancellationToken) is not { } calendars)
        {
            return new CalendarConnectionStatus(CalendarConnectionState.Broken, [], connection.SessionCalendarId);
        }

        // Kalendarz Sesji mógł zniknąć (usunięty albo ponowne połączenie innym kontem Google); wtedy trzeba wybrać nowy.
        var sessionCalendarId = calendars.Any(c => c.Id == connection.SessionCalendarId) ? connection.SessionCalendarId : null;
        return new CalendarConnectionStatus(CalendarConnectionState.Connected, calendars, sessionCalendarId);
    }

    /// <summary>
    /// Ustawia Kalendarz Sesji; zwraca błąd do pokazania albo <c>null</c>. Musi to być kalendarz z konta Google Klary
    /// inny niż główny.
    /// </summary>
    public async Task<string?> ChooseSessionCalendarAsync(string? calendarId, CancellationToken cancellationToken)
    {
        if (await store.FindAsync(cancellationToken) is not { } connection
            || await ListCalendarsOrNullAsync(connection, cancellationToken) is not { } calendars)
        {
            return "Najpierw połącz stronę z Google Calendar.";
        }

        if (calendars.SingleOrDefault(c => c.Id == calendarId) is not { } calendar)
        {
            return "Nie ma takiego kalendarza na Twoim koncie Google.";
        }

        if (calendar.Primary)
        {
            return "Główny kalendarz jest odczytywany jako zajętość; wybierz osobny kalendarz z blokami „Dostępna”.";
        }

        await store.SaveSessionCalendarAsync(calendar.Id, cancellationToken);
        return null;
    }

    private async Task<IReadOnlyList<GoogleCalendarInfo>?> ListCalendarsOrNullAsync(
        CalendarConnection connection, CancellationToken cancellationToken)
    {
        if (connection.RefreshToken is null)
        {
            return null;
        }

        try
        {
            return await google.ListCalendarsAsync(connection.RefreshToken, cancellationToken);
        }
        catch (Exception e) when (e is GoogleCalendarAccessLostException or GoogleCalendarNotConfiguredException)
        {
            return null;
        }
    }

    private Uri RedirectUri() =>
        Uri.TryCreate(shop.Value.SiteUrl, UriKind.Absolute, out var site)
            ? new Uri(site, CallbackPath)
            : throw new ShopNotConfiguredException();
}

public enum CalendarConnectionState
{
    /// <summary>Klara jeszcze nigdy nie połączyła strony z kalendarzem.</summary>
    NotConnected,

    Connected,

    /// <summary>Zapisany dostęp przestał działać (cofnięta zgoda, wygasły token); trzeba połączyć ponownie.</summary>
    Broken,
}

/// <param name="Calendars">Kalendarze Klary; puste, gdy połączenie nie działa.</param>
/// <param name="SessionCalendarId">Wybrany Kalendarz Sesji, o ile nadal jest na koncie Google Klary.</param>
public sealed record CalendarConnectionStatus(
    CalendarConnectionState State,
    IReadOnlyList<GoogleCalendarInfo> Calendars,
    string? SessionCalendarId);
