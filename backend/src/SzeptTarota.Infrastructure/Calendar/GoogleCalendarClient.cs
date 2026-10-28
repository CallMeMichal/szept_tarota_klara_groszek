using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using SzeptTarota.Application.Calendar;

namespace SzeptTarota.Infrastructure.Calendar;

/// <summary>Klient OAuth Google z sekcji <c>Google</c>, ten sam co przy logowaniu przez Google.</summary>
public sealed class GoogleCalendarOptions
{
    public const string SectionName = "Google";

    public string? ClientId { get; set; }

    public string? ClientSecret { get; set; }
}

/// <summary>
/// Google Calendar przez OAuth 2.0 dla aplikacji webowych (https://developers.google.com/identity/protocols/oauth2/web-server)
/// i Calendar API v3. Za każdym razem bierze świeży access token z refresh tokenu, bo zapytań jest mało.
/// </summary>
public sealed class GoogleCalendarClient(HttpClient http, IOptions<GoogleCalendarOptions> options) : IGoogleCalendar
{
    private const string ConsentEndpoint = "https://accounts.google.com/o/oauth2/v2/auth";
    private const string TokenEndpoint = "https://oauth2.googleapis.com/token";
    private const string CalendarListEndpoint = "https://www.googleapis.com/calendar/v3/users/me/calendarList";
    private const string CalendarsEndpoint = "https://www.googleapis.com/calendar/v3/calendars";

    /// <summary>Wydarzenia całodniowe trwają od północy do północy w tej strefie.</summary>
    private static readonly TimeZoneInfo Warsaw = TimeZoneInfo.FindSystemTimeZoneById("Europe/Warsaw");

    /// <summary>
    /// Lista kalendarzy (wybór kalendarza Sesji) i wydarzenia we wszystkich kalendarzach: odczyt bloków „Dostępna”
    /// i zajętości, bloki „Dostępna” dodawane w panelu oraz zapis Sesji z linkiem Meet. Węziej niż pełny zakres <c>calendar</c>.
    /// </summary>
    private static readonly string[] Scopes =
    [
        "https://www.googleapis.com/auth/calendar.calendarlist.readonly",
        "https://www.googleapis.com/auth/calendar.events",
    ];

    public Uri ConsentUrl(Uri redirectUri, string state)
    {
        var (clientId, _) = Client();
        return new Uri(QueryHelpers.AddQueryString(ConsentEndpoint, new Dictionary<string, string?>
        {
            ["client_id"] = clientId,
            ["redirect_uri"] = redirectUri.ToString(),
            ["response_type"] = "code",
            ["scope"] = string.Join(' ', Scopes),
            ["state"] = state,
            // Refresh token, żeby strona mogła pytać kalendarz bez Klary.
            ["access_type"] = "offline",
            // Google wydaje refresh token tylko przy wyświetlonej zgodzie, więc pytamy zawsze (też przy ponownym połączeniu).
            ["prompt"] = "consent select_account",
        }));
    }

    public async Task<string> ExchangeCodeAsync(string code, Uri redirectUri, CancellationToken cancellationToken)
    {
        var (clientId, clientSecret) = Client();
        using var response = await http.PostAsync(TokenEndpoint, new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret,
            ["redirect_uri"] = redirectUri.ToString(),
        }), cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new GoogleCalendarAuthorizationException(
                $"Google nie wymienił kodu na token ({(int)response.StatusCode}): {await response.Content.ReadAsStringAsync(cancellationToken)}");
        }

        var token = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken);
        if (token?.RefreshToken is not { Length: > 0 } refreshToken)
        {
            throw new GoogleCalendarAuthorizationException("Google nie wydał refresh tokenu.");
        }

        // Przy zgodzie Klara może odznaczyć pojedyncze dostępy; bez któregoś strona nie zadziała później (Terminy, Sesje).
        var granted = (token.Scope ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return Scopes.All(granted.Contains)
            ? refreshToken
            : throw new GoogleCalendarAuthorizationException($"Klara nie dała wszystkich dostępów do kalendarza: {token.Scope}");
    }

    public async Task<IReadOnlyList<GoogleCalendarInfo>> ListCalendarsAsync(string refreshToken, CancellationToken cancellationToken)
    {
        var accessToken = await AccessTokenAsync(refreshToken, cancellationToken);
        var calendars = new List<GoogleCalendarInfo>();
        string? pageToken = null;
        do
        {
            var url = pageToken is null
                ? CalendarListEndpoint
                : QueryHelpers.AddQueryString(CalendarListEndpoint, "pageToken", pageToken);
            using var request = new HttpRequestMessage(HttpMethod.Get, url)
            {
                Headers = { Authorization = new AuthenticationHeaderValue("Bearer", accessToken) },
            };
            using var response = await http.SendAsync(request, cancellationToken);
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                throw new GoogleCalendarAccessLostException(
                    $"Google odmówił dostępu do kalendarzy ({(int)response.StatusCode}): {await response.Content.ReadAsStringAsync(cancellationToken)}");
            }

            response.EnsureSuccessStatusCode();
            var page = await response.Content.ReadFromJsonAsync<CalendarListResponse>(cancellationToken);
            calendars.AddRange((page?.Items ?? []).Select(c => new GoogleCalendarInfo(
                c.Id, c.SummaryOverride ?? c.Summary ?? c.Id, c.Primary, Writable: c.AccessRole is "owner" or "writer")));
            pageToken = page?.NextPageToken;
        }
        while (pageToken is not null);

        return calendars;
    }

    public async Task<IReadOnlyList<GoogleCalendarEvent>> ListEventsAsync(
        string refreshToken, string calendarId, DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken)
    {
        var accessToken = await AccessTokenAsync(refreshToken, cancellationToken);
        var events = new List<GoogleCalendarEvent>();
        string? pageToken = null;
        do
        {
            var query = new Dictionary<string, string?>
            {
                ["timeMin"] = from.ToUniversalTime().ToString("O"),
                ["timeMax"] = to.ToUniversalTime().ToString("O"),
                // Powtarzane wydarzenia rozwinięte w pojedyncze wystąpienia.
                ["singleEvents"] = "true",
                ["maxResults"] = "2500",
                ["pageToken"] = pageToken,
            };
            var url = QueryHelpers.AddQueryString(EventsUrl(calendarId), query.Where(q => q.Value is not null));
            using var request = new HttpRequestMessage(HttpMethod.Get, url)
            {
                Headers = { Authorization = new AuthenticationHeaderValue("Bearer", accessToken) },
            };
            using var response = await http.SendAsync(request, cancellationToken);
            await ThrowIfAccessLostAsync(response, cancellationToken);
            response.EnsureSuccessStatusCode();
            var page = await response.Content.ReadFromJsonAsync<EventsResponse>(cancellationToken);
            events.AddRange((page?.Items ?? []).Where(e => e.Status != "cancelled").Select(ToEvent).OfType<GoogleCalendarEvent>());
            pageToken = page?.NextPageToken;
        }
        while (pageToken is not null);

        return events;
    }

    private static GoogleCalendarEvent? ToEvent(EventEntry entry)
    {
        if (Time(entry.Start) is not { } start || Time(entry.End) is not { } end)
        {
            return null;
        }

        var declined = entry.Attendees?.Any(a => a.Self && a.ResponseStatus == "declined") ?? false;
        return new GoogleCalendarEvent(entry.Id, start, end, Busy: entry.Transparency != "transparent" && !declined);
    }

    public async Task<string> CreateFreeEventAsync(
        string refreshToken, string calendarId, string summary, DateTimeOffset start, DateTimeOffset end, CancellationToken cancellationToken)
    {
        var accessToken = await AccessTokenAsync(refreshToken, cancellationToken);
        using var request = new HttpRequestMessage(HttpMethod.Post, EventsUrl(calendarId))
        {
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", accessToken) },
            Content = JsonContent.Create(new
            {
                summary,
                start = new { dateTime = start.ToString("O") },
                end = new { dateTime = end.ToString("O") },
                // Blok „Dostępna” nie zajmuje czasu Klary i nie przypomina o sobie.
                transparency = "transparent",
                reminders = new { useDefault = false },
            }),
        };
        using var response = await http.SendAsync(request, cancellationToken);
        await ThrowIfAccessLostAsync(response, cancellationToken);
        response.EnsureSuccessStatusCode();
        var created = await response.Content.ReadFromJsonAsync<EventEntry>(cancellationToken);
        return created?.Id ?? throw new HttpRequestException("Google nie zwrócił identyfikatora wydarzenia.");
    }

    public async Task DeleteEventAsync(string refreshToken, string calendarId, string eventId, CancellationToken cancellationToken)
    {
        var accessToken = await AccessTokenAsync(refreshToken, cancellationToken);
        using var request = new HttpRequestMessage(HttpMethod.Delete, $"{EventsUrl(calendarId)}/{Uri.EscapeDataString(eventId)}")
        {
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", accessToken) },
        };
        using var response = await http.SendAsync(request, cancellationToken);
        // Usunięte już wcześniej (np. przez Klarę w Google): 404 albo 410.
        if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Gone)
        {
            return;
        }

        await ThrowIfAccessLostAsync(response, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private static string EventsUrl(string calendarId) => $"{CalendarsEndpoint}/{Uri.EscapeDataString(calendarId)}/events";

    private static async Task ThrowIfAccessLostAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            throw new GoogleCalendarAccessLostException(
                $"Google odmówił dostępu do kalendarza ({(int)response.StatusCode}): {await response.Content.ReadAsStringAsync(cancellationToken)}");
        }
    }

    /// <summary>Godzina wydarzenia albo północ czasu polskiego dla wydarzenia całodniowego.</summary>
    private static DateTimeOffset? Time(EventTime? time)
    {
        if (time?.DateTime is { } dateTime)
        {
            return dateTime;
        }

        if (time?.Date is { } date)
        {
            var midnight = date.ToDateTime(TimeOnly.MinValue);
            return new DateTimeOffset(midnight, Warsaw.GetUtcOffset(midnight));
        }

        return null;
    }

    private async Task<string> AccessTokenAsync(string refreshToken, CancellationToken cancellationToken)
    {
        var (clientId, clientSecret) = Client();
        using var response = await http.PostAsync(TokenEndpoint, new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken,
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret,
        }), cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            // invalid_grant: Klara cofnęła zgodę, zmieniła hasło albo token wygasł (aplikacja w trybie „Testing”: 7 dni).
            // Inne błędy (np. invalid_client po zmianie kluczy) to nie zerwane połączenie, tylko awaria po naszej stronie.
            throw body.Contains("\"invalid_grant\"", StringComparison.Ordinal)
                ? new GoogleCalendarAccessLostException($"Google nie odświeżył dostępu: {body}")
                : new HttpRequestException($"Google nie odświeżył dostępu ({(int)response.StatusCode}): {body}");
        }

        var token = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken);
        return token?.AccessToken ?? throw new HttpRequestException("Google nie zwrócił access tokenu.");
    }

    private (string ClientId, string ClientSecret) Client() =>
        options.Value is { ClientId: { Length: > 0 } clientId, ClientSecret: { Length: > 0 } clientSecret }
            ? (clientId, clientSecret)
            : throw new GoogleCalendarNotConfiguredException();

    private sealed record TokenResponse(
        [property: JsonPropertyName("access_token")] string? AccessToken,
        [property: JsonPropertyName("refresh_token")] string? RefreshToken,
        [property: JsonPropertyName("scope")] string? Scope);

    private sealed record CalendarListResponse(
        [property: JsonPropertyName("items")] List<CalendarListEntry>? Items,
        [property: JsonPropertyName("nextPageToken")] string? NextPageToken);

    private sealed record EventsResponse(
        [property: JsonPropertyName("items")] List<EventEntry>? Items,
        [property: JsonPropertyName("nextPageToken")] string? NextPageToken);

    private sealed record EventEntry(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("status")] string? Status,
        [property: JsonPropertyName("start")] EventTime? Start,
        [property: JsonPropertyName("end")] EventTime? End,
        [property: JsonPropertyName("transparency")] string? Transparency,
        [property: JsonPropertyName("attendees")] List<EventAttendee>? Attendees);

    private sealed record EventTime(
        [property: JsonPropertyName("dateTime")] DateTimeOffset? DateTime,
        [property: JsonPropertyName("date")] DateOnly? Date);

    private sealed record EventAttendee(
        [property: JsonPropertyName("self")] bool Self,
        [property: JsonPropertyName("responseStatus")] string? ResponseStatus);

    private sealed record CalendarListEntry(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("summary")] string? Summary,
        [property: JsonPropertyName("summaryOverride")] string? SummaryOverride,
        [property: JsonPropertyName("primary")] bool Primary,
        [property: JsonPropertyName("accessRole")] string? AccessRole);
}
