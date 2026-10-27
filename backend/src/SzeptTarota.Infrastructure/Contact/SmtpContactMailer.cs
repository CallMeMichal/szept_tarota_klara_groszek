using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using SzeptTarota.Application.Contact;

namespace SzeptTarota.Infrastructure.Contact;

/// <summary>Konfiguracja sekcji <c>Contact</c>: dokąd i przez jaki serwer idą wiadomości z formularza.</summary>
public sealed class ContactOptions
{
    public const string SectionName = "Contact";

    /// <summary>Firmowy adres, na który trafiają wiadomości. Pusty: formularz odpowiada 503.</summary>
    public string? RecipientEmail { get; set; }

    public SmtpOptions Smtp { get; set; } = new();

    public sealed class SmtpOptions
    {
        public string? Host { get; set; }
        public int Port { get; set; } = 587;
        public string? Username { get; set; }
        public string? Password { get; set; }

        /// <summary>Nadawca wiadomości; domyślnie adres odbiorcy.</summary>
        public string? SenderEmail { get; set; }
    }
}

/// <summary>Wysyła wiadomości z formularza przez SMTP, z adresem osoby piszącej w Reply-To.</summary>
public sealed class SmtpContactMailer(IOptions<ContactOptions> options) : IContactMailer
{
    public async Task SendAsync(ContactMessage message, CancellationToken cancellationToken)
    {
        var contact = options.Value;
        if (string.IsNullOrWhiteSpace(contact.RecipientEmail) || string.IsNullOrWhiteSpace(contact.Smtp.Host))
        {
            throw new ContactMailerNotConfiguredException();
        }

        var email = new MimeMessage();
        var sender = string.IsNullOrWhiteSpace(contact.Smtp.SenderEmail) ? contact.RecipientEmail : contact.Smtp.SenderEmail;
        email.From.Add(new MailboxAddress("Szept Tarota", sender));
        email.To.Add(MailboxAddress.Parse(contact.RecipientEmail));
        email.ReplyTo.Add(new MailboxAddress(message.Name, message.Email));
        email.Subject = $"Wiadomość ze strony od {message.Name}";
        email.Body = new TextPart("plain")
        {
            Text = $"Od: {message.Name} <{message.Email}>\n\n{message.Message}",
        };

        using var smtp = new SmtpClient();
        await smtp.ConnectAsync(contact.Smtp.Host, contact.Smtp.Port, SecureSocketOptions.Auto, cancellationToken);
        if (!string.IsNullOrEmpty(contact.Smtp.Username))
        {
            await smtp.AuthenticateAsync(contact.Smtp.Username, contact.Smtp.Password ?? "", cancellationToken);
        }

        await smtp.SendAsync(email, cancellationToken);
        await smtp.DisconnectAsync(quit: true, cancellationToken);
    }
}
