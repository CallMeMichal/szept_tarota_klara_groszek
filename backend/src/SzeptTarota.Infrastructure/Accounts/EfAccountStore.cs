using Microsoft.EntityFrameworkCore;
using SzeptTarota.Application.Accounts;
using SzeptTarota.Domain.Accounts;
using SzeptTarota.Infrastructure.Persistence;

namespace SzeptTarota.Infrastructure.Accounts;

/// <summary>Konta i linki logowania w PostgreSQL.</summary>
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

    public Task<int> CountLoginLinksSinceAsync(string email, DateTimeOffset since, CancellationToken cancellationToken) =>
        db.LoginLinks.CountAsync(l => l.Email == email && l.CreatedAt > since, cancellationToken);

    public async Task AddLoginLinkAsync(LoginLink link, CancellationToken cancellationToken)
    {
        db.LoginLinks.Add(link);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<LoginLink?> UseLoginLinkAsync(string tokenHash, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var used = await db.LoginLinks
            .Where(l => l.TokenHash == tokenHash && l.UsedAt == null && l.ExpiresAt > now)
            .ExecuteUpdateAsync(l => l.SetProperty(x => x.UsedAt, now), cancellationToken);

        return used == 1
            ? await db.LoginLinks.AsNoTracking().SingleAsync(l => l.TokenHash == tokenHash, cancellationToken)
            : null;
    }
}
