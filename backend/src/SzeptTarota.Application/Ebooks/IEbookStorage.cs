namespace SzeptTarota.Application.Ebooks;

/// <summary>Magazyn plików Ebooków; plik wskazuje klucz z katalogu Produktów.</summary>
public interface IEbookStorage
{
    /// <exception cref="FileNotFoundException">Gdy pod kluczem nie ma pliku.</exception>
    Task<byte[]> ReadAsync(string fileKey, CancellationToken cancellationToken);
}
