using System.Net.Mail;
using Microsoft.Extensions.Options;
using SzeptTarota.Application.Accounts;
using SzeptTarota.Application.Appointments;
using SzeptTarota.Application.Cart;
using SzeptTarota.Application.Payments;
using SzeptTarota.Domain.Accounts;
using SzeptTarota.Domain.Orders;

namespace SzeptTarota.Application.Orders;

/// <summary>
/// Rozpoczęcie płatności, z logowaniem albo bez (ADR 0004): ponownie sprawdza Koszyk, dane Klienta i zgody, tworzy
/// Zamówienie oczekujące na płatność, blokuje jego Terminy na <see cref="HoldDuration"/> i zakłada dla niego sesję
/// płatności w bramce. Zamówienie zalogowanego należy do jego Konta. Potwierdzenie idzie na e-mail z formularza:
/// zalogowanemu domyślnie adres Konta, ale może podać inny.
/// </summary>
/// <exception cref="ShopNotConfiguredException">Gdy brakuje konfiguracji sekcji <c>Shop</c>.</exception>
/// <exception cref="PaymentGatewayNotConfiguredException">Gdy brakuje kluczy bramki płatności.</exception>
public sealed class CheckoutService(
    CartValidator cartValidator,
    IAccountStore accounts,
    IAppointmentStore appointments,
    IPaymentGateway paymentGateway,
    IOrderStore orders,
    TimeProvider time,
    IOptions<ShopOptions> shopOptions)
{
    private const int MaxEmailLength = 200;
    private const int MaxNameLength = 200;

    /// <summary>Jak długo wybrane Terminy czekają na płatność, zanim wrócą do puli.</summary>
    public static readonly TimeSpan HoldDuration = TimeSpan.FromMinutes(15);

    public async Task<CheckoutResult> StartAsync(CheckoutCommand command, CancellationToken cancellationToken)
    {
        Account? account = null;
        if (command.AccountId is { } accountId)
        {
            if (await accounts.FindAsync(accountId, cancellationToken) is not { } loggedIn)
            {
                return new CheckoutResult.NotLoggedIn();
            }

            account = loggedIn;
        }

        // Klient płaci dokładnie tyle, ile widział: każda zmiana ceny od ostatniego sprawdzenia wraca do Koszyka.
        var cart = await cartValidator.ValidateAsync(command.Items, cancellationToken);
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

        var email = command.Email!.Trim();
        var now = time.GetUtcNow();
        var orderId = Guid.NewGuid();
        var lines = cart.Lines
            .Select(l => new OrderLine(l.Product!.Product.Slug, l.Product.Product.Kind, l.Product.Product.Name, l.Product.Price, l.Product.Product.EbookFileKey)
            {
                Quantity = l.Quantity,
                FormAnswers = l.Product.FormAnswers.ToList(),
                // PostgreSQL przechowuje czas w UTC.
                AppointmentStart = l.Product.Appointment?.Start.ToUniversalTime(),
                AppointmentEnd = l.Product.Appointment?.End.ToUniversalTime(),
            })
            .ToList();

        // Bez adresu strony Stripe nie ma dokąd odesłać Klienta, a bez adresu Klary nie dowie się o Zamówieniu.
        var shop = shopOptions.Value;
        if (!Uri.TryCreate(shop.SiteUrl, UriKind.Absolute, out _) || string.IsNullOrWhiteSpace(shop.KlaraEmail))
        {
            throw new ShopNotConfiguredException();
        }

        // Baza przepuszcza tylko jedną z równoczesnych płatności za ten sam czas; przegrana nic nie tworzy.
        var holds = lines
            .Select((line, index) => (line, index))
            .Where(l => l.line is { AppointmentStart: not null, AppointmentEnd: not null })
            .Select(l => new OrderAppointment(
                Guid.NewGuid(), orderId, l.index, l.line.AppointmentStart!.Value, l.line.AppointmentEnd!.Value, now + HoldDuration))
            .ToList();
        if (holds.Count > 0 && await appointments.TryHoldAsync(holds, now, cancellationToken) is { } takenIndex)
        {
            return new CheckoutResult.CartChanged(cart.WithError(holds[takenIndex].LineIndex, CartError.AppointmentTaken));
        }

        try
        {
            var siteUrl = shop.SiteUrl.TrimEnd('/');
            var session = await paymentGateway.CreateCheckoutAsync(
                new PaymentSessionRequest(
                    orderId,
                    email,
                    lines.Select(l => new CheckoutLine(l.Name, l.Price, l.Quantity)).ToList(),
                    SuccessUrl: new Uri($"{siteUrl}/zamowienie/{orderId}"),
                    CancelUrl: new Uri($"{siteUrl}/koszyk?platnosc=przerwana&zamowienie={orderId}")),
                cancellationToken);

            // Kto nie zalogował się, ale ma Konto na ten adres, i tak dostaje Zamówienie na swoje Konto, a kto poprosił
            // o Konto, dostaje je razem z Zamówieniem, nawet gdy potem nie zapłaci (ADR 0004).
            var accountCreated = false;
            if (account is null)
            {
                var accountEmail = email.ToLowerInvariant();
                account = await accounts.FindByEmailAsync(accountEmail, cancellationToken);
                if (account is null && command.CreateAccount)
                {
                    account = await accounts.GetOrCreateAsync(accountEmail, now, cancellationToken);
                    accountCreated = true;
                }
            }

            var order = new Order(
                orderId,
                account?.Id,
                email,
                command.Name!.Trim(),
                lines,
                cart.RequiredConsents.Select(c => new AcceptedConsent(c.Key, c.Text, now)).ToList(),
                session.Id,
                now,
                accountCreated);
            await orders.AddAsync(order, cancellationToken);

            return new CheckoutResult.Started(orderId, session.RedirectUrl);
        }
        catch
        {
            // Bez Zamówienia nikt za te Terminy nie zapłaci.
            await appointments.ReleaseAsync(orderId, now, CancellationToken.None);
            throw;
        }
    }

    /// <summary>
    /// Klient wrócił ze Stripe, nie płacąc: sesja płatności wygasa, żeby nie dało się już jej dokończyć, a Terminy
    /// Zamówienia od razu wracają do puli, zamiast czekać na koniec blokady (ADR 0004). Zapłaconego nie zmienia.
    /// </summary>
    public async Task CancelAsync(Guid orderId, CancellationToken cancellationToken)
    {
        if (await orders.FindAsync(orderId, cancellationToken) is not { Status: OrderStatus.PendingPayment } order
            || !await paymentGateway.ExpireCheckoutAsync(order.PaymentSessionId, cancellationToken))
        {
            return;
        }

        var now = time.GetUtcNow();
        await orders.MarkExpiredAsync(order.Id, now, cancellationToken);
        await appointments.ReleaseAsync(order.Id, now, cancellationToken);
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

/// <summary>
/// Dane z formularza płatności: Koszyk z cenami, które Klient widział, e-mail, imię i nazwisko i zgody;
/// <see cref="AccountId"/> to Konto zalogowanego Klienta (brak, gdy płaci bez logowania), a <see cref="CreateAccount"/>
/// to prośba niezalogowanego o założenie Konta.
/// </summary>
public sealed record CheckoutCommand(
    IReadOnlyList<CartItem> Items,
    Guid? AccountId,
    string? Email,
    string? Name,
    IReadOnlyCollection<string> AcceptedConsents,
    bool CreateAccount = false);

public abstract record CheckoutResult
{
    /// <summary>Zamówienie czeka na płatność; Klient idzie na <see cref="RedirectUrl"/>.</summary>
    public sealed record Started(Guid OrderId, Uri RedirectUrl) : CheckoutResult;

    /// <summary>Koszyk ma błędy albo zmieniły się ceny; Klient musi go zobaczyć ponownie.</summary>
    public sealed record CartChanged(CartValidation Cart) : CheckoutResult;

    /// <summary>Konta z sesji już nie ma; Klient musi zalogować się ponownie.</summary>
    public sealed record NotLoggedIn : CheckoutResult;

    /// <summary>Błędy w danych Klienta albo brak wymaganych zgód, po kluczach pól.</summary>
    public sealed record Invalid(Dictionary<string, string[]> Errors) : CheckoutResult;
}
