using Microsoft.EntityFrameworkCore;
using SzeptTarota.Application.Accounts;
using SzeptTarota.Domain.Accounts;
using SzeptTarota.Infrastructure.Persistence;

namespace SzeptTarota.Infrastructure.Accounts;

/// <summary>Konta i kody logowania w PostgreSQL; zmiany stanu kodów to pojedyncze warunkowe UPDATE-y.</summary>
public sealed class EfAccountStore(SzeptTarotaDbContext db) : IAccountStore
{
    public Task<Account?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        db.Accounts.AsNoTracking().SingleOrDefaultAsync(a => a.Id == id, cancellationToken);

    public async Task<Account> GetOrCreateAsync(string email, DateTimeOffset now, CancellationToken cancellationToken)
    {
        // ON CONFLICT zamiast sprawdzenia i wstawienia: dwa równoczesne pierwsze logowania dostają to samo Konto.
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"""INSERT INTO accounts ("Id", "Email", "CreatedAt") VALUES ({Guid.NewGuid()}, {email}, {now}) ON CONFLICT ("Email") DO NOTHING""",
            cancellationToken);
        return await db.Accounts.AsNoTracking().SingleAsync(a => a.Email == email, cancellationToken);
    }

    public Task<int> CountLoginCodesSinceAsync(string email, DateTimeOffset since, CancellationToken cancellationToken) =>
        db.LoginCodes.CountAsync(c => c.Email == email && c.CreatedAt > since, cancellationToken);

    public async Task AddLoginCodeAsync(LoginCode code, CancellationToken cancellationToken)
    {
        db.LoginCodes.Add(code);
        await db.SaveChangesAsync(cancellationToken);
    }

    public Task<LoginCode?> FindNewestActiveLoginCodeAsync(string email, DateTimeOffset now, CancellationToken cancellationToken) =>
        db.LoginCodes.AsNoTracking()
            .Where(c => c.Email == email && c.UsedAt == null && c.ExpiresAt > now)
            .OrderByDescending(c => c.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<bool> UseLoginCodeAsync(Guid id, int maxFailedAttempts, DateTimeOffset now, CancellationToken cancellationToken) =>
        await db.LoginCodes
            .Where(c => c.Id == id && c.UsedAt == null && c.FailedAttempts < maxFailedAttempts)
            .ExecuteUpdateAsync(c => c.SetProperty(x => x.UsedAt, now), cancellationToken) == 1;

    public Task AddFailedLoginAttemptAsync(Guid id, CancellationToken cancellationToken) =>
        db.LoginCodes
            .Where(c => c.Id == id)
            .ExecuteUpdateAsync(c => c.SetProperty(x => x.FailedAttempts, x => x.FailedAttempts + 1), cancellationToken);
}
