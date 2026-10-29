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
    public async Task The_code_from_the_email_logs_the_Klient_in_to_their_Konto()
    {
        var email = NewEmail();

        var client = await _factory.LoggedInClient(_factory.Emails, email);

        var account = await client.GetFromJsonAsync<AccountResponse>("/api/account");
        Assert.Equal(email, account!.Email);
        // Kod logowania nie mówi nic o imieniu ani zdjęciu; te są tylko po logowaniu przez Google.
        Assert.Null(account.Name);
        Assert.Null(account.PictureUrl);
    }

    [Fact]
    public async Task The_code_is_6_digits()
    {
        var email = NewEmail();

        await RequestCode(email);

        Assert.Matches("^[0-9]{6}$", _factory.Emails.LoginCodeFor(email));
    }

    [Fact]
    public async Task The_Konto_email_is_the_address_the_code_was_sent_to_in_lower_case()
    {
        var email = NewEmail();

        var client = await _factory.LoggedInClient(_factory.Emails, "  " + email.ToUpperInvariant() + " ");

        var account = await client.GetFromJsonAsync<AccountResponse>("/api/account");
        Assert.Equal(email, account!.Email);
    }

    [Fact]
    public async Task A_login_code_works_only_once()
    {
        var email = NewEmail();
        await RequestCode(email);
        var code = _factory.Emails.LoginCodeFor(email);

        var first = await LogIn(_factory.CreateBrowser(), email, code);
        var second = await LogIn(_factory.CreateBrowser(), email, code);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, second.StatusCode);
    }

    [Fact]
    public async Task A_login_code_expires_after_15_minutes()
    {
        var email = NewEmail();
        await RequestCode(email);
        _factory.Time.Advance(TimeSpan.FromMinutes(15));

        var client = _factory.CreateBrowser();
        var response = await LogIn(client, email, _factory.Emails.LoginCodeFor(email));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/account")).StatusCode);
    }

    [Fact]
    public async Task A_code_works_only_for_the_address_it_was_sent_to()
    {
        var email = NewEmail();
        await RequestCode(email);

        var response = await LogIn(_factory.CreateBrowser(), NewEmail(), _factory.Emails.LoginCodeFor(email));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task After_5_wrong_codes_even_the_right_one_no_longer_works()
    {
        var email = NewEmail();
        await RequestCode(email);
        var code = _factory.Emails.LoginCodeFor(email);
        var wrong = code == "000000" ? "111111" : "000000";

        for (var i = 0; i < 5; i++)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await LogIn(_factory.CreateBrowser(), email, wrong)).StatusCode);
        }

        Assert.Equal(HttpStatusCode.Unauthorized, (await LogIn(_factory.CreateBrowser(), email, code)).StatusCode);
    }

    [Fact]
    public async Task Many_wrong_codes_sent_at_once_still_get_only_5_attempts()
    {
        var email = NewEmail();
        await RequestCode(email);
        var code = _factory.Emails.LoginCodeFor(email);
        var wrong = code == "000000" ? "111111" : "000000";

        await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => LogIn(_factory.CreateBrowser(), email, wrong)));

        Assert.Equal(HttpStatusCode.Unauthorized, (await LogIn(_factory.CreateBrowser(), email, code)).StatusCode);
    }

    [Fact]
    public async Task Limits_how_many_login_attempts_one_address_can_make()
    {
        await using var strictLimit = _factory.WithWebHostBuilder(builder =>
            builder.UseSetting("Account:LoginRateLimit:PermitLimit", "1"));
        var client = strictLimit.CreateClient();

        var first = await LogIn(client, NewEmail(), "123456");
        var second = await LogIn(client, NewEmail(), "123456");

        Assert.Equal(HttpStatusCode.Unauthorized, first.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, second.StatusCode);
    }

    [Fact]
    public async Task A_few_wrong_codes_still_leave_the_right_one_working()
    {
        var email = NewEmail();
        await RequestCode(email);
        var code = _factory.Emails.LoginCodeFor(email);
        var wrong = code == "000000" ? "111111" : "000000";

        for (var i = 0; i < 4; i++)
        {
            await LogIn(_factory.CreateBrowser(), email, wrong);
        }

        Assert.Equal(HttpStatusCode.OK, (await LogIn(_factory.CreateBrowser(), email, code)).StatusCode);
    }

    [Fact]
    public async Task Only_the_newest_code_works()
    {
        var email = NewEmail();
        await RequestCode(email);
        var older = _factory.Emails.LoginCodeFor(email);
        // Zegar w testach stoi; bez tego oba kody miałyby ten sam czas i „nowszy” byłby losowy.
        _factory.Time.Advance(TimeSpan.FromSeconds(1));
        await RequestCode(email);
        var newer = _factory.Emails.LoginCodeFor(email);

        // Kody są losowe, więc mogą się powtórzyć; wtedy nie ma czego sprawdzać.
        if (older != newer)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await LogIn(_factory.CreateBrowser(), email, older)).StatusCode);
        }

        Assert.Equal(HttpStatusCode.OK, (await LogIn(_factory.CreateBrowser(), email, newer)).StatusCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("12345")]
    [InlineData("abcdef")]
    public async Task A_malformed_code_does_not_log_in(string code)
    {
        var email = NewEmail();
        await RequestCode(email);

        var response = await LogIn(_factory.CreateBrowser(), email, code);

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
    public async Task An_invalid_email_gets_an_error_and_no_code()
    {
        var response = await _factory.CreateBrowser().PostAsJsonAsync("/api/account/login-code", new { email = "to nie jest email" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemResponse>();
        Assert.Equal(["email"], problem!.Errors.Keys);
        Assert.Empty(_factory.Emails.Sent);
    }

    [Fact]
    public async Task One_address_gets_at_most_3_login_codes_per_15_minutes()
    {
        var email = NewEmail();

        for (var i = 0; i < 4; i++)
        {
            await RequestCode(email);
        }

        Assert.Equal(3, _factory.Emails.Sent.Count(m => m.To == email));
        _factory.Time.Advance(TimeSpan.FromMinutes(15));
        await RequestCode(email);
        Assert.Equal(4, _factory.Emails.Sent.Count(m => m.To == email));
    }

    [Fact]
    public async Task Answers_service_unavailable_until_sending_emails_is_configured()
    {
        _factory.Emails.Configured = false;

        var response = await _factory.CreateBrowser().PostAsJsonAsync("/api/account/login-code", new { email = NewEmail() });

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task Limits_how_many_login_codes_one_address_can_ask_for()
    {
        await using var strictLimit = _factory.WithWebHostBuilder(builder =>
            builder.UseSetting("Account:LoginCodeRateLimit:PermitLimit", "1"));
        var client = strictLimit.CreateClient();

        var first = await client.PostAsJsonAsync("/api/account/login-code", new { email = NewEmail() });
        var second = await client.PostAsJsonAsync("/api/account/login-code", new { email = NewEmail() });

        Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, second.StatusCode);
        Assert.Single(_factory.Emails.Sent);
    }

    private async Task RequestCode(string email)
    {
        var response = await _factory.CreateBrowser().PostAsJsonAsync("/api/account/login-code", new { email });
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }

    private static Task<HttpResponseMessage> LogIn(HttpClient client, string email, string code) =>
        client.PostAsJsonAsync("/api/account/login", new { email, code });

    private static string NewEmail() => $"klient-{Guid.NewGuid():N}@example.com";

    private sealed record AccountResponse(string Email, string? Name, string? PictureUrl);

    private sealed record ValidationProblemResponse(Dictionary<string, string[]> Errors);
}
