using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.RateLimiting;
using SzeptTarota.Api.Orders;
using SzeptTarota.Application.Accounts;
using SzeptTarota.Application.Email;
using SzeptTarota.Application.Orders;
using SzeptTarota.Domain.Accounts;
using SzeptTarota.Domain.Orders;

namespace SzeptTarota.Api.Accounts;

public static class AccountEndpoints
{
    private const string LoginCodeRateLimitPolicy = "login-code";

    /// <summary>
    /// Sesja Klienta w ciasteczku (frontend i API pod jednym originem). Endpointy wymagające logowania
    /// odpowiadają 401 zamiast przekierowania na stronę logowania, bo pytają je zapytania z frontendu.
    /// </summary>
    public static IServiceCollection AddAccountAuthentication(this IServiceCollection services)
    {
        services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options =>
            {
                options.Cookie.Name = "szept-tarota-konto";
                options.Cookie.HttpOnly = true;
                // Lax: przeglądarka nie dołącza ciasteczka do POST-ów z innych stron (ochrona przed CSRF).
                options.Cookie.SameSite = SameSiteMode.Lax;
                // Tylko po HTTPS, także za proxy kończącym TLS (przeglądarki traktują localhost jako bezpieczny).
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                options.ExpireTimeSpan = TimeSpan.FromDays(30);
                options.SlidingExpiration = true;
                options.Events.OnRedirectToLogin = context =>
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return Task.CompletedTask;
                };
            });
        services.AddAuthorization();
        return services;
    }

    /// <summary>
    /// Limit kodów logowania z jednego adresu IP, żeby nikt nie zasypywał cudzej skrzynki mailami.
    /// Domyślnie 5 na 10 minut; zmienia się w <c>Account:LoginCodeRateLimit:PermitLimit</c> i <c>WindowMinutes</c>.
    /// </summary>
    public static IServiceCollection AddLoginCodeRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        var permitLimit = configuration.GetValue("Account:LoginCodeRateLimit:PermitLimit", 5);
        var window = TimeSpan.FromMinutes(configuration.GetValue("Account:LoginCodeRateLimit:WindowMinutes", 10));

        return services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(LoginCodeRateLimitPolicy, context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = permitLimit, Window = window }));
        });
    }

    public static IEndpointRouteBuilder MapAccountEndpoints(this IEndpointRouteBuilder api)
    {
        var account = api.MapGroup("/account");

        account.MapPost("/login-code", async Task<Results<Accepted, ValidationProblem, ProblemHttpResult>> (
                LoginCodeRequest request,
                LoginService login,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    return await login.SendCodeAsync(request.Email, cancellationToken) is { } errors
                        ? TypedResults.ValidationProblem(errors)
                        : TypedResults.Accepted((string?)null);
                }
                catch (EmailSenderNotConfiguredException)
                {
                    return TypedResults.Problem(
                        "Logowanie nie jest jeszcze skonfigurowane.",
                        statusCode: StatusCodes.Status503ServiceUnavailable);
                }
            })
            .RequireRateLimiting(LoginCodeRateLimitPolicy)
            .WithName("SendLoginCode");

        account.MapPost("/login", async Task<Results<Ok<AccountResponse>, UnauthorizedHttpResult>> (
                LoginRequest request,
                LoginService login,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                if (await login.LogInAsync(request.Email, request.Code, cancellationToken) is not { } loggedIn)
                {
                    return TypedResults.Unauthorized();
                }

                await context.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    Principal(loggedIn),
                    new AuthenticationProperties { IsPersistent = true });
                return TypedResults.Ok(new AccountResponse(loggedIn.Email));
            })
            .WithName("LogIn");

        account.MapPost("/logout", async (HttpContext context) =>
            {
                await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                return TypedResults.NoContent();
            })
            .WithName("LogOut");

        account.MapGet("", async Task<Results<Ok<AccountResponse>, UnauthorizedHttpResult>> (
                ClaimsPrincipal user,
                IAccountStore accounts,
                CancellationToken cancellationToken) =>
                await accounts.FindAsync(user.AccountId(), cancellationToken) is { } found
                    ? TypedResults.Ok(new AccountResponse(found.Email))
                    : TypedResults.Unauthorized())
            .RequireAuthorization()
            .WithName("GetAccount");

        account.MapGet("/orders", async (ClaimsPrincipal user, IOrderStore orders, CancellationToken cancellationToken) =>
                TypedResults.Ok((await orders.ListPaidAsync(user.AccountId(), cancellationToken)).Select(MyOrderResponse.From).ToList()))
            .RequireAuthorization()
            .WithName("GetMyOrders");

        return api;
    }

    /// <summary>Konto zalogowanego Klienta (endpoint musi wymagać logowania).</summary>
    public static Guid AccountId(this ClaimsPrincipal user) =>
        Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException("Brak Konta w sesji; endpoint musi wymagać logowania."));

    private static ClaimsPrincipal Principal(Account account) => new(new ClaimsIdentity(
        [new Claim(ClaimTypes.NameIdentifier, account.Id.ToString())],
        CookieAuthenticationDefaults.AuthenticationScheme));
}

/// <summary>Prośba o kod logowania na adres e-mail.</summary>
public sealed record LoginCodeRequest(string? Email);

/// <summary>Adres, na który przyszedł kod, i wpisany kod.</summary>
public sealed record LoginRequest(string? Email, string? Code);

public sealed record AccountResponse(string Email);

/// <summary>Opłacone Zamówienie na liście „Moje Zamówienia”.</summary>
public sealed record MyOrderResponse(Guid Id, DateTimeOffset PaidAt, IReadOnlyList<OrderLineResponse> Items, int Total)
{
    public static MyOrderResponse From(Order order) => new(
        order.Id,
        order.PaidAt!.Value,
        order.Lines.Select(OrderLineResponse.From).ToList(),
        order.Total);
}
