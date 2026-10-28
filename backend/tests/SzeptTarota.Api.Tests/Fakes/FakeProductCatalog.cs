using SzeptTarota.Application.Catalog;
using SzeptTarota.Domain.Catalog;

namespace SzeptTarota.Api.Tests.Fakes;

/// <summary>
/// Katalog z Produktami ustawianymi przez test. Prawdziwy katalog nie ma jeszcze Ebooków,
/// a test zmiany ceny musi móc ją zmienić w trakcie.
/// </summary>
public sealed class FakeProductCatalog : IProductCatalog
{
    public const string EbookSlug = "tarot-dla-poczatkujacych";
    public const string OtherEbookSlug = "rytualy-ksiezyca";
    public const string ServiceSlug = "mapa-duszy";
    public const string SessionSlug = "sesja-reiki-czakroterapia";

    private readonly List<Product> _products =
    [
        Ebook(EbookSlug, "Tarot dla początkujących", 4900),
        Ebook(OtherEbookSlug, "Rytuały Księżyca", 3900),
        new(ServiceSlug, ProductKind.Service, ProductCategory.Analyses, "Mapa Duszy", "Odkryj swoją ścieżkę",
            [], 34900, null,
            [
                new FormField("name", "Imię", FormFieldType.Text, true),
                new FormField("birthDate", "Data urodzenia", FormFieldType.Date, true),
                new FormField("question", "Pytanie do kart", FormFieldType.LongText, false),
                new FormField("partnerEmail", "Email partnera", FormFieldType.Email, false),
            ],
            null),
        new(SessionSlug, ProductKind.Session, ProductCategory.Sessions, "Sesja Reiki", "Reiki online",
            [], 24900, 60, [], null),
    ];

    public IReadOnlyList<Product> All => _products;

    public Product? FindBySlug(string slug) => _products.Find(p => p.Slug == slug);

    public void SetPrice(string slug, int price)
    {
        var index = _products.FindIndex(p => p.Slug == slug);
        _products[index] = _products[index] with { Price = price };
    }

    private static Product Ebook(string slug, string name, int price) => new(
        slug, ProductKind.Ebook, ProductCategory.Ebooks, name, "Ebook", [], price, null, [], $"{slug}.pdf");
}
