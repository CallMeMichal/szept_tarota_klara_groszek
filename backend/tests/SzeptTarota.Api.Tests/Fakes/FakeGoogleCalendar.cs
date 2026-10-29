using System.Collections.Concurrent;
using Microsoft.AspNetCore.WebUtilities;
using SzeptTarota.Application.Calendar;

namespace SzeptTarota.Api.Tests.Fakes;

/// <summary>
/// Udaje Google Calendar Klary. Test „zgadza się” w Google przez <see cref="GrantAccess"/> i dostaje kod, z którym
/// Google odesłałby przeglądarkę na stronę; za kod strona dostaje refresh token, a z nim listę <see cref="Calendars"/>.
/// <see cref="RevokeAccess"/> udaje cofnięcie zgody: wydane dotąd tokeny przestają działać. Wydarzenia leżą w pamięci
/// po kalendarzach; test dodaje je przez <see cref="AddAvailableBlock"/> i <see cref="AddMainCalendarEvent"/>, a strona
/// przez <see cref="CreateFreeEventAsync"/> i <see cref="CreateAppointmentEventAsync"/>; te drugie trafiają też do
/// <see cref="AppointmentEvents"/> z gościem, opisem i linkiem Meet.
/// </summary>
public sealed class FakeGoogleCalendar : IGoogleCalendar
{
    public static readonly GoogleCalendarInfo MainCalendar = new("klara@gmail.com", "Klara", Primary: true);

    public static readonly GoogleCalendarInfo SessionsCalendar = new("sesje@group.calendar.google.com", "Sesje", Primary: false);

    /// <summary>Subskrybowany kalendarz Google, w którym Klara nie może nic dodać.</summary>
    public static readonly GoogleCalendarInfo HolidaysCalendar =
        new("pl.polish#holiday@group.v.calendar.google.com", "Święta w Polsce", Primary: false, Writable: false);

    private readonly ConcurrentDictionary<string, string> _codes = new();
    private readonly ConcurrentDictionary<string, bool> _tokens = new();

    /// <summary>Wydarzenia po identyfikatorach kalendarzy.</summary>
    private readonly ConcurrentDictionary<string, List<GoogleCalendarEvent>> _events = new();

    private readonly List<CreatedAppointmentEvent> _appointmentEvents = [];

    /// <summary>Wydarzenia opłaconych Terminów w głównym kalendarzu Klary, od najwcześniej dodanego.</summary>
    public IReadOnlyList<CreatedAppointmentEvent> AppointmentEvents
    {
        get
        {
            lock (_appointmentEvents)
            {
                return [.. _appointmentEvents];
            }
        }
    }

    private int _appointmentEventCalls;

    /// <summary>Ile razy strona prosiła o dodanie wydarzenia Terminu, także już dodanego.</summary>
    public int AppointmentEventCalls => _appointmentEventCalls;

    /// <summary>Jak długo Google dodaje wydarzenie Terminu; równoczesne prośby mogą się wtedy minąć.</summary>
    public TimeSpan AppointmentEventDelay { get; set; }

    /// <summary>Kalendarze na koncie Google Klary.</summary>
    public List<GoogleCalendarInfo> Calendars { get; } = [MainCalendar, SessionsCalendar, HolidaysCalendar];

    /// <summary>Refresh tokeny wydane stronie, od najstarszego.</summary>
    public List<string> IssuedTokens { get; } = [];

    /// <summary>Kod, z jakim Google odsyła przeglądarkę po zgodzie Klary na dostęp do kalendarzy.</summary>
    public string GrantAccess()
    {
        var code = $"calendar-code-{Guid.NewGuid():N}";
        _codes[code] = $"refresh-token-{Guid.NewGuid():N}";
        return code;
    }

    /// <summary>Klara cofnęła stronie dostęp w ustawieniach konta Google.</summary>
    public void RevokeAccess()
    {
        foreach (var token in _tokens.Keys)
        {
            _tokens[token] = false;
        }
    }

    /// <summary>Blok „Dostępna” w Kalendarzu Sesji; zwraca identyfikator wydarzenia.</summary>
    public string AddAvailableBlock(DateTimeOffset start, DateTimeOffset end) =>
        AddEvent(SessionsCalendar.Id, start, end, busy: false);

    /// <summary>Wydarzenie w głównym kalendarzu Klary; <paramref name="busy"/> <c>false</c> to wydarzenie „Dostępna”.</summary>
    public void AddMainCalendarEvent(DateTimeOffset start, DateTimeOffset end, bool busy = true) =>
        AddEvent(MainCalendar.Id, start, end, busy);

    /// <summary>Wydarzenia w kalendarzu, od najwcześniejszego.</summary>
    public IReadOnlyList<GoogleCalendarEvent> EventsIn(string calendarId)
    {
        var events = _events.GetOrAdd(calendarId, _ => []);
        lock (events)
        {
            return [.. events.OrderBy(e => e.Start)];
        }
    }

    public void Reset()
    {
        Calendars.Clear();
        Calendars.AddRange([MainCalendar, SessionsCalendar, HolidaysCalendar]);
        _events.Clear();
        _appointmentEventCalls = 0;
        AppointmentEventDelay = TimeSpan.Zero;
        lock (_appointmentEvents)
        {
            _appointmentEvents.Clear();
        }
    }

    private string AddEvent(string calendarId, DateTimeOffset start, DateTimeOffset end, bool busy, string? id = null)
    {
        id ??= $"event-{Guid.NewGuid():N}";
        var events = _events.GetOrAdd(calendarId, _ => []);
        lock (events)
        {
            events.Add(new GoogleCalendarEvent(id, start, end, busy));
        }

        return id;
    }

    public Uri ConsentUrl(Uri redirectUri, string state) => new(QueryHelpers.AddQueryString(
        "https://accounts.google.com/o/oauth2/v2/auth",
        new Dictionary<string, string?> { ["redirect_uri"] = redirectUri.ToString(), ["state"] = state }));

    public Task<string> ExchangeCodeAsync(string code, Uri redirectUri, CancellationToken cancellationToken)
    {
        if (!_codes.TryRemove(code, out var token))
        {
            throw new GoogleCalendarAuthorizationException("Nieznany albo zużyty kod.");
        }

        _tokens[token] = true;
        lock (IssuedTokens)
        {
            IssuedTokens.Add(token);
        }

        return Task.FromResult(token);
    }

    public Task<IReadOnlyList<GoogleCalendarInfo>> ListCalendarsAsync(string refreshToken, CancellationToken cancellationToken) =>
        IsActive(refreshToken)
            ? Task.FromResult<IReadOnlyList<GoogleCalendarInfo>>([.. Calendars])
            : throw new GoogleCalendarAccessLostException("invalid_grant");

    public Task<IReadOnlyList<GoogleCalendarEvent>> ListEventsAsync(
        string refreshToken, string calendarId, DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken)
    {
        EnsureActive(refreshToken);
        IReadOnlyList<GoogleCalendarEvent> events = [.. EventsIn(Resolve(calendarId)).Where(e => e.Start < to && e.End > from)];
        return Task.FromResult(events);
    }

    public Task<string> CreateFreeEventAsync(
        string refreshToken, string calendarId, string summary, DateTimeOffset start, DateTimeOffset end, CancellationToken cancellationToken)
    {
        EnsureActive(refreshToken);
        return Task.FromResult(AddEvent(Resolve(calendarId), start, end, busy: false));
    }

    public async Task<GoogleAppointmentEvent> CreateAppointmentEventAsync(
        string refreshToken, AppointmentEventRequest request, CancellationToken cancellationToken)
    {
        EnsureActive(refreshToken);
        Interlocked.Increment(ref _appointmentEventCalls);
        await Task.Delay(AppointmentEventDelay, cancellationToken);
        lock (_appointmentEvents)
        {
            // Jak Google: drugie wydarzenie o tym samym identyfikatorze nie powstaje.
            if (_appointmentEvents.Find(e => e.Request.Id == request.Id) is not { } created)
            {
                created = new CreatedAppointmentEvent(request, $"https://meet.google.com/{request.Id[..10]}");
                _appointmentEvents.Add(created);
                AddEvent(MainCalendar.Id, request.Start, request.End, busy: true, request.Id);
            }

            return new GoogleAppointmentEvent(created.Request.Id, created.MeetLink);
        }
    }

    public Task DeleteEventAsync(string refreshToken, string calendarId, string eventId, CancellationToken cancellationToken)
    {
        EnsureActive(refreshToken);
        var events = _events.GetOrAdd(Resolve(calendarId), _ => []);
        lock (events)
        {
            events.RemoveAll(e => e.Id == eventId);
        }

        return Task.CompletedTask;
    }

    private static string Resolve(string calendarId) => calendarId == GoogleCalendarIds.Primary ? MainCalendar.Id : calendarId;

    private void EnsureActive(string refreshToken)
    {
        if (!IsActive(refreshToken))
        {
            throw new GoogleCalendarAccessLostException("invalid_grant");
        }
    }

    private bool IsActive(string refreshToken) => _tokens.TryGetValue(refreshToken, out var active) && active;
}

/// <summary>Wydarzenie opłaconego Terminu dodane przez stronę i link Meet, który Google do niego utworzył.</summary>
public sealed record CreatedAppointmentEvent(AppointmentEventRequest Request, string MeetLink);
