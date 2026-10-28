using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using SzeptTarota.Api.Tests.Fakes;
using SzeptTarota.Api.Tests.Infrastructure;
using SzeptTarota.Application.Catalog;

namespace SzeptTarota.Api.Tests;

[Collection(ApiCollection.Name)]
public class CartValidationTests : IAsyncLifetime
{
    private readonly ApiFactory _factory;
    private readonly FakeProductCatalog _catalog = new();
    private readonly WebApplicationFactory<Program> _api;

    /// <summary>Wolne Terminy dla Usług w tych testach; każda Usługa w Koszyku bierze kolejny.</summary>
    private DateTimeOffset[] _terminy = [];
    private int _nextTermin;

    public CartValidationTests(ApiFactory factory)
    {
        _factory = factory;
        _api = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.ReplaceWithFake<IProductCatalog>(_catalog)));
    }

    public async Task InitializeAsync() => _terminy = await _factory.OfferTerminy();

    public Task DisposeAsync() => _api.DisposeAsync().AsTask();

    [Fact]
    public async Task Cart_with_an_Ebook_has_its_current_price_total_and_required_consents()
    {
        var cart = await Validate(new { slug = FakeProductCatalog.EbookSlug, price = 4900 });

        Assert.True(cart.IsValid);
        var line = Assert.Single(cart.Items);
        Assert.Equal(FakeProductCatalog.EbookSlug, line.Slug);
        Assert.Equal("Tarot dla początkujących", line.Name);
        Assert.Equal("ebook", line.Kind);
        Assert.Equal(4900, line.Price);
        Assert.Null(line.PreviousPrice);
        Assert.Empty(line.Errors);
        Assert.Equal(4900, cart.Total);
        Assert.Equal(["termsAndPrivacy"], cart.RequiredConsents.Select(c => c.Key));
        Assert.All(cart.RequiredConsents, c => Assert.False(string.IsNullOrWhiteSpace(c.Text)));
    }

    [Fact]
    public async Task Total_sums_every_Ebook_in_the_cart()
    {
        var cart = await Validate(
            new { slug = FakeProductCatalog.EbookSlug, price = 4900 },
            new { slug = FakeProductCatalog.OtherEbookSlug, price = 3900 });

        Assert.True(cart.IsValid);
        Assert.Equal(8800, cart.Total);
        Assert.Equal(["termsAndPrivacy"], cart.RequiredConsents.Select(c => c.Key));
    }

    [Fact]
    public async Task An_Ebook_can_be_bought_in_any_quantity_and_the_total_counts_every_copy()
    {
        var cart = await Validate(
            new { slug = FakeProductCatalog.EbookSlug, price = 4900, quantity = 3 },
            new { slug = FakeProductCatalog.EbookSlug, price = 4900 });

        Assert.True(cart.IsValid);
        Assert.Equal([3, 1], cart.Items.Select(l => l.Quantity));
        Assert.All(cart.Items, line => Assert.Equal(4900, line.Price));
        Assert.Equal(19600, cart.Total);
    }

    [Fact]
    public async Task A_quantity_below_one_or_above_the_limit_or_more_than_one_Service_per_form_is_an_error()
    {
        var cart = await Validate(
            new { slug = FakeProductCatalog.EbookSlug, price = 4900, quantity = 0 },
            new { slug = FakeProductCatalog.EbookSlug, price = 4900, quantity = 100 },
            new { slug = FakeProductCatalog.ServiceSlug, price = 34900, quantity = 2, form = new { name = "Anna", birthDate = "1990-04-12" }, appointmentStart = _terminy[0] });

        Assert.False(cart.IsValid);
        Assert.All(cart.Items, line => Assert.Equal("invalidQuantity", Assert.Single(line.Errors).Code));
        Assert.Equal(0, cart.Total);
    }

    [Fact]
    public async Task An_unknown_Product_is_an_error_and_not_counted_in_the_total()
    {
        var cart = await Validate(
            new { slug = "nie-ma-takiego-ebooka", price = 100 },
            new { slug = FakeProductCatalog.EbookSlug, price = 4900 });

        Assert.False(cart.IsValid);
        var unknown = cart.Items[0];
        Assert.Equal("nie-ma-takiego-ebooka", unknown.Slug);
        Assert.Null(unknown.Name);
        Assert.Null(unknown.Price);
        Assert.Equal("unknownProduct", Assert.Single(unknown.Errors).Code);
        Assert.Equal(4900, cart.Total);
        Assert.Equal(["termsAndPrivacy"], cart.RequiredConsents.Select(c => c.Key));
    }

    [Fact]
    public async Task A_price_changed_since_adding_shows_the_current_price_and_the_one_the_Klient_saw()
    {
        _catalog.SetPrice(FakeProductCatalog.EbookSlug, 3500);

        var cart = await Validate(new { slug = FakeProductCatalog.EbookSlug, price = 4900 });

        Assert.True(cart.IsValid);
        var line = Assert.Single(cart.Items);
        Assert.Equal(3500, line.Price);
        Assert.Equal(4900, line.PreviousPrice);
        Assert.Equal(3500, cart.Total);
    }

    [Fact]
    public async Task A_Service_with_its_form_filled_in_is_valid_and_needs_only_the_terms_consent()
    {
        var cart = await Validate(Service(new { name = "Anna", birthDate = "1990-04-12" }));

        Assert.True(cart.IsValid);
        var line = Assert.Single(cart.Items);
        Assert.Equal("service", line.Kind);
        Assert.Empty(line.Errors);
        Assert.Equal(34900, cart.Total);
        Assert.Equal(["termsAndPrivacy"], cart.RequiredConsents.Select(c => c.Key));
    }

    [Fact]
    public async Task Missing_required_fields_and_badly_formatted_answers_are_errors_on_their_fields()
    {
        var cart = await Validate(
            Service(new { name = " ", birthDate = "12.04.1990", partnerEmail = "nie-email" }),
            Service(new { name = "Anna", birthDate = "1990-02-30" }));

        Assert.False(cart.IsValid);
        var first = cart.Items[0];
        Assert.Equal("formInvalid", Assert.Single(first.Errors).Code);
        Assert.Equal(["birthDate", "name", "partnerEmail"], first.FormErrors.Keys.Order());
        Assert.All(first.FormErrors.Values, message => Assert.False(string.IsNullOrWhiteSpace(message)));
        Assert.Equal(["birthDate"], cart.Items[1].FormErrors.Keys);
        Assert.Equal(0, cart.Total);
    }

    [Fact]
    public async Task The_same_Service_can_be_in_the_cart_many_times_each_with_its_own_form()
    {
        var cart = await Validate(
            Service(new { name = "Anna", birthDate = "1990-04-12" }),
            Service(new { name = "Maria", birthDate = "1962-11-03", question = "Co mnie czeka?" }));

        Assert.True(cart.IsValid);
        Assert.All(cart.Items, line => Assert.Empty(line.FormErrors));
        Assert.Equal(69800, cart.Total);
    }

    [Fact]
    public async Task A_cart_line_shows_the_Services_form_so_the_Klient_can_correct_it()
    {
        var line = Assert.Single((await Validate(Service(new { name = "Anna", birthDate = "1990-04-12" }))).Items);

        Assert.Equal(
            [("name", "Imię", "text", true), ("birthDate", "Data urodzenia", "date", true), ("question", "Pytanie do kart", "longText", false), ("partnerEmail", "Email partnera", "email", false)],
            line.Form.Select(f => (f.Key, f.Label, f.Type, f.Required)));
    }

    [Fact]
    public async Task An_empty_cart_is_not_valid_and_needs_no_consents()
    {
        var cart = await Validate();

        Assert.False(cart.IsValid);
        Assert.Empty(cart.Items);
        Assert.Equal(0, cart.Total);
        Assert.Empty(cart.RequiredConsents);
    }

    [Fact]
    public async Task A_cart_that_is_not_a_list_of_slugs_is_a_bad_request()
    {
        var client = _api.CreateClient();

        var withoutItems = await client.PostAsJsonAsync("/api/cart/validate", new { });
        var withoutSlug = await client.PostAsJsonAsync("/api/cart/validate", new { items = new[] { new { price = 4900 } } });
        var tooBig = await client.PostAsJsonAsync("/api/cart/validate",
            new { items = Enumerable.Repeat(new { slug = FakeProductCatalog.EbookSlug }, 51) });

        Assert.Equal(HttpStatusCode.BadRequest, withoutItems.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, withoutSlug.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, tooBig.StatusCode);
    }

    private object Service(object form) =>
        new { slug = FakeProductCatalog.ServiceSlug, price = 34900, form, appointmentStart = _terminy[_nextTermin++] };

    private async Task<CartResponse> Validate(params object[] items)
    {
        var response = await _api.CreateClient().PostAsJsonAsync("/api/cart/validate", new { items });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CartResponse>())!;
    }

    private sealed record CartResponse(bool IsValid, List<LineResponse> Items, int Total, List<ConsentResponse> RequiredConsents);

    private sealed record LineResponse(
        string Slug,
        string? Name,
        string? Kind,
        int Quantity,
        int? Price,
        int? PreviousPrice,
        List<ErrorResponse> Errors,
        List<FormFieldResponse> Form,
        Dictionary<string, string> FormErrors);

    private sealed record FormFieldResponse(string Key, string Label, string Type, bool Required);

    private sealed record ErrorResponse(string Code, string Message);

    private sealed record ConsentResponse(string Key, string Text);
}
