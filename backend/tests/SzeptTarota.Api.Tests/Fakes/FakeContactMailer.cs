using SzeptTarota.Application.Contact;

namespace SzeptTarota.Api.Tests.Fakes;

/// <summary>Zapamiętuje wiadomości z formularza zamiast je wysyłać.</summary>
public sealed class FakeContactMailer : IContactMailer
{
    private readonly List<ContactMessage> _sent = [];

    public IReadOnlyList<ContactMessage> Sent => _sent;

    /// <summary>Udaje brak konfiguracji poczty, jak przed ustawieniem firmowego adresu.</summary>
    public bool Configured { get; set; } = true;

    public Task SendAsync(ContactMessage message, CancellationToken cancellationToken)
    {
        if (!Configured)
        {
            throw new ContactMailerNotConfiguredException();
        }

        _sent.Add(message);
        return Task.CompletedTask;
    }

    public void Reset()
    {
        _sent.Clear();
        Configured = true;
    }
}
