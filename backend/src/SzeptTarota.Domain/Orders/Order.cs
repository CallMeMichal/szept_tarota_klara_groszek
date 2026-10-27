using SzeptTarota.Domain.Catalog;

namespace SzeptTarota.Domain.Orders;

/// <summary>
/// Jeden zakup jednego lub kilku Produktów, opłacany jedną płatnością. Powstaje jako oczekujące na płatność
/// i trzyma kopię nazw i cen z chwili zakupu, zaakceptowane zgody i dane do faktury, więc późniejsze zmiany
/// katalogu go nie zmieniają.
/// </summary>
public sealed class Order
{
    public Order(
        Guid id,
        string email,
        BillingDetails billing,
        IReadOnlyList<OrderLine> lines,
        IReadOnlyList<AcceptedConsent> consents,
        string paymentSessionId,
        DateTimeOffset createdAt)
    {
        Id = id;
        Email = email;
        Billing = billing;
        Lines = lines.ToList();
        Consents = consents.ToList();
        Total = lines.Sum(l => l.Price);
        PaymentSessionId = paymentSessionId;
        CreatedAt = createdAt;
        Status = OrderStatus.PendingPayment;
    }

    // Dla EF Core.
    private Order()
    {
        Email = null!;
        Billing = null!;
        Lines = null!;
        Consents = null!;
        PaymentSessionId = null!;
    }

    public Guid Id { get; private set; }

    public OrderStatus Status { get; private set; }

    public string Email { get; private set; }

    public BillingDetails Billing { get; private set; }

    public List<OrderLine> Lines { get; private set; }

    public List<AcceptedConsent> Consents { get; private set; }

    /// <summary>Suma do zapłaty w groszach.</summary>
    public int Total { get; private set; }

    /// <summary>Identyfikator sesji płatności w bramce (Stripe Checkout).</summary>
    public string PaymentSessionId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? PaidAt { get; private set; }

    public DateTimeOffset? ExpiredAt { get; private set; }

    /// <summary>Kiedy wysłano maile o opłaconym Zamówieniu (do Klienta i do Klary).</summary>
    public DateTimeOffset? NotificationsSentAt { get; private set; }
}

public enum OrderStatus
{
    PendingPayment,
    Paid,
    Expired,
}

/// <summary>Pozycja Zamówienia: Produkt z nazwą i ceną (w groszach) z chwili zakupu.</summary>
public sealed record OrderLine(string Slug, ProductKind Kind, string Name, int Price, string? EbookFileKey);

/// <summary>Dane do faktury; <see cref="TaxId"/> (NIP) tylko przy zakupie na firmę.</summary>
public sealed record BillingDetails(string Name, string Street, string PostalCode, string City, string? TaxId);

/// <summary>Zgoda zaakceptowana przed płatnością: jej klucz, treść widziana przez Klienta i czas akceptacji.</summary>
public sealed record AcceptedConsent(string Key, string Text, DateTimeOffset AcceptedAt);
