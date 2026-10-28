using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using SzeptTarota.Application.Email;
using SzeptTarota.Domain.Accounts;

namespace SzeptTarota.Application.Accounts;

/// <summary>
/// Logowanie bez hasła (ADR 0003): Klient podaje e-mail, dostaje na niego jednorazowy kod i wpisuje go na stronie,
/// albo loguje się przez Google. Konto zakłada się przy pierwszym poprawnym kodzie (więc samo poproszenie o kod na cudzy
/// adres niczego nie tworzy) albo pierwszym logowaniu przez Google; oba sposoby prowadzą do jednego Konta na adres.
/// </summary>
public sealed class LoginService(IAccountStore accounts, IEmailSender emails, TimeProvider time)
{
    /// <summary>Jak długo kod logowania jest ważny.</summary>
    public static readonly TimeSpan CodeLifetime = TimeSpan.FromMinutes(15);

    /// <summary>
    /// Najwięcej kodów na jeden adres w czasie <see cref="CodeLifetime"/>. Limit na adres IP (AccountEndpoints) nie chroni
    /// skrzynki, na którą proszą o kody z wielu adresów, więc ponad ten limit prośba jest przyjmowana, ale mail nie wychodzi.
    /// Razem z <see cref="MaxAttempts"/> ogranicza też zgadywanie: najwyżej 15 prób na 15 minut wobec miliona kodów.
    /// </summary>
    public const int MaxCodesPerEmail = 3;

    /// <summary>Po tylu próbach wpisania kod przestaje działać i trzeba poprosić o nowy.</summary>
    public const int MaxAttempts = 5;

    private const int MaxEmailLength = 200;

    /// <summary>Wysyła kod logowania na <paramref name="email"/>; zwraca błędy pól albo <c>null</c>.</summary>
    /// <exception cref="EmailSenderNotConfiguredException">Gdy maile nie mogą jeszcze wychodzić.</exception>
    public async Task<Dictionary<string, string[]>?> SendCodeAsync(string? email, CancellationToken cancellationToken)
    {
        if (NormalizeEmail(email) is not { } address)
        {
            return new() { ["email"] = ["Podaj poprawny adres email."] };
        }

        var now = time.GetUtcNow();
        if (await accounts.CountLoginCodesSinceAsync(address, now - CodeLifetime, cancellationToken) >= MaxCodesPerEmail)
        {
            return null;
        }

        var id = Guid.NewGuid();
        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        await accounts.AddLoginCodeAsync(new LoginCode(id, address, Hash(id, code), now, now + CodeLifetime), cancellationToken);

        var html = new StringBuilder()
            .Append("<h1>Twój kod logowania</h1>")
            .Append("<p>Wpisz ten kod na stronie Szept Tarota, żeby się zalogować:</p>")
            .Append($"<p style=\"font-size:32px;letter-spacing:6px\"><strong>{code}</strong></p>")
            .Append($"<p>Kod działa raz, przez {CodeLifetime.TotalMinutes} minut.</p>")
            .Append("<p>Jeśli to nie Ty prosisz o logowanie, zignoruj tego maila.</p>")
            .ToString();
        await emails.SendAsync(new EmailMessage(address, "Kod logowania do Szept Tarota", html, []), cancellationToken);
        return null;
    }

    /// <summary>
    /// Konto, do którego loguje kod; <c>null</c>, gdy kod jest zły, wygasł, był już użyty albo po zbyt wielu próbach.
    /// Działa tylko najnowszy kod wysłany na adres.
    /// </summary>
    public async Task<Account?> LogInAsync(string? email, string? code, CancellationToken cancellationToken)
    {
        if (NormalizeEmail(email) is not { } address || code?.Trim() is not { Length: 6 } trimmed || !trimmed.All(char.IsAsciiDigit))
        {
            return null;
        }

        var now = time.GetUtcNow();
        // Próbę doliczamy, zanim porównamy kod, więc równoczesne zgadnięcia nie zobaczą wszystkie zera prób.
        if (await accounts.FindNewestActiveLoginCodeAsync(address, now, cancellationToken) is not { } loginCode
            || !await accounts.TryStartLoginAttemptAsync(loginCode.Id, MaxAttempts, cancellationToken)
            || !CryptographicOperations.FixedTimeEquals(
                Encoding.ASCII.GetBytes(loginCode.CodeHash), Encoding.ASCII.GetBytes(Hash(loginCode.Id, trimmed))))
        {
            return null;
        }

        return await accounts.UseLoginCodeAsync(loginCode.Id, now, cancellationToken)
            ? await accounts.GetOrCreateAsync(address, now, cancellationToken)
            : null;
    }

    /// <summary>
    /// Konto, do którego loguje konto Google z adresem <paramref name="email"/>: to samo, co przy logowaniu Kodem
    /// logowania na ten adres, albo nowe. <c>null</c>, gdy Google nie potwierdził adresu, bo wtedy ktoś mógłby
    /// wejść na cudze Konto, wpisując w Google cudzy e-mail.
    /// </summary>
    public async Task<Account?> LogInWithGoogleAsync(string? email, bool emailVerified, CancellationToken cancellationToken) =>
        emailVerified && NormalizeEmail(email) is { } address
            ? await accounts.GetOrCreateAsync(address, time.GetUtcNow(), cancellationToken)
            : null;

    /// <summary>Poprawny adres małymi literami, bez spacji wokół; <c>null</c> dla niepoprawnego.</summary>
    private static string? NormalizeEmail(string? email)
    {
        var trimmed = email?.Trim();
        if (string.IsNullOrEmpty(trimmed) || trimmed.Length > MaxEmailLength
            || !MailAddress.TryCreate(trimmed, out var address) || address.Address != trimmed)
        {
            return null;
        }

        return trimmed.ToLowerInvariant();
    }

    /// <summary>Skrót kodu z identyfikatorem jako solą, żeby jedna tablica skrótów miliona kodów nie pasowała do wszystkich.</summary>
    private static string Hash(Guid id, string code) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{id:N}:{code}")));
}
