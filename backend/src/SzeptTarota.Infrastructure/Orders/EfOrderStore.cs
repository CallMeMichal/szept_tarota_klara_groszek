using Microsoft.EntityFrameworkCore;
using Npgsql;
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

    public async Task<IReadOnlyList<Order>> ListAllPaidAsync(PaidOrderFilter filter, CancellationToken cancellationToken)
    {
        var orders = db.Orders.AsNoTracking().Where(o => o.Status == OrderStatus.Paid);
        // PostgreSQL przyjmuje tylko czas UTC.
        if (filter.PaidFrom?.ToUniversalTime() is { } from)
        {
            orders = orders.Where(o => o.PaidAt >= from);
        }

        if (filter.PaidBefore?.ToUniversalTime() is { } before)
        {
            orders = orders.Where(o => o.PaidAt < before);
        }

        if (filter.ProductSlug is { } slug)
        {
            orders = orders.Where(o => o.Lines.Any(l => l.Slug == slug));
        }

        return await orders.OrderByDescending(o => o.PaidAt).ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Blokada doradcza PostgreSQL na osobnym połączeniu, więc nie miesza się z transakcjami kontekstu, w których
    /// zajmuje się Terminy. Trzyma ją sesja połączenia, a pula połączeń jej nie zwalnia, więc zwalniamy ją wprost.
    /// </summary>
    public async Task<IAsyncDisposable> LockAsync(Guid id, CancellationToken cancellationToken)
    {
        var connection = new NpgsqlConnection(db.Database.GetConnectionString());
        try
        {
            await connection.OpenAsync(cancellationToken);
            await using var command = new NpgsqlCommand("SELECT pg_advisory_lock($1)", connection)
            {
                Parameters = { new NpgsqlParameter { Value = LockKey(id) } },
            };
            await command.ExecuteNonQueryAsync(cancellationToken);
            return new OrderLock(connection, LockKey(id));
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }

    /// <summary>Klucz blokady z identyfikatora Zamówienia; zderzenie z innym kluczem tylko by kazało poczekać.</summary>
    private static long LockKey(Guid id) => BitConverter.ToInt64(id.ToByteArray());

    private sealed class OrderLock(NpgsqlConnection connection, long key) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            try
            {
                await using var command = new NpgsqlCommand("SELECT pg_advisory_unlock($1)", connection)
                {
                    Parameters = { new NpgsqlParameter { Value = key } },
                };
                await command.ExecuteNonQueryAsync();
            }
            finally
            {
                await connection.DisposeAsync();
            }
        }
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
