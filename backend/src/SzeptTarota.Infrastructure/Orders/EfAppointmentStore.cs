using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using SzeptTarota.Application.Appointments;
using SzeptTarota.Domain.Orders;
using SzeptTarota.Infrastructure.Persistence;

namespace SzeptTarota.Infrastructure.Orders;

/// <summary>
/// Terminy Zamówień w PostgreSQL. Ograniczenie wykluczające na tabeli nie pozwala, żeby dwa niezwolnione Terminy
/// na siebie nachodziły, więc z dwóch równoczesnych płatności za ten sam czas przechodzi tylko jedna. Zajmowanie
/// Terminów idzie po kolei pod blokadą doradczą: przy kilku równoczesnych wstawieniach ograniczenie wykluczające potrafi
/// się zakleszczyć, a ruch na stronie jest mały.
/// </summary>
public sealed class EfAppointmentStore(SzeptTarotaDbContext db) : IAppointmentStore
{
    /// <summary>Kod błędu PostgreSQL przy naruszeniu ograniczenia wykluczającego.</summary>
    private const string ExclusionViolation = "23P01";

    /// <summary>Klucz blokady doradczej PostgreSQL na zajmowanie Terminów.</summary>
    private const long TakingLock = 0x5A_54_45_52_4D_49_4E;

    public async Task<int?> TryHoldAsync(IReadOnlyList<OrderAppointment> holds, DateTimeOffset now, CancellationToken cancellationToken)
    {
        // PostgreSQL przyjmuje tylko czas UTC.
        var nowUtc = now.ToUniversalTime();
        await using var transaction = await BeginTakingAsync(cancellationToken);

        await db.OrderAppointments
            .Where(a => a.ReleasedAt == null && a.HeldUntil != null && a.HeldUntil <= nowUtc)
            .ExecuteUpdateAsync(a => a.SetProperty(x => x.ReleasedAt, nowUtc), cancellationToken);

        // Po jednej, żeby wiedzieć, która nachodzi na zajęty Termin.
        for (var i = 0; i < holds.Count; i++)
        {
            db.OrderAppointments.Add(holds[i]);
            try
            {
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException e) when (IsOverlap(e))
            {
                db.ChangeTracker.Clear();
                await transaction.RollbackAsync(cancellationToken);
                return i;
            }
        }

        await transaction.CommitAsync(cancellationToken);
        db.ChangeTracker.Clear();
        return null;
    }

    public async Task<IReadOnlyList<Appointment>> ListTakenAsync(
        DateTimeOffset from, DateTimeOffset to, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var (fromUtc, toUtc, nowUtc) = (from.ToUniversalTime(), to.ToUniversalTime(), now.ToUniversalTime());
        return await db.OrderAppointments.AsNoTracking()
            .Where(a => a.ReleasedAt == null && a.Start < toUtc && a.End > fromUtc)
            .Where(a => a.HeldUntil == null || a.HeldUntil > nowUtc)
            .Select(a => new Appointment(a.Start, a.End))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<OrderAppointment>> ConfirmAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var ids = await db.OrderAppointments.Where(a => a.OrderId == orderId).Select(a => a.Id).ToListAsync(cancellationToken);
        foreach (var id in ids)
        {
            // Niezwolnionej blokady (nawet wygasłej) nikt inny nie mógł zająć: kto zakłada blokadę, najpierw zwalnia wygasłe.
            var confirmed = await db.OrderAppointments
                .Where(a => a.Id == id && a.ReleasedAt == null)
                .ExecuteUpdateAsync(a => a.SetProperty(x => x.HeldUntil, (DateTimeOffset?)null), cancellationToken);
            if (confirmed > 0)
            {
                continue;
            }

            await using var transaction = await BeginTakingAsync(cancellationToken);
            try
            {
                await db.OrderAppointments
                    .Where(a => a.Id == id)
                    .ExecuteUpdateAsync(a => a
                        .SetProperty(x => x.ReleasedAt, (DateTimeOffset?)null)
                        .SetProperty(x => x.HeldUntil, (DateTimeOffset?)null), cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
            catch (PostgresException e) when (e.SqlState == ExclusionViolation)
            {
                // Czas zajął w międzyczasie ktoś inny; Termin zostaje zwolniony.
                await transaction.RollbackAsync(cancellationToken);
            }
        }

        return await db.OrderAppointments.AsNoTracking()
            .Where(a => a.OrderId == orderId)
            .OrderBy(a => a.LineIndex)
            .ToListAsync(cancellationToken);
    }

    public Task ReleaseAsync(Guid orderId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var nowUtc = now.ToUniversalTime();
        return db.OrderAppointments
            .Where(a => a.OrderId == orderId && a.ReleasedAt == null && a.HeldUntil != null)
            .ExecuteUpdateAsync(a => a.SetProperty(x => x.ReleasedAt, nowUtc), cancellationToken);
    }

    public Task SaveCalendarEventAsync(
        Guid id, string calendarEventId, string? meetLink, string? calendarEventUid, CancellationToken cancellationToken) =>
        db.OrderAppointments
            .Where(a => a.Id == id)
            .ExecuteUpdateAsync(a => a
                .SetProperty(x => x.CalendarEventId, calendarEventId)
                .SetProperty(x => x.MeetLink, meetLink)
                .SetProperty(x => x.CalendarEventUid, calendarEventUid), cancellationToken);

    /// <summary>Transakcja, w której tylko ona zajmuje teraz Terminy; blokada znika z jej końcem.</summary>
    private async Task<IDbContextTransaction> BeginTakingAsync(CancellationToken cancellationToken)
    {
        var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock({TakingLock})", cancellationToken);
        return transaction;
    }

    private static bool IsOverlap(DbUpdateException e) =>
        e.InnerException is PostgresException { SqlState: ExclusionViolation };
}
