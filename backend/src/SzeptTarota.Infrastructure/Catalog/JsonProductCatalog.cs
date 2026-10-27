using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using SzeptTarota.Application.Catalog;
using SzeptTarota.Domain.Catalog;

namespace SzeptTarota.Infrastructure.Catalog;

/// <summary>
/// Katalog Produktów z pliku <c>products.json</c> w katalogu treści; grafiki są we frontendzie, tu tylko ich nazwy plików.
/// Błędny katalog zatrzymuje start aplikacji, zamiast pokazać Klientom niepełną ofertę.
/// </summary>
public sealed partial class JsonProductCatalog : IProductCatalog
{
    public const string FileName = "products.json";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) },
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    private readonly Dictionary<string, Product> _bySlug;

    private JsonProductCatalog(IReadOnlyList<Product> products)
    {
        All = products;
        _bySlug = products.ToDictionary(p => p.Slug);
    }

    public IReadOnlyList<Product> All { get; }

    public Product? FindBySlug(string slug) => _bySlug.GetValueOrDefault(slug);

    public static JsonProductCatalog Load(string catalogDirectory)
    {
        var path = Path.Combine(catalogDirectory, FileName);
        using var stream = File.OpenRead(path);
        var file = JsonSerializer.Deserialize<CatalogFile>(stream, JsonOptions)
            ?? throw new InvalidDataException($"Pusty katalog Produktów: {path}.");

        var products = file.Products.Select(p => p.ToProduct()).ToList();
        var errors = Validate(products).ToList();
        if (errors.Count > 0)
        {
            throw new InvalidDataException($"Błędny katalog Produktów ({path}):{Environment.NewLine}{string.Join(Environment.NewLine, errors)}");
        }

        return new JsonProductCatalog(products);
    }

    private static IEnumerable<string> Validate(IReadOnlyList<Product> products)
    {
        foreach (var duplicate in products.GroupBy(p => p.Slug).Where(g => g.Count() > 1))
        {
            yield return $"- slug '{duplicate.Key}' występuje {duplicate.Count()} razy";
        }

        foreach (var product in products)
        {
            var at = $"- {product.Slug}:";
            if (!SlugPattern().IsMatch(product.Slug)) yield return $"{at} slug może mieć tylko małe litery a-z, cyfry i myślniki";
            if (string.IsNullOrWhiteSpace(product.Name)) yield return $"{at} brak nazwy";
            if (product.Price <= 0) yield return $"{at} cena musi być dodatnia (w groszach)";

            if (product.Kind == ProductKind.Session && product.DurationMinutes is not > 0) yield return $"{at} Sesja musi mieć długość w minutach";
            if (product.Kind != ProductKind.Session && product.DurationMinutes is not null) yield return $"{at} długość ma tylko Sesja";

            if (product.Kind == ProductKind.Ebook && string.IsNullOrWhiteSpace(product.EbookFileKey)) yield return $"{at} Ebook musi mieć klucz pliku";
            if (product.Kind != ProductKind.Ebook && product.EbookFileKey is not null) yield return $"{at} klucz pliku ma tylko Ebook";
            if (product.Kind == ProductKind.Ebook && product.Form.Count > 0) yield return $"{at} Ebook nie ma formularza";

            foreach (var duplicate in product.Form.GroupBy(f => f.Key).Where(g => g.Count() > 1))
            {
                yield return $"{at} pole formularza '{duplicate.Key}' występuje {duplicate.Count()} razy";
            }

            if (product.Image is not null && Path.GetFileName(product.Image) != product.Image)
            {
                yield return $"{at} grafika '{product.Image}' ma być samą nazwą pliku z frontend/public/products";
            }
        }
    }

    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$")]
    private static partial Regex SlugPattern();

    private sealed record CatalogFile(IReadOnlyList<ProductEntry> Products);

    private sealed record ProductEntry(
        string Slug,
        ProductKind Kind,
        ProductCategory Category,
        string Name,
        string Tagline,
        IReadOnlyList<DescriptionBlock> Description,
        int Price,
        string? Image = null,
        int? DurationMinutes = null,
        IReadOnlyList<FormField>? Form = null,
        string? EbookFileKey = null)
    {
        public Product ToProduct() =>
            new(Slug, Kind, Category, Name, Tagline, Description, Image, Price, DurationMinutes, Form ?? [], EbookFileKey);
    }
}
