using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SzeptTarota.Api.Tests.Fakes;
using SzeptTarota.Api.Tests.Infrastructure;
using SzeptTarota.Infrastructure.Persistence;
using static SzeptTarota.Api.Tests.Infrastructure.CalendarExtensions;

namespace SzeptTarota.Api.Tests;

/// <summary>
/// Klara zaznacza w panelu godziny, w których przyjmuje: strona zapisuje je jako bloki „Dostępna” w jej Kalendarzu
/// Sesji w Google (ADR 0002), więc widzi je też w Google Calendar, a Klienci jako wolne Terminy.
/// </summary>
[Collection(ApiCollection.Name)]
public class PanelAvailabilityTests : IAsyncLifetime
{
    private readonly ApiFactory _factory;
    private HttpClient _klara = null!;

    /// <summary>Dzisiejszy dzień w Warszawie; teraz jest w nim 10:00.</summary>
    private DateOnly _today;

    public PanelAvailabilityTests(ApiFactory factory) => _factory = factory;

    /// <summary>Zegar jest wspólny dla kolekcji i nie cofa się, więc test przesuwa go na najbliższą 10:00 w Warszawie.</summary>
    public async Task InitializeAsync()
    {
        var now = TimeZoneInfo.ConvertTime(_factory.Time.GetUtcNow(), Warsaw);
        _today = DateOnly.FromDateTime(now.Date).AddDays(1);
        _factory.Time.SetUtcNow(At(0, 10).ToUniversalTime());
        await _factory.ConnectKlarasCalendar();
        _klara = _factory.CreateBrowser();
        await _klara.LogInWithGoogle(_factory.Google, ApiFactory.KlaraEmail);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private FakeGoogleCalendar Calendar => _factory.GoogleCalendar;

    [Fact]
    public async Task Hours_Klara_marks_as_available_land_in_her_Sessions_calendar_and_become_Terminy()
    {
        var response = await _klara.PostAsJsonAsync("/api/panel/availability", new { start = Local(3, 10), end = Local(3, 12) });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var block = (await response.Content.ReadFromJsonAsync<BlockResponse>())!;
        Assert.Equal((At(3, 10), At(3, 12)), (block.Start, block.End));
        var inGoogle = Assert.Single(Calendar.EventsIn(FakeGoogleCalendar.SessionsCalendar.Id));
        Assert.Equal((block.Id, At(3, 10), At(3, 12), false), (inGoogle.Id, inGoogle.Start, inGoogle.End, inGoogle.Busy));
        var free = await _factory.CreateClient().GetFromJsonAsync<FreeTerminyResponse>(
            $"/api/products/sesja-reiki-czakroterapia/appointments?from={Day(3)}&to={Day(3)}");
        Assert.Equal([At(3, 10), At(3, 11)], free!.Appointments.Select(a => a.Start));
    }

    [Fact]
    public async Task Klara_sees_her_available_blocks_and_busy_time_in_the_chosen_days()
    {
        var fromGoogle = Calendar.AddAvailableBlock(At(4, 9), At(4, 13));
        Calendar.AddAvailableBlock(At(9, 9), At(9, 13));
        Calendar.AddMainCalendarEvent(At(4, 11), At(4, 12));
        Calendar.AddMainCalendarEvent(At(5, 0), At(6, 0), busy: false);

        var week = await Week(4, 8);

        var block = Assert.Single(week.Blocks);
        Assert.Equal((fromGoogle, At(4, 9), At(4, 13)), (block.Id, block.Start, block.End));
        var busy = Assert.Single(week.Busy);
        Assert.Equal((At(4, 11), At(4, 12)), (busy.Start, busy.End));
    }

    [Fact]
    public async Task Klara_removes_an_available_block()
    {
        var keep = Calendar.AddAvailableBlock(At(5, 9), At(5, 10));
        var remove = Calendar.AddAvailableBlock(At(5, 14), At(5, 16));

        var response = await _klara.DeleteAsync($"/api/panel/availability/{remove}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal([keep], Calendar.EventsIn(FakeGoogleCalendar.SessionsCalendar.Id).Select(e => e.Id));
        Assert.Equal([keep], (await Week(5, 5)).Blocks.Select(b => b.Id));
    }

    [Theory]
    [InlineData("12:00", "12:00")]
    [InlineData("12:00", "10:00")]
    [InlineData("nie-godzina", "12:00")]
    public async Task A_block_must_end_after_it_starts(string start, string end)
    {
        var day = Day(6);

        var response = await _klara.PostAsJsonAsync("/api/panel/availability", new { start = $"{day}T{start}", end = $"{day}T{end}" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(Calendar.EventsIn(FakeGoogleCalendar.SessionsCalendar.Id));
    }

    [Fact]
    public async Task A_block_cannot_end_in_the_past_or_last_longer_than_a_day()
    {
        var past = await _klara.PostAsJsonAsync("/api/panel/availability", new { start = Local(0, 8), end = Local(0, 9, 30) });
        var tooLong = await _klara.PostAsJsonAsync("/api/panel/availability", new { start = Local(6, 8), end = Local(7, 9) });

        Assert.Equal(HttpStatusCode.BadRequest, past.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, tooLong.StatusCode);
        Assert.Empty(Calendar.EventsIn(FakeGoogleCalendar.SessionsCalendar.Id));
    }

    [Fact]
    public async Task A_block_can_last_until_midnight()
    {
        var response = await _klara.PostAsJsonAsync("/api/panel/availability", new { start = Local(7, 20), end = Local(8, 0) });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Without_a_Sessions_calendar_there_is_nowhere_to_mark_availability()
    {
        await ForgetSessionCalendar();

        var week = await _klara.GetAsync($"/api/panel/availability?from={Day(1)}&to={Day(7)}");
        var added = await _klara.PostAsJsonAsync("/api/panel/availability", new { start = Local(3, 10), end = Local(3, 12) });

        Assert.Equal(HttpStatusCode.Conflict, week.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, added.StatusCode);
    }

    [Fact]
    public async Task When_Google_access_is_lost_availability_is_unavailable()
    {
        Calendar.RevokeAccess();

        var week = await _klara.GetAsync($"/api/panel/availability?from={Day(1)}&to={Day(7)}");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, week.StatusCode);
    }

    [Fact]
    public async Task Only_Klara_manages_availability()
    {
        var anonymous = _factory.CreateBrowser();
        var klient = _factory.CreateBrowser();
        await klient.LogInWithGoogle(_factory.Google, $"klient-{Guid.NewGuid():N}@gmail.com");
        var block = Calendar.AddAvailableBlock(At(3, 10), At(3, 12));

        foreach (var (client, expected) in new[] { (anonymous, HttpStatusCode.Unauthorized), (klient, HttpStatusCode.Forbidden) })
        {
            Assert.Equal(expected, (await client.GetAsync($"/api/panel/availability?from={Day(1)}&to={Day(7)}")).StatusCode);
            Assert.Equal(expected, (await client.PostAsJsonAsync("/api/panel/availability", new { start = Local(3, 14), end = Local(3, 15) })).StatusCode);
            Assert.Equal(expected, (await client.DeleteAsync($"/api/panel/availability/{block}")).StatusCode);
        }

        Assert.Equal([block], Calendar.EventsIn(FakeGoogleCalendar.SessionsCalendar.Id).Select(e => e.Id));
    }

    private async Task<WeekResponse> Week(int fromDays, int toDays)
    {
        var response = await _klara.GetAsync($"/api/panel/availability?from={Day(fromDays)}&to={Day(toDays)}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<WeekResponse>())!;
    }

    /// <summary>Kalendarz Sesji nie jest wybrany, jak zaraz po połączeniu (baza jest wspólna dla kolekcji).</summary>
    private async Task ForgetSessionCalendar()
    {
        using var scope = _factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<SzeptTarotaDbContext>().CalendarConnections
            .ExecuteUpdateAsync(c => c.SetProperty(x => x.SessionCalendarId, (string?)null));
    }

    /// <summary>Godzina czasu polskiego <paramref name="days"/> dni od dzisiaj.</summary>
    private DateTimeOffset At(int days, int hour, int minute = 0) => InWarsaw(_today.AddDays(days), hour, minute);

    /// <summary>Ta sama godzina tak, jak wysyła ją panel: czas polski bez strefy, np. <c>2026-02-03T10:00</c>.</summary>
    private string Local(int days, int hour, int minute = 0) =>
        _today.AddDays(days).ToDateTime(new TimeOnly(hour, minute)).ToString("yyyy-MM-dd'T'HH:mm", CultureInfo.InvariantCulture);

    private string Day(int days) => _today.AddDays(days).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private sealed record BlockResponse(string Id, DateTimeOffset Start, DateTimeOffset End);

    private sealed record PeriodResponse(DateTimeOffset Start, DateTimeOffset End);

    private sealed record WeekResponse(List<BlockResponse> Blocks, List<PeriodResponse> Busy);

    private sealed record FreeTerminyResponse(List<PeriodResponse> Appointments);
}
