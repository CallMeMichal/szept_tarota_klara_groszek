using Microsoft.EntityFrameworkCore;
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
