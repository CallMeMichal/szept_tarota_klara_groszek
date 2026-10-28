using System.Globalization;
using Microsoft.AspNetCore.Http.HttpResults;
using SzeptTarota.Application.Appointments;
using SzeptTarota.Application.Catalog;
using SzeptTarota.Domain.Catalog;

namespace SzeptTarota.Api.Catalog;

public static class ProductEndpoints
{
    public static IEndpointRouteBuilder MapProductEndpoints(this IEndpointRouteBuilder api)
    {
        var products = api.MapGroup("/products");

        products.MapGet("/", (IProductCatalog catalog) =>
                catalog.All.Select(ProductSummary.From))
            .WithName("ListProducts");

        products.MapGet("/{slug}", Results<Ok<ProductDetails>, NotFound> (string slug, IProductCatalog catalog) =>
                catalog.FindBySlug(slug) is { } product
                    ? TypedResults.Ok(ProductDetails.From(product))
                    : TypedResults.NotFound())
            .WithName("GetProduct");

        // Daty `from` i `to` (RRRR-MM-DD, włącznie) to dni w czasie polskim.
        products.MapGet("/{slug}/appointments", async Task<Results<Ok<FreeAppointmentsResponse>, NotFound, ValidationProblem, ProblemHttpResult>> (
                string slug,
                string? from,
                string? to,
                IProductCatalog catalog,
                AvailabilityService availability,
                CancellationToken cancellationToken) =>
            {
                if (catalog.FindBySlug(slug) is not { RequiresAppointment: true } product)
                {
                    return TypedResults.NotFound();
                }

                var errors = new Dictionary<string, string[]>();
                var fromDay = ParseDay(from, "from", errors);
                var toDay = ParseDay(to, "to", errors);
                if (fromDay > toDay || toDay?.DayNumber - fromDay?.DayNumber >= MaxRangeDays)
                {
                    errors["to"] = [$"Zakres może mieć od 1 do {MaxRangeDays} dni."];
                }

                if (fromDay is null || toDay is null || errors.Count > 0)
                {
                    return TypedResults.ValidationProblem(errors);
                }

                return await availability.FreeAsync(product, fromDay.Value, toDay.Value, cancellationToken) is { } free
                    ? TypedResults.Ok(new FreeAppointmentsResponse(product.DurationMinutes!.Value, free))
                    : TypedResults.Problem(
                        "Kalendarz Klary jest chwilowo niedostępny. Spróbuj ponownie później.",
                        statusCode: StatusCodes.Status503ServiceUnavailable);
            })
            .WithName("ListFreeAppointments");

        return api;
    }

    /// <summary>
    /// Najdłuższy zakres w jednym zapytaniu (kalendarz pokazuje miesiąc); ogranicza tylko wielkość zapytania do Google,
    /// nie to, na ile do przodu Klara przyjmuje Terminy.
    /// </summary>
    private const int MaxRangeDays = 62;

    private static DateOnly? ParseDay(string? value, string key, Dictionary<string, string[]> errors)
    {
        if (DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day))
        {
            return day;
        }

        errors[key] = ["Podaj datę w formacie RRRR-MM-DD."];
        return null;
    }
}

/// <summary>Wolne Terminy Produktu od najwcześniejszego, z przesunięciem czasu polskiego, i ich długość.</summary>
public sealed record FreeAppointmentsResponse(int DurationMinutes, IReadOnlyList<Appointment> Appointments);

/// <summary>Produkt na liście w sekcji Oferta.</summary>
public sealed record ProductSummary(
    string Slug,
    ProductKind Kind,
    ProductCategory Category,
    string Name,
    string Tagline,
    int Price,
    int? DurationMinutes)
{
    public static ProductSummary From(Product product) => new(
        product.Slug,
        product.Kind,
        product.Category,
        product.Name,
        product.Tagline,
        product.Price,
        product.DurationMinutes);
}

/// <summary>Produkt na jego podstronie: z opisem i formularzem, który Klient wypełni przy dodawaniu do Koszyka.</summary>
public sealed record ProductDetails(
    string Slug,
    ProductKind Kind,
    ProductCategory Category,
    string Name,
    string Tagline,
    int Price,
    int? DurationMinutes,
    IReadOnlyList<DescriptionBlock> Description,
    IReadOnlyList<FormField> Form)
{
    public static ProductDetails From(Product product) => new(
        product.Slug,
        product.Kind,
        product.Category,
        product.Name,
        product.Tagline,
        product.Price,
        product.DurationMinutes,
        product.Description,
        product.Form);
}
