using Microsoft.EntityFrameworkCore;
using SzeptTarota.Domain.Orders;

namespace SzeptTarota.Infrastructure.Persistence;

public sealed class SzeptTarotaDbContext(DbContextOptions<SzeptTarotaDbContext> options) : DbContext(options)
{
    public DbSet<Order> Orders => Set<Order>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
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

            order.ComplexCollection(o => o.Lines, lines => lines.ToJson());
            order.ComplexCollection(o => o.Consents, consents => consents.ToJson());
        });
    }
}
