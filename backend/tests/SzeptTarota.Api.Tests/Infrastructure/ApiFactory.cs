using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Time.Testing;
using Testcontainers.PostgreSql;

namespace SzeptTarota.Api.Tests.Infrastructure;

/// <summary>
/// Uruchamia API w pamięci z prawdziwym PostgreSQL w Testcontainers (wymaga Dockera).
/// Adaptery zewnętrznych usług są podmieniane na fake'i w <see cref="ConfigureWebHost"/>:
/// każdy fake to publiczna właściwość fabryki, rejestrowana przez <see cref="FakeServiceCollectionExtensions.ReplaceWithFake{TService}"/>.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _database = new PostgreSqlBuilder("postgres:17-alpine").Build();

    public FakeTimeProvider Time { get; } = new(new DateTimeOffset(2026, 1, 15, 9, 0, 0, TimeSpan.Zero));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Database", _database.GetConnectionString());
        builder.UseSetting("Database:ApplyMigrationsOnStartup", "true");

        builder.ConfigureTestServices(services =>
        {
            services.ReplaceWithFake<TimeProvider>(Time);
        });
    }

    public Task InitializeAsync() => _database.StartAsync();

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        await _database.DisposeAsync();
    }
}
