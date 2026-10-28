using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.WebUtilities;
using SzeptTarota.Api.Tests.Infrastructure;

namespace SzeptTarota.Api.Tests;

[Collection(ApiCollection.Name)]
public class GoogleLoginTests
{
    private readonly ApiFactory _factory;

    public GoogleLoginTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Logging_in_with_Google_opens_a_Konto_on_the_Google_email()
    {
        var email = NewEmail();
        var client = _factory.CreateBrowser();

        await client.LogInWithGoogle(_factory.Google, email);

        var account = await client.GetFromJsonAsync<AccountResponse>("/api/account");
        Assert.Equal(email, account!.Email);
    }

    [Fact]
    public async Task The_Konto_shows_the_name_and_profile_picture_from_Google()
    {
        var client = _factory.CreateBrowser();

        await client.LogInWithGoogle(
            _factory.Google, NewEmail(), name: "Klara Groszek", picture: "https://lh3.googleusercontent.com/a/klara=s96-c");

        var account = await client.GetFromJsonAsync<AccountResponse>("/api/account");
        Assert.Equal("Klara Groszek", account!.Name);
        Assert.Equal("https://lh3.googleusercontent.com/a/klara=s96-c", account.PictureUrl);
    }

    [Fact]
    public async Task Google_sends_the_Klient_back_to_the_site_address_not_to_the_API_behind_it()
    {
        var challenge = await _factory.CreateBrowser().GetAsync("/api/account/google");

        var redirectUri = QueryHelpers.ParseQuery(challenge.Headers.Location!.Query)["redirect_uri"].ToString();
        Assert.Equal($"{ApiFactory.SiteUrl}/api/account/google/callback", redirectUri);
    }

    [Fact]
    public async Task Google_always_asks_which_Google_account_to_use()
    {
        var challenge = await _factory.CreateBrowser().GetAsync("/api/account/google");

        Assert.Equal("select_account", QueryHelpers.ParseQuery(challenge.Headers.Location!.Query)["prompt"].ToString());
    }

    [Fact]
    public async Task After_logging_in_with_Google_the_Klient_lands_where_they_started()
    {
        var client = _factory.CreateBrowser();

        var response = await client.LogInWithGoogle(_factory.Google, NewEmail(), returnUrl: "/koszyk");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/koszyk", response.Headers.Location!.OriginalString);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("https://zla-strona.example/")]
    [InlineData("//zla-strona.example/")]
    public async Task Without_a_return_page_on_this_site_the_Klient_lands_in_their_Konto(string? returnUrl)
    {
        var client = _factory.CreateBrowser();

        var response = await client.LogInWithGoogle(_factory.Google, NewEmail(), returnUrl: returnUrl);

        Assert.Equal("/konto", response.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task A_Google_account_with_an_unconfirmed_email_does_not_log_in()
    {
        var client = _factory.CreateBrowser();

        var response = await client.LogInWithGoogle(_factory.Google, NewEmail(), emailVerified: false, returnUrl: "/koszyk");

        Assert.Equal("/logowanie?google=nieudane&dalej=%2Fkoszyk", response.Headers.Location!.OriginalString);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/account")).StatusCode);
    }

    [Fact]
    public async Task Cancelling_at_Google_returns_to_the_login_page()
    {
        var client = _factory.CreateBrowser();
        var challenge = await client.GetAsync("/api/account/google?returnUrl=%2Fkoszyk");
        var state = QueryHelpers.ParseQuery(challenge.Headers.Location!.Query)["state"].ToString();

        var response = await client.GetAsync($"/api/account/google/callback?error=access_denied&state={Uri.EscapeDataString(state)}");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/logowanie?google=nieudane&dalej=%2Fkoszyk", response.Headers.Location!.OriginalString);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/account")).StatusCode);
    }

    [Fact]
    public async Task Without_Google_keys_the_Klient_returns_to_the_login_page()
    {
        await using var withoutGoogle = _factory.WithWebHostBuilder(builder => builder.UseSetting("Google:ClientId", ""));

        var response = await withoutGoogle.CreateBrowser().GetAsync("/api/account/google");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/logowanie?google=nieudane", response.Headers.Location!.OriginalString);
    }

    private static string NewEmail() => $"klient-{Guid.NewGuid():N}@gmail.com";

    private sealed record AccountResponse(string Email, string? Name = null, string? PictureUrl = null);
}
