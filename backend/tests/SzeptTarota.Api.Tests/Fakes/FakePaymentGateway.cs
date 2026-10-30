using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SzeptTarota.Application.Payments;

namespace SzeptTarota.Api.Tests.Fakes;

/// <summary>
/// Zapamiętuje założone sesje płatności zamiast łączyć się ze Stripe i pozwala zbudować
/// podpisany webhook, jaki wysłałby Stripe, gdy Klient zapłaci albo sesja wygaśnie.
/// </summary>
public sealed class FakePaymentGateway : IPaymentGateway
{
    private readonly List<(PaymentSessionRequest Request, CheckoutSession Session)> _sessions = [];

    public IReadOnlyList<(PaymentSessionRequest Request, CheckoutSession Session)> Sessions => _sessions;

    /// <summary>Sesje, które strona wygasiła, zanim Klient zapłacił.</summary>
    public List<string> ExpiredSessions { get; } = [];

    /// <summary>Udaje brak kluczy Stripe w konfiguracji.</summary>
    public bool Configured { get; set; } = true;

    public Task<CheckoutSession> CreateCheckoutAsync(PaymentSessionRequest request, CancellationToken cancellationToken)
    {
        if (!Configured)
        {
            throw new PaymentGatewayNotConfiguredException();
        }

        // Równoczesne płatności w testach blokad Terminów.
        lock (_sessions)
        {
            var id = $"cs_test_{_sessions.Count + 1}_{Guid.NewGuid():N}";
            var session = new CheckoutSession(id, new Uri($"https://checkout.stripe.test/pay/{id}"));
            _sessions.Add((request, session));
            return Task.FromResult(session);
        }
    }

    public Task<bool> ExpireCheckoutAsync(string sessionId, CancellationToken cancellationToken)
    {
        if (!Configured)
        {
            throw new PaymentGatewayNotConfiguredException();
        }

        lock (_sessions)
        {
            ExpiredSessions.Add(sessionId);
        }

        return Task.FromResult(true);
    }

    public PaymentEvent? ReadWebhook(string payload, string? signature)
    {
        if (signature != Sign(payload))
        {
            throw new InvalidPaymentWebhookException("Zły podpis.");
        }

        var webhook = JsonSerializer.Deserialize<WebhookPayload>(payload)!;
        return webhook.Outcome is { } outcome ? new PaymentEvent(outcome, webhook.SessionId) : null;
    }

    /// <summary>Treść i podpis webhooka o sesji; <paramref name="outcome"/> <c>null</c> to zdarzenie, które nas nie dotyczy.</summary>
    public static (string Payload, string Signature) Webhook(string sessionId, PaymentOutcome? outcome)
    {
        var payload = JsonSerializer.Serialize(new WebhookPayload(sessionId, outcome));
        return (payload, Sign(payload));
    }

    public void Reset()
    {
        _sessions.Clear();
        ExpiredSessions.Clear();
        Configured = true;
    }

    private static string Sign(string payload) =>
        "fake=" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));

    private sealed record WebhookPayload(string SessionId, PaymentOutcome? Outcome);
}
