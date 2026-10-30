using SzeptTarota.Application.Appointments;
using SzeptTarota.Application.Catalog;
using SzeptTarota.Domain.Catalog;
using SzeptTarota.Domain.Orders;

namespace SzeptTarota.Application.Cart;

/// <summary>
/// Sprawdza Koszyk przysłany z przeglądarki: przelicza ceny z katalogu, sprawdza Terminy w kalendarzu Klary,
/// wskazuje błędne pozycje i podaje zgody, które Klient musi zaakceptować przed płatnością. Przeglądarka nigdy nie
/// jest źródłem prawdy o cenie ani dostępności: cena z Koszyka służy tylko do pokazania, że się zmieniła.
/// </summary>
public sealed class CartValidator(IProductCatalog catalog, AvailabilityService availabilityService)
{
    /// <summary>Najwięcej sztuk Ebooka w jednej pozycji; chroni przed literówkami i absurdalnymi kwotami.</summary>
    public const int MaxQuantity = 99;

    private static readonly IReadOnlyDictionary<string, string> NoFormErrors = new Dictionary<string, string>();

    public async Task<CartValidation> ValidateAsync(IReadOnlyList<CartItem> items, CancellationToken cancellationToken)
    {
        var lines = new List<CartLine>(items.Count);
        var availability = await LoadAvailabilityAsync(items, cancellationToken);

        foreach (var item in items)
        {
            var product = catalog.FindBySlug(item.Slug);
            if (product is null)
            {
                lines.Add(new CartLine(item.Slug, item.Quantity, null, [CartError.UnknownProduct], NoFormErrors));
                continue;
            }

            var errors = new List<CartError>();
            if (item.Quantity is < 1 or > MaxQuantity || (product.Kind != ProductKind.Ebook && item.Quantity != 1))
            {
                // Każda Usługa ma własny formularz i Termin, więc kolejna sztuka to kolejna pozycja.
                errors.Add(CartError.InvalidQuantity);
            }

            Appointment? appointment = null;
            if (product.RequiresAppointment)
            {
                if (item.AppointmentStart is not { } start)
                {
                    errors.Add(CartError.AppointmentMissing);
                }
                else if (!availabilityService.IsWithinHorizon(start))
                {
                    // Miniony albo absurdalnie odległy; liczenie końca takiego Terminu mogłoby przepełnić datę.
                    errors.Add(CartError.AppointmentUnavailable);
                }
                else
                {
                    appointment = AvailabilityService.AppointmentAt(product, start);
                    if (availability is null)
                    {
                        errors.Add(CartError.AppointmentUnverifiable);
                    }
                    else if (!availability.IsFree(appointment))
                    {
                        errors.Add(availability.IsTaken(appointment) ? CartError.AppointmentTaken : CartError.AppointmentUnavailable);
                    }
                    else
                    {
                        // Kolejne pozycje tego Koszyka nie mogą zająć tego samego czasu.
                        availability.Occupy(appointment);
                    }
                }
            }

            var form = ServiceForm.Check(product.Form, item.Form);
            if (form.Errors.Count > 0)
            {
                errors.Add(CartError.FormInvalid);
            }

            var previousPrice = item.Price is { } seen && seen != product.Price ? seen : (int?)null;
            lines.Add(new CartLine(item.Slug, item.Quantity, new CartProduct(product, product.Price, previousPrice, form.Answers, appointment), errors, form.Errors));
        }

        // Suma i zgody dotyczą tylko pozycji, które da się kupić.
        var purchasable = lines.Where(l => l.Errors.Count == 0).ToList();
        return new CartValidation(
            lines,
            Total: purchasable.Sum(l => l.Product!.Price * l.Quantity),
            RequiredConsents: Consent.RequiredFor(purchasable.Select(l => l.Product!.Product).ToList()));
    }

    /// <summary>Dostępność Klary w okresie wybranych Terminów; <c>null</c>, gdy nie ma Terminów albo kalendarza nie da się odczytać.</summary>
    private async Task<Availability?> LoadAvailabilityAsync(IReadOnlyList<CartItem> items, CancellationToken cancellationToken)
    {
        var starts = items
            .Where(i => catalog.FindBySlug(i.Slug) is { RequiresAppointment: true })
            .Select(i => i.AppointmentStart)
            .OfType<DateTimeOffset>()
            .Where(availabilityService.IsWithinHorizon)
            .ToList();
        return starts.Count == 0
            ? null
            : await availabilityService.LoadAsync(starts.Min(), starts.Max().AddTicks(1), cancellationToken);
    }
}

/// <summary>
/// Pozycja Koszyka zapisana w przeglądarce; <see cref="Price"/> to cena jednej sztuki widziana przy dodaniu,
/// <see cref="Quantity"/> to liczba sztuk (Ebooka), <see cref="Form"/> to odpowiedzi z formularza Usługi po kluczach pól,
/// a <see cref="AppointmentStart"/> to początek wybranego Terminu Usługi albo Sesji.
/// </summary>
public sealed record CartItem(
    string Slug,
    int? Price,
    int Quantity = 1,
    IReadOnlyDictionary<string, string?>? Form = null,
    DateTimeOffset? AppointmentStart = null);

public sealed record CartValidation(IReadOnlyList<CartLine> Lines, int Total, IReadOnlyList<Consent> RequiredConsents)
{
    public bool IsValid => Lines.Count > 0 && Lines.All(l => l.Errors.Count == 0);

    /// <summary>Ten sam Koszyk z błędem pozycji <paramref name="index"/>, której nie da się już kupić.</summary>
    public CartValidation WithError(int index, CartError error)
    {
        var lines = Lines.Select((l, i) => i == index ? l with { Errors = [.. l.Errors, error] } : l).ToList();
        var purchasable = lines.Where(l => l.Errors.Count == 0).ToList();
        return this with { Lines = lines, Total = purchasable.Sum(l => l.Product!.Price * l.Quantity) };
    }
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
/// <see cref="FormAnswers"/> to poprawne odpowiedzi z formularza Usługi, a <see cref="Appointment"/> to wybrany Termin
/// (także zajęty, żeby Klient widział, który zmienić).
/// </summary>
public sealed record CartProduct(
    Product Product,
    int Price,
    int? PreviousPrice,
    IReadOnlyList<FormAnswer> FormAnswers,
    Appointment? Appointment);

public sealed record CartError(string Code, string Message)
{
    public static readonly CartError UnknownProduct =
        new("unknownProduct", "Tego Produktu nie ma już w ofercie. Usuń go z Koszyka.");

    public static readonly CartError InvalidQuantity =
        new("invalidQuantity", $"Liczba sztuk musi być od 1 do {CartValidator.MaxQuantity}, a każda Usługa to osobna pozycja z własnym formularzem.");

    public static readonly CartError FormInvalid =
        new("formInvalid", "Uzupełnij formularz tej Usługi: popraw zaznaczone pola.");

    public static readonly CartError AppointmentMissing =
        new("appointmentMissing", "Wybierz Termin.");

    public static readonly CartError AppointmentUnavailable =
        new("appointmentUnavailable", "Ten Termin jest już zajęty albo niedostępny. Wybierz inny.");

    /// <summary>Termin opłacił albo właśnie opłaca inny Klient.</summary>
    public static readonly CartError AppointmentTaken =
        new("appointmentTaken", "Ten termin jest już zajęty, wybierz inny.");

    public static readonly CartError AppointmentUnverifiable =
        new("appointmentUnverifiable", "Nie udało się teraz sprawdzić Terminu w kalendarzu. Spróbuj ponownie za chwilę.");
}

/// <summary>Zgoda, którą Klient zaznacza przed płatnością, zależnie od Produktów w Koszyku.</summary>
public sealed record Consent(string Key, string Text)
{
    public static readonly Consent TermsAndPrivacy = new(
        "termsAndPrivacy",
        "Akceptuję regulamin i politykę prywatności.");

    public static IReadOnlyList<Consent> RequiredFor(IReadOnlyCollection<Product> products)
    {
        if (products.Count == 0)
        {
            return [];
        }

        // Bez osobnej zgody na rozpoczęcie Usługi przed upływem 14 dni: przy każdym Koszyku wystarcza regulamin.
        return [TermsAndPrivacy];
    }
}
