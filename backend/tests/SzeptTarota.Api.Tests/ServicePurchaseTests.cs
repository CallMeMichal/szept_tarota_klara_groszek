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
public class ServicePurchaseTests : IAsyncLifetime
{
    // Każdy test na własny adres: linków logowania na jeden adres jest najwyżej kilka na 15 minut.
    private readonly string _email = $"anna-{Guid.NewGuid():N}@example.com";

    private readonly ApiFactory _factory;
    private readonly FakeProductCatalog _catalog = new();
    private readonly WebApplicationFactory<Program> _api;

    public ServicePurchaseTests(ApiFactory factory)
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
    public async Task Without_the_consent_to_start_within_14_days_payment_for_a_Service_does_not_start()
    {
        var response = await StartCheckout(
            [Service(new { name = "Anna", birthDate = "1990-04-12" })],
            consents: ["termsAndPrivacy"]);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = (await response.Content.ReadFromJsonAsync<ValidationProblemResponse>())!;
        var error = Assert.Single(Assert.Single(problem.Errors, e => e.Key == "acceptedConsents").Value);
        Assert.Contains("14 dni", error);
        Assert.Empty(_factory.Payments.Sessions);
    }

    [Fact]
    public async Task A_Service_with_an_unfilled_form_sends_the_Klient_back_to_the_cart()
    {
        var response = await StartCheckout([Service(new { name = "Anna" })]);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var cart = (await response.Content.ReadFromJsonAsync<CartResponse>())!;
        Assert.Equal(["birthDate"], Assert.Single(cart.Items).FormErrors.Keys);
        Assert.Empty(_factory.Payments.Sessions);
    }

    [Fact]
    public async Task Klara_gets_the_answers_from_every_Services_form_with_the_paid_Order()
    {
        var response = await StartCheckout(
        [
            Service(new { name = "Anna", birthDate = "1990-04-12", question = "Czy <zmienić> pracę?\nI kiedy?", unknownField = "pominięte" }),
            Service(new { name = "Maria", birthDate = "1962-11-03" }),
            new { slug = FakeProductCatalog.EbookSlug, price = 4900 },
        ]);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var started = (await response.Content.ReadFromJsonAsync<CheckoutStartedResponse>())!;

        var webhook = await SendPaidWebhook(started.OrderId);

        Assert.Equal(HttpStatusCode.OK, webhook.StatusCode);
        var toKlara = Assert.Single(_factory.Emails.Sent, m => m.To == ApiFactory.KlaraEmail).Html;
        Assert.Contains("Imię: Anna", toKlara);
        Assert.Contains("Data urodzenia: 1990-04-12", toKlara);
        Assert.Contains("Pytanie do kart: Czy &lt;zmienić&gt; pracę?<br>I kiedy?", toKlara);
        Assert.Contains("Imię: Maria", toKlara);
        Assert.Contains("Data urodzenia: 1962-11-03", toKlara);
        Assert.DoesNotContain("pominięte", toKlara);
        Assert.Contains("Żądam rozpoczęcia wykonywania Usługi przed upływem 14 dni", toKlara);
        Assert.True(toKlara.IndexOf("Anna", StringComparison.Ordinal) < toKlara.IndexOf("Maria", StringComparison.Ordinal));

        var toKlient = Assert.Single(_factory.Emails.Sent, m => m.To == _email);
        Assert.Contains("747,00 zł", toKlient.Html);
    }

    private static object Service(object form) => new { slug = FakeProductCatalog.ServiceSlug, price = 34900, form };

    private Task<HttpResponseMessage> StartCheckout(object[] items, string[]? consents = null) =>
        _client.PostAsJsonAsync("/api/checkout", new
        {
            items,
            name = "Anna Nowak",
            acceptedConsents = consents ?? ["termsAndPrivacy", "serviceEarlyStart"],
        });

    private Task<HttpResponseMessage> SendPaidWebhook(Guid orderId)
    {
        var sessionId = _factory.Payments.Sessions.Single(s => s.Request.OrderId == orderId).Session.Id;
        var (payload, signature) = FakePaymentGateway.Webhook(sessionId, PaymentOutcome.Paid);
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/payments/stripe/webhook")
        {
            Content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json"),
            Headers = { { "Stripe-Signature", signature } },
        };
        return _api.CreateClient().SendAsync(request);
    }

    private sealed record ValidationProblemResponse(Dictionary<string, string[]> Errors);

    private sealed record CartResponse(bool IsValid, List<CartLineResponse> Items);

    private sealed record CartLineResponse(string Slug, Dictionary<string, string> FormErrors);

    private sealed record CheckoutStartedResponse(Guid OrderId, Uri RedirectUrl);
}
