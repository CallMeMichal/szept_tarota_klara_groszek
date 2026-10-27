namespace SzeptTarota.Api.Catalog;

/// <summary>Grafiki Produktów z katalogu, serwowane pod <c>/api</c>, żeby frontend rozmawiał tylko z API.</summary>
public static class CatalogImages
{
    public const string RequestPath = "/api/catalog/images";

    public static string? UrlFor(string? image) => image is null ? null : $"{RequestPath}/{image}";
}
