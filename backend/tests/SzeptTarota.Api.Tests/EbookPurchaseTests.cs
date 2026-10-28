using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using SzeptTarota.Api.Tests.Fakes;
using SzeptTarota.Api.Tests.Infrastructure;
using SzeptTarota.Application.Catalog;
using SzeptTarota.Application.Payments;

namespace SzeptTarota.Api.Tests;

[Collection(ApiCollection.Name)]
public class EbookPurchaseTests : IAsyncLifetime
{
    // Każdy test na własny adres: linków logowania na jeden adres jest najwyżej kilka na 15 minut.
    private readonly string _email = $"anna-{Guid.NewGuid():N}@example.com";

    private static readonly string[] RequiredConsents = ["termsAndPrivacy"];

    private readonly ApiFactory _factory;
    private readonly FakeProductCatalog _catalog = new();
    private readonly WebApplicationFactory<Program> _api;

    public EbookPurchaseTests(ApiFactory factory)
    {
        _factory = factory;
        _factory.Payments.Reset();
        _factory.Emails.Reset();
        _api = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.ReplaceWithFake<IProductCatalog>(_catalog)));
    }

    private HttpClient _client = null!;

    /// <summary>Klient płaci po zalogowaniu; mail z linkiem logowania nie liczy się do maili w testach.</summary>
    public async Task InitializeAsync()
    {
        _client = await _api.LoggedInClient(_factory.Emails, _email);
        _factory.Emails.Reset();
    }

    public Task DisposeAsync() => _api.DisposeAsync().AsTask();

    [Fact]
    public async Task Starting_checkout_creates_an_Order_awaiting_payment_and_sends_the_Klient_to_Stripe()
    {
        var response = await StartCheckout(Checkout(
            [Item(FakeProductCatalog.EbookSlug, 4900), Item(FakeProductCatalog.OtherEbookSlug, 3900)]));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var started = (await response.Content.ReadFromJsonAsync<CheckoutStartedResponse>())!;
        var (request, session) = Assert.Single(_factory.Payments.Sessions);
        Assert.Equal(session.RedirectUrl, started.RedirectUrl);
        Assert.Equal(_email, request.Email);
        Assert.Equal(
            [("Tarot dla początkujących", 4900), ("Rytuały Księżyca", 3900)],
            request.Lines.Select(l => (l.Name, l.Price)));
        Assert.Equal(new Uri($"{ApiFactory.SiteUrl}/zamowienie/{started.OrderId}"), request.SuccessUrl);
        Assert.Equal(new Uri($"{ApiFactory.SiteUrl}/koszyk?platnosc=przerwana"), request.CancelUrl);

        var order = await GetOrder(started.OrderId);
        Assert.Equal("pendingPayment", order.Status);
        Assert.Equal(8800, order.Total);
    }

    [Fact]
    public async Task Without_the_required_consents_payment_does_not_start()
    {
        var response = await StartCheckout(Checkout([Item(FakeProductCatalog.EbookSlug, 4900)], consents: []));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemResponse>();
        Assert.Equal(["acceptedConsents"], problem!.Errors.Keys);
        Assert.Empty(_factory.Payments.Sessions);
    }

    [Fact]
    public async Task A_missing_name_is_an_error_on_its_field()
    {
        var checkout = Checkout([Item(FakeProductCatalog.EbookSlug, 4900)]);
        checkout["name"] = " ";

        var response = await StartCheckout(checkout);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemResponse>();
        Assert.Equal(["name"], problem!.Errors.Keys);
        Assert.Empty(_factory.Payments.Sessions);
    }

    [Fact]
    public async Task A_price_changed_since_the_Klient_saw_it_sends_them_back_to_the_cart()
    {
        _catalog.SetPrice(FakeProductCatalog.EbookSlug, 3500);

        var response = await StartCheckout(Checkout([Item(FakeProductCatalog.EbookSlug, 4900)]));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var cart = (await response.Content.ReadFromJsonAsync<CartResponse>())!;
        var line = Assert.Single(cart.Items);
        Assert.Equal(3500, line.Price);
        Assert.Equal(4900, line.PreviousPrice);
        Assert.Empty(_factory.Payments.Sessions);
    }

    [Fact]
    public async Task A_cart_with_errors_sends_the_Klient_back_to_the_cart()
    {
        var response = await StartCheckout(Checkout(
            [Item(FakeProductCatalog.EbookSlug, 4900), Item("nie-ma-takiego-ebooka", 4900)]));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var cart = (await response.Content.ReadFromJsonAsync<CartResponse>())!;
        Assert.False(cart.IsValid);
        Assert.Empty(_factory.Payments.Sessions);
    }

    [Fact]
    public async Task Answers_service_unavailable_until_Stripe_is_configured()
    {
        _factory.Payments.Configured = false;

        var response = await StartCheckout(Checkout([Item(FakeProductCatalog.EbookSlug, 4900)]));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task Answers_service_unavailable_until_Klaras_email_is_configured()
    {
        // Bez adresu strony nie da się nawet zalogować (AccountTests), więc tu brakuje tylko adresu Klary.
        await using var unconfigured = _api.WithWebHostBuilder(builder => builder.UseSetting("Shop:KlaraEmail", ""));
        var client = await unconfigured.LoggedInClient(_factory.Emails, _email);

        var response = await client.PostAsJsonAsync(
            "/api/checkout", Checkout([Item(FakeProductCatalog.EbookSlug, 4900)]));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Empty(_factory.Payments.Sessions);
    }

    [Fact]
    public async Task Confirmed_payment_marks_the_Order_paid_and_emails_the_Klient_their_Ebooks_and_Klara_the_Order()
    {
        var started = await StartPurchase(
            Item(FakeProductCatalog.EbookSlug, 4900), Item(FakeProductCatalog.OtherEbookSlug, 3900));

        var response = await SendWebhook(started, PaymentOutcome.Paid);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("paid", (await GetOrder(started.OrderId)).Status);

        var toKlient = Assert.Single(_factory.Emails.Sent, m => m.To == _email);
        Assert.Contains("Tarot dla początkujących", toKlient.Html);
        Assert.Contains("88,00 zł", toKlient.Html);
        Assert.Equal(
            [
                ("tarot-dla-poczatkujacych.pdf", FakeEbookStorage.ContentOf("tarot-dla-poczatkujacych.pdf")),
                ("rytualy-ksiezyca.pdf", FakeEbookStorage.ContentOf("rytualy-ksiezyca.pdf")),
            ],
            toKlient.Attachments.Select(a => (a.FileName, a.Content)));
        Assert.All(toKlient.Attachments, a => Assert.Equal("application/pdf", a.ContentType));

        var toKlara = Assert.Single(_factory.Emails.Sent, m => m.To == ApiFactory.KlaraEmail);
        Assert.Contains(_email, toKlara.Html);
        Assert.Contains("Anna Nowak", toKlara.Html);
        Assert.Contains("Rytuały Księżyca", toKlara.Html);
        Assert.Contains("88,00 zł", toKlara.Html);
        Assert.Empty(toKlara.Attachments);
        Assert.Equal(2, _factory.Emails.Sent.Count);
    }

    [Fact]
    public async Task Several_copies_of_an_Ebook_are_paid_once_per_copy_and_the_file_is_attached_once()
    {
        var started = await StartPurchase(new { slug = FakeProductCatalog.EbookSlug, price = 4900, quantity = 3 });

        var request = Assert.Single(_factory.Payments.Sessions).Request;
        var line = Assert.Single(request.Lines);
        Assert.Equal(("Tarot dla początkujących", 4900, 3), (line.Name, line.Price, line.Quantity));

        await SendWebhook(started, PaymentOutcome.Paid);

        var order = await GetOrder(started.OrderId);
        Assert.Equal(14700, order.Total);
        Assert.Equal((3, 4900), (Assert.Single(order.Items).Quantity, order.Items[0].Price));
        var toKlient = Assert.Single(_factory.Emails.Sent, m => m.To == _email);
        Assert.Contains("3 × Tarot dla początkujących", toKlient.Html);
        Assert.Contains("147,00 zł", toKlient.Html);
        Assert.Single(toKlient.Attachments);
        Assert.Contains("3 × Tarot dla początkujących", Assert.Single(_factory.Emails.Sent, m => m.To == ApiFactory.KlaraEmail).Html);
    }

    [Fact]
    public async Task The_same_payment_confirmed_again_sends_nothing_a_second_time()
    {
        var started = await StartPurchase(Item(FakeProductCatalog.EbookSlug, 4900));

        var first = await SendWebhook(started, PaymentOutcome.Paid);
        var again = await Task.WhenAll(SendWebhook(started, PaymentOutcome.Paid), SendWebhook(started, PaymentOutcome.Paid));

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.All(again, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
        Assert.Equal("paid", (await GetOrder(started.OrderId)).Status);
        Assert.Single(_factory.Emails.Sent, m => m.To == _email);
        Assert.Single(_factory.Emails.Sent, m => m.To == ApiFactory.KlaraEmail);
    }

    [Fact]
    public async Task A_webhook_with_a_wrong_signature_is_rejected_and_changes_nothing()
    {
        var started = await StartPurchase(Item(FakeProductCatalog.EbookSlug, 4900));
        var sessionId = _factory.Payments.Sessions.Single().Session.Id;
        var (payload, _) = FakePaymentGateway.Webhook(sessionId, PaymentOutcome.Paid);

        var response = await SendWebhook(payload, "fake=podrobiony");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("pendingPayment", (await GetOrder(started.OrderId)).Status);
        Assert.Empty(_factory.Emails.Sent);
    }

    [Fact]
    public async Task An_expired_payment_session_marks_the_Order_expired_without_emails()
    {
        var started = await StartPurchase(Item(FakeProductCatalog.EbookSlug, 4900));

        var response = await SendWebhook(started, PaymentOutcome.Expired);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("expired", (await GetOrder(started.OrderId)).Status);
        Assert.Empty(_factory.Emails.Sent);
    }

    [Fact]
    public async Task Webhooks_that_do_not_concern_an_Order_are_acknowledged_and_ignored()
    {
        var started = await StartPurchase(Item(FakeProductCatalog.EbookSlug, 4900));
        var (otherSession, otherSignature) = FakePaymentGateway.Webhook("cs_test_spoza_sklepu", PaymentOutcome.Paid);

        var unknownSession = await SendWebhook(otherSession, otherSignature);
        var otherEvent = await SendWebhook(started, outcome: null);

        Assert.Equal(HttpStatusCode.OK, unknownSession.StatusCode);
        Assert.Equal(HttpStatusCode.OK, otherEvent.StatusCode);
        Assert.Equal("pendingPayment", (await GetOrder(started.OrderId)).Status);
        Assert.Empty(_factory.Emails.Sent);
    }

    [Fact]
    public async Task The_Order_keeps_names_and_prices_from_the_moment_of_purchase()
    {
        var started = await StartPurchase(Item(FakeProductCatalog.EbookSlug, 4900));
        _catalog.SetPrice(FakeProductCatalog.EbookSlug, 2900);

        await SendWebhook(started, PaymentOutcome.Paid);

        var order = await GetOrder(started.OrderId);
        var line = Assert.Single(order.Items);
        Assert.Equal((FakeProductCatalog.EbookSlug, "Tarot dla początkujących", "ebook", 1, 4900), (line.Slug, line.Name, line.Kind, line.Quantity, line.Price));
        Assert.Equal(4900, order.Total);
        Assert.Contains("49,00 zł", Assert.Single(_factory.Emails.Sent, m => m.To == _email).Html);
    }

    [Fact]
    public async Task Klara_sees_the_Klients_name_and_the_consents_with_the_time_they_were_accepted()
    {
        _factory.Time.SetUtcNow(new DateTimeOffset(2026, 3, 2, 17, 45, 0, TimeSpan.Zero));
        // Po takim skoku zegara sesja sprzed niego mogła wygasnąć.
        _client = await _api.LoggedInClient(_factory.Emails, _email);
        _factory.Emails.Reset();
        var checkout = Checkout([Item(FakeProductCatalog.EbookSlug, 4900)]);
        checkout["name"] = "Anna <Nowak>";
        var started = (await (await StartCheckout(checkout)).Content.ReadFromJsonAsync<CheckoutStartedResponse>())!;

        await SendWebhook(started, PaymentOutcome.Paid);

        var toKlara = Assert.Single(_factory.Emails.Sent, m => m.To == ApiFactory.KlaraEmail).Html;
        Assert.Contains("Anna &lt;Nowak&gt;", toKlara);
        Assert.Contains("Akceptuję regulamin i politykę prywatności. (2 marca 2026, 18:45)", toKlara);
        Assert.DoesNotContain("odstąpienia", toKlara);
    }

    [Fact]
    public async Task When_emails_fail_the_webhook_fails_so_Stripe_retries_and_the_retry_sends_them()
    {
        var started = await StartPurchase(Item(FakeProductCatalog.EbookSlug, 4900));
        _factory.Emails.Failing = true;

        var failed = await SendWebhook(started, PaymentOutcome.Paid);
        _factory.Emails.Failing = false;
        var retried = await SendWebhook(started, PaymentOutcome.Paid);

        Assert.Equal(HttpStatusCode.InternalServerError, failed.StatusCode);
        Assert.Equal(HttpStatusCode.OK, retried.StatusCode);
        Assert.Equal("paid", (await GetOrder(started.OrderId)).Status);
        Assert.Single(_factory.Emails.Sent, m => m.To == _email);
        Assert.Single(_factory.Emails.Sent, m => m.To == ApiFactory.KlaraEmail);
    }

    [Fact]
    public async Task When_only_Klaras_email_fails_the_retry_does_not_send_the_Klient_a_second_copy()
    {
        var started = await StartPurchase(Item(FakeProductCatalog.EbookSlug, 4900));
        _factory.Emails.FailingRecipient = ApiFactory.KlaraEmail;

        var failed = await SendWebhook(started, PaymentOutcome.Paid);
        _factory.Emails.FailingRecipient = null;
        var retried = await SendWebhook(started, PaymentOutcome.Paid);

        Assert.Equal(HttpStatusCode.InternalServerError, failed.StatusCode);
        Assert.Equal(HttpStatusCode.OK, retried.StatusCode);
        Assert.Single(_factory.Emails.Sent, m => m.To == _email);
        Assert.Single(_factory.Emails.Sent, m => m.To == ApiFactory.KlaraEmail);
    }

    private static object Item(string slug, int price) => new { slug, price };

    private async Task<CheckoutStartedResponse> StartPurchase(params object[] items)
    {
        var response = await StartCheckout(Checkout(items));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CheckoutStartedResponse>())!;
    }

    /// <summary>Webhook Stripe o sesji płatności rozpoczętej dla <paramref name="started"/>.</summary>
    private Task<HttpResponseMessage> SendWebhook(CheckoutStartedResponse started, PaymentOutcome? outcome)
    {
        var sessionId = _factory.Payments.Sessions.Single(s => s.Request.OrderId == started.OrderId).Session.Id;
        var (payload, signature) = FakePaymentGateway.Webhook(sessionId, outcome);
        return SendWebhook(payload, signature);
    }

    private Task<HttpResponseMessage> SendWebhook(string payload, string signature)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/payments/stripe/webhook")
        {
            Content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json"),
            Headers = { { "Stripe-Signature", signature } },
        };
        return _api.CreateClient().SendAsync(request);
    }

    private Dictionary<string, object?> Checkout(object[] items, string[]? consents = null) => new()
    {
        ["items"] = items,
        ["email"] = _email,
        ["name"] = "Anna Nowak",
        ["acceptedConsents"] = consents ?? RequiredConsents,
    };

    private Task<HttpResponseMessage> StartCheckout(object checkout) =>
        _client.PostAsJsonAsync("/api/checkout", checkout);

    private async Task<OrderResponse> GetOrder(Guid id)
    {
        var response = await _client.GetAsync($"/api/orders/{id}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<OrderResponse>())!;
    }

    private sealed record ValidationProblemResponse(Dictionary<string, string[]> Errors);

    private sealed record CartResponse(bool IsValid, List<CartLineResponse> Items);

    private sealed record CartLineResponse(string Slug, int? Price, int? PreviousPrice);

    private sealed record CheckoutStartedResponse(Guid OrderId, Uri RedirectUrl);

    private sealed record OrderResponse(Guid Id, string Status, List<OrderLineResponse> Items, int Total);

    private sealed record OrderLineResponse(string Slug, string Name, string Kind, int Quantity, int Price);
}
