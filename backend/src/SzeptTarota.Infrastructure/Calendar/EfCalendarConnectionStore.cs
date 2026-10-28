using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using SzeptTarota.Application.Calendar;
using SzeptTarota.Infrastructure.Persistence;

namespace SzeptTarota.Infrastructure.Calendar;

/// <summary>
/// Połączenie z Google Calendar w PostgreSQL jako jeden wiersz. Refresh token daje pełny dostęp do kalendarzy Klary,
/// więc w bazie leży zaszyfrowany przez ASP.NET Data Protection, a klucze trzymamy poza bazą.
/// </summary>
public sealed class EfCalendarConnectionStore(SzeptTarotaDbContext db, IDataProtectionProvider dataProtection)
    : ICalendarConnectionStore
{
    private readonly IDataProtector _protector = dataProtection.CreateProtector("SzeptTarota.GoogleCalendar.RefreshToken");

    public async Task<CalendarConnection?> FindAsync(CancellationToken cancellationToken) =>
        await db.CalendarConnections.AsNoTracking().SingleOrDefaultAsync(c => c.Id == StoredCalendarConnection.SingleId, cancellationToken)
            is { } stored
            ? new CalendarConnection(Unprotect(stored.ProtectedRefreshToken), stored.SessionCalendarId, stored.ConnectedAt)
            : null;

    public async Task SaveAccessAsync(string refreshToken, DateTimeOffset connectedAt, CancellationToken cancellationToken)
    {
        var protectedToken = _protector.Protect(refreshToken);
        // Upsert, żeby dwa równoczesne powroty ze zgody nie wywróciły się na kluczu głównym.
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO calendar_connections ("Id", "ProtectedRefreshToken", "ConnectedAt") VALUES ({StoredCalendarConnection.SingleId}, {protectedToken}, {connectedAt})
            ON CONFLICT ("Id") DO UPDATE SET "ProtectedRefreshToken" = EXCLUDED."ProtectedRefreshToken", "ConnectedAt" = EXCLUDED."ConnectedAt"
            """,
            cancellationToken);
    }

    public Task SaveSessionCalendarAsync(string calendarId, CancellationToken cancellationToken) =>
        db.CalendarConnections
            .Where(c => c.Id == StoredCalendarConnection.SingleId)
            .ExecuteUpdateAsync(c => c.SetProperty(x => x.SessionCalendarId, calendarId), cancellationToken);

    /// <summary>Token albo <c>null</c>, gdy kluczy, którymi go zaszyfrowano, już nie ma.</summary>
    private string? Unprotect(string protectedToken)
    {
        try
        {
            return _protector.Unprotect(protectedToken);
        }
        catch (CryptographicException)
        {
            return null;
        }
    }
}

/// <summary>Wiersz tabeli <c>calendar_connections</c>; jest najwyżej jeden, bo strona łączy się tylko z kalendarzem Klary.</summary>
public sealed class StoredCalendarConnection
{
    public const int SingleId = 1;

    public int Id { get; set; }

    /// <summary>Refresh token zaszyfrowany przez Data Protection.</summary>
    public string ProtectedRefreshToken { get; set; } = "";

    public string? SessionCalendarId { get; set; }

    public DateTimeOffset ConnectedAt { get; set; }
}
