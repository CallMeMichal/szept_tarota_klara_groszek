namespace SzeptTarota.Domain.Accounts;

/// <summary>
/// Jednorazowy kod logowania wysłany na <see cref="Email"/>. W bazie leży tylko skrót kodu. Kod jest krótki, więc
/// po kilku próbach wpisania (<see cref="Attempts"/>) przestaje działać, żeby nie dało się go zgadnąć.
/// </summary>
public sealed class LoginCode
{
    public LoginCode(Guid id, string email, string codeHash, DateTimeOffset createdAt, DateTimeOffset expiresAt)
    {
        Id = id;
        Email = email;
        CodeHash = codeHash;
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
    }

    // Dla EF Core.
    private LoginCode()
    {
        Email = null!;
        CodeHash = null!;
    }

    public Guid Id { get; private set; }

    public string Email { get; private set; }

    public string CodeHash { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    /// <summary>Kiedy kod został użyty; użyty kod nie loguje drugi raz.</summary>
    public DateTimeOffset? UsedAt { get; private set; }

    /// <summary>Ile razy próbowano wpisać ten kod (także poprawnie).</summary>
    public int Attempts { get; private set; }
}
