using SzeptTarota.Application.Appointments;
using SzeptTarota.Domain.Orders;

namespace SzeptTarota.Application.Orders;

/// <summary>
/// Zamówienia w bazie. Zmiany stanu są warunkowe i atomowe, żeby kilka równoczesnych
/// powiadomień z bramki płatności nie zmieniło stanu dwa razy.
/// </summary>
public interface IOrderStore
{
    Task AddAsync(Order order, CancellationToken cancellationToken);

    Task<Order?> FindAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Opłacone Zamówienia Konta, od najnowszego.</summary>
    Task<IReadOnlyList<Order>> ListPaidAsync(Guid accountId, CancellationToken cancellationToken);

    /// <summary>Terminy z opłaconych Zamówień, które nachodzą na okres od <paramref name="from"/> do <paramref name="to"/>.</summary>
    Task<IReadOnlyList<Appointment>> ListPaidAppointmentsAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken);

    Task<Order?> FindByPaymentSessionAsync(string paymentSessionId, CancellationToken cancellationToken);

    /// <summary>
    /// Oznacza Zamówienie jako opłacone, o ile jeszcze nie jest. Także wygasłe: pieniądze, które
    /// faktycznie dotarły, są ważniejsze niż wygaśnięcie sesji.
    /// </summary>
    Task MarkPaidAsync(Guid id, DateTimeOffset paidAt, CancellationToken cancellationToken);

    /// <summary>Oznacza jako wygasłe Zamówienie, które wciąż czeka na płatność.</summary>
    Task MarkExpiredAsync(Guid id, DateTimeOffset expiredAt, CancellationToken cancellationToken);

    /// <summary>Zapisuje, że maile o opłaconym Zamówieniu poszły; kolejne powiadomienia już ich nie wysyłają.</summary>
    Task MarkNotificationsSentAsync(Guid id, DateTimeOffset sentAt, CancellationToken cancellationToken);
}
