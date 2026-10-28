using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using SzeptTarota.Application.Email;

namespace SzeptTarota.Infrastructure.Email;

/// <summary>Konfiguracja sekcji <c>Resend</c>.</summary>
public sealed class ResendOptions
{
    public const string SectionName = "Resend";

    /// <summary>Klucz API Resend (<c>re_…</c>); trzymamy go poza repo.</summary>
    public string? ApiKey { get; set; }

    /// <summary>Nadawca; do czasu weryfikacji domeny Klary to domena testowa Resend.</summary>
    public string From { get; set; } = "Szept Tarota <onboarding@resend.dev>";
}

/// <summary>Wysyła maile przez API Resend (https://resend.com/docs/api-reference/emails/send-email).</summary>
public sealed class ResendEmailSender(HttpClient http, IOptions<ResendOptions> options) : IEmailSender
{
    public static readonly Uri BaseAddress = new("https://api.resend.com/");

    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        var resend = options.Value;
        if (string.IsNullOrWhiteSpace(resend.ApiKey))
        {
            throw new EmailSenderNotConfiguredException();
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, "emails")
        {
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", resend.ApiKey) },
            Content = JsonContent.Create(new
            {
                from = resend.From,
                to = new[] { message.To },
                subject = message.Subject,
                html = message.Html,
                attachments = message.Attachments.Select(a => new
                {
                    filename = a.FileName,
                    content = Convert.ToBase64String(a.Content),
                    content_type = a.ContentType,
                }),
            }),
        };

        if (message.IdempotencyKey is not null)
        {
            request.Headers.Add("Idempotency-Key", message.IdempotencyKey);
        }

        using var response = await http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new HttpRequestException($"Resend odrzucił maila ({(int)response.StatusCode}): {body}");
        }
    }
}
