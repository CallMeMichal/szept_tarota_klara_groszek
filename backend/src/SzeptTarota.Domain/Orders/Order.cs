using SzeptTarota.Domain.Catalog;

namespace SzeptTarota.Domain.Orders;

/// <summary>
/// Jeden zakup jednego lub kilku Produktów, opłacany jedną płatnością. Powstaje jako oczekujące na płatność
/// i trzyma kopię nazw i cen z chwili zakupu, imię i nazwisko Klienta i zaakceptowane zgody, więc późniejsze zmiany
/// katalogu go nie zmieniają.
/// </summary>
public sealed class Order
{
    public Order(
        Guid id,
        string email,
        string customerName,
        IReadOnlyList<OrderLine> lines,
        IReadOnlyList<AcceptedConsent> consents,
        string paymentSessionId,
        DateTimeOffset createdAt)
    {
        Id = id;
        Email = email;
        CustomerName = customerName;
        Lines = lines.ToList();
        Consents = consents.ToList();
        Total = lines.Sum(l => l.Price * l.Quantity);
        PaymentSessionId = paymentSessionId;
        CreatedAt = createdAt;
        Status = OrderStatus.PendingPayment;
    }

    // Dla EF Core.
    private Order()
    {
        Email = null!;
        CustomerName = null!;
        Lines = null!;
        Consents = null!;
        PaymentSessionId = null!;
    }

    public Guid Id { get; private set; }

    public OrderStatus Status { get; private set; }

    public string Email { get; private set; }

    /// <summary>Imię i nazwisko Klienta.</summary>
    public string CustomerName { get; private set; }

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

/// <summary>
/// Pozycja Zamówienia: Produkt z nazwą i ceną jednej sztuki (w groszach) z chwili zakupu, liczbą sztuk
/// oraz odpowiedziami z formularza Usługi (z etykietami pól z chwili zakupu).
/// </summary>
public sealed record OrderLine(string Slug, ProductKind Kind, string Name, int Price, string? EbookFileKey)
{
    public int Quantity { get; init; } = 1;

    // Poza konstruktorem, bo EF Core nie wiąże kolekcji przez parametry konstruktora.
    public List<FormAnswer> FormAnswers { get; init; } = [];
}

/// <summary>Odpowiedź z formularza Usługi: klucz pola, jego etykieta i wartość wpisana przez Klienta.</summary>
public sealed record FormAnswer(string Key, string Label, string Value);

/// <summary>Zgoda zaakceptowana przed płatnością: jej klucz, treść widziana przez Klienta i czas akceptacji.</summary>
public sealed record AcceptedConsent(string Key, string Text, DateTimeOffset AcceptedAt);
