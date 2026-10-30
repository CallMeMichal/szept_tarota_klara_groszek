using SzeptTarota.Domain.Orders;

namespace SzeptTarota.Application.Appointments;

/// <summary>
/// Terminy Zamówień w bazie: blokady na czas płatności i opłacone Terminy. Ochronę przed podwójnym zajęciem Terminu daje
/// baza: dwa niezwolnione Terminy nie mogą na siebie nachodzić, także przy równoczesnych płatnościach.
/// </summary>
public interface IAppointmentStore
{
    /// <summary>
    /// Zakłada blokady Terminów jednego Zamówienia: wszystkie albo żadnej. Najpierw zwalnia wygasłe blokady. Zwraca, która z
    /// <paramref name="holds"/> nachodzi na zajęty już Termin, albo <c>null</c>, gdy wszystkie założono.
    /// </summary>
    Task<int?> TryHoldAsync(IReadOnlyList<OrderAppointment> holds, DateTimeOffset now, CancellationToken cancellationToken);

    /// <summary>
    /// Zajęte Terminy (opłacone i aktywne blokady), które nachodzą na okres od <paramref name="from"/> do
    /// <paramref name="to"/>.
    /// </summary>
    Task<IReadOnlyList<Appointment>> ListTakenAsync(
        DateTimeOffset from, DateTimeOffset to, DateTimeOffset now, CancellationToken cancellationToken);

    /// <summary>
    /// Zamienia blokady opłaconego Zamówienia na Terminy bez wygaśnięcia, także te, które już wygasły albo zostały
    /// zwolnione, o ile nikt inny nie zajął w międzyczasie ich czasu. Zwraca Terminy Zamówienia po tej zmianie;
    /// zajęte przez kogoś innego zostają zwolnione.
    /// </summary>
    Task<IReadOnlyList<OrderAppointment>> ConfirmAsync(Guid orderId, CancellationToken cancellationToken);

    /// <summary>Zwalnia nieopłacone blokady Zamówienia (wygasła sesja płatności albo nie udało się jej założyć).</summary>
    Task ReleaseAsync(Guid orderId, DateTimeOffset now, CancellationToken cancellationToken);

    /// <summary>Zapisuje przy Terminie jego wydarzenie w kalendarzu Klary, link Meet i identyfikator iCalendar.</summary>
    Task SaveCalendarEventAsync(Guid id, string calendarEventId, string? meetLink, string? calendarEventUid, CancellationToken cancellationToken);
}
