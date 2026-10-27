using System.Net;
using System.Net.Http.Json;
using SzeptTarota.Api.Tests.Infrastructure;
using SzeptTarota.Application.Contact;

namespace SzeptTarota.Api.Tests;

[Collection(ApiCollection.Name)]
public class ContactEndpointTests : IDisposable
{
    private readonly ApiFactory _factory;

    public ContactEndpointTests(ApiFactory factory)
    {
        _factory = factory;
        _factory.ContactMailer.Reset();
    }

    public void Dispose() => _factory.ContactMailer.Reset();

    [Fact]
    public async Task Sends_the_message_to_Klara()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/contact", new
        {
            name = "  Anna ",
            email = "anna@example.com",
            message = "Chcę zapytać o Analizę partnerską.",
        });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var sent = Assert.Single(_factory.ContactMailer.Sent);
        Assert.Equal(new ContactMessage("Anna", "anna@example.com", "Chcę zapytać o Analizę partnerską."), sent);
    }

    [Fact]
    public async Task Rejects_a_message_without_name_valid_email_or_text()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/contact", new
        {
            name = " ",
            email = "to nie jest email",
            message = "",
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemResponse>();
        Assert.Equal(["email", "message", "name"], problem!.Errors.Keys.Order());
        Assert.Empty(_factory.ContactMailer.Sent);
    }

    [Fact]
    public async Task Silently_drops_a_message_with_the_bot_trap_filled_in()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/contact", new
        {
            name = "Bot",
            email = "bot@example.com",
            message = "Kup tanie linki",
            website = "https://spam.example.com",
        });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Empty(_factory.ContactMailer.Sent);
    }

    [Fact]
    public async Task Answers_service_unavailable_until_the_company_email_is_configured()
    {
        _factory.ContactMailer.Configured = false;
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/contact", new
        {
            name = "Anna",
            email = "anna@example.com",
            message = "Dzień dobry!",
        });

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    private sealed record ValidationProblemResponse(Dictionary<string, string[]> Errors);
}
