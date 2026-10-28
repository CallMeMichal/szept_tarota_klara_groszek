using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using SzeptTarota.Api.Tests.Fakes;

namespace SzeptTarota.Api.Tests.Infrastructure;

public static partial class LoginExtensions
{
    /// <summary>
    /// Klient zalogowany na <paramref name="email"/> tak jak w przeglądarce: prosi o link logowania,
    /// odczytuje go z maila i loguje się nim. Klient zapamiętuje ciasteczko sesji.
    /// </summary>
    public static async Task<HttpClient> LoggedInClient(
        this WebApplicationFactory<Program> api, FakeEmailSender emails, string email)
    {
        var client = api.CreateBrowser();
        var requested = await client.PostAsJsonAsync("/api/account/login-link", new { email });
        Assert.Equal(HttpStatusCode.Accepted, requested.StatusCode);

        var loggedIn = await client.PostAsJsonAsync("/api/account/login", new { token = emails.LoginTokenFor(email) });
        Assert.Equal(HttpStatusCode.OK, loggedIn.StatusCode);
        return client;
    }

    /// <summary>Klient HTTP, który jak przeglądarka odsyła ciasteczka ustawione przez API (<see cref="BrowserCookies"/>).</summary>
    public static HttpClient CreateBrowser(this WebApplicationFactory<Program> api) =>
        api.CreateDefaultClient(new BrowserCookies());

    /// <summary>Token z ostatniego linku logowania wysłanego na <paramref name="email"/>.</summary>
    public static string LoginTokenFor(this FakeEmailSender emails, string email)
    {
        var html = emails.Sent
            .Last(m => string.Equals(m.To, email.Trim(), StringComparison.OrdinalIgnoreCase) && m.Subject.Contains("Zaloguj"))
            .Html;
        var match = LoginLink().Match(html);
        Assert.True(match.Success, $"Brak linku logowania w mailu: {html}");
        return match.Groups["token"].Value;
    }

    [GeneratedRegex("""href="https://szept-tarota\.test/logowanie/link\?token=(?<token>[A-Za-z0-9_-]+)""")]
    private static partial Regex LoginLink();
}
