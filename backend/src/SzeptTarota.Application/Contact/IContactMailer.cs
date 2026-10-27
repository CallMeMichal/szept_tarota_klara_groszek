namespace SzeptTarota.Application.Contact;

/// <summary>Wiadomość z formularza w sekcji Kontakt, wysyłana na firmowy adres email.</summary>
public sealed record ContactMessage(string Name, string Email, string Message);

/// <summary>Dostarcza wiadomości z formularza kontaktowego do Klary.</summary>
public interface IContactMailer
{
    /// <exception cref="ContactMailerNotConfiguredException">Gdy nie ustawiono adresu odbiorcy albo serwera poczty.</exception>
    Task SendAsync(ContactMessage message, CancellationToken cancellationToken);
}

/// <summary>Formularz nie ma jeszcze gdzie wysyłać: brakuje konfiguracji <c>Contact</c>.</summary>
public sealed class ContactMailerNotConfiguredException()
    : InvalidOperationException("Brak konfiguracji 'Contact:RecipientEmail' albo 'Contact:Smtp:Host'.");
