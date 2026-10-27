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
    private readonly FakeProductCatalog _catalog = new();
    private readonly WebApplicationFactory<Program> _api;

    public CartValidationTests(ApiFactory factory)
    {
        _api = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.ReplaceWithFake<IProductCatalog>(_catalog)));
    }

    public Task InitializeAsync() => Task.CompletedTask;

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
        Assert.Equal(["termsAndPrivacy", "ebookImmediateDelivery"], cart.RequiredConsents.Select(c => c.Key));
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
        Assert.Equal(["termsAndPrivacy", "ebookImmediateDelivery"], cart.RequiredConsents.Select(c => c.Key));
    }

    [Fact]
    public async Task The_same_Ebook_twice_is_an_error_on_the_second_line()
    {
        var cart = await Validate(
            new { slug = FakeProductCatalog.EbookSlug, price = 4900 },
            new { slug = FakeProductCatalog.EbookSlug, price = 4900 });

        Assert.False(cart.IsValid);
        Assert.Empty(cart.Items[0].Errors);
        Assert.Equal("ebookTwice", Assert.Single(cart.Items[1].Errors).Code);
        Assert.Equal(4900, cart.Total);
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
        Assert.Equal(["termsAndPrivacy", "ebookImmediateDelivery"], cart.RequiredConsents.Select(c => c.Key));
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
    public async Task A_Service_cannot_be_in_the_cart_yet()
    {
        var cart = await Validate(new { slug = FakeProductCatalog.ServiceSlug, price = 34900 });

        Assert.False(cart.IsValid);
        Assert.Equal("kindNotInCartYet", Assert.Single(Assert.Single(cart.Items).Errors).Code);
        Assert.Equal(0, cart.Total);
        Assert.Empty(cart.RequiredConsents);
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

    private async Task<CartResponse> Validate(params object[] items)
    {
        var response = await _api.CreateClient().PostAsJsonAsync("/api/cart/validate", new { items });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CartResponse>())!;
    }

    private sealed record CartResponse(bool IsValid, List<LineResponse> Items, int Total, List<ConsentResponse> RequiredConsents);

    private sealed record LineResponse(string Slug, string? Name, string? Kind, int? Price, int? PreviousPrice, List<ErrorResponse> Errors);

    private sealed record ErrorResponse(string Code, string Message);

    private sealed record ConsentResponse(string Key, string Text);
}
