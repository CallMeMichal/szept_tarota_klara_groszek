using System.Collections.Concurrent;

namespace SzeptTarota.Application.Email;

/// <summary>
/// Grafiki maili wbudowane w aplikację: nagłówek i księżyc marki oraz okładki Produktów, te same pliki co na stronie
/// (frontend/src/assets/products/&lt;slug&gt;.webp). Idą w treści maila, więc widać je bez publicznego adresu strony.
/// </summary>
public static class EmailImages
{
    private static readonly ConcurrentDictionary<string, byte[]?> Cache = new();

    /// <summary>Nagłówek: księżyc z nazwą marki, „Karty nie mówią głośno.” i karty tarota, jak na stronie głównej.</summary>
    public static EmailImage Header => Load("Email/header.jpg", "naglowek", "image/jpeg")!;

    /// <summary>Złoty sierp księżyca do stopki.</summary>
    public static EmailImage Moon => Load("Email/moon.png", "ksiezyc", "image/png")!;

    /// <summary>Okładka Produktu o tym slugu; <c>null</c>, gdy Produkt nie ma grafiki.</summary>
    public static EmailImage? Product(string slug) => Load($"Products/{slug}.webp", $"produkt-{slug}", "image/webp");

    private static EmailImage? Load(string resource, string contentId, string contentType)
    {
        var content = Cache.GetOrAdd(resource, name =>
        {
            using var stream = typeof(EmailImages).Assembly.GetManifestResourceStream(name);
            if (stream is null)
            {
                return null;
            }

            using var copy = new MemoryStream();
            stream.CopyTo(copy);
            return copy.ToArray();
        });

        return content is null ? null : new EmailImage(contentId, resource[(resource.IndexOf('/') + 1)..], contentType, content);
    }
}
