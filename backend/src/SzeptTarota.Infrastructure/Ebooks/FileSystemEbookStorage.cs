using SzeptTarota.Application.Ebooks;

namespace SzeptTarota.Infrastructure.Ebooks;

/// <summary>
/// Pliki Ebooków w katalogu na dysku serwera, poza repo (to płatna treść). Klucz z katalogu
/// Produktów to ścieżka względna, np. <c>ebooks/tarot-dla-poczatkujacych.pdf</c>.
/// </summary>
public sealed class FileSystemEbookStorage(string rootDirectory) : IEbookStorage
{
    private readonly string _root = Path.GetFullPath(rootDirectory);

    public Task<byte[]> ReadAsync(string fileKey, CancellationToken cancellationToken)
    {
        var path = Path.GetFullPath(Path.Combine(_root, fileKey));
        // Klucz nie może wyprowadzić poza katalog Ebooków (np. "../appsettings.json").
        if (!path.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new FileNotFoundException($"Klucz pliku Ebooka spoza magazynu: {fileKey}.");
        }

        return File.ReadAllBytesAsync(path, cancellationToken);
    }
}
