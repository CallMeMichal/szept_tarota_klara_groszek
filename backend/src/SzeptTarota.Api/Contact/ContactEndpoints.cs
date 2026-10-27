using System.Net.Mail;
using Microsoft.AspNetCore.Http.HttpResults;
using SzeptTarota.Application.Contact;

namespace SzeptTarota.Api.Contact;

public static class ContactEndpoints
{
    private const int MaxNameLength = 100;
    private const int MaxEmailLength = 200;
    private const int MaxMessageLength = 5000;

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
            .WithName("SendContactMessage");

        return api;
    }

    private static Dictionary<string, string[]> Validate(ContactRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > MaxNameLength)
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
