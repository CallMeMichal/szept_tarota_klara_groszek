using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using SzeptTarota.Api.Tests.Fakes;
using SzeptTarota.Api.Tests.Infrastructure;
using SzeptTarota.Application.Catalog;
using SzeptTarota.Application.Payments;

namespace SzeptTarota.Api.Tests;

[Collection(ApiCollection.Name)]
public class MyOrdersTests : IAsyncLifetime
{
    private readonly ApiFactory _factory;
    private readonly WebApplicationFactory<Program> _api;

    public MyOrdersTests(ApiFactory factory)
    {
        _factory = factory;
        _factory.Payments.Reset();
        _factory.Emails.Reset();
        _api = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.ReplaceWithFake<IProductCatalog>(new FakeProductCatalog())));
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync() => _api.DisposeAsync().AsTask();

    [Fact]
    public async Task Paying_requires_logging_in()
    {
        var response = await _api.CreateBrowser().PostAsJsonAsync("/api/checkout", Checkout(FakeProductCatalog.EbookSlug, 4900));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Empty(_factory.Payments.Sessions);
    }

    [Fact]
    public async Task The_Order_goes_to_the_email_from_the_form_and_stays_in_the_Klients_Konto()
    {
        var klient = await _api.LoggedInClient(_factory.Emails, NewEmail());
        var otherEmail = NewEmail();

        var order = await Buy(klient, FakeProductCatalog.EbookSlug, 4900, email: otherEmail);
        await Pay(order);

        Assert.Equal(otherEmail, Assert.Single(_factory.Payments.Sessions).Request.Email);
        Assert.Single(_factory.Emails.Sent, m => m.To == otherEmail && m.Subject.Contains("Zamówienie"));
        Assert.Equal([order], (await MyOrders(klient)).Select(o => o.Id));
    }

    [Fact]
    public async Task An_invalid_email_in_the_form_is_an_error_on_its_field()
    {
        var klient = await _api.LoggedInClient(_factory.Emails, NewEmail());

        var response = await klient.PostAsJsonAsync(
            "/api/checkout", Checkout(FakeProductCatalog.EbookSlug, 4900, email: "to nie jest email"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemResponse>();
        Assert.Equal(["email"], problem!.Errors.Keys);
        Assert.Empty(_factory.Payments.Sessions);
    }

    [Fact]
    public async Task My_Orders_lists_the_Klients_paid_Orders_newest_first()
    {
        var klient = await _api.LoggedInClient(_factory.Emails, NewEmail());
        var older = await Buy(klient, FakeProductCatalog.EbookSlug, 4900);
        await Pay(older);
        _factory.Time.Advance(TimeSpan.FromMinutes(5));
        var newer = await Buy(klient, FakeProductCatalog.OtherEbookSlug, 3900, quantity: 2);
        await Pay(newer);
        var unpaid = await Buy(klient, FakeProductCatalog.EbookSlug, 4900);
        var expired = await Buy(klient, FakeProductCatalog.EbookSlug, 4900);
        await SendWebhook(expired, PaymentOutcome.Expired);

        var orders = await MyOrders(klient);

        Assert.Equal([newer, older], orders.Select(o => o.Id));
        Assert.DoesNotContain(orders, o => o.Id == unpaid || o.Id == expired);
        var latest = orders[0];
        Assert.Equal(7800, latest.Total);
        Assert.Equal(_factory.Time.GetUtcNow(), latest.PaidAt);
        var line = Assert.Single(latest.Items);
        Assert.Equal(("Rytuały Księżyca", "ebook", 2, 3900), (line.Name, line.Kind, line.Quantity, line.Price));
    }

    [Fact]
    public async Task My_Orders_never_shows_another_Klients_Orders()
    {
        var anna = await _api.LoggedInClient(_factory.Emails, NewEmail());
        var maria = await _api.LoggedInClient(_factory.Emails, NewEmail());
        var annasOrder = await Buy(anna, FakeProductCatalog.EbookSlug, 4900);
        await Pay(annasOrder);

        Assert.Empty(await MyOrders(maria));
        Assert.Equal(HttpStatusCode.NotFound, (await maria.GetAsync($"/api/orders/{annasOrder}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await anna.GetAsync($"/api/orders/{annasOrder}")).StatusCode);
    }

    [Fact]
    public async Task Without_logging_in_neither_My_Orders_nor_an_Order_is_visible()
    {
        var klient = await _api.LoggedInClient(_factory.Emails, NewEmail());
        var order = await Buy(klient, FakeProductCatalog.EbookSlug, 4900);
        await Pay(order);
        var visitor = _api.CreateBrowser();

        Assert.Equal(HttpStatusCode.Unauthorized, (await visitor.GetAsync("/api/account/orders")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await visitor.GetAsync($"/api/orders/{order}")).StatusCode);
    }

    [Fact]
    public async Task Logging_in_again_on_the_same_email_shows_the_same_Orders()
    {
        var email = NewEmail();
        var firstVisit = await _api.LoggedInClient(_factory.Emails, email);
        var order = await Buy(firstVisit, FakeProductCatalog.EbookSlug, 4900);
        await Pay(order);

        var secondVisit = await _api.LoggedInClient(_factory.Emails, email.ToUpperInvariant());

        Assert.Equal([order], (await MyOrders(secondVisit)).Select(o => o.Id));
    }

    [Fact]
    public async Task Logging_in_with_Google_on_the_same_email_shows_the_Orders_bought_after_logging_in_with_a_code()
    {
        var email = NewEmail();
        var withCode = await _api.LoggedInClient(_factory.Emails, email);
        var order = await Buy(withCode, FakeProductCatalog.EbookSlug, 4900);
        await Pay(order);

        var withGoogle = _api.CreateBrowser();
        await withGoogle.LogInWithGoogle(_factory.Google, email.ToUpperInvariant());

        Assert.Equal([order], (await MyOrders(withGoogle)).Select(o => o.Id));
    }

    [Fact]
    public async Task Logging_in_with_a_code_on_the_same_email_shows_the_Orders_bought_after_logging_in_with_Google()
    {
        var email = NewEmail();
        var withGoogle = _api.CreateBrowser();
        await withGoogle.LogInWithGoogle(_factory.Google, email);
        var order = await Buy(withGoogle, FakeProductCatalog.EbookSlug, 4900);
        await Pay(order);

        var withCode = await _api.LoggedInClient(_factory.Emails, email);

        Assert.Equal([order], (await MyOrders(withCode)).Select(o => o.Id));
    }

    private static string NewEmail() => $"klient-{Guid.NewGuid():N}@example.com";

    private static object Checkout(string slug, int price, int quantity = 1, string email = "anna@example.com") => new
    {
        items = new[] { new { slug, price, quantity } },
        email,
        name = "Anna Nowak",
        acceptedConsents = new[] { "termsAndPrivacy" },
    };

    private static async Task<Guid> Buy(HttpClient klient, string slug, int price, int quantity = 1, string email = "anna@example.com")
    {
        var response = await klient.PostAsJsonAsync("/api/checkout", Checkout(slug, price, quantity, email));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CheckoutStartedResponse>())!.OrderId;
    }

    private async Task Pay(Guid orderId) =>
        Assert.Equal(HttpStatusCode.OK, (await SendWebhook(orderId, PaymentOutcome.Paid)).StatusCode);

    private Task<HttpResponseMessage> SendWebhook(Guid orderId, PaymentOutcome outcome)
    {
        var sessionId = _factory.Payments.Sessions.Single(s => s.Request.OrderId == orderId).Session.Id;
        var (payload, signature) = FakePaymentGateway.Webhook(sessionId, outcome);
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/payments/stripe/webhook")
        {
            Content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json"),
            Headers = { { "Stripe-Signature", signature } },
        };
        return _api.CreateClient().SendAsync(request);
    }

    private static async Task<List<MyOrderResponse>> MyOrders(HttpClient klient)
    {
        var response = await klient.GetAsync("/api/account/orders");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<List<MyOrderResponse>>())!;
    }

    private sealed record CheckoutStartedResponse(Guid OrderId);

    private sealed record ValidationProblemResponse(Dictionary<string, string[]> Errors);

    private sealed record MyOrderResponse(Guid Id, DateTimeOffset PaidAt, List<OrderLineResponse> Items, int Total);

    private sealed record OrderLineResponse(string Slug, string Name, string Kind, int Quantity, int Price);
}
