using System.Net.Mail;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.RateLimiting;
using SzeptTarota.Application.Contact;

namespace SzeptTarota.Api.Contact;

public static class ContactEndpoints
{
    private const int MaxNameLength = 100;
    private const int MaxEmailLength = 200;
    private const int MaxMessageLength = 5000;
    private const string RateLimitPolicy = "contact";

    /// <summary>
    /// Limit wiadomości z jednego adresu IP, żeby formularz nie służył do zasypywania skrzynki.
    /// Domyślnie 5 na 10 minut; zmienia się w <c>Contact:RateLimit:PermitLimit</c> i <c>WindowMinutes</c>.
    /// </summary>
    public static IServiceCollection AddContactRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        var permitLimit = configuration.GetValue("Contact:RateLimit:PermitLimit", 5);
        var window = TimeSpan.FromMinutes(configuration.GetValue("Contact:RateLimit:WindowMinutes", 10));

        return services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(RateLimitPolicy, context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = permitLimit, Window = window }));
        });
    }

    public static IEndpointRouteBuilder MapContactEndpoints(this IEndpointRouteBuilder api)
    {
        api.MapPost("/contact", async Task<Results<NoContent, ValidationProblem, ProblemHttpResult>> (
                ContactRequest request,
                IContactMailer mailer,
                CancellationToken cancellationToken) =>
            {
                // Pole-pułapka niewidoczne dla ludzi: wypełnia je tylko bot, więc
                // udajemy sukces, żeby nie uczył się, jak obejść formularz.
                if (!string.IsNullOrEmpty(request.Website))
                {
                    return TypedResults.NoContent();
                }

                var errors = Validate(request);
                if (errors.Count > 0)
                {
                    return TypedResults.ValidationProblem(errors);
                }

                try
                {
                    await mailer.SendAsync(
                        new ContactMessage(request.Name!.Trim(), request.Email!.Trim(), request.Message!.Trim()),
                        cancellationToken);
                }
                catch (ContactMailerNotConfiguredException)
                {
                    return TypedResults.Problem(
                        "Formularz kontaktowy nie jest jeszcze skonfigurowany.",
                        statusCode: StatusCodes.Status503ServiceUnavailable);
                }

                return TypedResults.NoContent();
            })
            .RequireRateLimiting(RateLimitPolicy)
            .WithName("SendContactMessage");

        return api;
    }

    private static Dictionary<string, string[]> Validate(ContactRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        // Imię trafia do nagłówka Reply-To, więc bez znaków sterujących (np. nowych linii).
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > MaxNameLength
            || request.Name.Any(char.IsControl))
        {
            errors["name"] = [$"Podaj imię (do {MaxNameLength} znaków)."];
        }

        var email = request.Email?.Trim();
        if (string.IsNullOrEmpty(email) || email.Length > MaxEmailLength
            || !MailAddress.TryCreate(email, out var address) || address.Address != email)
        {
            errors["email"] = ["Podaj poprawny adres email."];
        }

        if (string.IsNullOrWhiteSpace(request.Message) || request.Message.Trim().Length > MaxMessageLength)
        {
            errors["message"] = [$"Napisz wiadomość (do {MaxMessageLength} znaków)."];
        }

        return errors;
    }
}

/// <summary>Wiadomość z formularza w sekcji Kontakt; <see cref="Website"/> to pole-pułapka na boty.</summary>
public sealed record ContactRequest(string? Name, string? Email, string? Message, string? Website);
