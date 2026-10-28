using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using SzeptTarota.Api.Accounts;
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
        api.MapPost("/checkout", async Task<Results<Ok<CheckoutStartedResponse>, ValidationProblem, Conflict<CartResponse>, UnauthorizedHttpResult, ProblemHttpResult>> (
                CheckoutRequest request,
                ClaimsPrincipal user,
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
                    request.Items.Select(i => i!.ToCartItem()).ToList(),
                    user.AccountId(),
                    request.Email,
                    request.Name,
                    (request.AcceptedConsents ?? []).OfType<string>().ToHashSet());

                try
                {
                    return await checkout.StartAsync(command, cancellationToken) switch
                    {
                        CheckoutResult.Started started => TypedResults.Ok(new CheckoutStartedResponse(started.OrderId, started.RedirectUrl)),
                        CheckoutResult.CartChanged changed => TypedResults.Conflict(CartResponse.From(changed.Cart)),
                        CheckoutResult.Invalid invalid => TypedResults.ValidationProblem(invalid.Errors),
                        CheckoutResult.NotLoggedIn => TypedResults.Unauthorized(),
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
            .RequireAuthorization()
            .WithName("StartCheckout");

        // Cudze Zamówienie wygląda jak nieistniejące, żeby nie zdradzać, że taki identyfikator jest.
        api.MapGet("/orders/{id:guid}", async Task<Results<Ok<OrderSummaryResponse>, NotFound>> (
                Guid id,
                ClaimsPrincipal user,
                IOrderStore orders,
                CancellationToken cancellationToken) =>
                await orders.FindAsync(id, cancellationToken) is { } order && order.AccountId == user.AccountId()
                    ? TypedResults.Ok(OrderSummaryResponse.From(order))
                    : TypedResults.NotFound())
            .RequireAuthorization()
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

/// <summary>
/// Formularz płatności zalogowanego Klienta: Koszyk z cenami, które widział, e-mail na potwierdzenie (frontend
/// wpisuje adres Konta, Klient może go zmienić), imię i nazwisko i klucze zaakceptowanych zgód.
/// </summary>
public sealed record CheckoutRequest(
    IReadOnlyList<CartItemRequest?>? Items,
    string? Email,
    string? Name,
    IReadOnlyList<string?>? AcceptedConsents);

/// <summary>Zamówienie czeka na płatność; przeglądarka przechodzi na <see cref="RedirectUrl"/> (Stripe Checkout).</summary>
public sealed record CheckoutStartedResponse(Guid OrderId, Uri RedirectUrl);

/// <summary>
/// Podsumowanie Zamówienia na stronie z podziękowaniem (adres powrotu ze Stripe), widoczne tylko dla Konta,
/// które je złożyło.
/// </summary>
public sealed record OrderSummaryResponse(Guid Id, OrderStatus Status, IReadOnlyList<OrderLineResponse> Items, int Total)
{
    public static OrderSummaryResponse From(Order order) => new(
        order.Id,
        order.Status,
        order.Lines.Select(OrderLineResponse.From).ToList(),
        order.Total);
}

/// <summary>Pozycja Zamówienia: <see cref="Price"/> to cena jednej sztuki w groszach.</summary>
public sealed record OrderLineResponse(string Slug, string Name, ProductKind Kind, int Quantity, int Price)
{
    public static OrderLineResponse From(OrderLine line) => new(line.Slug, line.Name, line.Kind, line.Quantity, line.Price);
}
