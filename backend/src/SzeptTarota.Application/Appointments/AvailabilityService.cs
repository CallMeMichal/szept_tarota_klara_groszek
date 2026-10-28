using SzeptTarota.Application.Calendar;
using SzeptTarota.Application.Orders;
using SzeptTarota.Domain.Catalog;

namespace SzeptTarota.Application.Appointments;

/// <summary>
/// Wolne Terminy Klary (ADR 0002): bloki „Dostępna” z Kalendarza Sesji minus zajęte okresy z jej głównego kalendarza
/// minus opłacone Terminy. O tym, kiedy przyjmuje, decyduje wyłącznie Klara swoimi blokami; strona dba tylko o to, żeby
/// Termin zaczynał się w przyszłości. Dni i godziny liczymy w czasie polskim.
/// </summary>
public sealed class AvailabilityService(
    ICalendarConnectionStore connections,
    IGoogleCalendar google,
    IOrderStore orders,
    TimeProvider time)
{
    public static readonly TimeZoneInfo Warsaw = TimeZoneInfo.FindSystemTimeZoneById("Europe/Warsaw");

    /// <summary>Zapas przy pytaniu kalendarza: bloki i zajęte okresy mogą zaczynać się przed okresem i kończyć po nim.</summary>
    private static readonly TimeSpan Margin = TimeSpan.FromDays(1);

    /// <summary>
    /// Wolne Terminy Produktu zaczynające się w dniach od <paramref name="from"/> do <paramref name="to"/> (włącznie),
    /// w czasie polskim; <c>null</c>, gdy kalendarza Klary nie da się teraz odczytać.
    /// </summary>
    public async Task<IReadOnlyList<Appointment>?> FreeAsync(
        Product product, DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        var (start, end) = (MidnightInWarsaw(from), MidnightInWarsaw(to.AddDays(1)));
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

        var paid = await orders.ListPaidAppointmentsAsync(from - Margin, to + Margin, cancellationToken);
        return new Availability(
            // Każde wydarzenie w Kalendarzu Sesji to blok „Dostępna”, także oznaczone w Google jako wolne.
            blocks.Select(e => new Appointment(e.Start, e.End)),
            mainCalendar.Where(e => e.Busy).Select(e => new Appointment(e.Start, e.End)).Concat(paid),
            time.GetUtcNow());
    }

    /// <summary>Termin Produktu zaczynający się o <paramref name="start"/>.</summary>
    public static Appointment AppointmentAt(Product product, DateTimeOffset start) => new(start, start + Duration(product));

    /// <summary>Termin z przesunięciem czasu polskiego, tak jak widzi go Klient.</summary>
    public static Appointment InWarsaw(Appointment appointment) =>
        new(TimeZoneInfo.ConvertTime(appointment.Start, Warsaw), TimeZoneInfo.ConvertTime(appointment.End, Warsaw));

    private static TimeSpan Duration(Product product) =>
        TimeSpan.FromMinutes(product.DurationMinutes
            ?? throw new InvalidOperationException($"Produkt '{product.Slug}' nie ma długości Terminu."));

    private static DateTimeOffset MidnightInWarsaw(DateOnly day)
    {
        var midnight = day.ToDateTime(TimeOnly.MinValue);
        return new DateTimeOffset(midnight, Warsaw.GetUtcOffset(midnight));
    }
}
