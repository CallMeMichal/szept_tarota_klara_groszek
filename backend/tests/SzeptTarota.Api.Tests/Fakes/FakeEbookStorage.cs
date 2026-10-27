using System.Text;
using SzeptTarota.Application.Ebooks;

namespace SzeptTarota.Api.Tests.Fakes;

/// <summary>Magazyn, w którym plik każdego Ebooka to tekst „PDF &lt;klucz&gt;”.</summary>
public sealed class FakeEbookStorage : IEbookStorage
{
    public static byte[] ContentOf(string fileKey) => Encoding.UTF8.GetBytes($"PDF {fileKey}");

    public Task<byte[]> ReadAsync(string fileKey, CancellationToken cancellationToken) =>
        Task.FromResult(ContentOf(fileKey));
}
