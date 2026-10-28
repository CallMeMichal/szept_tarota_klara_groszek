using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using SzeptTarota.Api.Tests.Fakes;

namespace SzeptTarota.Api.Tests.Infrastructure;

public static partial class LoginExtensions
{
    /// <summary>
    /// Klient zalogowany na <paramref name="email"/> tak jak w przeglądarce: prosi o kod logowania,
    /// odczytuje go z maila i wpisuje. Klient zapamiętuje ciasteczko sesji.
    /// </summary>
    public static async Task<HttpClient> LoggedInClient(
        this WebApplicationFactory<Program> api, FakeEmailSender emails, string email)
    {
        var client = api.CreateBrowser();
        var requested = await client.PostAsJsonAsync("/api/account/login-code", new { email });
        Assert.Equal(HttpStatusCode.Accepted, requested.StatusCode);

        var loggedIn = await client.PostAsJsonAsync("/api/account/login", new { email, code = emails.LoginCodeFor(email) });
        Assert.Equal(HttpStatusCode.OK, loggedIn.StatusCode);
        return client;
    }

    /// <summary>
    /// Przechodzi logowanie przez Google tak jak przeglądarka: API odsyła na stronę Google, Klient loguje się tam
    /// na konto z adresem <paramref name="email"/>, a Google odsyła go z kodem z powrotem do API. Zwraca odpowiedź
    /// API na ten powrót (przekierowanie na stronę); ciasteczka zostają w kliencie.
    /// </summary>
    public static async Task<HttpResponseMessage> LogInWithGoogle(
        this HttpClient client, FakeGoogle google, string email, bool emailVerified = true, string? returnUrl = null)
    {
        var challenge = await client.GetAsync(
            returnUrl is null ? "/api/account/google" : $"/api/account/google?returnUrl={Uri.EscapeDataString(returnUrl)}");
        Assert.Equal(HttpStatusCode.Redirect, challenge.StatusCode);
        var googlePage = challenge.Headers.Location!;
        Assert.Equal("accounts.google.com", googlePage.Host);

        var query = QueryHelpers.ParseQuery(googlePage.Query);
        var callback = new Uri(query["redirect_uri"].ToString());
        var code = google.SignIn(email, emailVerified);
        return await client.GetAsync(
            $"{callback.AbsolutePath}?code={Uri.EscapeDataString(code)}&state={Uri.EscapeDataString(query["state"].ToString())}");
    }

    /// <summary>Klient HTTP, który jak przeglądarka odsyła ciasteczka ustawione przez API (<see cref="BrowserCookies"/>).</summary>
    public static HttpClient CreateBrowser(this WebApplicationFactory<Program> api) =>
        api.CreateDefaultClient(new BrowserCookies());

    /// <summary>Kod z ostatniego maila z kodem logowania wysłanego na <paramref name="email"/>.</summary>
    public static string LoginCodeFor(this FakeEmailSender emails, string email)
    {
        var html = emails.Sent
            .Last(m => string.Equals(m.To, email.Trim(), StringComparison.OrdinalIgnoreCase) && m.Subject.Contains("Kod logowania"))
            .Html;
        var match = LoginCode().Match(html);
        Assert.True(match.Success, $"Brak kodu logowania w mailu: {html}");
        return match.Groups["code"].Value;
    }

    [GeneratedRegex("<strong[^>]*>(?<code>[0-9]{6})</strong>")]
    private static partial Regex LoginCode();
}
