using System.Net;
using System.Net.Http.Json;
using SzeptTarota.Api.Tests.Infrastructure;

namespace SzeptTarota.Api.Tests;

[Collection(ApiCollection.Name)]
public class ProductCatalogTests(ApiFactory factory)
{
    [Fact]
    public async Task List_returns_every_Product_imported_from_naffy_and_the_sample_Ebooks()
    {
        var client = factory.CreateClient();

        var products = await client.GetFromJsonAsync<List<ProductResponse>>("/api/products");

        Assert.Equal(17, products!.Count);
        Assert.Equal(3, products.Count(p => p.Kind == "ebook"));
        var mapaDuszy = Assert.Single(products, p => p.Slug == "mapa-duszy");
        Assert.Equal("Mapa Duszy", mapaDuszy.Name);
        Assert.Equal("Odkryj swoją prawdziwą ścieżkę życia", mapaDuszy.Tagline);
        Assert.Equal(34900, mapaDuszy.Price);
        Assert.Equal("service", mapaDuszy.Kind);
        Assert.Equal("analyses", mapaDuszy.Category);
    }

    [Fact]
    public async Task Details_of_a_Session_include_its_length_description_and_form()
    {
        var client = factory.CreateClient();

        var reiki = await client.GetFromJsonAsync<ProductDetailsResponse>("/api/products/sesja-reiki-czakroterapia");

        Assert.Equal("Sesja Reiki + Czakroterapia", reiki!.Name);
        Assert.Equal("session", reiki.Kind);
        Assert.Equal("sessions", reiki.Category);
        Assert.Equal(24900, reiki.Price);
        Assert.Equal(60, reiki.DurationMinutes);
        Assert.Contains(reiki.Description, block =>
            block.Type == "paragraph" && block.Text == "60 minut pełnego skupienia na Twojej energii.");
        Assert.Empty(reiki.Form);
    }

    [Fact]
    public async Task Details_of_a_Service_have_no_Session_length()
    {
        var client = factory.CreateClient();

        var ritual = await client.GetFromJsonAsync<ProductDetailsResponse>("/api/products/rytual-na-przyciagniecie-milosci");

        Assert.Equal("service", ritual!.Kind);
        Assert.Equal("rituals", ritual.Category);
        Assert.Equal(33300, ritual.Price);
        Assert.Null(ritual.DurationMinutes);
        Assert.NotEmpty(ritual.Description);
    }

    [Fact]
    public async Task Every_Service_has_a_form_asking_at_least_for_the_name_and_date_of_birth()
    {
        var client = factory.CreateClient();
        var services = (await client.GetFromJsonAsync<List<ProductResponse>>("/api/products"))!.Where(p => p.Kind == "service");

        foreach (var service in services)
        {
            var details = await client.GetFromJsonAsync<ProductDetailsResponse>($"/api/products/{service.Slug}");
            Assert.Contains(details!.Form, f => f is { Key: "name", Type: "text", Required: true });
            Assert.Contains(details.Form, f => f is { Key: "birthDate", Type: "date", Required: true });
        }

        var partnerska = await client.GetFromJsonAsync<ProductDetailsResponse>("/api/products/analiza-partnerska");
        Assert.Contains(partnerska!.Form, f => f is { Key: "partnerBirthDate", Type: "date", Required: true });
    }

    [Fact]
    public async Task Details_of_an_unknown_slug_return_not_found()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/products/nie-ma-takiego-produktu");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private sealed record ProductDetailsResponse(
        string Slug,
        string Kind,
        string Category,
        string Name,
        string Tagline,
        int Price,
        int? DurationMinutes,
        List<DescriptionBlockResponse> Description,
        List<FormFieldResponse> Form);

    private sealed record DescriptionBlockResponse(string Type, string? Text, List<string>? Items);

    private sealed record FormFieldResponse(string Key, string Label, string Type, bool Required);

    private sealed record ProductResponse(
        string Slug,
        string Kind,
        string Category,
        string Name,
        string Tagline,
        int Price,
        int? DurationMinutes);
}
