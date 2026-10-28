using SzeptTarota.Application.Catalog;
using SzeptTarota.Domain.Catalog;
using SzeptTarota.Domain.Orders;

namespace SzeptTarota.Application.Cart;

/// <summary>
/// Sprawdza Koszyk przysłany z przeglądarki: przelicza ceny z katalogu, wskazuje błędne pozycje
/// i podaje zgody, które Klient musi zaakceptować przed płatnością. Przeglądarka nigdy nie jest
/// źródłem prawdy o cenie: cena z Koszyka służy tylko do pokazania, że się zmieniła.
/// </summary>
public sealed class CartValidator(IProductCatalog catalog)
{
    /// <summary>Najwięcej sztuk Ebooka w jednej pozycji; chroni przed literówkami i absurdalnymi kwotami.</summary>
    public const int MaxQuantity = 99;

    private static readonly IReadOnlyDictionary<string, string> NoFormErrors = new Dictionary<string, string>();

    public CartValidation Validate(IReadOnlyList<CartItem> items)
    {
        var lines = new List<CartLine>(items.Count);

        foreach (var item in items)
        {
            var product = catalog.FindBySlug(item.Slug);
            if (product is null)
            {
                lines.Add(new CartLine(item.Slug, item.Quantity, null, [CartError.UnknownProduct], NoFormErrors));
                continue;
            }

            var errors = new List<CartError>();
            if (product.Kind == ProductKind.Session)
            {
                errors.Add(CartError.KindNotInCartYet);
            }
            else if (item.Quantity is < 1 or > MaxQuantity || (product.Kind != ProductKind.Ebook && item.Quantity != 1))
            {
                // Każda Usługa ma własny formularz, więc kolejna sztuka to kolejna pozycja.
                errors.Add(CartError.InvalidQuantity);
            }

            var form = ServiceForm.Check(product.Form, item.Form);
            if (form.Errors.Count > 0)
            {
                errors.Add(CartError.FormInvalid);
            }

            var previousPrice = item.Price is { } seen && seen != product.Price ? seen : (int?)null;
            lines.Add(new CartLine(item.Slug, item.Quantity, new CartProduct(product, product.Price, previousPrice, form.Answers), errors, form.Errors));
        }

        // Suma i zgody dotyczą tylko pozycji, które da się kupić.
        var purchasable = lines.Where(l => l.Errors.Count == 0).ToList();
        return new CartValidation(
            lines,
            Total: purchasable.Sum(l => l.Product!.Price * l.Quantity),
            RequiredConsents: Consent.RequiredFor(purchasable.Select(l => l.Product!.Product).ToList()));
    }
}

/// <summary>
/// Pozycja Koszyka zapisana w przeglądarce; <see cref="Price"/> to cena jednej sztuki widziana przy dodaniu,
/// <see cref="Quantity"/> to liczba sztuk (Ebooka), a <see cref="Form"/> to odpowiedzi z formularza Usługi po kluczach pól.
/// </summary>
public sealed record CartItem(string Slug, int? Price, int Quantity = 1, IReadOnlyDictionary<string, string?>? Form = null);

public sealed record CartValidation(IReadOnlyList<CartLine> Lines, int Total, IReadOnlyList<Consent> RequiredConsents)
{
    public bool IsValid => Lines.Count > 0 && Lines.All(l => l.Errors.Count == 0);
}

/// <summary>
/// Pozycja po sprawdzeniu; <see cref="Product"/> jest pusty, gdy Produktu nie ma w katalogu.
/// <see cref="FormErrors"/> to błędy formularza Usługi po kluczach pól.
/// </summary>
public sealed record CartLine(
    string Slug,
    int Quantity,
    CartProduct? Product,
    IReadOnlyList<CartError> Errors,
    IReadOnlyDictionary<string, string> FormErrors);

/// <summary>
/// Produkt w Koszyku z aktualną ceną jednej sztuki; <see cref="PreviousPrice"/> to inna cena, którą Klient widział przy dodaniu,
/// a <see cref="FormAnswers"/> to poprawne odpowiedzi z formularza Usługi.
/// </summary>
public sealed record CartProduct(Product Product, int Price, int? PreviousPrice, IReadOnlyList<FormAnswer> FormAnswers);

public sealed record CartError(string Code, string Message)
{
    public static readonly CartError UnknownProduct =
        new("unknownProduct", "Tego Produktu nie ma już w ofercie. Usuń go z Koszyka.");

    public static readonly CartError InvalidQuantity =
        new("invalidQuantity", $"Liczba sztuk musi być od 1 do {CartValidator.MaxQuantity}, a każda Usługa to osobna pozycja z własnym formularzem.");

    public static readonly CartError FormInvalid =
        new("formInvalid", "Uzupełnij formularz tej Usługi: popraw zaznaczone pola.");

    public static readonly CartError KindNotInCartYet =
        new("kindNotInCartYet", "Ten Produkt nie może jeszcze trafić do Koszyka.");
}

/// <summary>Zgoda, którą Klient zaznacza przed płatnością, zależnie od Produktów w Koszyku.</summary>
public sealed record Consent(string Key, string Text)
{
    public static readonly Consent TermsAndPrivacy = new(
        "termsAndPrivacy",
        "Akceptuję regulamin i politykę prywatności.");

    public static readonly Consent ServiceEarlyStart = new(
        "serviceEarlyStart",
        "Żądam rozpoczęcia wykonywania Usługi przed upływem 14 dni od zawarcia umowy i przyjmuję do wiadomości, że po jej pełnym wykonaniu tracę prawo do odstąpienia od umowy.");

    public static IReadOnlyList<Consent> RequiredFor(IReadOnlyCollection<Product> products)
    {
        if (products.Count == 0)
        {
            return [];
        }

        return products.Any(p => p.Kind != ProductKind.Ebook) ? [TermsAndPrivacy, ServiceEarlyStart] : [TermsAndPrivacy];
    }
}
