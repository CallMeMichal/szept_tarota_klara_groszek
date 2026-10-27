namespace SzeptTarota.Application.Email;

/// <summary>Wysyłka maili do Klientów i do Klary (Resend).</summary>
public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken);
}

/// <summary>
/// Mail do wysłania. Drugi mail z tym samym <see cref="IdempotencyKey"/> nie zostanie wysłany (Resend pamięta
/// klucze przez 24 godziny), więc ponowiona wysyłka po częściowej awarii nie dubluje wiadomości.
/// </summary>
public sealed record EmailMessage(
    string To,
    string Subject,
    string Html,
    IReadOnlyList<EmailAttachment> Attachments,
    string? IdempotencyKey = null);

public sealed record EmailAttachment(string FileName, string ContentType, byte[] Content);
