namespace SzeptTarota.Domain.Accounts;

/// <summary>
/// Jednorazowy link logowania wysłany na <see cref="Email"/>. W bazie leży tylko skrót tokenu, więc wyciek bazy
/// nie pozwala nikomu się zalogować. <see cref="ReturnTo"/> to ścieżka strony, na którą Klient wraca po zalogowaniu.
/// </summary>
public sealed class LoginLink
{
    public LoginLink(string tokenHash, string email, string? returnTo, DateTimeOffset createdAt, DateTimeOffset expiresAt)
    {
        TokenHash = tokenHash;
        Email = email;
        ReturnTo = returnTo;
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
    }

    // Dla EF Core.
    private LoginLink()
    {
        TokenHash = null!;
        Email = null!;
    }

    public string TokenHash { get; private set; }

    public string Email { get; private set; }

    public string? ReturnTo { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    /// <summary>Kiedy link został użyty; użyty link nie loguje drugi raz.</summary>
    public DateTimeOffset? UsedAt { get; private set; }
}
