using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SzeptTarota.Api.Tests.Fakes;
using SzeptTarota.Api.Tests.Infrastructure;
using SzeptTarota.Infrastructure.Persistence;

namespace SzeptTarota.Api.Tests;

[Collection(ApiCollection.Name)]
public class PanelCalendarTests
{
    private readonly ApiFactory _factory;

    public PanelCalendarTests(ApiFactory factory)
    {
        _factory = factory;
        _factory.GoogleCalendar.Reset();
    }

    [Fact]
    public async Task Only_Klara_sees_the_calendar_connection()
    {
        var anonymous = _factory.CreateBrowser();
        var klient = _factory.CreateBrowser();
        await klient.LogInWithGoogle(_factory.Google, $"klient-{Guid.NewGuid():N}@gmail.com");

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/panel/calendar")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await klient.GetAsync("/api/panel/calendar")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await (await Klara()).GetAsync("/api/panel/calendar")).StatusCode);
    }

    [Fact]
    public async Task The_Konto_tells_whether_it_is_Klaras()
    {
        var klient = _factory.CreateBrowser();
        await klient.LogInWithGoogle(_factory.Google, $"klient-{Guid.NewGuid():N}@gmail.com");

        Assert.True((await (await Klara()).GetFromJsonAsync<AccountResponse>("/api/account"))!.IsKlara);
        Assert.False((await klient.GetFromJsonAsync<AccountResponse>("/api/account"))!.IsKlara);
    }

    [Fact]
    public async Task A_Google_account_on_Klaras_address_without_a_confirmed_email_has_no_access()
    {
        var impostor = _factory.CreateBrowser();
        await impostor.LogInWithGoogle(_factory.Google, ApiFactory.KlaraEmail, emailVerified: false);

        Assert.Equal(HttpStatusCode.Unauthorized, (await impostor.GetAsync("/api/panel/calendar")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await impostor.GetAsync("/api/panel/calendar/connect")).StatusCode);
    }

    [Fact]
    public async Task Klara_connects_the_site_with_her_Google_Calendar()
    {
        var klara = await Klara();

        var response = await Connect(klara, _factory.GoogleCalendar.GrantAccess());

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/panel?kalendarz=polaczono", response.Headers.Location!.OriginalString);
        var connection = await Connection(klara);
        Assert.Equal("connected", connection.Status);
        Assert.Equal(
            [FakeGoogleCalendar.MainCalendar.Id, FakeGoogleCalendar.SessionsCalendar.Id],
            connection.Calendars.Select(c => c.Id));
        Assert.True(connection.Calendars.Single(c => c.Id == FakeGoogleCalendar.MainCalendar.Id).Primary);
    }

    [Fact]
    public async Task Google_sends_Klara_back_to_the_site_address()
    {
        var consent = await (await Klara()).GetAsync("/api/panel/calendar/connect");

        Assert.Equal(HttpStatusCode.Redirect, consent.StatusCode);
        var redirectUri = QueryHelpers.ParseQuery(consent.Headers.Location!.Query)["redirect_uri"].ToString();
        Assert.Equal($"{ApiFactory.SiteUrl}/api/panel/calendar/callback", redirectUri);
    }

    [Fact]
    public async Task Before_connecting_the_panel_shows_no_connection()
    {
        await ForgetConnection();

        var connection = await Connection(await Klara());

        Assert.Equal("notConnected", connection.Status);
        Assert.Empty(connection.Calendars);
        Assert.Null(connection.SessionCalendarId);
    }

    [Fact]
    public async Task A_return_from_Google_that_Klara_did_not_start_connects_nothing()
    {
        await ForgetConnection();
        var klara = await Klara();

        var response = await Connect(klara, _factory.GoogleCalendar.GrantAccess(), state: "cudzy-state");

        Assert.Equal("/panel?kalendarz=nieudane", response.Headers.Location!.OriginalString);
        Assert.Equal("notConnected", (await Connection(klara)).Status);
    }

    [Fact]
    public async Task Refusing_access_at_Google_connects_nothing()
    {
        await ForgetConnection();
        var klara = await Klara();
        var consent = await klara.GetAsync("/api/panel/calendar/connect");
        var state = QueryHelpers.ParseQuery(consent.Headers.Location!.Query)["state"].ToString();

        var response = await klara.GetAsync($"/api/panel/calendar/callback?error=access_denied&state={Uri.EscapeDataString(state)}");

        Assert.Equal("/panel?kalendarz=nieudane", response.Headers.Location!.OriginalString);
        Assert.Equal("notConnected", (await Connection(klara)).Status);
    }

    [Fact]
    public async Task When_Google_stops_honouring_the_access_the_panel_shows_a_broken_connection_and_Klara_reconnects()
    {
        var klara = await Klara();
        await Connect(klara, _factory.GoogleCalendar.GrantAccess());

        _factory.GoogleCalendar.RevokeAccess();

        var broken = await Connection(klara);
        Assert.Equal("broken", broken.Status);
        Assert.Empty(broken.Calendars);

        var response = await Connect(klara, _factory.GoogleCalendar.GrantAccess());

        Assert.Equal("/panel?kalendarz=polaczono", response.Headers.Location!.OriginalString);
        Assert.Equal("connected", (await Connection(klara)).Status);
    }

    [Fact]
    public async Task Klara_chooses_the_calendar_with_her_Dostepna_blocks_and_it_survives_reconnecting()
    {
        var klara = await Klara();
        await Connect(klara, _factory.GoogleCalendar.GrantAccess());

        var chosen = await klara.PutAsJsonAsync(
            "/api/panel/calendar/session-calendar", new { calendarId = FakeGoogleCalendar.SessionsCalendar.Id });

        Assert.Equal(HttpStatusCode.NoContent, chosen.StatusCode);
        Assert.Equal(FakeGoogleCalendar.SessionsCalendar.Id, (await Connection(klara)).SessionCalendarId);

        await Connect(klara, _factory.GoogleCalendar.GrantAccess());
        Assert.Equal(FakeGoogleCalendar.SessionsCalendar.Id, (await Connection(klara)).SessionCalendarId);
    }

    [Fact]
    public async Task A_Kalendarz_Sesji_missing_from_the_connected_Google_account_is_no_longer_chosen()
    {
        var klara = await Klara();
        await Connect(klara, _factory.GoogleCalendar.GrantAccess());
        await klara.PutAsJsonAsync("/api/panel/calendar/session-calendar", new { calendarId = FakeGoogleCalendar.SessionsCalendar.Id });

        // Klara połączyła się ponownie innym kontem Google albo usunęła kalendarz.
        _factory.GoogleCalendar.Calendars.Remove(FakeGoogleCalendar.SessionsCalendar);

        var connection = await Connection(klara);
        Assert.Equal("connected", connection.Status);
        Assert.Null(connection.SessionCalendarId);
    }

    [Fact]
    public async Task The_main_calendar_cannot_be_the_Sessions_calendar_because_it_is_read_as_busy_time()
    {
        var klara = await Klara();
        await Connect(klara, _factory.GoogleCalendar.GrantAccess());

        var response = await klara.PutAsJsonAsync(
            "/api/panel/calendar/session-calendar", new { calendarId = FakeGoogleCalendar.MainCalendar.Id });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(["calendarId"], (await response.Content.ReadFromJsonAsync<ValidationProblemResponse>())!.Errors.Keys);
    }

    [Fact]
    public async Task Only_a_calendar_from_Klaras_Google_account_can_be_the_Sessions_calendar()
    {
        var klara = await Klara();
        await Connect(klara, _factory.GoogleCalendar.GrantAccess());

        var response = await klara.PutAsJsonAsync("/api/panel/calendar/session-calendar", new { calendarId = "obcy@group.calendar.google.com" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task The_refresh_token_is_not_stored_in_plain_text()
    {
        var klara = await Klara();

        await Connect(klara, _factory.GoogleCalendar.GrantAccess());

        // Wymaganie dotyczy samego zapisu, więc zaglądamy do bazy.
        var token = _factory.GoogleCalendar.IssuedTokens[^1];
        using var scope = _factory.Services.CreateScope();
        var stored = await scope.ServiceProvider.GetRequiredService<SzeptTarotaDbContext>().CalendarConnections.SingleAsync();
        Assert.DoesNotContain(token, stored.ProtectedRefreshToken);
        Assert.Equal("connected", (await Connection(klara)).Status);
    }

    /// <summary>Usuwa zapisane połączenie, jakby Klara jeszcze nigdy się nie połączyła (baza jest wspólna dla kolekcji).</summary>
    private async Task ForgetConnection()
    {
        using var scope = _factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<SzeptTarotaDbContext>().CalendarConnections.ExecuteDeleteAsync();
    }

    /// <summary>
    /// Przechodzi połączenie tak jak przeglądarka: przycisk w panelu, strona zgody w Google, powrót z kodem
    /// <paramref name="code"/>. Zwraca odpowiedź API na powrót.
    /// </summary>
    private static async Task<HttpResponseMessage> Connect(HttpClient klara, string code, string? state = null)
    {
        var consent = await klara.GetAsync("/api/panel/calendar/connect");
        Assert.Equal(HttpStatusCode.Redirect, consent.StatusCode);
        var query = QueryHelpers.ParseQuery(consent.Headers.Location!.Query);
        var callback = new Uri(query["redirect_uri"].ToString());
        return await klara.GetAsync(
            $"{callback.AbsolutePath}?code={Uri.EscapeDataString(code)}&state={Uri.EscapeDataString(state ?? query["state"].ToString())}");
    }

    private static async Task<ConnectionResponse> Connection(HttpClient klara) =>
        (await klara.GetFromJsonAsync<ConnectionResponse>("/api/panel/calendar"))!;

    /// <summary>Klara zalogowana przez Google (logowanie kodem ma limit 3 kodów na adres na 15 minut).</summary>
    private async Task<HttpClient> Klara()
    {
        var client = _factory.CreateBrowser();
        await client.LogInWithGoogle(_factory.Google, ApiFactory.KlaraEmail);
        return client;
    }

    private sealed record ConnectionResponse(string Status, List<CalendarResponse> Calendars, string? SessionCalendarId);

    private sealed record CalendarResponse(string Id, string Name, bool Primary);

    private sealed record AccountResponse(string Email, bool IsKlara);

    private sealed record ValidationProblemResponse(Dictionary<string, string[]> Errors);
}
