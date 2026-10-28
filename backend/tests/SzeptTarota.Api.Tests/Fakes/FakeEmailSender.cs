using SzeptTarota.Application.Email;

namespace SzeptTarota.Api.Tests.Fakes;

/// <summary>
/// Zbiera maile zamiast je wysyłać. Jak Resend pomija maila z kluczem idempotencji,
/// który już przyszedł, więc ponowiona wysyłka nie dubluje wiadomości.
/// </summary>
public sealed class FakeEmailSender : IEmailSender
{
    private readonly List<EmailMessage> _sent = [];

    public IReadOnlyList<EmailMessage> Sent => _sent;

    /// <summary>Udaje brak klucza Resend w konfiguracji.</summary>
    public bool Configured { get; set; } = true;

    /// <summary>Udaje awarię wysyłki (np. niedostępny Resend).</summary>
    public bool Failing { get; set; }

    /// <summary>Udaje awarię wysyłki tylko na ten adres.</summary>
    public string? FailingRecipient { get; set; }

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        if (!Configured)
        {
            throw new EmailSenderNotConfiguredException();
        }

        if (Failing || message.To == FailingRecipient)
        {
            throw new HttpRequestException("Resend jest niedostępny.");
        }

        lock (_sent)
        {
            if (message.IdempotencyKey is null || _sent.All(m => m.IdempotencyKey != message.IdempotencyKey))
            {
                _sent.Add(message);
            }
        }

        return Task.CompletedTask;
    }

    public void Reset()
    {
        _sent.Clear();
        Configured = true;
        Failing = false;
        FailingRecipient = null;
    }
}
