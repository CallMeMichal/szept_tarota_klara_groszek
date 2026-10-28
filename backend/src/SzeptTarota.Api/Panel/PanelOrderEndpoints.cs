using System.Globalization;
using Microsoft.AspNetCore.Http.HttpResults;
using SzeptTarota.Api.Orders;
using SzeptTarota.Application.Appointments;
using SzeptTarota.Application.Orders;
using SzeptTarota.Domain.Catalog;
using SzeptTarota.Domain.Orders;

namespace SzeptTarota.Api.Panel;

/// <summary>Opłacone Zamówienia wszystkich Klientów w panelu Klary.</summary>
public static class PanelOrderEndpoints
{
    public static void MapPanelOrderEndpoints(this RouteGroupBuilder orders)
    {
        // Filtry są opcjonalne: from i to to dni zapłaty (RRRR-MM-DD, czas polski, włącznie), product to slug Produktu.
        orders.MapGet("", async Task<Results<Ok<List<PanelOrderResponse>>, ValidationProblem>> (
                string? from,
                string? to,
                string? product,
                IOrderStore store,
                CancellationToken cancellationToken) =>
            {
                var errors = new Dictionary<string, string[]>();
                var filter = new PaidOrderFilter(
                    ParseDay(from, "from", errors),
                    ParseDay(to, "to", errors),
                    string.IsNullOrWhiteSpace(product) ? null : product.Trim());
                if (errors.Count > 0)
                {
                    return TypedResults.ValidationProblem(errors);
                }

                return TypedResults.Ok((await store.ListAllPaidAsync(filter, cancellationToken)).Select(PanelOrderResponse.From).ToList());
            })
            .WithName("ListPanelOrders");

        // Zamówienie, które czeka na płatność albo wygasło, nie jest jeszcze Zamówieniem do wykonania.
        orders.MapGet("/{id:guid}", async Task<Results<Ok<PanelOrderDetailsResponse>, NotFound>> (
                Guid id,
                IOrderStore store,
                CancellationToken cancellationToken) =>
                await store.FindAsync(id, cancellationToken) is { Status: OrderStatus.Paid } order
                    ? TypedResults.Ok(PanelOrderDetailsResponse.From(order))
                    : TypedResults.NotFound())
            .WithName("GetPanelOrder");
    }

    /// <summary>
    /// Dzień z filtra albo <c>null</c>, gdy go nie podano. Tylko lata 2000–2999: skrajne daty przepełniłyby
    /// przeliczenie na północ w czasie polskim, a Zamówień spoza tych lat i tak nie ma.
    /// </summary>
    private static DateOnly? ParseDay(string? value, string key, Dictionary<string, string[]> errors)
    {
        if (string.IsNullOrEmpty(value))
        {
            return null;
        }

        if (DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day)
            && day.Year is >= 2000 and <= 2999)
        {
            return day;
        }

        errors[key] = ["Podaj datę w formacie RRRR-MM-DD."];
        return null;
    }
}

/// <summary>Opłacone Zamówienie na liście w panelu: kiedy, kto (imię i nazwisko, e-mail z płatności), co i za ile.</summary>
public sealed record PanelOrderResponse(
    Guid Id,
    DateTimeOffset PaidAt,
    string CustomerName,
    string Email,
    IReadOnlyList<OrderLineResponse> Items,
    int Total)
{
    public static PanelOrderResponse From(Order order) => new(
        order.Id,
        order.PaidAt!.Value,
        order.CustomerName,
        order.Email,
        order.Lines.Select(OrderLineResponse.From).ToList(),
        order.Total);
}

/// <summary>Szczegóły opłaconego Zamówienia w panelu, z odpowiedziami z formularza i Terminem każdej Usługi.</summary>
public sealed record PanelOrderDetailsResponse(
    Guid Id,
    DateTimeOffset PaidAt,
    string CustomerName,
    string Email,
    IReadOnlyList<PanelOrderLineResponse> Items,
    int Total)
{
    public static PanelOrderDetailsResponse From(Order order) => new(
        order.Id,
        order.PaidAt!.Value,
        order.CustomerName,
        order.Email,
        order.Lines.Select(PanelOrderLineResponse.From).ToList(),
        order.Total);
}

/// <summary>
/// Pozycja Zamówienia w panelu: odpowiedzi z formularza z etykietami z chwili zakupu i Termin w czasie polskim;
/// <see cref="Price"/> to cena jednej sztuki w groszach.
/// </summary>
public sealed record PanelOrderLineResponse(
    string Slug,
    string Name,
    ProductKind Kind,
    int Quantity,
    int Price,
    IReadOnlyList<FormAnswer> FormAnswers,
    Appointment? Appointment)
{
    public static PanelOrderLineResponse From(OrderLine line) => new(
        line.Slug,
        line.Name,
        line.Kind,
        line.Quantity,
        line.Price,
        line.FormAnswers,
        line is { AppointmentStart: { } start, AppointmentEnd: { } end }
            ? AvailabilityService.InWarsaw(new Appointment(start, end))
            : null);
}
