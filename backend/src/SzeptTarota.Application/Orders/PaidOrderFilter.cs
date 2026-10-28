using SzeptTarota.Application.Appointments;

namespace SzeptTarota.Application.Orders;

/// <summary>
/// Filtr listy Zamówień w panelu Klary: dni zapłaty od <see cref="From"/> do <see cref="To"/> (włącznie, w czasie
/// polskim) i Produkt, który jest w Zamówieniu; brak wartości to brak ograniczenia.
/// </summary>
public sealed record PaidOrderFilter(DateOnly? From = null, DateOnly? To = null, string? ProductSlug = null)
{
    /// <summary>Najwcześniejsza chwila zapłaty: północ dnia <see cref="From"/> w czasie polskim.</summary>
    public DateTimeOffset? PaidFrom => From is { } from ? AvailabilityService.MidnightInWarsaw(from) : null;

    /// <summary>Pierwsza chwila po zakresie: północ dnia po <see cref="To"/> w czasie polskim.</summary>
    public DateTimeOffset? PaidBefore => To is { } to ? AvailabilityService.MidnightInWarsaw(to.AddDays(1)) : null;
}
