using Microsoft.AspNetCore.Http.HttpResults;
using SzeptTarota.Application.Catalog;
using SzeptTarota.Domain.Catalog;

namespace SzeptTarota.Api.Catalog;

public static class ProductEndpoints
{
    public static IEndpointRouteBuilder MapProductEndpoints(this IEndpointRouteBuilder api)
    {
        var products = api.MapGroup("/products");

        products.MapGet("/", (IProductCatalog catalog) =>
                catalog.All.Select(ProductSummary.From))
            .WithName("ListProducts");

        products.MapGet("/{slug}", Results<Ok<ProductDetails>, NotFound> (string slug, IProductCatalog catalog) =>
                catalog.FindBySlug(slug) is { } product
                    ? TypedResults.Ok(ProductDetails.From(product))
                    : TypedResults.NotFound())
            .WithName("GetProduct");

        return api;
    }
}

/// <summary>Produkt na liście w sekcji Oferta.</summary>
public sealed record ProductSummary(
    string Slug,
    ProductKind Kind,
    ProductCategory Category,
    string Name,
    string Tagline,
    int Price,
    string? ImageUrl,
    int? DurationMinutes)
{
    public static ProductSummary From(Product product) => new(
        product.Slug,
        product.Kind,
        product.Category,
        product.Name,
        product.Tagline,
        product.Price,
        CatalogImages.UrlFor(product.Image),
        product.DurationMinutes);
}

/// <summary>Produkt na jego podstronie: z opisem i formularzem, który Klient wypełni przy dodawaniu do Koszyka.</summary>
public sealed record ProductDetails(
    string Slug,
    ProductKind Kind,
    ProductCategory Category,
    string Name,
    string Tagline,
    int Price,
    string? ImageUrl,
    int? DurationMinutes,
    IReadOnlyList<DescriptionBlock> Description,
    IReadOnlyList<FormField> Form)
{
    public static ProductDetails From(Product product) => new(
        product.Slug,
        product.Kind,
        product.Category,
        product.Name,
        product.Tagline,
        product.Price,
        CatalogImages.UrlFor(product.Image),
        product.DurationMinutes,
        product.Description,
        product.Form);
}
