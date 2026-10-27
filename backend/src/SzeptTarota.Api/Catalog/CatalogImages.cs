namespace SzeptTarota.Api.Catalog;

/// <summary>
/// Grafiki Produktów leżą na razie we frontendzie (<c>frontend/public/products</c>), żeby było je widać
/// także na podglądzie na GitHub Pages; katalog podaje tylko nazwę pliku.
/// </summary>
public static class CatalogImages
{
    public const string RequestPath = "/products";

    public static string? UrlFor(string? image) => image is null ? null : $"{RequestPath}/{image}";
}
