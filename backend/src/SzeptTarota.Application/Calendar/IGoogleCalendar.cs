namespace SzeptTarota.Application.Calendar;

/// <summary>
/// Google Calendar Klary (ADR 0002): zgoda na dostęp przez OAuth 2.0 i odczyt jej kalendarzy. Dostęp trzyma
/// refresh token, który strona dostaje raz, przy zgodzie, i z którym potem sama pyta Google.
/// </summary>
public interface IGoogleCalendar
{
    /// <summary>
    /// Strona Google, na której Klara zgadza się na dostęp do kalendarzy; Google odsyła potem przeglądarkę na
    /// <paramref name="redirectUri"/> z kodem i tym samym <paramref name="state"/>.
    /// </summary>
    /// <exception cref="GoogleCalendarNotConfiguredException">Gdy brakuje kluczy klienta OAuth.</exception>
    Uri ConsentUrl(Uri redirectUri, string state);

    /// <summary>Wymienia kod z powrotu ze zgody na refresh token.</summary>
    /// <exception cref="GoogleCalendarAuthorizationException">
    /// Gdy Google nie wyda refresh tokenu za ten kod albo Klara odznaczyła w zgodzie któryś z potrzebnych dostępów.
    /// </exception>
    /// <exception cref="GoogleCalendarNotConfiguredException">Gdy brakuje kluczy klienta OAuth.</exception>
    Task<string> ExchangeCodeAsync(string code, Uri redirectUri, CancellationToken cancellationToken);

    /// <summary>Kalendarze, które Klara widzi na swoim koncie Google.</summary>
    /// <exception cref="GoogleCalendarAccessLostException">Gdy refresh token już nie działa (cofnięta zgoda, wygasł).</exception>
    /// <exception cref="GoogleCalendarNotConfiguredException">Gdy brakuje kluczy klienta OAuth.</exception>
    Task<IReadOnlyList<GoogleCalendarInfo>> ListCalendarsAsync(string refreshToken, CancellationToken cancellationToken);
}

/// <summary>Kalendarz na koncie Google Klary; <see cref="Primary"/> to jej główny kalendarz.</summary>
public sealed record GoogleCalendarInfo(string Id, string Name, bool Primary);

public sealed class GoogleCalendarNotConfiguredException()
    : InvalidOperationException("Brak konfiguracji 'Google:ClientId' albo 'Google:ClientSecret'.");

/// <summary>Google nie dał dostępu do kalendarzy (np. zły albo zużyty kod ze zgody).</summary>
public sealed class GoogleCalendarAuthorizationException(string message) : Exception(message);

/// <summary>Zapisany dostęp do kalendarzy przestał działać i Klara musi połączyć się ponownie.</summary>
public sealed class GoogleCalendarAccessLostException(string message) : Exception(message);
