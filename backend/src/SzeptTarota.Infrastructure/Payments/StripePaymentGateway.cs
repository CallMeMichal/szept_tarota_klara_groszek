using Microsoft.Extensions.Options;
using Stripe;
using Stripe.Checkout;
using SzeptTarota.Application.Payments;

namespace SzeptTarota.Infrastructure.Payments;

/// <summary>Konfiguracja sekcji <c>Stripe</c>; klucze trzymamy w user-secrets albo zmiennych środowiskowych.</summary>
public sealed class StripeOptions
{
    public const string SectionName = "Stripe";

    /// <summary>Klucz tajny API (<c>sk_test_…</c> / <c>sk_live_…</c>).</summary>
    public string? SecretKey { get; set; }

    /// <summary>Sekret podpisywania webhooków (<c>whsec_…</c>), np. z <c>stripe listen</c>.</summary>
    public string? WebhookSecret { get; set; }
}

/// <summary>Płatności przez Stripe Checkout: BLIK, Przelewy24 i karta, w złotówkach.</summary>
public sealed class StripePaymentGateway(IOptions<StripeOptions> options, TimeProvider time) : IPaymentGateway
{
    private const string Currency = "pln";

    /// <summary>
    /// Czas życia sesji Checkout: Stripe przyjmuje najmniej 30 minut od utworzenia sesji,
    /// minuta zapasu chroni przed odrzuceniem przez opóźnienie sieci albo różnicę zegarów.
    /// </summary>
    private static readonly TimeSpan SessionLifetime = TimeSpan.FromMinutes(31);

    public async Task<CheckoutSession> CreateCheckoutAsync(PaymentSessionRequest request, CancellationToken cancellationToken)
    {
        var secretKey = options.Value.SecretKey;
        if (string.IsNullOrWhiteSpace(secretKey))
        {
            throw new PaymentGatewayNotConfiguredException();
        }

        var sessions = new SessionService(new StripeClient(secretKey));
        var session = await sessions.CreateAsync(
            new SessionCreateOptions
            {
                Mode = "payment",
                PaymentMethodTypes = ["blik", "p24", "card"],
                Currency = Currency,
                Locale = "pl",
                CustomerEmail = request.Email,
                ClientReferenceId = request.OrderId.ToString(),
                LineItems = request.Lines.Select(line => new SessionLineItemOptions
                {
                    Quantity = line.Quantity,
                    PriceData = new SessionLineItemPriceDataOptions
                    {
                        Currency = Currency,
                        UnitAmount = line.Price,
                        ProductData = new SessionLineItemPriceDataProductDataOptions { Name = line.Name },
                    },
                }).ToList(),
                SuccessUrl = request.SuccessUrl.ToString(),
                CancelUrl = request.CancelUrl.ToString(),
                ExpiresAt = time.GetUtcNow().Add(SessionLifetime).UtcDateTime,
            },
            cancellationToken: cancellationToken);

        return new CheckoutSession(session.Id, new Uri(session.Url));
    }

    public async Task<bool> ExpireCheckoutAsync(string sessionId, CancellationToken cancellationToken)
    {
        var secretKey = options.Value.SecretKey;
        if (string.IsNullOrWhiteSpace(secretKey))
        {
            throw new PaymentGatewayNotConfiguredException();
        }

        // Wygasić można tylko otwartą sesję; już wygasła też nie da się zapłacić, a zapłaconej nie ruszamy.
        var sessions = new SessionService(new StripeClient(secretKey));
        var session = await sessions.GetAsync(sessionId, cancellationToken: cancellationToken);
        if (session.Status != "open")
        {
            return session.Status == "expired";
        }

        try
        {
            await sessions.ExpireAsync(sessionId, cancellationToken: cancellationToken);
            return true;
        }
        catch (StripeException)
        {
            // Sesja zamknęła się między sprawdzeniem a wygaszeniem (np. Klient jednak zapłacił): o tym zdecyduje webhook.
            return false;
        }
    }

    public PaymentEvent? ReadWebhook(string payload, string? signature)
    {
        var webhookSecret = options.Value.WebhookSecret;
        if (string.IsNullOrWhiteSpace(webhookSecret))
        {
            throw new PaymentGatewayNotConfiguredException();
        }

        if (string.IsNullOrEmpty(signature))
        {
            throw new InvalidPaymentWebhookException("Brak nagłówka Stripe-Signature.");
        }

        Event stripeEvent;
        try
        {
            stripeEvent = EventUtility.ConstructEvent(
                payload,
                signature,
                webhookSecret,
                tolerance: 300,
                utcNow: time.GetUtcNow().ToUnixTimeSeconds(),
                throwOnApiVersionMismatch: false);
        }
        catch (StripeException exception)
        {
            throw new InvalidPaymentWebhookException("Niepoprawny webhook Stripe.", exception);
        }

        if (stripeEvent.Data.Object is not Session session)
        {
            return null;
        }

        return stripeEvent.Type switch
        {
            // BLIK i karta płacą od razu; przy metodach opóźnionych przyjdzie jeszcze async_payment_succeeded.
            EventTypes.CheckoutSessionCompleted when session.PaymentStatus == "paid" => new PaymentEvent(PaymentOutcome.Paid, session.Id),
            EventTypes.CheckoutSessionAsyncPaymentSucceeded => new PaymentEvent(PaymentOutcome.Paid, session.Id),
            EventTypes.CheckoutSessionExpired or EventTypes.CheckoutSessionAsyncPaymentFailed => new PaymentEvent(PaymentOutcome.Expired, session.Id),
            _ => null,
        };
    }
}
