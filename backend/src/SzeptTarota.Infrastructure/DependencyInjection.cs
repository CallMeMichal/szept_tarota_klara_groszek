using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SzeptTarota.Application;
using SzeptTarota.Application.Accounts;
using SzeptTarota.Application.Appointments;
using SzeptTarota.Application.Calendar;
using SzeptTarota.Application.Catalog;
using SzeptTarota.Application.Contact;
using SzeptTarota.Application.Ebooks;
using SzeptTarota.Application.Email;
using SzeptTarota.Application.Orders;
using SzeptTarota.Application.Payments;
using SzeptTarota.Infrastructure.Accounts;
using SzeptTarota.Infrastructure.Calendar;
using SzeptTarota.Infrastructure.Catalog;
using SzeptTarota.Infrastructure.Contact;
using SzeptTarota.Infrastructure.Ebooks;
using SzeptTarota.Infrastructure.Email;
using SzeptTarota.Infrastructure.Orders;
using SzeptTarota.Infrastructure.Payments;
using SzeptTarota.Infrastructure.Persistence;

namespace SzeptTarota.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.TryAddSingleton(TimeProvider.System);

        services.AddDbContext<SzeptTarotaDbContext>((serviceProvider, options) =>
        {
            var connectionString = serviceProvider.GetRequiredService<IConfiguration>().GetConnectionString("Database")
                ?? throw new InvalidOperationException("Brak connection stringa 'ConnectionStrings:Database'.");
            options.UseNpgsql(connectionString);
        });

        services.AddScoped<IOrderStore, EfOrderStore>();
        services.AddScoped<IAppointmentStore, EfAppointmentStore>();
        services.AddScoped<IAccountStore, EfAccountStore>();
        services.AddScoped<ICalendarConnectionStore, EfCalendarConnectionStore>();

        services.AddHealthChecks()
            .AddDbContextCheck<SzeptTarotaDbContext>("database");

        services.Configure<ContactOptions>(configuration.GetSection(ContactOptions.SectionName));
        services.AddSingleton<IContactMailer, SmtpContactMailer>();

        services.Configure<ShopOptions>(configuration.GetSection(ShopOptions.SectionName));

        services.Configure<StripeOptions>(configuration.GetSection(StripeOptions.SectionName));
        services.AddSingleton<IPaymentGateway, StripePaymentGateway>();

        services.Configure<ResendOptions>(configuration.GetSection(ResendOptions.SectionName));
        services.AddHttpClient<IEmailSender, ResendEmailSender>(http => http.BaseAddress = ResendEmailSender.BaseAddress);

        services.Configure<GoogleCalendarOptions>(configuration.GetSection(GoogleCalendarOptions.SectionName));
        services.AddHttpClient<IGoogleCalendar, GoogleCalendarClient>();

        AddDataProtection(services, configuration);

        return services;
    }

    /// <summary>
    /// Klucze Data Protection szyfrują ciasteczko sesji i refresh token Google Calendar. Domyślnie leżą w profilu
    /// użytkownika serwera; na hostingu <c>DataProtection:KeysDirectory</c> wskazuje trwały katalog poza bazą (np. wolumen),
    /// żeby restart nie wylogował Klientów ani nie zerwał połączenia z kalendarzem.
    /// </summary>
    private static void AddDataProtection(IServiceCollection services, IConfiguration configuration)
    {
        var dataProtection = services.AddDataProtection();
        if (configuration["DataProtection:KeysDirectory"] is { Length: > 0 } keysDirectory)
        {
            // Stała nazwa aplikacji: klucze pasują po zmianie katalogu wdrożenia.
            dataProtection.SetApplicationName("SzeptTarota").PersistKeysToFileSystem(new DirectoryInfo(keysDirectory));
        }
    }

    /// <summary>Wczytuje katalog Produktów z <paramref name="catalogDirectory"/> od razu, żeby błędny katalog zatrzymał start.</summary>
    public static IServiceCollection AddProductCatalog(this IServiceCollection services, string catalogDirectory)
    {
        services.AddSingleton<IProductCatalog>(JsonProductCatalog.Load(catalogDirectory));
        return services;
    }

    /// <summary>Pliki Ebooków z katalogu <paramref name="ebookDirectory"/> na dysku serwera.</summary>
    public static IServiceCollection AddEbookStorage(this IServiceCollection services, string ebookDirectory)
    {
        services.AddSingleton<IEbookStorage>(new FileSystemEbookStorage(ebookDirectory));
        return services;
    }

    /// <summary>Stosuje oczekujące migracje, jeśli włączono <c>Database:ApplyMigrationsOnStartup</c>.</summary>
    public static void ApplyMigrationsIfEnabled(this IServiceProvider services)
    {
        var configuration = services.GetRequiredService<IConfiguration>();
        if (!configuration.GetValue<bool>("Database:ApplyMigrationsOnStartup"))
        {
            return;
        }

        using var scope = services.CreateScope();
        scope.ServiceProvider.GetRequiredService<SzeptTarotaDbContext>().Database.Migrate();
    }
}
