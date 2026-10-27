using Microsoft.Extensions.DependencyInjection;
using SzeptTarota.Application.Cart;

namespace SzeptTarota.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton<CartValidator>();

        return services;
    }
}
