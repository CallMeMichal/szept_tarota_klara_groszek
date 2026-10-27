using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace SzeptTarota.Api.Tests.Infrastructure;

public static class FakeServiceCollectionExtensions
{
    /// <summary>
    /// Usuwa produkcyjne rejestracje <typeparamref name="TService"/> i rejestruje podany fake jako singleton,
    /// żeby test mógł go ustawiać i sprawdzać przez właściwość <see cref="ApiFactory"/>.
    /// </summary>
    public static IServiceCollection ReplaceWithFake<TService>(this IServiceCollection services, TService fake)
        where TService : class
    {
        services.RemoveAll<TService>();
        services.AddSingleton(fake);
        return services;
    }
}
