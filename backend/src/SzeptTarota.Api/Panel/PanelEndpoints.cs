using System.Globalization;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;
using SzeptTarota.Application;
using SzeptTarota.Application.Accounts;
using SzeptTarota.Application.Appointments;
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

        MapAvailabilityEndpoints(api.MapGroup("/panel/availability").RequireAuthorization(KlaraPolicy));
        api.MapGroup("/panel/orders").RequireAuthorization(KlaraPolicy).MapPanelOrderEndpoints();

        return api;
    }

    /// <summary>Dostępność zaznaczana w panelu: bloki „Dostępna” w Kalendarzu Sesji Klary (AvailabilityBlockService).</summary>
    private static void MapAvailabilityEndpoints(RouteGroupBuilder availability)
    {
        // Zakres dni: DayRange.
        availability.MapGet("", async Task<Results<Ok<AvailabilityWeek>, ValidationProblem, ProblemHttpResult>> (
                string? from,
                string? to,
                AvailabilityBlockService blocks,
                CancellationToken cancellationToken) =>
            {
                if (DayRange.Parse(from, to, out var errors) is not { } days)
                {
                    return TypedResults.ValidationProblem(errors);
                }

                return await blocks.ListAsync(days.From, days.To, cancellationToken) switch
                {
                    AvailabilityResult<AvailabilityWeek>.Ok ok => TypedResults.Ok(ok.Value),
                    var failure => Problem(failure),
                };
            })
            .WithName("ListAvailability");

        // Godziny w czasie polskim bez strefy, np. 2026-11-03T10:00; koniec o północy to 00:00 następnego dnia.
        availability.MapPost("", async Task<Results<Created<AvailabilityBlock>, ValidationProblem, ProblemHttpResult>> (
                AvailabilityBlockRequest request,
                AvailabilityBlockService blocks,
                CancellationToken cancellationToken) =>
            {
                if (ParseLocalTime(request.Start) is not { } start || ParseLocalTime(request.End) is not { } end)
                {
                    return TypedResults.ValidationProblem(new Dictionary<string, string[]>
                    {
                        ["start"] = ["Podaj początek i koniec w formacie RRRR-MM-DDTGG:MM."],
                    });
                }

                return await blocks.AddAsync(start, end, cancellationToken) switch
                {
                    AvailabilityResult<AvailabilityBlock>.Ok ok =>
                        TypedResults.Created($"/api/panel/availability/{Uri.EscapeDataString(ok.Value.Id)}", ok.Value),
                    AvailabilityResult<AvailabilityBlock>.Invalid invalid =>
                        TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["end"] = [invalid.Error] }),
                    var failure => Problem(failure),
                };
            })
            .WithName("AddAvailabilityBlock");

        availability.MapDelete("/{id}", async Task<Results<NoContent, ProblemHttpResult>> (
                string id,
                AvailabilityBlockService blocks,
                CancellationToken cancellationToken) =>
                await blocks.RemoveAsync(id, cancellationToken) switch
                {
                    AvailabilityResult<string>.Ok => TypedResults.NoContent(),
                    var failure => Problem(failure),
                })
            .WithName("RemoveAvailabilityBlock");
    }

    private static ProblemHttpResult Problem<T>(AvailabilityResult<T> failure) => failure switch
    {
        AvailabilityResult<T>.NoSessionCalendar => TypedResults.Problem(
            "Najpierw połącz stronę z Google Calendar i wybierz Kalendarz Sesji.", statusCode: StatusCodes.Status409Conflict),
        _ => TypedResults.Problem(
            "Google Calendar chwilowo nie odpowiada albo strona straciła dostęp. Spróbuj ponownie albo połącz kalendarz jeszcze raz.",
            statusCode: StatusCodes.Status503ServiceUnavailable),
    };

    private static DateTime? ParseLocalTime(string? value) =>
        DateTime.TryParseExact(value, "yyyy-MM-dd'T'HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var time)
            ? time
            : null;

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

/// <summary>Nowy blok „Dostępna”: początek i koniec w czasie polskim bez strefy (RRRR-MM-DDTGG:MM).</summary>
public sealed record AvailabilityBlockRequest(string? Start, string? End);

/// <summary>Kalendarz Klary; <see cref="Writable"/>: może w nim dodawać wydarzenia, więc nadaje się na Kalendarz Sesji.</summary>
public sealed record CalendarResponse(string Id, string Name, bool Primary, bool Writable);

/// <summary>Stan połączenia z Google Calendar dla panelu.</summary>
public sealed record CalendarConnectionResponse(
    CalendarConnectionState Status,
    IReadOnlyList<CalendarResponse> Calendars,
    string? SessionCalendarId)
{
    public static CalendarConnectionResponse From(CalendarConnectionStatus status) => new(
        status.State,
        status.Calendars.Select(c => new CalendarResponse(c.Id, c.Name, c.Primary, c.Writable)).ToList(),
        status.SessionCalendarId);
}
