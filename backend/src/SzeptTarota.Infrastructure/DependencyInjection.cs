using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SzeptTarota.Application.Catalog;
using SzeptTarota.Infrastructure.Catalog;
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

        services.AddHealthChecks()
            .AddDbContextCheck<SzeptTarotaDbContext>("database");

        return services;
    }

    /// <summary>Wczytuje katalog Produktów z <paramref name="catalogDirectory"/> od razu, żeby błędny katalog zatrzymał start.</summary>
    public static IServiceCollection AddProductCatalog(this IServiceCollection services, string catalogDirectory)
    {
        services.AddSingleton<IProductCatalog>(JsonProductCatalog.Load(catalogDirectory));
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
