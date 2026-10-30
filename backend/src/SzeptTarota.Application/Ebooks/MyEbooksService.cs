using SzeptTarota.Application.Orders;
using SzeptTarota.Domain.Catalog;

namespace SzeptTarota.Application.Ebooks;

/// <summary>
/// Moje Ebooki: Ebooki z opłaconych Zamówień Konta, do pobierania bez limitu. Plik wskazuje klucz zapisany w Zamówieniu,
/// więc późniejsze zmiany katalogu nie odbierają kupionego Ebooka.
/// </summary>
public sealed class MyEbooksService(IOrderStore orders, IEbookStorage storage)
{
    /// <summary>Każdy kupiony Ebook raz, nawet kupiony kilka razy; nazwa z najnowszego zakupu.</summary>
    public async Task<IReadOnlyList<OwnedEbook>> ListAsync(Guid accountId, CancellationToken cancellationToken) =>
        (await orders.ListPaidAsync(accountId, cancellationToken))
            .SelectMany(o => o.Lines)
            .Where(l => l.Kind == ProductKind.Ebook && l.EbookFileKey is not null)
            .DistinctBy(l => l.Slug)
            .Select(l => new OwnedEbook(l.Slug, l.Name, l.EbookFileKey!))
            .ToList();

    /// <summary>Plik Ebooka albo <c>null</c>, gdy Konto go nie kupiło.</summary>
    public async Task<EbookFile?> DownloadAsync(Guid accountId, string slug, CancellationToken cancellationToken)
    {
        if ((await ListAsync(accountId, cancellationToken)).FirstOrDefault(e => e.Slug == slug) is not { } ebook)
        {
            return null;
        }

        var fileName = Path.GetFileName(ebook.FileKey);
        return new EbookFile(fileName, EbookFile.ContentTypeOf(fileName), await storage.ReadAsync(ebook.FileKey, cancellationToken));
    }
}

/// <summary>Ebook kupiony przez Konto: slug Produktu, nazwa z chwili zakupu i klucz pliku.</summary>
public sealed record OwnedEbook(string Slug, string Name, string FileKey);

/// <summary>Plik Ebooka do pobrania albo załącznika: nazwa, typ zawartości i treść.</summary>
public sealed record EbookFile(string FileName, string ContentType, byte[] Content)
{
    public static string ContentTypeOf(string fileName) =>
        Path.GetExtension(fileName).Equals(".pdf", StringComparison.OrdinalIgnoreCase)
            ? "application/pdf"
            : "application/octet-stream";
}
