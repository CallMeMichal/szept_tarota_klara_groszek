using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using SzeptTarota.Api.Accounts;
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

        // Zakres dni: DayRange.
        products.MapGet("/{slug}/appointments", async Task<Results<Ok<FreeAppointmentsResponse>, NotFound, ValidationProblem, ProblemHttpResult>> (
                string slug,
                string? from,
                string? to,
                ClaimsPrincipal user,
                IProductCatalog catalog,
                AvailabilityService availability,
                CancellationToken cancellationToken) =>
            {
                if (catalog.FindBySlug(slug) is not { RequiresAppointment: true } product)
                {
                    return TypedResults.NotFound();
                }

                if (DayRange.Parse(from, to, out var errors) is not { } days)
                {
                    return TypedResults.ValidationProblem(errors);
                }

                return await availability.FreeAsync(product, days.From, days.To, user.OptionalAccountId(), cancellationToken) is { } free
                    ? TypedResults.Ok(new FreeAppointmentsResponse(product.DurationMinutes!.Value, free))
                    : TypedResults.Problem(
                        "Kalendarz Klary jest chwilowo niedostępny. Spróbuj ponownie później.",
                        statusCode: StatusCodes.Status503ServiceUnavailable);
            })
            .WithName("ListFreeAppointments");

        return api;
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
