using Microsoft.Extensions.DependencyInjection;
using SzeptTarota.Application.Cart;
using SzeptTarota.Application.Orders;

namespace SzeptTarota.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton<CartValidator>();
        services.AddScoped<CheckoutService>();
        services.AddScoped<PaymentWebhookHandler>();

        return services;
    }
}
