using Microsoft.EntityFrameworkCore;
using SzeptTarota.Application.Appointments;
using SzeptTarota.Application.Orders;
using SzeptTarota.Domain.Orders;
using SzeptTarota.Infrastructure.Persistence;

namespace SzeptTarota.Infrastructure.Orders;

/// <summary>Zamówienia w PostgreSQL; zmiany stanu to pojedyncze warunkowe UPDATE-y.</summary>
public sealed class EfOrderStore(SzeptTarotaDbContext db) : IOrderStore
{
    public async Task AddAsync(Order order, CancellationToken cancellationToken)
    {
        db.Orders.Add(order);
        await db.SaveChangesAsync(cancellationToken);
    }

    public Task<Order?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        db.Orders.AsNoTracking().SingleOrDefaultAsync(o => o.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Order>> ListPaidAsync(Guid accountId, CancellationToken cancellationToken) =>
        await db.Orders.AsNoTracking()
            .Where(o => o.AccountId == accountId && o.Status == OrderStatus.Paid)
            .OrderByDescending(o => o.PaidAt)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Appointment>> ListPaidAppointmentsAsync(
        DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken)
    {
        // PostgreSQL przyjmuje tylko czas UTC.
        var (fromUtc, toUtc) = (from.ToUniversalTime(), to.ToUniversalTime());
        return await db.Orders.AsNoTracking()
            .Where(o => o.Status == OrderStatus.Paid)
            .SelectMany(o => o.Lines)
            .Where(l => l.AppointmentStart < toUtc && l.AppointmentEnd > fromUtc)
            .Select(l => new Appointment(l.AppointmentStart!.Value, l.AppointmentEnd!.Value))
            .ToListAsync(cancellationToken);
    }

    public Task<Order?> FindByPaymentSessionAsync(string paymentSessionId, CancellationToken cancellationToken) =>
        db.Orders.AsNoTracking().SingleOrDefaultAsync(o => o.PaymentSessionId == paymentSessionId, cancellationToken);

    public Task MarkPaidAsync(Guid id, DateTimeOffset paidAt, CancellationToken cancellationToken) =>
        db.Orders
            .Where(o => o.Id == id && o.Status != OrderStatus.Paid)
            .ExecuteUpdateAsync(o => o
                .SetProperty(x => x.Status, OrderStatus.Paid)
                .SetProperty(x => x.PaidAt, paidAt), cancellationToken);

    public Task MarkExpiredAsync(Guid id, DateTimeOffset expiredAt, CancellationToken cancellationToken) =>
        db.Orders
            .Where(o => o.Id == id && o.Status == OrderStatus.PendingPayment)
            .ExecuteUpdateAsync(o => o
                .SetProperty(x => x.Status, OrderStatus.Expired)
                .SetProperty(x => x.ExpiredAt, expiredAt), cancellationToken);

    public Task MarkNotificationsSentAsync(Guid id, DateTimeOffset sentAt, CancellationToken cancellationToken) =>
        db.Orders
            .Where(o => o.Id == id && o.NotificationsSentAt == null)
            .ExecuteUpdateAsync(o => o.SetProperty(x => x.NotificationsSentAt, sentAt), cancellationToken);
}
