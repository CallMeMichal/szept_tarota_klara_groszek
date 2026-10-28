namespace SzeptTarota.Domain.Accounts;

/// <summary>
/// Konto Klienta: tożsamość, pod którą widzi swoje Zamówienia. Konto nie ma hasła: Klient loguje się Kodem logowania
/// wysłanym na <see cref="Email"/> albo przez Google z tym samym, potwierdzonym w Google adresem, więc samo zalogowanie
/// potwierdza, że adres jest jego (ADR 0003).
/// </summary>
public sealed class Account
{
    public Account(Guid id, string email, DateTimeOffset createdAt)
    {
        Id = id;
        Email = email;
        CreatedAt = createdAt;
    }

    // Dla EF Core.
    private Account()
    {
        Email = null!;
    }

    public Guid Id { get; private set; }

    /// <summary>Adres e-mail małymi literami; jedno Konto na adres.</summary>
    public string Email { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
}
