using SzeptTarota.Application.Calendar;

namespace SzeptTarota.Application.Appointments;

/// <summary>
/// Dostępność, którą Klara zaznacza w panelu (ADR 0002): każdy blok to wydarzenie „Dostępna” w jej Kalendarzu Sesji
/// w Google, więc widzi i poprawia je też w Google Calendar. Panel pokazuje obok zajęte godziny (główny kalendarz,
/// Terminy opłacone i zablokowane na czas płatności), żeby wiedziała, czego Klienci i tak nie dostaną. Godziny z panelu są w czasie polskim.
/// </summary>
public sealed class AvailabilityBlockService(
    ICalendarConnectionStore connections,
    IGoogleCalendar google,
    IAppointmentStore appointments,
    TimeProvider time)
{
    /// <summary>Tytuł bloku w Google Calendar.</summary>
    public const string BlockSummary = "Dostępna";

    /// <summary>Najdłuższy blok: jeden dzień, np. od 20:00 do północy albo przez całą dobę.</summary>
    private static readonly TimeSpan MaxBlockLength = TimeSpan.FromDays(1);

    /// <summary>Jak daleko wstecz panel pokazuje dostępność.</summary>
    private static readonly TimeSpan History = TimeSpan.FromDays(366);

    /// <summary>Bloki „Dostępna” i zajęte okresy w dniach od <paramref name="from"/> do <paramref name="to"/> (włącznie).</summary>
    public async Task<AvailabilityResult<AvailabilityWeek>> ListAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        if (await SessionCalendarAsync(cancellationToken) is not { } session)
        {
            return new AvailabilityResult<AvailabilityWeek>.NoSessionCalendar();
        }

        var (refreshToken, calendarId) = session;

        // Skrajne daty przycinamy, zanim cokolwiek z nich policzymy, żeby nie przepełniły dat.
        var now = time.GetUtcNow();
        var (firstDay, lastDay) = (DayInWarsaw(now - History), DayInWarsaw(now + AvailabilityService.Horizon));
        if (from > lastDay || to < firstDay)
        {
            return new AvailabilityResult<AvailabilityWeek>.Ok(new AvailabilityWeek([], []));
        }

        var start = AvailabilityService.MidnightInWarsaw(from < firstDay ? firstDay : from);
        var end = AvailabilityService.MidnightInWarsaw((to > lastDay ? lastDay : to).AddDays(1));
        try
        {
            var blocks = await google.ListEventsAsync(refreshToken, calendarId, start, end, cancellationToken);
            var mainCalendar = await google.ListEventsAsync(refreshToken, GoogleCalendarIds.Primary, start, end, cancellationToken);
            var taken = await appointments.ListTakenAsync(start, end, now, cancellationToken);
            return new AvailabilityResult<AvailabilityWeek>.Ok(new AvailabilityWeek(
                blocks.OrderBy(e => e.Start).Select(e => new AvailabilityBlock(e.Id, InWarsaw(e.Start), InWarsaw(e.End))).ToList(),
                mainCalendar.Where(e => e.Busy).Select(e => new Appointment(e.Start, e.End)).Concat(taken)
                    .OrderBy(p => p.Start)
                    .Select(AvailabilityService.InWarsaw)
                    .ToList()));
        }
        catch (Exception e) when (IsCalendarFailure(e))
        {
            return new AvailabilityResult<AvailabilityWeek>.CalendarUnavailable();
        }
    }

    /// <summary>Dodaje blok od <paramref name="start"/> do <paramref name="end"/>, podanych w czasie polskim.</summary>
    public async Task<AvailabilityResult<AvailabilityBlock>> AddAsync(DateTime start, DateTime end, CancellationToken cancellationToken)
    {
        if (await SessionCalendarAsync(cancellationToken) is not { } session)
        {
            return new AvailabilityResult<AvailabilityBlock>.NoSessionCalendar();
        }

        var (refreshToken, calendarId) = session;

        if (Validate(start, end) is { } error)
        {
            return new AvailabilityResult<AvailabilityBlock>.Invalid(error);
        }

        var (startAt, endAt) = (InWarsaw(start), InWarsaw(end));
        try
        {
            var id = await google.CreateFreeEventAsync(refreshToken, calendarId, BlockSummary, startAt, endAt, cancellationToken);
            return new AvailabilityResult<AvailabilityBlock>.Ok(new AvailabilityBlock(id, startAt, endAt));
        }
        catch (Exception e) when (IsCalendarFailure(e))
        {
            return new AvailabilityResult<AvailabilityBlock>.CalendarUnavailable();
        }
    }

    /// <summary>Usuwa blok z Kalendarza Sesji (i tylko z niego).</summary>
    public async Task<AvailabilityResult<string>> RemoveAsync(string blockId, CancellationToken cancellationToken)
    {
        if (await SessionCalendarAsync(cancellationToken) is not { } session)
        {
            return new AvailabilityResult<string>.NoSessionCalendar();
        }

        var (refreshToken, calendarId) = session;

        try
        {
            await google.DeleteEventAsync(refreshToken, calendarId, blockId, cancellationToken);
            return new AvailabilityResult<string>.Ok(blockId);
        }
        catch (Exception e) when (IsCalendarFailure(e))
        {
            return new AvailabilityResult<string>.CalendarUnavailable();
        }
    }

    private string? Validate(DateTime start, DateTime end)
    {
        if (AvailabilityService.Warsaw.IsInvalidTime(start) || AvailabilityService.Warsaw.IsInvalidTime(end))
        {
            return "Tej godziny nie ma przy zmianie czasu na letni. Wybierz inną.";
        }

        if (end <= start)
        {
            return "Koniec bloku musi być po jego początku.";
        }

        if (end - start > MaxBlockLength)
        {
            return "Blok może trwać najwyżej dobę.";
        }

        var now = time.GetUtcNow();
        if (InWarsaw(end) <= now)
        {
            return "Ten czas już minął.";
        }

        return InWarsaw(start) > now + AvailabilityService.Horizon ? "To za daleko w przyszłość." : null;
    }

    /// <summary>Refresh token i Kalendarz Sesji; <c>null</c>, gdy Klara nie połączyła strony albo nie wybrała Kalendarza Sesji.</summary>
    private async Task<(string RefreshToken, string CalendarId)?> SessionCalendarAsync(CancellationToken cancellationToken) =>
        await connections.FindAsync(cancellationToken) is { RefreshToken: { } refreshToken, SessionCalendarId: { } calendarId }
            ? (refreshToken, calendarId)
            : null;

    private static bool IsCalendarFailure(Exception e) =>
        e is GoogleCalendarAccessLostException or GoogleCalendarNotConfiguredException or HttpRequestException;

    private static DateTimeOffset InWarsaw(DateTimeOffset time) => TimeZoneInfo.ConvertTime(time, AvailabilityService.Warsaw);

    private static DateTimeOffset InWarsaw(DateTime local) => new(local, AvailabilityService.Warsaw.GetUtcOffset(local));

    private static DateOnly DayInWarsaw(DateTimeOffset time) => DateOnly.FromDateTime(InWarsaw(time).Date);
}

/// <summary>Blok „Dostępna” w Kalendarzu Sesji: identyfikator wydarzenia w Google, początek i koniec.</summary>
public sealed record AvailabilityBlock(string Id, DateTimeOffset Start, DateTimeOffset End);

/// <summary>Bloki „Dostępna” i zajęte okresy (główny kalendarz, Terminy Klientów) w wybranych dniach, od najwcześniejszych.</summary>
public sealed record AvailabilityWeek(IReadOnlyList<AvailabilityBlock> Blocks, IReadOnlyList<Appointment> Busy);

public abstract record AvailabilityResult<T>
{
    public sealed record Ok(T Value) : AvailabilityResult<T>;

    /// <summary>Błędny blok; <see cref="Error"/> do pokazania Klarze.</summary>
    public sealed record Invalid(string Error) : AvailabilityResult<T>;

    /// <summary>Klara nie połączyła strony z Google albo nie wybrała Kalendarza Sesji.</summary>
    public sealed record NoSessionCalendar : AvailabilityResult<T>;

    /// <summary>Google nie odpowiada albo strona straciła dostęp do kalendarza.</summary>
    public sealed record CalendarUnavailable : AvailabilityResult<T>;
}
