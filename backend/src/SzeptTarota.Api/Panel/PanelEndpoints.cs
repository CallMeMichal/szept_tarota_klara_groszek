using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;
using SzeptTarota.Application;
using SzeptTarota.Application.Accounts;
using SzeptTarota.Application.Calendar;

namespace SzeptTarota.Api.Panel;

/// <summary>Panel Klary: dostęp ma wyłącznie Konto o adresie <c>Shop:KlaraEmail</c>.</summary>
public static class PanelEndpoints
{
    public const string KlaraPolicy = "Klara";

    /// <summary>Strona panelu we frontendzie; tu wraca przeglądarka po zgodzie w Google.</summary>
    private const string PanelPagePath = "/panel";

    /// <summary>Ciasteczko z <c>state</c> zgody w Google, sprawdzanym po powrocie (ochrona przed CSRF w OAuth).</summary>
    private const string StateCookie = "szept-tarota-kalendarz";

    /// <summary>Ciasteczko <see cref="StateCookie"/> trafia tylko do endpointów połączenia z kalendarzem.</summary>
    private const string StateCookiePath = "/api/panel/calendar";

    /// <summary>Polityka <see cref="KlaraPolicy"/>: zalogowane Konto o adresie Klary.</summary>
    public static IServiceCollection AddKlaraAuthorization(this IServiceCollection services)
    {
        services.AddAuthorizationBuilder()
            .AddPolicy(KlaraPolicy, policy => policy.RequireAuthenticatedUser().AddRequirements(new KlaraRequirement()));
        return services.AddScoped<IAuthorizationHandler, KlaraHandler>();
    }

    public static IEndpointRouteBuilder MapPanelEndpoints(this IEndpointRouteBuilder api)
    {
        var calendar = api.MapGroup("/panel/calendar").RequireAuthorization(KlaraPolicy);

        calendar.MapGet("", async (CalendarConnectionService connection, CancellationToken cancellationToken) =>
                TypedResults.Ok(CalendarConnectionResponse.From(await connection.GetStatusAsync(cancellationToken))))
            .WithName("GetCalendarConnection");

        // Przeglądarka przechodzi tu z przycisku „Połącz z Google Calendar”; API odsyła ją do zgody w Google.
        calendar.MapGet("/connect", (CalendarConnectionService connection, HttpContext context) =>
            {
                var state = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
                Uri consentUrl;
                try
                {
                    consentUrl = connection.ConsentUrl(state);
                }
                catch (Exception e) when (e is GoogleCalendarNotConfiguredException or ShopNotConfiguredException)
                {
                    return TypedResults.Redirect(ResultPath(connected: false));
                }

                context.Response.Cookies.Append(StateCookie, state, new CookieOptions
                {
                    HttpOnly = true,
                    Secure = true,
                    // Lax: ciasteczko wraca z przekierowaniem z Google (nawigacja na najwyższym poziomie).
                    SameSite = SameSiteMode.Lax,
                    Path = StateCookiePath,
                    MaxAge = TimeSpan.FromMinutes(15),
                });
                return TypedResults.Redirect(consentUrl.ToString());
            })
            .WithName("ConnectCalendar");

        // Powrót z Google (CalendarConnectionService.CallbackPath): z kodem po zgodzie albo z błędem po odmowie.
        calendar.MapGet("/callback", async (
                string? code,
                string? state,
                CalendarConnectionService connection,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var expectedState = context.Request.Cookies[StateCookie];
                context.Response.Cookies.Delete(StateCookie, new CookieOptions { Path = StateCookiePath, Secure = true });

                var connected = code is { Length: > 0 }
                    && expectedState is { Length: > 0 } && state is { Length: > 0 }
                    && CryptographicOperations.FixedTimeEquals(
                        Encoding.ASCII.GetBytes(expectedState), Encoding.ASCII.GetBytes(state))
                    && await connection.ConnectAsync(code, cancellationToken);
                return TypedResults.Redirect(ResultPath(connected));
            })
            .WithName("CalendarConnected");

        calendar.MapPut("/session-calendar", async Task<Results<NoContent, ValidationProblem>> (
                SessionCalendarRequest request,
                CalendarConnectionService connection,
                CancellationToken cancellationToken) =>
                await connection.ChooseSessionCalendarAsync(request.CalendarId, cancellationToken) is { } error
                    ? TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["calendarId"] = [error] })
                    : TypedResults.NoContent())
            .WithName("ChooseSessionCalendar");

        return api;
    }

    private static string ResultPath(bool connected) =>
        $"{PanelPagePath}?kalendarz={(connected ? "polaczono" : "nieudane")}";

    private sealed class KlaraRequirement : IAuthorizationRequirement;

    /// <summary>Sprawdza adres Konta w bazie, bo ciasteczko sesji niesie tylko identyfikator Konta.</summary>
    private sealed class KlaraHandler(IAccountStore accounts, IOptions<ShopOptions> shop)
        : AuthorizationHandler<KlaraRequirement>
    {
        protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, KlaraRequirement requirement)
        {
            if (context.User.FindFirstValue(ClaimTypes.NameIdentifier) is { } id
                && Guid.TryParse(id, out var accountId)
                && await accounts.FindAsync(accountId, CancellationToken.None) is { } account
                && shop.Value.IsKlara(account.Email))
            {
                context.Succeed(requirement);
            }
        }
    }
}

public sealed record SessionCalendarRequest(string? CalendarId);

public sealed record CalendarResponse(string Id, string Name, bool Primary);

/// <summary>Stan połączenia z Google Calendar dla panelu.</summary>
public sealed record CalendarConnectionResponse(
    CalendarConnectionState Status,
    IReadOnlyList<CalendarResponse> Calendars,
    string? SessionCalendarId)
{
    public static CalendarConnectionResponse From(CalendarConnectionStatus status) => new(
        status.State,
        status.Calendars.Select(c => new CalendarResponse(c.Id, c.Name, c.Primary)).ToList(),
        status.SessionCalendarId);
}
