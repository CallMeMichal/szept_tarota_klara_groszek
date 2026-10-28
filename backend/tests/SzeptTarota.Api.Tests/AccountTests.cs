using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using SzeptTarota.Api.Tests.Infrastructure;

namespace SzeptTarota.Api.Tests;

[Collection(ApiCollection.Name)]
public class AccountTests
{
    private readonly ApiFactory _factory;

    public AccountTests(ApiFactory factory)
    {
        _factory = factory;
        _factory.Emails.Reset();
    }

    [Fact]
    public async Task The_link_from_the_email_logs_the_Klient_in_to_their_Konto()
    {
        var email = NewEmail();

        var client = await _factory.LoggedInClient(_factory.Emails, email);

        var account = await client.GetFromJsonAsync<AccountResponse>("/api/account");
        Assert.Equal(email, account!.Email);
    }

    [Fact]
    public async Task The_Konto_email_is_the_address_the_link_was_sent_to_in_lower_case()
    {
        var email = NewEmail();

        var client = await _factory.LoggedInClient(_factory.Emails, "  " + email.ToUpperInvariant() + " ");

        var account = await client.GetFromJsonAsync<AccountResponse>("/api/account");
        Assert.Equal(email, account!.Email);
    }

    [Fact]
    public async Task A_login_link_works_only_once()
    {
        var email = NewEmail();
        await RequestLink(email);
        var token = _factory.Emails.LoginTokenFor(email);

        var first = await LogIn(_factory.CreateBrowser(), token);
        var second = await LogIn(_factory.CreateBrowser(), token);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, second.StatusCode);
    }

    [Fact]
    public async Task A_login_link_expires_after_15_minutes()
    {
        var email = NewEmail();
        await RequestLink(email);
        _factory.Time.Advance(TimeSpan.FromMinutes(15));

        var client = _factory.CreateBrowser();
        var response = await LogIn(client, _factory.Emails.LoginTokenFor(email));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/account")).StatusCode);
    }

    [Fact]
    public async Task A_made_up_token_does_not_log_in()
    {
        var response = await LogIn(_factory.CreateBrowser(), "zmyslony-token");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Without_logging_in_there_is_no_Konto()
    {
        var response = await _factory.CreateBrowser().GetAsync("/api/account");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Logging_out_ends_the_session()
    {
        var client = await _factory.LoggedInClient(_factory.Emails, NewEmail());

        var logout = await client.PostAsync("/api/account/logout", null);

        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/account")).StatusCode);
    }

    [Fact]
    public async Task An_invalid_email_gets_an_error_and_no_link()
    {
        var response = await _factory.CreateBrowser().PostAsJsonAsync("/api/account/login-link", new { email = "to nie jest email" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemResponse>();
        Assert.Equal(["email"], problem!.Errors.Keys);
        Assert.Empty(_factory.Emails.Sent);
    }

    [Fact]
    public async Task After_logging_in_the_Klient_returns_to_the_page_they_came_from()
    {
        var email = NewEmail();
        await RequestLink(email, returnTo: "/koszyk");

        var response = await LogIn(_factory.CreateBrowser(), _factory.Emails.LoginTokenFor(email));

        Assert.Equal("/koszyk", (await response.Content.ReadFromJsonAsync<LoggedInResponse>())!.ReturnTo);
    }

    [Theory]
    [InlineData("https://zla-strona.example")]
    [InlineData("//zla-strona.example")]
    [InlineData(@"/\zla-strona.example")]
    public async Task The_link_never_leads_to_another_site(string returnTo)
    {
        var email = NewEmail();
        await RequestLink(email, returnTo);

        var response = await LogIn(_factory.CreateBrowser(), _factory.Emails.LoginTokenFor(email));

        Assert.Null((await response.Content.ReadFromJsonAsync<LoggedInResponse>())!.ReturnTo);
    }

    [Fact]
    public async Task Answers_service_unavailable_until_the_site_address_is_configured()
    {
        await using var unconfigured = _factory.WithWebHostBuilder(builder => builder.UseSetting("Shop:SiteUrl", ""));

        var response = await unconfigured.CreateClient().PostAsJsonAsync("/api/account/login-link", new { email = NewEmail() });

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Empty(_factory.Emails.Sent);
    }

    [Fact]
    public async Task One_address_gets_at_most_3_login_links_per_15_minutes()
    {
        var email = NewEmail();

        for (var i = 0; i < 4; i++)
        {
            await RequestLink(email);
        }

        Assert.Equal(3, _factory.Emails.Sent.Count(m => m.To == email));
        _factory.Time.Advance(TimeSpan.FromMinutes(15));
        await RequestLink(email);
        Assert.Equal(4, _factory.Emails.Sent.Count(m => m.To == email));
    }

    [Fact]
    public async Task Answers_service_unavailable_until_sending_emails_is_configured()
    {
        _factory.Emails.Configured = false;

        var response = await _factory.CreateBrowser().PostAsJsonAsync("/api/account/login-link", new { email = NewEmail() });

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task Limits_how_many_login_links_one_address_can_ask_for()
    {
        await using var strictLimit = _factory.WithWebHostBuilder(builder =>
            builder.UseSetting("Account:LoginLinkRateLimit:PermitLimit", "1"));
        var client = strictLimit.CreateClient();

        var first = await client.PostAsJsonAsync("/api/account/login-link", new { email = NewEmail() });
        var second = await client.PostAsJsonAsync("/api/account/login-link", new { email = NewEmail() });

        Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, second.StatusCode);
        Assert.Single(_factory.Emails.Sent);
    }

    private async Task RequestLink(string email, string? returnTo = null)
    {
        var response = await _factory.CreateBrowser().PostAsJsonAsync("/api/account/login-link", new { email, returnTo });
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }

    private static Task<HttpResponseMessage> LogIn(HttpClient client, string token) =>
        client.PostAsJsonAsync("/api/account/login", new { token });

    private static string NewEmail() => $"klient-{Guid.NewGuid():N}@example.com";

    private sealed record AccountResponse(string Email);

    private sealed record LoggedInResponse(string Email, string? ReturnTo);

    private sealed record ValidationProblemResponse(Dictionary<string, string[]> Errors);
}
