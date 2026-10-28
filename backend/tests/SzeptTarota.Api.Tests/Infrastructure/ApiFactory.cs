using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Time.Testing;
using SzeptTarota.Api.Tests.Fakes;
using SzeptTarota.Application.Contact;
using SzeptTarota.Application.Ebooks;
using SzeptTarota.Application.Email;
using SzeptTarota.Application.Payments;
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

    public FakeContactMailer ContactMailer { get; } = new();

    public FakePaymentGateway Payments { get; } = new();

    public FakeEmailSender Emails { get; } = new();

    public FakeEbookStorage Ebooks { get; } = new();

    public const string SiteUrl = "https://szept-tarota.test";

    public const string KlaraEmail = "klara@szept-tarota.test";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Database", _database.GetConnectionString());
        builder.UseSetting("Database:ApplyMigrationsOnStartup", "true");
        // Wszystkie zapytania z testów idą z jednego „adresu”, więc limity formularza
        // kontaktowego i kodów logowania podnosimy; testy limitów ustawiają własne.
        builder.UseSetting("Contact:RateLimit:PermitLimit", "1000");
        builder.UseSetting("Account:LoginCodeRateLimit:PermitLimit", "1000");
        builder.UseSetting("Account:LoginRateLimit:PermitLimit", "1000");
        builder.UseSetting("Shop:SiteUrl", SiteUrl);
        builder.UseSetting("Shop:KlaraEmail", KlaraEmail);

        builder.ConfigureTestServices(services =>
        {
            services.ReplaceWithFake<TimeProvider>(Time);
            services.ReplaceWithFake<IContactMailer>(ContactMailer);
            services.ReplaceWithFake<IPaymentGateway>(Payments);
            services.ReplaceWithFake<IEmailSender>(Emails);
            services.ReplaceWithFake<IEbookStorage>(Ebooks);
        });
    }

    public Task InitializeAsync() => _database.StartAsync();

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        await _database.DisposeAsync();
    }
}
