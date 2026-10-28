using System.Net.Mail;
using Microsoft.Extensions.Options;
using SzeptTarota.Application.Cart;
using SzeptTarota.Application.Payments;
using SzeptTarota.Domain.Orders;

namespace SzeptTarota.Application.Orders;

/// <summary>
/// Rozpoczęcie płatności: ponownie sprawdza Koszyk, dane Klienta i zgody, tworzy Zamówienie
/// oczekujące na płatność i zakłada dla niego sesję płatności w bramce.
/// </summary>
/// <exception cref="ShopNotConfiguredException">Gdy brakuje konfiguracji sekcji <c>Shop</c>.</exception>
/// <exception cref="PaymentGatewayNotConfiguredException">Gdy brakuje kluczy bramki płatności.</exception>
public sealed class CheckoutService(
    CartValidator cartValidator,
    IPaymentGateway paymentGateway,
    IOrderStore orders,
    TimeProvider time,
    IOptions<ShopOptions> shopOptions)
{
    private const int MaxEmailLength = 200;
    private const int MaxNameLength = 200;

    public async Task<CheckoutResult> StartAsync(CheckoutCommand command, CancellationToken cancellationToken)
    {
        // Klient płaci dokładnie tyle, ile widział: każda zmiana ceny od ostatniego sprawdzenia wraca do Koszyka.
        var cart = cartValidator.Validate(command.Items);
        if (!cart.IsValid || cart.Lines.Any(l => l.Product!.PreviousPrice is not null))
        {
            return new CheckoutResult.CartChanged(cart);
        }

        var errors = ValidateCustomer(command);
        var missingConsents = cart.RequiredConsents.Where(c => !command.AcceptedConsents.Contains(c.Key)).ToList();
        if (missingConsents.Count > 0)
        {
            errors["acceptedConsents"] = missingConsents.Select(c => $"Zaznacz zgodę: {c.Text}").ToArray();
        }

        if (errors.Count > 0)
        {
            return new CheckoutResult.Invalid(errors);
        }

        var now = time.GetUtcNow();
        var orderId = Guid.NewGuid();
        var email = command.Email!.Trim();
        var lines = cart.Lines
            .Select(l => l.Product!)
            .Select(p => new OrderLine(p.Product.Slug, p.Product.Kind, p.Product.Name, p.Price, p.Product.EbookFileKey)
            {
                FormAnswers = p.FormAnswers.ToList(),
            })
            .ToList();

        // Bez adresu strony Stripe nie ma dokąd odesłać Klienta, a bez adresu Klary nie dowie się o Zamówieniu.
        var shop = shopOptions.Value;
        if (!Uri.TryCreate(shop.SiteUrl, UriKind.Absolute, out _) || string.IsNullOrWhiteSpace(shop.KlaraEmail))
        {
            throw new ShopNotConfiguredException();
        }

        var siteUrl = shop.SiteUrl.TrimEnd('/');
        var session = await paymentGateway.CreateCheckoutAsync(
            new PaymentSessionRequest(
                orderId,
                email,
                lines.Select(l => new CheckoutLine(l.Name, l.Price)).ToList(),
                SuccessUrl: new Uri($"{siteUrl}/zamowienie/{orderId}"),
                CancelUrl: new Uri($"{siteUrl}/koszyk?platnosc=przerwana")),
            cancellationToken);

        var order = new Order(
            orderId,
            email,
            command.Name!.Trim(),
            lines,
            cart.RequiredConsents.Select(c => new AcceptedConsent(c.Key, c.Text, now)).ToList(),
            session.Id,
            now);
        await orders.AddAsync(order, cancellationToken);

        return new CheckoutResult.Started(orderId, session.RedirectUrl);
    }

    private static Dictionary<string, string[]> ValidateCustomer(CheckoutCommand command)
    {
        var errors = new Dictionary<string, string[]>();

        var email = command.Email?.Trim();
        if (string.IsNullOrEmpty(email) || email.Length > MaxEmailLength
            || !MailAddress.TryCreate(email, out var address) || address.Address != email)
        {
            errors["email"] = ["Podaj poprawny adres email."];
        }

        // Imię trafia do maila Klary, więc bez znaków sterujących.
        var name = command.Name;
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > MaxNameLength || name.Any(char.IsControl))
        {
            errors["name"] = [$"Podaj imię i nazwisko (do {MaxNameLength} znaków)."];
        }

        return errors;
    }
}

/// <summary>Dane z formularza płatności: Koszyk z cenami, które Klient widział, imię i nazwisko, e-mail i zgody.</summary>
public sealed record CheckoutCommand(
    IReadOnlyList<CartItem> Items,
    string? Email,
    string? Name,
    IReadOnlyCollection<string> AcceptedConsents);

public abstract record CheckoutResult
{
    /// <summary>Zamówienie czeka na płatność; Klient idzie na <see cref="RedirectUrl"/>.</summary>
    public sealed record Started(Guid OrderId, Uri RedirectUrl) : CheckoutResult;

    /// <summary>Koszyk ma błędy albo zmieniły się ceny; Klient musi go zobaczyć ponownie.</summary>
    public sealed record CartChanged(CartValidation Cart) : CheckoutResult;

    /// <summary>Błędy w danych Klienta albo brak wymaganych zgód, po kluczach pól.</summary>
    public sealed record Invalid(Dictionary<string, string[]> Errors) : CheckoutResult;
}
