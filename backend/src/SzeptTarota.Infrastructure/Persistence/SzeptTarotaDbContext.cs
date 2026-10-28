using Microsoft.EntityFrameworkCore;
using SzeptTarota.Domain.Accounts;
using SzeptTarota.Domain.Orders;

namespace SzeptTarota.Infrastructure.Persistence;

public sealed class SzeptTarotaDbContext(DbContextOptions<SzeptTarotaDbContext> options) : DbContext(options)
{
    public DbSet<Order> Orders => Set<Order>();

    public DbSet<Account> Accounts => Set<Account>();

    public DbSet<LoginLink> LoginLinks => Set<LoginLink>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Account>(account =>
        {
            account.ToTable("accounts");
            account.HasKey(a => a.Id);
            account.Property(a => a.Id).ValueGeneratedNever();
            account.Property(a => a.Email).HasMaxLength(200);
            // Jedno Konto na adres e-mail.
            account.HasIndex(a => a.Email).IsUnique();
        });

        modelBuilder.Entity<LoginLink>(link =>
        {
            link.ToTable("login_links");
            link.HasKey(l => l.TokenHash);
            link.Property(l => l.TokenHash).HasMaxLength(64);
            link.Property(l => l.Email).HasMaxLength(200);
            link.Property(l => l.ReturnTo).HasMaxLength(200);
            link.HasIndex(l => new { l.Email, l.CreatedAt });
        });

        modelBuilder.Entity<Order>(order =>
        {
            order.ToTable("orders");
            order.HasKey(o => o.Id);
            order.Property(o => o.Id).ValueGeneratedNever();
            order.Property(o => o.Status).HasConversion<string>().HasMaxLength(32);
            order.Property(o => o.Email).HasMaxLength(200);
            order.Property(o => o.CustomerName).HasMaxLength(200);
            order.Property(o => o.PaymentSessionId).HasMaxLength(255);
            // Jedna sesja płatności to najwyżej jedno Zamówienie.
            order.HasIndex(o => o.PaymentSessionId).IsUnique();
            // Zamówienia sprzed Kont nie mają Konta.
            order.HasOne<Account>().WithMany().HasForeignKey(o => o.AccountId).OnDelete(DeleteBehavior.SetNull);

            order.ComplexCollection(o => o.Lines, lines => lines.ToJson());
            order.ComplexCollection(o => o.Consents, consents => consents.ToJson());
        });
    }
}
