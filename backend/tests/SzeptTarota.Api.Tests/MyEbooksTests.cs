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
public class MyEbooksTests : IAsyncLifetime
{
    private readonly ApiFactory _factory;
    private readonly WebApplicationFactory<Program> _api;

    public MyEbooksTests(ApiFactory factory)
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
    public async Task My_Ebooks_lists_each_Ebook_from_the_Klients_paid_Orders_once()
    {
        var klient = await _api.LoggedInClient(_factory.Emails, NewEmail());
        await Pay(await Buy(klient, FakeProductCatalog.EbookSlug, 4900));
        _factory.Time.Advance(TimeSpan.FromMinutes(5));
        await Pay(await Buy(klient, FakeProductCatalog.EbookSlug, 4900, quantity: 2));
        await Buy(klient, FakeProductCatalog.OtherEbookSlug, 3900);
        var other = await _api.LoggedInClient(_factory.Emails, NewEmail());
        await Pay(await Buy(other, FakeProductCatalog.OtherEbookSlug, 3900));

        var response = await klient.GetAsync("/api/account/ebooks");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var ebooks = await response.Content.ReadFromJsonAsync<List<MyEbookResponse>>();
        Assert.Equal([new MyEbookResponse(FakeProductCatalog.EbookSlug, "Tarot dla początkujących")], ebooks);
    }

    [Fact]
    public async Task Without_logging_in_My_Ebooks_are_not_visible()
    {
        var visitor = _api.CreateBrowser();

        Assert.Equal(HttpStatusCode.Unauthorized, (await visitor.GetAsync("/api/account/ebooks")).StatusCode);
    }

    [Fact]
    public async Task The_Klient_downloads_a_bought_Ebook_as_many_times_as_they_want()
    {
        var klient = await _api.LoggedInClient(_factory.Emails, NewEmail());
        await Pay(await Buy(klient, FakeProductCatalog.EbookSlug, 4900));

        for (var i = 0; i < 3; i++)
        {
            var response = await klient.GetAsync($"/api/account/ebooks/{FakeProductCatalog.EbookSlug}");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
            Assert.Equal("tarot-dla-poczatkujacych.pdf", response.Content.Headers.ContentDisposition?.FileNameStar);
            Assert.Equal(FakeEbookStorage.ContentOf("tarot-dla-poczatkujacych.pdf"), await response.Content.ReadAsByteArrayAsync());
        }
    }

    [Fact]
    public async Task Another_Klient_cannot_download_an_Ebook_they_did_not_buy()
    {
        var anna = await _api.LoggedInClient(_factory.Emails, NewEmail());
        var maria = await _api.LoggedInClient(_factory.Emails, NewEmail());
        await Pay(await Buy(anna, FakeProductCatalog.EbookSlug, 4900));

        var response = await maria.GetAsync($"/api/account/ebooks/{FakeProductCatalog.EbookSlug}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task An_Ebook_from_an_unpaid_Order_cannot_be_downloaded()
    {
        var klient = await _api.LoggedInClient(_factory.Emails, NewEmail());
        await Buy(klient, FakeProductCatalog.EbookSlug, 4900);

        var response = await klient.GetAsync($"/api/account/ebooks/{FakeProductCatalog.EbookSlug}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Without_logging_in_no_Ebook_can_be_downloaded()
    {
        var anna = await _api.LoggedInClient(_factory.Emails, NewEmail());
        await Pay(await Buy(anna, FakeProductCatalog.EbookSlug, 4900));
        var visitor = _api.CreateBrowser();

        var response = await visitor.GetAsync($"/api/account/ebooks/{FakeProductCatalog.EbookSlug}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static string NewEmail() => $"klient-{Guid.NewGuid():N}@example.com";

    private static async Task<Guid> Buy(HttpClient klient, string slug, int price, int quantity = 1)
    {
        var response = await klient.PostAsJsonAsync("/api/checkout", new
        {
            items = new[] { new { slug, price, quantity } },
            email = "anna@example.com",
            name = "Anna Nowak",
            acceptedConsents = new[] { "termsAndPrivacy" },
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CheckoutStartedResponse>())!.OrderId;
    }

    private async Task Pay(Guid orderId)
    {
        var sessionId = _factory.Payments.Sessions.Single(s => s.Request.OrderId == orderId).Session.Id;
        var (payload, signature) = FakePaymentGateway.Webhook(sessionId, PaymentOutcome.Paid);
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/payments/stripe/webhook")
        {
            Content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json"),
            Headers = { { "Stripe-Signature", signature } },
        };
        Assert.Equal(HttpStatusCode.OK, (await _api.CreateClient().SendAsync(request)).StatusCode);
    }

    private sealed record CheckoutStartedResponse(Guid OrderId);

    private sealed record MyEbookResponse(string Slug, string Name);
}
