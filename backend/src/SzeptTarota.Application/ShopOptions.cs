namespace SzeptTarota.Application;

/// <summary>Konfiguracja sekcji <c>Shop</c>: adres strony (powroty ze Stripe) i adres Klary (maile o Zamówieniach).</summary>
public sealed class ShopOptions
{
    public const string SectionName = "Shop";

    /// <summary>Adres frontendu, np. <c>http://localhost:5173</c>; na niego Stripe odsyła Klienta po płatności.</summary>
    public string SiteUrl { get; set; } = "";

    /// <summary>Adres, na który przychodzą powiadomienia o nowych Zamówieniach.</summary>
    public string KlaraEmail { get; set; } = "";

    /// <summary>
    /// Czy Konto o adresie <paramref name="accountEmail"/> to Klara, czyli ma dostęp do panelu. Każde Konto ma adres
    /// potwierdzony przy logowaniu (Kod logowania albo Google z <c>email_verified</c>, ADR 0003).
    /// </summary>
    public bool IsKlara(string accountEmail) =>
        KlaraEmail.Trim() is { Length: > 0 } klara && string.Equals(accountEmail, klara, StringComparison.OrdinalIgnoreCase);
}

/// <summary>Sklep nie może jeszcze przyjmować płatności: brakuje <c>Shop:SiteUrl</c> albo <c>Shop:KlaraEmail</c>.</summary>
public sealed class ShopNotConfiguredException()
    : InvalidOperationException("Brak konfiguracji 'Shop:SiteUrl' albo 'Shop:KlaraEmail'.");
