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
/// Zamówienia w panelu Klary. Baza jest wspólna dla wszystkich testów, więc testy patrzą tylko na swoje Zamówienia.
/// </summary>
[Collection(ApiCollection.Name)]
public class PanelOrdersTests : IAsyncLifetime
{
    private readonly ApiFactory _factory;
    private readonly WebApplicationFactory<Program> _api;
    private HttpClient _klara = null!;

    public PanelOrdersTests(ApiFactory factory)
    {
        _factory = factory;
        _factory.Payments.Reset();
        _factory.Emails.Reset();
        _api = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.ReplaceWithFake<IProductCatalog>(new FakeProductCatalog())));
    }

    /// <summary>Klara zalogowana przez Google (logowanie kodem ma limit 3 kodów na adres na 15 minut).</summary>
    public async Task InitializeAsync()
    {
        _klara = _api.CreateBrowser();
        await _klara.LogInWithGoogle(_factory.Google, ApiFactory.KlaraEmail);
    }

    public Task DisposeAsync() => _api.DisposeAsync().AsTask();

    [Fact]
    public async Task Klara_sees_every_Klients_paid_Orders_newest_first()
    {
        var anna = await Klient();
        var maria = await Klient();
        var older = await Buy(anna, [Ebook(FakeProductCatalog.EbookSlug, 4900)], name: "Anna Nowak", email: "anna@example.com");
        await Pay(older);
        _factory.Time.Advance(TimeSpan.FromMinutes(5));
        var newer = await Buy(maria, [Ebook(FakeProductCatalog.OtherEbookSlug, 3900, quantity: 2)], name: "Maria Kowalska", email: "maria@example.com");
        await Pay(newer);
        var unpaid = await Buy(anna, [Ebook(FakeProductCatalog.EbookSlug, 4900)]);

        var orders = (await PanelOrders()).Where(o => o.Id == older || o.Id == newer || o.Id == unpaid).ToList();

        Assert.Equal([newer, older], orders.Select(o => o.Id));
        var latest = orders[0];
        Assert.Equal(_factory.Time.GetUtcNow(), latest.PaidAt);
        Assert.Equal(("Maria Kowalska", "maria@example.com", 7800), (latest.CustomerName, latest.Email, latest.Total));
        var line = Assert.Single(latest.Items);
        Assert.Equal(("Rytuały Księżyca", "ebook", 2, 3900), (line.Name, line.Kind, line.Quantity, line.Price));
    }

    [Fact]
    public async Task Klara_filters_the_Orders_by_the_Polish_day_of_payment_and_by_Produkt()
    {
        var klient = await Klient();
        // Zapłacone 30 minut po północy czasu polskiego, czyli jeszcze poprzedniego dnia w UTC.
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(_factory.Time.GetUtcNow(), CalendarExtensions.Warsaw).Date);
        var first = today.AddDays(1);
        _factory.Time.SetUtcNow(CalendarExtensions.InWarsaw(first, 0, 30).ToUniversalTime());
        var ebook = await Buy(klient, [Ebook(FakeProductCatalog.EbookSlug, 4900)]);
        await Pay(ebook);
        _factory.Time.Advance(TimeSpan.FromDays(1));
        var other = await Buy(klient, [Ebook(FakeProductCatalog.OtherEbookSlug, 3900)]);
        await Pay(other);
        _factory.Time.Advance(TimeSpan.FromDays(1));
        var both = await Buy(klient, [Ebook(FakeProductCatalog.OtherEbookSlug, 3900), Ebook(FakeProductCatalog.EbookSlug, 4900)]);
        await Pay(both);
        Guid[] mine = [ebook, other, both];

        async Task<IEnumerable<Guid>> Filtered(string query) =>
            (await PanelOrders(query)).Select(o => o.Id).Where(mine.Contains);

        Assert.Equal([ebook], await Filtered($"?from={first:yyyy-MM-dd}&to={first:yyyy-MM-dd}"));
        Assert.Empty(await Filtered($"?to={today:yyyy-MM-dd}"));
        Assert.Equal([both, other], await Filtered($"?from={first.AddDays(1):yyyy-MM-dd}"));
        Assert.Equal([both, ebook], await Filtered($"?product={FakeProductCatalog.EbookSlug}"));
        Assert.Equal([other], await Filtered($"?product={FakeProductCatalog.OtherEbookSlug}&to={first.AddDays(1):yyyy-MM-dd}"));
    }

    [Theory]
    [InlineData("from=2026-13-01&to=jutro")]
    [InlineData("from=0001-01-01&to=9999-12-31")]
    public async Task A_filter_with_a_wrong_date_is_an_error_on_its_field(string query)
    {
        var response = await _klara.GetAsync($"/api/panel/orders?{query}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = (await response.Content.ReadFromJsonAsync<ValidationProblemResponse>())!;
        Assert.Equal(["from", "to"], problem.Errors.Keys.Order());
    }

    [Fact]
    public async Task Only_Klara_sees_the_Orders()
    {
        var anonymous = _api.CreateBrowser();
        var klient = await Klient();
        var impostor = _api.CreateBrowser();
        await impostor.LogInWithGoogle(_factory.Google, ApiFactory.KlaraEmail, emailVerified: false);
        var order = await Buy(klient, [Ebook(FakeProductCatalog.EbookSlug, 4900)]);
        await Pay(order);

        foreach (var path in new[] { "/api/panel/orders", $"/api/panel/orders/{order}" })
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(path)).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await impostor.GetAsync(path)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await klient.GetAsync(path)).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await _klara.GetAsync(path)).StatusCode);
        }
    }

    [Fact]
    public async Task The_Order_details_show_every_Services_form_answers_and_Termin()
    {
        var terminy = await _factory.OfferTerminy();
        var klient = await Klient();
        var order = await Buy(klient,
        [
            Service(new { name = "Anna", birthDate = "1990-04-12", question = "Czy zmienić pracę?" }, terminy[0]),
            Service(new { name = "Maria", birthDate = "1962-11-03" }, terminy[1]),
            Ebook(FakeProductCatalog.EbookSlug, 4900),
        ], name: "Anna Nowak", email: "anna@example.com");
        await Pay(order);

        var response = await _klara.GetAsync($"/api/panel/orders/{order}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var details = (await response.Content.ReadFromJsonAsync<PanelOrderDetailsResponse>())!;
        Assert.Equal((order, "Anna Nowak", "anna@example.com", 74700), (details.Id, details.CustomerName, details.Email, details.Total));
        Assert.Equal(
            [FakeProductCatalog.ServiceSlug, FakeProductCatalog.ServiceSlug, FakeProductCatalog.EbookSlug],
            details.Items.Select(i => i.Slug));
        var anna = details.Items[0];
        Assert.Equal(
            [("Imię", "Anna"), ("Data urodzenia", "1990-04-12"), ("Pytanie do kart", "Czy zmienić pracę?")],
            anna.FormAnswers.Select(a => (a.Label, a.Value)));
        Assert.Equal(terminy[0], anna.Appointment!.Start);
        Assert.Equal(terminy[0].AddMinutes(90), anna.Appointment.End);
        Assert.Equal([("Imię", "Maria"), ("Data urodzenia", "1962-11-03")], details.Items[1].FormAnswers.Select(a => (a.Label, a.Value)));
        Assert.Empty(details.Items[2].FormAnswers);
        Assert.Null(details.Items[2].Appointment);
    }

    [Fact]
    public async Task An_unpaid_or_unknown_Order_has_no_details()
    {
        var unpaid = await Buy(await Klient(), [Ebook(FakeProductCatalog.EbookSlug, 4900)]);

        Assert.Equal(HttpStatusCode.NotFound, (await _klara.GetAsync($"/api/panel/orders/{unpaid}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _klara.GetAsync($"/api/panel/orders/{Guid.NewGuid()}")).StatusCode);
    }

    private static object Service(object form, DateTimeOffset appointmentStart) =>
        new { slug = FakeProductCatalog.ServiceSlug, price = 34900, form, appointmentStart };

    private async Task<HttpClient> Klient()
    {
        var klient = _api.CreateBrowser();
        await klient.LogInWithGoogle(_factory.Google, $"klient-{Guid.NewGuid():N}@example.com");
        return klient;
    }

    private static object Ebook(string slug, int price, int quantity = 1) => new { slug, price, quantity };

    private static async Task<Guid> Buy(
        HttpClient klient, object[] items, string name = "Anna Nowak", string email = "anna@example.com")
    {
        var response = await klient.PostAsJsonAsync("/api/checkout", new
        {
            items,
            email,
            name,
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

    private async Task<List<PanelOrderResponse>> PanelOrders(string query = "")
    {
        var response = await _klara.GetAsync($"/api/panel/orders{query}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<List<PanelOrderResponse>>())!;
    }

    private sealed record CheckoutStartedResponse(Guid OrderId);

    private sealed record ValidationProblemResponse(Dictionary<string, string[]> Errors);

    private sealed record PanelOrderResponse(
        Guid Id, DateTimeOffset PaidAt, string CustomerName, string Email, List<OrderLineResponse> Items, int Total);

    private sealed record OrderLineResponse(string Slug, string Name, string Kind, int Quantity, int Price);

    private sealed record PanelOrderDetailsResponse(
        Guid Id, DateTimeOffset PaidAt, string CustomerName, string Email, List<PanelOrderLineResponse> Items, int Total);

    private sealed record PanelOrderLineResponse(
        string Slug, string Name, int Quantity, int Price, List<FormAnswerResponse> FormAnswers, AppointmentResponse? Appointment);

    private sealed record FormAnswerResponse(string Key, string Label, string Value);

    private sealed record AppointmentResponse(DateTimeOffset Start, DateTimeOffset End);
}
