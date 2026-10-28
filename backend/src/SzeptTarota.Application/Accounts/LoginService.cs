using System.Buffers.Text;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using SzeptTarota.Application.Email;
using SzeptTarota.Domain.Accounts;
using static System.Net.WebUtility;

namespace SzeptTarota.Application.Accounts;

/// <summary>
/// Logowanie bez hasła (ADR 0003): Klient podaje e-mail i dostaje na niego jednorazowy link. Konto zakłada się
/// przy pierwszym użyciu linku, więc samo poproszenie o link na cudzy adres niczego nie tworzy.
/// </summary>
public sealed class LoginService(
    IAccountStore accounts,
    IEmailSender emails,
    TimeProvider time,
    IOptions<ShopOptions> shopOptions)
{
    /// <summary>Jak długo link logowania jest ważny.</summary>
    public static readonly TimeSpan LinkLifetime = TimeSpan.FromMinutes(15);

    /// <summary>
    /// Najwięcej linków na jeden adres w czasie <see cref="LinkLifetime"/>. Limit na adres IP (AccountEndpoints) nie chroni
    /// skrzynki, na którą proszą linki z wielu adresów, więc ponad ten limit prośba jest przyjmowana, ale mail nie wychodzi.
    /// </summary>
    public const int MaxLinksPerEmail = 3;

    private const int MaxEmailLength = 200;
    private const int MaxReturnToLength = 200;

    /// <summary>Wysyła link logowania na <paramref name="email"/>; zwraca błędy pól albo <c>null</c>.</summary>
    /// <exception cref="ShopNotConfiguredException">Gdy brakuje adresu strony, na który prowadzi link.</exception>
    /// <exception cref="EmailSenderNotConfiguredException">Gdy maile nie mogą jeszcze wychodzić.</exception>
    public async Task<Dictionary<string, string[]>?> SendLinkAsync(string? email, string? returnTo, CancellationToken cancellationToken)
    {
        if (NormalizeEmail(email) is not { } address)
        {
            return new() { ["email"] = ["Podaj poprawny adres email."] };
        }

        if (!Uri.TryCreate(shopOptions.Value.SiteUrl, UriKind.Absolute, out _))
        {
            throw new ShopNotConfiguredException();
        }

        var now = time.GetUtcNow();
        if (await accounts.CountLoginLinksSinceAsync(address, now - LinkLifetime, cancellationToken) >= MaxLinksPerEmail)
        {
            return null;
        }

        var token = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));
        var safeReturnTo = IsLocalPath(returnTo) ? returnTo : null;
        await accounts.AddLoginLinkAsync(new LoginLink(Hash(token), address, safeReturnTo, now, now + LinkLifetime), cancellationToken);

        // Ścieżka powrotu nie jedzie w linku: zna ją serwer i oddaje ją przy logowaniu.
        var link = $"{shopOptions.Value.SiteUrl.TrimEnd('/')}/logowanie/link?token={token}";
        var html = new StringBuilder()
            .Append("<h1>Zaloguj się do Szept Tarota</h1>")
            .Append($"<p>Kliknij przycisk, żeby się zalogować. Link działa raz, przez {LinkLifetime.TotalMinutes} minut.</p>")
            .Append($"<p><a href=\"{HtmlEncode(link)}\">Zaloguj się</a></p>")
            .Append("<p>Jeśli to nie Ty prosisz o logowanie, zignoruj tego maila.</p>")
            .ToString();
        await emails.SendAsync(new EmailMessage(address, "Zaloguj się do Szept Tarota", html, []), cancellationToken);
        return null;
    }

    /// <summary>Konto, do którego loguje link z tokenem; <c>null</c>, gdy link jest zły, wygasł albo był już użyty.</summary>
    public async Task<LoginResult?> LogInAsync(string? token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(token) || token.Length > 100)
        {
            return null;
        }

        var now = time.GetUtcNow();
        if (await accounts.UseLoginLinkAsync(Hash(token), now, cancellationToken) is not { } link)
        {
            return null;
        }

        var account = await accounts.GetOrCreateAsync(link.Email, now, cancellationToken);
        return new LoginResult(account, link.ReturnTo);
    }

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

    /// <summary>Ścieżka tej strony (np. <c>/koszyk</c>), a nie adres innej, żeby link nie przekierowywał poza stronę.</summary>
    private static bool IsLocalPath(string? path) =>
        path is { Length: > 0 and <= MaxReturnToLength }
        && path[0] == '/'
        && !path.StartsWith("//", StringComparison.Ordinal)
        && !path.Contains('\\')
        && !path.Any(char.IsControl);

    private static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}

/// <summary>Zalogowane Konto i ścieżka, na którą Klient wraca (albo <c>null</c>).</summary>
public sealed record LoginResult(Account Account, string? ReturnTo);
