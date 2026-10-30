using SzeptTarota.Application.Calendar;
using SzeptTarota.Domain.Catalog;

namespace SzeptTarota.Application.Appointments;

/// <summary>
/// Wolne Terminy Klary (ADR 0002): bloki „Dostępna” z Kalendarza Sesji minus zajęte okresy z jej głównego kalendarza
/// minus Terminy opłacone albo zablokowane na czas płatności. O tym, kiedy przyjmuje, decyduje wyłącznie Klara swoimi blokami; strona dba tylko o to, żeby
/// Termin zaczynał się w przyszłości. Dni i godziny liczymy w czasie polskim.
/// </summary>
public sealed class AvailabilityService(
    ICalendarConnectionStore connections,
    IGoogleCalendar google,
    IAppointmentStore appointments,
    TimeProvider time)
{
    public static readonly TimeZoneInfo Warsaw = TimeZoneInfo.FindSystemTimeZoneById("Europe/Warsaw");

    /// <summary>Zapas przy pytaniu kalendarza: bloki i zajęte okresy mogą zaczynać się przed okresem i kończyć po nim.</summary>
    private static readonly TimeSpan Margin = TimeSpan.FromDays(1);

    /// <summary>
    /// Najdalszy początek Terminu, jaki strona w ogóle sprawdza. To nie zasada Klary, tylko ochrona przed absurdalnymi
    /// datami z zapytań (przepełnienie dat, ogromne zapytania do Google).
    /// </summary>
    internal static readonly TimeSpan Horizon = TimeSpan.FromDays(2 * 366);

    /// <summary>
    /// Wolne Terminy Produktu zaczynające się w dniach od <paramref name="from"/> do <paramref name="to"/> (włącznie),
    /// w czasie polskim; <c>null</c>, gdy kalendarza Klary nie da się teraz odczytać.
    /// </summary>
    public async Task<IReadOnlyList<Appointment>?> FreeAsync(
        Product product, DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        // Dni przycinamy do horyzontu, zanim cokolwiek z nich policzymy, żeby skrajne daty nie przepełniły dat.
        var now = time.GetUtcNow();
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, Warsaw).Date);
        var lastDay = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now + Horizon, Warsaw).Date);
        if (from > lastDay || to < today)
        {
            return [];
        }

        var start = MidnightInWarsaw(from < today ? today : from);
        var end = MidnightInWarsaw((to > lastDay ? lastDay : to).AddDays(1));
        if (await LoadAsync(start, end, cancellationToken) is not { } availability)
        {
            return null;
        }

        return availability.Free(Duration(product), start, end).Select(InWarsaw).ToList();
    }

    /// <summary>
    /// Dostępność Klary dla Terminów zaczynających się od <paramref name="from"/> do <paramref name="to"/>; <c>null</c>,
    /// gdy strona nie jest połączona z kalendarzem, Klara nie wybrała Kalendarza Sesji albo Google nie odpowiada.
    /// </summary>
    public async Task<Availability?> LoadAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken)
    {
        if (await connections.FindAsync(cancellationToken) is not { RefreshToken: { } refreshToken, SessionCalendarId: { } sessionCalendarId })
        {
            return null;
        }

        IReadOnlyList<GoogleCalendarEvent> blocks, mainCalendar;
        try
        {
            blocks = await google.ListEventsAsync(refreshToken, sessionCalendarId, from - Margin, to + Margin, cancellationToken);
            mainCalendar = await google.ListEventsAsync(refreshToken, GoogleCalendarIds.Primary, from - Margin, to + Margin, cancellationToken);
        }
        catch (Exception e) when (e is GoogleCalendarAccessLostException or GoogleCalendarNotConfiguredException or HttpRequestException)
        {
            // HttpRequestException także wtedy, gdy Kalendarza Sesji już nie ma na koncie Google Klary (404).
            return null;
        }

        var now = time.GetUtcNow();
        var taken = await appointments.ListTakenAsync(from - Margin, to + Margin, now, cancellationToken);
        return new Availability(
            // Każde wydarzenie w Kalendarzu Sesji to blok „Dostępna”, także oznaczone w Google jako wolne.
            blocks.Select(e => new Appointment(e.Start, e.End)),
            mainCalendar.Where(e => e.Busy).Select(e => new Appointment(e.Start, e.End)),
            taken,
            now);
    }

    /// <summary>Czy Termin zaczynający się o <paramref name="start"/> w ogóle może być wolny: w przyszłości i w horyzoncie.</summary>
    public bool IsWithinHorizon(DateTimeOffset start)
    {
        var now = time.GetUtcNow();
        return start > now && start <= now + Horizon;
    }

    /// <summary>Termin Produktu zaczynający się o <paramref name="start"/>.</summary>
    public static Appointment AppointmentAt(Product product, DateTimeOffset start) => new(start, start + Duration(product));

    /// <summary>Termin z przesunięciem czasu polskiego, tak jak widzi go Klient.</summary>
    public static Appointment InWarsaw(Appointment appointment) =>
        new(TimeZoneInfo.ConvertTime(appointment.Start, Warsaw), TimeZoneInfo.ConvertTime(appointment.End, Warsaw));

    private static TimeSpan Duration(Product product) =>
        TimeSpan.FromMinutes(product.DurationMinutes
            ?? throw new InvalidOperationException($"Produkt '{product.Slug}' nie ma długości Terminu."));

    internal static DateTimeOffset MidnightInWarsaw(DateOnly day)
    {
        var midnight = day.ToDateTime(TimeOnly.MinValue);
        return new DateTimeOffset(midnight, Warsaw.GetUtcOffset(midnight));
    }
}
