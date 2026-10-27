using System.Net.Mail;
using System.Text.RegularExpressions;
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
public sealed partial class CheckoutService(
    CartValidator cartValidator,
    IPaymentGateway paymentGateway,
    IOrderStore orders,
    TimeProvider time,
    IOptions<ShopOptions> shopOptions)
{
    private const int MaxEmailLength = 200;
    private const int MaxFieldLength = 200;

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
            .Select(p => new OrderLine(p.Product.Slug, p.Product.Kind, p.Product.Name, p.Price, p.Product.EbookFileKey))
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

        var billing = command.Billing!;
        var order = new Order(
            orderId,
            email,
            new BillingDetails(
                billing.Name!.Trim(),
                billing.Street!.Trim(),
                billing.PostalCode!.Trim(),
                billing.City!.Trim(),
                NormalizeTaxId(billing.TaxId)),
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

        var billing = command.Billing ?? new BillingInput(null, null, null, null, null);
        RequireText(errors, "billing.name", billing.Name, "Podaj imię i nazwisko albo nazwę firmy.");
        RequireText(errors, "billing.street", billing.Street, "Podaj ulicę i numer.");
        RequireText(errors, "billing.city", billing.City, "Podaj miejscowość.");

        if (billing.PostalCode is null || !PostalCodePattern().IsMatch(billing.PostalCode.Trim()))
        {
            errors["billing.postalCode"] = ["Podaj kod pocztowy w formacie 00-000."];
        }

        if (!string.IsNullOrWhiteSpace(billing.TaxId) && !TaxIdPattern().IsMatch(NormalizeTaxId(billing.TaxId)!))
        {
            errors["billing.taxId"] = ["NIP to 10 cyfr."];
        }

        return errors;
    }

    private static void RequireText(Dictionary<string, string[]> errors, string key, string? value, string message)
    {
        // Dane trafiają do maili i na fakturę, więc bez znaków sterujących.
        if (string.IsNullOrWhiteSpace(value) || value.Trim().Length > MaxFieldLength || value.Any(char.IsControl))
        {
            errors[key] = [$"{message} (do {MaxFieldLength} znaków)"];
        }
    }

    private static string? NormalizeTaxId(string? taxId) =>
        string.IsNullOrWhiteSpace(taxId) ? null : taxId.Replace("-", "").Replace(" ", "").Trim();

    [GeneratedRegex("^[0-9]{2}-[0-9]{3}$")]
    private static partial Regex PostalCodePattern();

    [GeneratedRegex("^[0-9]{10}$")]
    private static partial Regex TaxIdPattern();
}

/// <summary>Dane z formularza płatności: Koszyk z cenami, które Klient widział, e-mail, dane do faktury i zgody.</summary>
public sealed record CheckoutCommand(
    IReadOnlyList<CartItem> Items,
    string? Email,
    BillingInput? Billing,
    IReadOnlyCollection<string> AcceptedConsents);

public sealed record BillingInput(string? Name, string? Street, string? PostalCode, string? City, string? TaxId);

public abstract record CheckoutResult
{
    /// <summary>Zamówienie czeka na płatność; Klient idzie na <see cref="RedirectUrl"/>.</summary>
    public sealed record Started(Guid OrderId, Uri RedirectUrl) : CheckoutResult;

    /// <summary>Koszyk ma błędy albo zmieniły się ceny; Klient musi go zobaczyć ponownie.</summary>
    public sealed record CartChanged(CartValidation Cart) : CheckoutResult;

    /// <summary>Błędy w danych Klienta albo brak wymaganych zgód, po kluczach pól.</summary>
    public sealed record Invalid(Dictionary<string, string[]> Errors) : CheckoutResult;
}
