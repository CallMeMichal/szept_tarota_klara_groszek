using Microsoft.AspNetCore.Http.HttpResults;
using SzeptTarota.Api.Cart;
using SzeptTarota.Application;
using SzeptTarota.Application.Cart;
using SzeptTarota.Application.Orders;
using SzeptTarota.Application.Payments;
using SzeptTarota.Domain.Catalog;
using SzeptTarota.Domain.Orders;

namespace SzeptTarota.Api.Orders;

public static class OrderEndpoints
{
    public static IEndpointRouteBuilder MapOrderEndpoints(this IEndpointRouteBuilder api)
    {
        api.MapPost("/checkout", async Task<Results<Ok<CheckoutStartedResponse>, ValidationProblem, Conflict<CartResponse>, ProblemHttpResult>> (
                CheckoutRequest request,
                CheckoutService checkout,
                CancellationToken cancellationToken) =>
            {
                if (request.Items is null || request.Items.Count > CartEndpoints.MaxItems
                    || request.Items.Any(i => string.IsNullOrWhiteSpace(i?.Slug) || i.Price is null))
                {
                    return TypedResults.ValidationProblem(new Dictionary<string, string[]>
                    {
                        ["items"] = [$"Koszyk musi być listą do {CartEndpoints.MaxItems} pozycji, każda ze slugiem Produktu i ceną."],
                    });
                }

                var command = new CheckoutCommand(
                    request.Items.Select(i => new CartItem(i!.Slug!, i.Price)).ToList(),
                    request.Email,
                    request.Billing is { } b ? new BillingInput(b.Name, b.Street, b.PostalCode, b.City, b.TaxId) : null,
                    (request.AcceptedConsents ?? []).OfType<string>().ToHashSet());

                try
                {
                    return await checkout.StartAsync(command, cancellationToken) switch
                    {
                        CheckoutResult.Started started => TypedResults.Ok(new CheckoutStartedResponse(started.OrderId, started.RedirectUrl)),
                        CheckoutResult.CartChanged changed => TypedResults.Conflict(CartResponse.From(changed.Cart)),
                        CheckoutResult.Invalid invalid => TypedResults.ValidationProblem(invalid.Errors),
                        _ => throw new InvalidOperationException("Nieznany wynik rozpoczęcia płatności."),
                    };
                }
                catch (Exception exception) when (exception is PaymentGatewayNotConfiguredException or ShopNotConfiguredException)
                {
                    return TypedResults.Problem(
                        "Płatności nie są jeszcze skonfigurowane.",
                        statusCode: StatusCodes.Status503ServiceUnavailable);
                }
            })
            .WithName("StartCheckout");

        api.MapGet("/orders/{id:guid}", async Task<Results<Ok<OrderSummaryResponse>, NotFound>> (
                Guid id,
                IOrderStore orders,
                CancellationToken cancellationToken) =>
                await orders.FindAsync(id, cancellationToken) is { } order
                    ? TypedResults.Ok(OrderSummaryResponse.From(order))
                    : TypedResults.NotFound())
            .WithName("GetOrder");

        // Stripe podpisuje surową treść żądania, więc czytamy ją jako tekst, zanim cokolwiek ją sparsuje.
        api.MapPost("/payments/stripe/webhook", async Task<Results<Ok, BadRequest, ProblemHttpResult>> (
                HttpRequest request,
                PaymentWebhookHandler handler,
                ILogger<PaymentWebhookHandler> logger,
                CancellationToken cancellationToken) =>
            {
                using var reader = new StreamReader(request.Body);
                var payload = await reader.ReadToEndAsync(cancellationToken);
                try
                {
                    await handler.HandleAsync(payload, request.Headers["Stripe-Signature"], cancellationToken);
                    return TypedResults.Ok();
                }
                catch (InvalidPaymentWebhookException)
                {
                    return TypedResults.BadRequest();
                }
                catch (PaymentGatewayNotConfiguredException)
                {
                    return TypedResults.Problem(
                        "Webhooki Stripe nie są jeszcze skonfigurowane.",
                        statusCode: StatusCodes.Status503ServiceUnavailable);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    // Odpowiedź 500 sprawia, że Stripe ponowi powiadomienie (np. gdy nie poszły maile).
                    logger.LogError(exception, "Nie udało się obsłużyć webhooka Stripe.");
                    return TypedResults.Problem(statusCode: StatusCodes.Status500InternalServerError);
                }
            })
            .WithName("StripeWebhook");

        return api;
    }
}

/// <summary>Formularz płatności: Koszyk z cenami, które Klient widział, e-mail, dane do faktury i klucze zaakceptowanych zgód.</summary>
public sealed record CheckoutRequest(
    IReadOnlyList<CartItemRequest?>? Items,
    string? Email,
    BillingRequest? Billing,
    IReadOnlyList<string?>? AcceptedConsents);

public sealed record BillingRequest(string? Name, string? Street, string? PostalCode, string? City, string? TaxId);

/// <summary>Zamówienie czeka na płatność; przeglądarka przechodzi na <see cref="RedirectUrl"/> (Stripe Checkout).</summary>
public sealed record CheckoutStartedResponse(Guid OrderId, Uri RedirectUrl);

/// <summary>
/// Podsumowanie Zamówienia na stronie z podziękowaniem. Identyfikator Zamówienia jest losowy i zna go tylko
/// Klient (z adresu powrotu ze Stripe), a podsumowanie nie zawiera danych osobowych.
/// </summary>
public sealed record OrderSummaryResponse(Guid Id, OrderStatus Status, IReadOnlyList<OrderLineResponse> Items, int Total)
{
    public static OrderSummaryResponse From(Order order) => new(
        order.Id,
        order.Status,
        order.Lines.Select(l => new OrderLineResponse(l.Slug, l.Name, l.Kind, l.Price)).ToList(),
        order.Total);
}

public sealed record OrderLineResponse(string Slug, string Name, ProductKind Kind, int Price);
