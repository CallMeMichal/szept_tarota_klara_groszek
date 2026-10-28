using System.Collections.Concurrent;
using Microsoft.AspNetCore.WebUtilities;
using SzeptTarota.Application.Calendar;

namespace SzeptTarota.Api.Tests.Fakes;

/// <summary>
/// Udaje Google Calendar Klary. Test „zgadza się” w Google przez <see cref="GrantAccess"/> i dostaje kod, z którym
/// Google odesłałby przeglądarkę na stronę; za kod strona dostaje refresh token, a z nim listę <see cref="Calendars"/>.
/// <see cref="RevokeAccess"/> udaje cofnięcie zgody: wydane dotąd tokeny przestają działać.
/// </summary>
public sealed class FakeGoogleCalendar : IGoogleCalendar
{
    public static readonly GoogleCalendarInfo MainCalendar = new("klara@gmail.com", "Klara", Primary: true);

    public static readonly GoogleCalendarInfo SessionsCalendar = new("sesje@group.calendar.google.com", "Sesje", Primary: false);

    private readonly ConcurrentDictionary<string, string> _codes = new();
    private readonly ConcurrentDictionary<string, bool> _tokens = new();

    /// <summary>Kalendarze na koncie Google Klary.</summary>
    public List<GoogleCalendarInfo> Calendars { get; } = [MainCalendar, SessionsCalendar];

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

    /// <summary>Wydarzenia po identyfikatorach kalendarzy.</summary>
    private readonly ConcurrentDictionary<string, List<GoogleCalendarEvent>> _events = new();

    /// <summary>Blok „Dostępna” w Kalendarzu Sesji.</summary>
    public void AddAvailableBlock(DateTimeOffset start, DateTimeOffset end) =>
        AddEvent(SessionsCalendar.Id, new GoogleCalendarEvent(start, end, Busy: false));

    /// <summary>Wydarzenie w głównym kalendarzu Klary; <paramref name="busy"/> <c>false</c> to wydarzenie „Dostępna”.</summary>
    public void AddMainCalendarEvent(DateTimeOffset start, DateTimeOffset end, bool busy = true) =>
        AddEvent(MainCalendar.Id, new GoogleCalendarEvent(start, end, busy));

    public void Reset()
    {
        Calendars.Clear();
        Calendars.AddRange([MainCalendar, SessionsCalendar]);
        _events.Clear();
    }

    private void AddEvent(string calendarId, GoogleCalendarEvent calendarEvent) =>
        _events.GetOrAdd(calendarId, _ => []).Add(calendarEvent);

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
        if (!IsActive(refreshToken))
        {
            throw new GoogleCalendarAccessLostException("invalid_grant");
        }

        var id = calendarId == GoogleCalendarIds.Primary ? MainCalendar.Id : calendarId;
        IReadOnlyList<GoogleCalendarEvent> events = [.. _events.GetValueOrDefault(id, []).Where(e => e.Start < to && e.End > from)];
        return Task.FromResult(events);
    }

    private bool IsActive(string refreshToken) => _tokens.TryGetValue(refreshToken, out var active) && active;
}
