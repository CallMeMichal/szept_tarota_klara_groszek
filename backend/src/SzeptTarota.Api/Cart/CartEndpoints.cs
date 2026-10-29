using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using SzeptTarota.Api.Accounts;
using SzeptTarota.Application.Appointments;
using SzeptTarota.Application.Cart;
using SzeptTarota.Domain.Catalog;

namespace SzeptTarota.Api.Cart;

public static class CartEndpoints
{
    /// <summary>Więcej pozycji nie ma sensu w jednym Zamówieniu; limit chroni przed zapytaniami-olbrzymami.</summary>
    internal const int MaxItems = 50;

    public static IEndpointRouteBuilder MapCartEndpoints(this IEndpointRouteBuilder api)
    {
        api.MapPost("/cart/validate", async Task<Results<Ok<CartResponse>, ValidationProblem>> (
                CartRequest request, ClaimsPrincipal user, CartValidator validator, CancellationToken cancellationToken) =>
            {
                if (request.Items is null || request.Items.Count > MaxItems
                    || request.Items.Any(i => string.IsNullOrWhiteSpace(i?.Slug)))
                {
                    return TypedResults.ValidationProblem(new Dictionary<string, string[]>
                    {
                        ["items"] = [$"Koszyk musi być listą do {MaxItems} pozycji, każda ze slugiem Produktu."],
                    });
                }

                var validation = await validator.ValidateAsync(request.Items.Select(i => i!.ToCartItem()).ToList(), user.OptionalAccountId(), cancellationToken);
                return TypedResults.Ok(CartResponse.From(validation));
            })
            .WithName("ValidateCart");

        return api;
    }
}

/// <summary>Koszyk z przeglądarki.</summary>
public sealed record CartRequest(IReadOnlyList<CartItemRequest?>? Items);

/// <summary>
/// Pozycja Koszyka: slug Produktu, cena jednej sztuki widziana przy dodaniu (w groszach), liczba sztuk
/// (domyślnie 1), odpowiedzi z formularza Usługi i początek wybranego Terminu.
/// </summary>
public sealed record CartItemRequest(
    string? Slug, int? Price, int? Quantity, Dictionary<string, string?>? Form, DateTimeOffset? AppointmentStart)
{
    public CartItem ToCartItem() => new(Slug!, Price, Quantity ?? 1, Form, AppointmentStart);
}

/// <summary>Sprawdzony Koszyk: pozycje w kolejności z zapytania, suma poprawnych pozycji i wymagane zgody.</summary>
public sealed record CartResponse(
    bool IsValid,
    IReadOnlyList<CartLineResponse> Items,
    int Total,
    IReadOnlyList<Consent> RequiredConsents)
{
    public static CartResponse From(CartValidation validation) => new(
        validation.IsValid,
        validation.Lines.Select(CartLineResponse.From).ToList(),
        validation.Total,
        validation.RequiredConsents);
}

/// <summary>
/// Pozycja Koszyka z liczbą sztuk i aktualną ceną jednej sztuki. <see cref="PreviousPrice"/> to cena widziana przy dodaniu, gdy od tamtej
/// pory się zmieniła. Dla nieznanego Produktu znany jest tylko slug. <see cref="Form"/> to definicja formularza
/// Usługi (do poprawienia go w Koszyku), a <see cref="FormErrors"/> to jego błędy po kluczach pól. <see cref="Appointment"/>
/// to wybrany Termin w czasie polskim, a <see cref="DurationMinutes"/> to długość Terminu Produktu.
/// </summary>
public sealed record CartLineResponse(
    string Slug,
    string? Name,
    ProductKind? Kind,
    int Quantity,
    int? Price,
    int? PreviousPrice,
    IReadOnlyList<CartError> Errors,
    IReadOnlyList<FormField> Form,
    IReadOnlyDictionary<string, string> FormErrors,
    Appointment? Appointment,
    int? DurationMinutes)
{
    public static CartLineResponse From(CartLine line) => new(
        line.Slug,
        line.Product?.Product.Name,
        line.Product?.Product.Kind,
        line.Quantity,
        line.Product?.Price,
        line.Product?.PreviousPrice,
        line.Errors,
        line.Product?.Product.Form ?? [],
        line.FormErrors,
        line.Product?.Appointment is { } appointment ? AvailabilityService.InWarsaw(appointment) : null,
        line.Product?.Product.DurationMinutes);
}
