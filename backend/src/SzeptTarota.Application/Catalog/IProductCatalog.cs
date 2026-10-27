using SzeptTarota.Domain.Catalog;

namespace SzeptTarota.Application.Catalog;

/// <summary>Katalog Produktów: treść w repo, wczytywana raz przy starcie aplikacji.</summary>
public interface IProductCatalog
{
    /// <summary>Produkty w kolejności z katalogu.</summary>
    IReadOnlyList<Product> All { get; }

    Product? FindBySlug(string slug);
}
