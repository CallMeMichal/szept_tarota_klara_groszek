using Microsoft.Extensions.DependencyInjection;
using SzeptTarota.Application.Accounts;
using SzeptTarota.Application.Appointments;
using SzeptTarota.Application.Calendar;
using SzeptTarota.Application.Cart;
using SzeptTarota.Application.Orders;

namespace SzeptTarota.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<CartValidator>();
        services.AddScoped<CheckoutService>();
        services.AddScoped<PaymentWebhookHandler>();
        services.AddScoped<LoginService>();
        services.AddScoped<CalendarConnectionService>();
        services.AddScoped<AvailabilityService>();
        services.AddScoped<AvailabilityBlockService>();

        return services;
    }
}
