using System.Security.Claims;
using System.Text.Json;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using SzeptTarota.Application;
using SzeptTarota.Api.Orders;
using SzeptTarota.Api.Panel;
using SzeptTarota.Application.Accounts;
using SzeptTarota.Application.Email;
using SzeptTarota.Application.Orders;
using SzeptTarota.Domain.Accounts;
using SzeptTarota.Domain.Orders;

namespace SzeptTarota.Api.Accounts;

public static class AccountEndpoints
{
    private const string LoginCodeRateLimitPolicy = "login-code";
    private const string LoginRateLimitPolicy = "login";

    /// <summary>Strona frontendu, na którą Klient wraca po zalogowaniu, gdy nie wskazał innej.</summary>
    private const string AccountPagePath = "/konto";

    /// <summary>Strona logowania frontendu (<c>loginPath</c>); <c>dalej</c> to powrót po zalogowaniu.</summary>
    private const string LoginPagePath = "/logowanie";

    /// <summary>Czy Google potwierdził adres; bez tego logowanie przez Google nie wchodzi na Konto.</summary>
    private const string EmailVerifiedClaim = "email_verified";

    /// <summary>
    /// Sesja Klienta w ciasteczku (frontend i API pod jednym originem). Endpointy wymagające logowania
    /// odpowiadają 401 zamiast przekierowania na stronę logowania, bo pytają je zapytania z frontendu.
    /// Logowanie przez Google działa, gdy w konfiguracji są <c>Google:ClientId</c> i <c>Google:ClientSecret</c>.
    /// </summary>
    public static IServiceCollection AddAccountAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var authentication = services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
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
                // Zalogowany, ale bez dostępu (np. Klient w panelu Klary).
                options.Events.OnRedirectToAccessDenied = context =>
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return Task.CompletedTask;
                };
            });

        // Bez kluczy nie rejestrujemy Google wcale: handler z pustym ClientId psułby każde zapytanie.
        var google = configuration.GetSection("Google");
        if (!string.IsNullOrEmpty(google["ClientId"]) && !string.IsNullOrEmpty(google["ClientSecret"]))
        {
            authentication.AddGoogle(options =>
            {
                options.ClientId = google["ClientId"]!;
                options.ClientSecret = google["ClientSecret"]!;
                // Pod /api, żeby powrót z Google trafił przez proxy frontendu do API.
                options.CallbackPath = "/api/account/google/callback";
                options.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.ClaimActions.MapCustomJson(EmailVerifiedClaim, user =>
                    user.TryGetProperty("email_verified", out var verified) && verified.ValueKind == JsonValueKind.True
                        ? "true"
                        : null);
                options.Events.OnTicketReceived = LogInWithGoogleAsync;
                options.Events.OnRemoteFailure = context =>
                {
                    // Np. Klient anulował logowanie w Google.
                    context.Response.Redirect(GoogleLoginFailedPath(context.Properties?.RedirectUri));
                    context.HandleResponse();
                    return Task.CompletedTask;
                };
            });
        }

        services.AddKlaraAuthorization();
        return services;
    }

    /// <summary>
    /// Adres powrotu z Google (redirect_uri) handler buduje z adresu zapytania, a API stoi za proxy frontendu
    /// (lokalnie Vite, na hostingu proxy kończące TLS), więc widziałby swój wewnętrzny adres. Dla ścieżek logowania
    /// przez Google podstawiamy adres strony z <c>Shop:SiteUrl</c>: Google odsyła Klienta na stronę, a wymiana kodu
    /// podaje ten sam adres. Musi stać przed <c>UseAuthentication</c>.
    /// </summary>
    public static IApplicationBuilder UseSiteAddressForGoogleLogin(this IApplicationBuilder app) =>
        app.Use((context, next) =>
        {
            var siteUrl = context.RequestServices.GetRequiredService<IOptions<ShopOptions>>().Value.SiteUrl;
            if (context.Request.Path.StartsWithSegments("/api/account/google")
                && Uri.TryCreate(siteUrl, UriKind.Absolute, out var site))
            {
                context.Request.Scheme = site.Scheme;
                // Authority pomija domyślny port, więc adres zgadza się z tym wpisanym w Google.
                context.Request.Host = new HostString(site.Authority);
            }

            return next(context);
        });

    /// <summary>
    /// Zamienia tożsamość z Google na Konto o tym samym adresie, więc ciasteczko sesji jest takie samo jak po
    /// logowaniu Kodem logowania. Bez potwierdzonego w Google adresu Klient wraca na stronę logowania.
    /// </summary>
    private static async Task LogInWithGoogleAsync(TicketReceivedContext context)
    {
        var login = context.HttpContext.RequestServices.GetRequiredService<LoginService>();
        if (await login.LogInWithGoogleAsync(
                context.Principal?.FindFirstValue(ClaimTypes.Email),
                context.Principal?.HasClaim(EmailVerifiedClaim, "true") == true,
                context.HttpContext.RequestAborted) is not { } account)
        {
            context.Response.Redirect(GoogleLoginFailedPath(context.ReturnUri));
            context.HandleResponse();
            return;
        }

        context.Principal = Principal(account);
    }

    /// <summary>Strona logowania z komunikatem o nieudanym logowaniu przez Google i tym samym powrotem.</summary>
    private static string GoogleLoginFailedPath(string? returnUrl) =>
        LocalPathOrNull(returnUrl) is { } path && path != AccountPagePath
            ? $"{LoginPagePath}?google=nieudane&dalej={Uri.EscapeDataString(path)}"
            : $"{LoginPagePath}?google=nieudane";

    /// <summary>
    /// Ścieżka na tej stronie (np. <c>/koszyk</c>) albo <c>null</c>, żeby powrót po logowaniu nie wyprowadził Klienta
    /// na cudzą stronę; te same zasady co <c>localPathOrNull</c> we frontendzie.
    /// </summary>
    private static string? LocalPathOrNull(string? path) =>
        path is { Length: > 0 and <= 200 } && path.StartsWith('/') && !path.StartsWith("//")
        && !path.Contains('\\') && !path.Any(char.IsControl)
            ? path
            : null;

    /// <summary>
    /// Limity z jednego adresu IP: prośby o kod (domyślnie 5 na 10 minut, <c>Account:LoginCodeRateLimit</c>), żeby nikt
    /// nie zasypywał cudzej skrzynki mailami, i próby logowania (domyślnie 20 na 10 minut, <c>Account:LoginRateLimit</c>),
    /// żeby nikt nie zgadywał kodów wielu adresów naraz. Każdy ma <c>PermitLimit</c> i <c>WindowMinutes</c>.
    /// </summary>
    public static IServiceCollection AddLoginRateLimiting(this IServiceCollection services, IConfiguration configuration) =>
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            AddPerIpPolicy(options, LoginCodeRateLimitPolicy, configuration.GetSection("Account:LoginCodeRateLimit"), defaultLimit: 5);
            AddPerIpPolicy(options, LoginRateLimitPolicy, configuration.GetSection("Account:LoginRateLimit"), defaultLimit: 20);
        });

    private static void AddPerIpPolicy(RateLimiterOptions options, string policy, IConfiguration settings, int defaultLimit)
    {
        var permitLimit = settings.GetValue("PermitLimit", defaultLimit);
        var window = TimeSpan.FromMinutes(settings.GetValue("WindowMinutes", 10));
        options.AddPolicy(policy, context => RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = permitLimit, Window = window }));
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
                IOptions<ShopOptions> shop,
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
                return TypedResults.Ok(AccountResponse.From(loggedIn, shop.Value));
            })
            .RequireRateLimiting(LoginRateLimitPolicy)
            .WithName("LogIn");

        // Przeglądarka przechodzi tu z przycisku „Zaloguj przez Google”; API odsyła ją do Google, a po powrocie
        // (CallbackPath) loguje do Konta i przekierowuje na returnUrl, domyślnie do Konta.
        account.MapGet("/google", async Task<Results<ChallengeHttpResult, RedirectHttpResult>> (
                string? returnUrl,
                IAuthenticationSchemeProvider schemes) =>
                await schemes.GetSchemeAsync(GoogleDefaults.AuthenticationScheme) is null
                    ? TypedResults.Redirect(GoogleLoginFailedPath(returnUrl))
                    : TypedResults.Challenge(
                        new GoogleChallengeProperties
                        {
                            RedirectUri = LocalPathOrNull(returnUrl) ?? AccountPagePath,
                            IsPersistent = true,
                            // Bez tego Google sam wybiera ostatnio używane konto i nie da się zalogować innym.
                            Prompt = "select_account",
                        },
                        [GoogleDefaults.AuthenticationScheme]))
            .WithName("LogInWithGoogle");

        account.MapPost("/logout", async (HttpContext context) =>
            {
                await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                return TypedResults.NoContent();
            })
            .WithName("LogOut");

        account.MapGet("", async Task<Results<Ok<AccountResponse>, UnauthorizedHttpResult>> (
                ClaimsPrincipal user,
                IAccountStore accounts,
                IOptions<ShopOptions> shop,
                CancellationToken cancellationToken) =>
                await accounts.FindAsync(user.AccountId(), cancellationToken) is { } found
                    ? TypedResults.Ok(AccountResponse.From(found, shop.Value))
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

/// <param name="IsKlara">Czy to Konto Klary, które ma dostęp do panelu.</param>
public sealed record AccountResponse(string Email, bool IsKlara)
{
    public static AccountResponse From(Account account, ShopOptions shop) => new(account.Email, shop.IsKlara(account.Email));
}

/// <summary>Opłacone Zamówienie na liście „Moje Zamówienia”.</summary>
public sealed record MyOrderResponse(Guid Id, DateTimeOffset PaidAt, IReadOnlyList<OrderLineResponse> Items, int Total)
{
    public static MyOrderResponse From(Order order) => new(
        order.Id,
        order.PaidAt!.Value,
        order.Lines.Select(OrderLineResponse.From).ToList(),
        order.Total);
}
