using SzeptTarota.Application.Catalog;
using SzeptTarota.Domain.Catalog;

namespace SzeptTarota.Application.Cart;

/// <summary>
/// Sprawdza Koszyk przysłany z przeglądarki: przelicza ceny z katalogu, wskazuje błędne pozycje
/// i podaje zgody, które Klient musi zaakceptować przed płatnością. Przeglądarka nigdy nie jest
/// źródłem prawdy o cenie: cena z Koszyka służy tylko do pokazania, że się zmieniła.
/// </summary>
public sealed class CartValidator(IProductCatalog catalog)
{
    public CartValidation Validate(IReadOnlyList<CartItem> items)
    {
        var lines = new List<CartLine>(items.Count);
        var ebooksInCart = new HashSet<string>();

        foreach (var item in items)
        {
            var product = catalog.FindBySlug(item.Slug);
            if (product is null)
            {
                lines.Add(new CartLine(item.Slug, null, [CartError.UnknownProduct]));
                continue;
            }

            var errors = new List<CartError>();
            if (product.Kind != ProductKind.Ebook)
            {
                errors.Add(CartError.KindNotInCartYet);
            }
            else if (!ebooksInCart.Add(product.Slug))
            {
                errors.Add(CartError.EbookTwice);
            }

            var previousPrice = item.Price is { } seen && seen != product.Price ? seen : (int?)null;
            lines.Add(new CartLine(item.Slug, new CartProduct(product, product.Price, previousPrice), errors));
        }

        // Suma i zgody dotyczą tylko pozycji, które da się kupić.
        var purchasable = lines.Where(l => l.Errors.Count == 0).Select(l => l.Product!).ToList();
        return new CartValidation(
            lines,
            Total: purchasable.Sum(p => p.Price),
            RequiredConsents: Consent.RequiredFor(purchasable.Select(p => p.Product).ToList()));
    }
}

/// <summary>Pozycja Koszyka zapisana w przeglądarce; <see cref="Price"/> to cena widziana przy dodaniu.</summary>
public sealed record CartItem(string Slug, int? Price);

public sealed record CartValidation(IReadOnlyList<CartLine> Lines, int Total, IReadOnlyList<Consent> RequiredConsents)
{
    public bool IsValid => Lines.Count > 0 && Lines.All(l => l.Errors.Count == 0);
}

/// <summary>Pozycja po sprawdzeniu; <see cref="Product"/> jest pusty, gdy Produktu nie ma w katalogu.</summary>
public sealed record CartLine(string Slug, CartProduct? Product, IReadOnlyList<CartError> Errors);

/// <summary>Produkt w Koszyku z aktualną ceną; <see cref="PreviousPrice"/> to inna cena, którą Klient widział przy dodaniu.</summary>
public sealed record CartProduct(Product Product, int Price, int? PreviousPrice);

public sealed record CartError(string Code, string Message)
{
    public static readonly CartError UnknownProduct =
        new("unknownProduct", "Tego Produktu nie ma już w ofercie. Usuń go z Koszyka.");

    public static readonly CartError EbookTwice =
        new("ebookTwice", "Ten Ebook jest już w Koszyku. Ebook można kupić tylko raz.");

    public static readonly CartError KindNotInCartYet =
        new("kindNotInCartYet", "Ten Produkt nie może jeszcze trafić do Koszyka.");
}

/// <summary>Zgoda, którą Klient zaznacza przed płatnością, zależnie od Produktów w Koszyku.</summary>
public sealed record Consent(string Key, string Text)
{
    public static readonly Consent TermsAndPrivacy = new(
        "termsAndPrivacy",
        "Akceptuję regulamin i politykę prywatności.");

    public static IReadOnlyList<Consent> RequiredFor(IReadOnlyCollection<Product> products) =>
        products.Count == 0 ? [] : [TermsAndPrivacy];
}
