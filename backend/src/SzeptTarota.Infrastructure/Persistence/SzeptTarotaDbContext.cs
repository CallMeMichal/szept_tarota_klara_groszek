using Microsoft.EntityFrameworkCore;
using SzeptTarota.Domain.Accounts;
using SzeptTarota.Domain.Orders;
using SzeptTarota.Infrastructure.Calendar;

namespace SzeptTarota.Infrastructure.Persistence;

public sealed class SzeptTarotaDbContext(DbContextOptions<SzeptTarotaDbContext> options) : DbContext(options)
{
    public DbSet<Order> Orders => Set<Order>();

    public DbSet<OrderAppointment> OrderAppointments => Set<OrderAppointment>();

    public DbSet<Account> Accounts => Set<Account>();

    public DbSet<LoginCode> LoginCodes => Set<LoginCode>();

    public DbSet<StoredCalendarConnection> CalendarConnections => Set<StoredCalendarConnection>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<StoredCalendarConnection>(connection =>
        {
            connection.ToTable("calendar_connections");
            connection.HasKey(c => c.Id);
            connection.Property(c => c.Id).ValueGeneratedNever();
            connection.Property(c => c.SessionCalendarId).HasMaxLength(1024);
        });

        modelBuilder.Entity<Account>(account =>
        {
            account.ToTable("accounts");
            account.HasKey(a => a.Id);
            account.Property(a => a.Id).ValueGeneratedNever();
            account.Property(a => a.Email).HasMaxLength(200);
            // Jedno Konto na adres e-mail.
            account.HasIndex(a => a.Email).IsUnique();
        });

        modelBuilder.Entity<LoginCode>(code =>
        {
            code.ToTable("login_codes");
            code.HasKey(c => c.Id);
            code.Property(c => c.Id).ValueGeneratedNever();
            code.Property(c => c.Email).HasMaxLength(200);
            code.Property(c => c.CodeHash).HasMaxLength(64);
            code.HasIndex(c => new { c.Email, c.CreatedAt });
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

        modelBuilder.Entity<OrderAppointment>(appointment =>
        {
            appointment.ToTable("order_appointments");
            appointment.HasKey(a => a.Id);
            appointment.Property(a => a.Id).ValueGeneratedNever();
            appointment.Property(a => a.CalendarEventId).HasMaxLength(1024);
            appointment.Property(a => a.MeetLink).HasMaxLength(1024);
            appointment.Property(a => a.CalendarEventUid).HasMaxLength(1024);
            appointment.HasIndex(a => a.OrderId);
            // Ograniczenie wykluczające nachodzące na siebie niezwolnione Terminy jest w migracji AddOrderAppointments,
            // bo EF Core nie umie go opisać.
        });
    }
}
