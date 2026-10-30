namespace SzeptTarota.Application.Payments;

/// <summary>Bramka płatności (Stripe Checkout): sesje płatności za Zamówienia i ich podpisane powiadomienia.</summary>
public interface IPaymentGateway
{
    /// <summary>Zakłada sesję płatności za Zamówienie oczekujące na płatność i zwraca adres, na który trafia Klient.</summary>
    /// <exception cref="PaymentGatewayNotConfiguredException">Gdy nie ustawiono kluczy bramki.</exception>
    Task<CheckoutSession> CreateCheckoutAsync(PaymentSessionRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Wygasza sesję płatności, za którą Klient jeszcze nie zapłacił, żeby nie dało się już jej dokończyć;
    /// <c>true</c> także dla już wygasłej, <c>false</c>, gdy Klient jednak zapłacił.
    /// </summary>
    /// <exception cref="PaymentGatewayNotConfiguredException">Gdy nie ustawiono kluczy bramki.</exception>
    Task<bool> ExpireCheckoutAsync(string sessionId, CancellationToken cancellationToken);

    /// <summary>
    /// Sprawdza podpis powiadomienia (webhooka) i odczytuje, co stało się z sesją płatności.
    /// Zwraca <c>null</c> dla powiadomień, które nie zmieniają Zamówienia.
    /// </summary>
    /// <exception cref="InvalidPaymentWebhookException">Gdy podpis się nie zgadza albo treść jest nieczytelna.</exception>
    /// <exception cref="PaymentGatewayNotConfiguredException">Gdy nie ustawiono sekretu webhooków.</exception>
    PaymentEvent? ReadWebhook(string payload, string? signature);
}

/// <summary>Pozycja do zapłaty: nazwa, cena jednej sztuki w groszach i liczba sztuk.</summary>
public sealed record CheckoutLine(string Name, int Price, int Quantity);

public sealed record PaymentSessionRequest(
    Guid OrderId,
    string Email,
    IReadOnlyList<CheckoutLine> Lines,
    Uri SuccessUrl,
    Uri CancelUrl);

/// <summary>Założona sesja płatności: jej identyfikator w bramce i adres strony płatności.</summary>
public sealed record CheckoutSession(string Id, Uri RedirectUrl);

/// <summary>Co bramka zgłosiła dla sesji płatności.</summary>
public sealed record PaymentEvent(PaymentOutcome Outcome, string SessionId);

public enum PaymentOutcome
{
    /// <summary>Pieniądze są zapłacone.</summary>
    Paid,

    /// <summary>Sesja wygasła albo płatność się nie udała; Klient nie zapłacił.</summary>
    Expired,
}

public sealed class PaymentGatewayNotConfiguredException()
    : InvalidOperationException("Brak konfiguracji bramki płatności ('Stripe:SecretKey' albo 'Stripe:WebhookSecret').");

public sealed class InvalidPaymentWebhookException(string message, Exception? inner = null)
    : Exception(message, inner);
