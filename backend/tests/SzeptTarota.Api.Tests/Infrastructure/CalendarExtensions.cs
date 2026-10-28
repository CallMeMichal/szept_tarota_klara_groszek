using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.WebUtilities;
using SzeptTarota.Api.Tests.Fakes;

namespace SzeptTarota.Api.Tests.Infrastructure;

public static class CalendarExtensions
{
    /// <summary>Strefa, w której Klara wpisuje bloki i w której strona pokazuje Terminy.</summary>
    public static readonly TimeZoneInfo Warsaw = TimeZoneInfo.FindSystemTimeZoneById("Europe/Warsaw");

    /// <summary>
    /// Klara łączy stronę ze swoim Google Calendar w panelu i wybiera Kalendarz Sesji, tak jak w przeglądarce.
    /// Kalendarz zaczyna pusty (<see cref="FakeGoogleCalendar.Reset"/>).
    /// </summary>
    public static async Task ConnectKlarasCalendar(this ApiFactory factory)
    {
        factory.GoogleCalendar.Reset();
        var klara = factory.CreateBrowser();
        await klara.LogInWithGoogle(factory.Google, ApiFactory.KlaraEmail);

        var consent = await klara.GetAsync("/api/panel/calendar/connect");
        var query = QueryHelpers.ParseQuery(consent.Headers.Location!.Query);
        var callback = new Uri(query["redirect_uri"].ToString());
        var connected = await klara.GetAsync(
            $"{callback.AbsolutePath}?code={Uri.EscapeDataString(factory.GoogleCalendar.GrantAccess())}&state={Uri.EscapeDataString(query["state"].ToString())}");
        Assert.Equal("/panel?kalendarz=polaczono", connected.Headers.Location!.OriginalString);

        var chosen = await klara.PutAsJsonAsync(
            "/api/panel/calendar/session-calendar", new { calendarId = FakeGoogleCalendar.SessionsCalendar.Id });
        Assert.Equal(HttpStatusCode.NoContent, chosen.StatusCode);
    }

    /// <summary>
    /// Dni od dzisiaj dla <see cref="OfferTerminy"/>: dalej niż dni z testów Terminów, ale w dwuletnim horyzoncie strony, i za każdym razem dalej. Zegar
    /// też idzie tylko do przodu, więc każde wywołanie dostaje nowy dzień, bez opłaconych już Terminów.
    /// </summary>
    private static int s_daysAhead = 500;

    /// <summary>
    /// Klara łączy kalendarz i udostępnia nowy dzień, 8:00–20:00. Zwraca cztery wolne Terminy tego dnia, które pasują
    /// i do Sesji (60 minut), i do Usługi z fake'owego katalogu (90 minut).
    /// </summary>
    public static async Task<DateTimeOffset[]> OfferTerminy(this ApiFactory factory)
    {
        await factory.ConnectKlarasCalendar();
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(factory.Time.GetUtcNow(), Warsaw).Date);
        var day = today.AddDays(Interlocked.Increment(ref s_daysAhead));
        factory.GoogleCalendar.AddAvailableBlock(InWarsaw(day, 8), InWarsaw(day, 20));
        return [InWarsaw(day, 8), InWarsaw(day, 11), InWarsaw(day, 14), InWarsaw(day, 17)];
    }

    /// <summary>Chwila o podanej godzinie czasu polskiego w dniu <paramref name="day"/>.</summary>
    public static DateTimeOffset InWarsaw(DateOnly day, int hour, int minute = 0)
    {
        var local = day.ToDateTime(new TimeOnly(hour, minute));
        return new DateTimeOffset(local, Warsaw.GetUtcOffset(local));
    }
}
