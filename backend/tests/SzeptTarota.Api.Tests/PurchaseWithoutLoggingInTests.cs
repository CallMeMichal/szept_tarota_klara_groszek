using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using SzeptTarota.Api.Tests.Fakes;
using SzeptTarota.Api.Tests.Infrastructure;
using SzeptTarota.Application.Catalog;
using SzeptTarota.Application.Payments;

namespace SzeptTarota.Api.Tests;

/// <summary>
/// Zakup bez logowania (ADR 0004): Klient podaje e-mail w formularzu płatności. Zamówienie należy do Konta, gdy Klient
/// był zalogowany, gdy na ten adres istnieje Konto albo gdy poprosił o założenie Konta; inaczej jest bez Konta.
/// </summary>
[Collection(ApiCollection.Name)]
public class PurchaseWithoutLoggingInTests : IAsyncLifetime
{
    private readonly string _email = $"bez-konta-{Guid.NewGuid():N}@example.com";

    private readonly ApiFactory _factory;
    private readonly WebApplicationFactory<Program> _api;

    public PurchaseWithoutLoggingInTests(ApiFactory factory)
    {
        _factory = factory;
        _api = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.ReplaceWithFake<IProductCatalog>(new FakeProductCatalog())));
    }

    public Task InitializeAsync()
    {
        _factory.Payments.Reset();
        _factory.Emails.Reset();
        return Task.CompletedTask;
    }

    public Task DisposeAsync() => _api.DisposeAsync().AsTask();

    [Fact]
    public async Task A_Klient_can_pay_without_logging_in_and_gets_the_confirmation_with_the_Ebook()
    {
        var visitor = _api.CreateBrowser();

        var response = await visitor.PostAsJsonAsync("/api/checkout", Checkout());
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var started = (await response.Content.ReadFromJsonAsync<CheckoutStartedResponse>())!;
        await Pay(started.OrderId);

        Assert.Equal(_email, Assert.Single(_factory.Payments.Sessions).Request.Email);
        var mail = Assert.Single(_factory.Emails.Sent, m => m.To == _email);
        Assert.Contains("Tarot dla początkujących", mail.Html);
        Assert.Single(mail.Attachments);
    }

    [Fact]
    public async Task An_Order_paid_without_logging_in_goes_to_the_Konto_that_already_exists_on_that_email()
    {
        var klient = await _api.LoggedInClient(_factory.Emails, _email);

        var order = await BuyWithoutLoggingIn();
        await Pay(order);

        Assert.Equal([order], await MyOrders(klient));
    }

    [Fact]
    public async Task Without_a_Konto_and_without_asking_for_one_the_Order_stays_without_a_Konto_for_good()
    {
        var order = await BuyWithoutLoggingIn();
        await Pay(order);

        var later = await _api.LoggedInClient(_factory.Emails, _email);

        Assert.Empty(await MyOrders(later));
    }

    [Fact]
    public async Task Asking_for_a_Konto_creates_it_and_the_mail_says_so()
    {
        var order = await BuyWithoutLoggingIn(createAccount: true);
        await Pay(order);

        var html = Assert.Single(_factory.Emails.Sent, m => m.To == _email).Html;
        Assert.Contains("Założyłam Ci Konto", html);
        var klient = await _api.LoggedInClient(_factory.Emails, _email);
        Assert.Equal([order], await MyOrders(klient));
    }

    [Fact]
    public async Task The_Konto_is_created_even_when_the_payment_never_goes_through()
    {
        var unpaid = await BuyWithoutLoggingIn(createAccount: true);
        await Expire(unpaid);

        // Konto już jest, więc kolejny zakup bez zaznaczonego pola i tak na nie trafia.
        var next = await BuyWithoutLoggingIn();
        await Pay(next);

        var html = Assert.Single(_factory.Emails.Sent, m => m.To == _email).Html;
        Assert.DoesNotContain("Założyłam Ci Konto", html);
        var klient = await _api.LoggedInClient(_factory.Emails, _email);
        Assert.Equal([next], await MyOrders(klient));
    }

    [Fact]
    public async Task Asking_for_a_Konto_that_already_exists_does_not_say_it_was_created()
    {
        var klient = await _api.LoggedInClient(_factory.Emails, _email);
        _factory.Emails.Reset();

        var order = await BuyWithoutLoggingIn(createAccount: true);
        await Pay(order);

        Assert.DoesNotContain("Założyłam Ci Konto", Assert.Single(_factory.Emails.Sent, m => m.To == _email).Html);
        Assert.Equal([order], await MyOrders(klient));
    }

    [Fact]
    public async Task The_page_after_payment_shows_anyone_with_the_link_a_summary_without_personal_data()
    {
        var order = await BuyWithoutLoggingIn();
        await Pay(order);

        var response = await _api.CreateBrowser().GetAsync($"/api/orders/{order}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"status\":\"paid\"", json);
        Assert.Contains("Tarot dla początkujących", json);
        Assert.DoesNotContain(_email, json);
        Assert.DoesNotContain("Nowak", json);
    }

    private async Task<Guid> BuyWithoutLoggingIn(bool? createAccount = null)
    {
        var response = await _api.CreateBrowser().PostAsJsonAsync("/api/checkout", Checkout(createAccount));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CheckoutStartedResponse>())!.OrderId;
    }

    private static async Task<List<Guid>> MyOrders(HttpClient klient)
    {
        var response = await klient.GetAsync("/api/account/orders");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<List<OrderIdResponse>>())!.Select(o => o.Id).ToList();
    }

    private sealed record OrderIdResponse(Guid Id);

    private Dictionary<string, object?> Checkout(bool? createAccount = null) => new()
    {
        ["items"] = new[] { new { slug = FakeProductCatalog.EbookSlug, price = 4900 } },
        ["email"] = _email,
        ["name"] = "Ewa Nowak",
        ["acceptedConsents"] = new[] { "termsAndPrivacy" },
        ["createAccount"] = createAccount,
    };

    private Task Pay(Guid orderId) => SendWebhook(orderId, PaymentOutcome.Paid);

    private Task Expire(Guid orderId) => SendWebhook(orderId, PaymentOutcome.Expired);

    private async Task SendWebhook(Guid orderId, PaymentOutcome outcome)
    {
        var sessionId = _factory.Payments.Sessions.Single(s => s.Request.OrderId == orderId).Session.Id;
        var (payload, signature) = FakePaymentGateway.Webhook(sessionId, outcome);
        var response = await _api.CreateClient().SendAsync(new HttpRequestMessage(HttpMethod.Post, "/api/payments/stripe/webhook")
        {
            Content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json"),
            Headers = { { "Stripe-Signature", signature } },
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private sealed record CheckoutStartedResponse(Guid OrderId, Uri RedirectUrl);
}
