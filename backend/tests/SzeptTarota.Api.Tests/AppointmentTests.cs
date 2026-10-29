using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.WebUtilities;
using SzeptTarota.Api.Tests.Fakes;
using SzeptTarota.Api.Tests.Infrastructure;
using SzeptTarota.Application.Catalog;
using SzeptTarota.Application.Payments;
using static SzeptTarota.Api.Tests.Infrastructure.CalendarExtensions;

namespace SzeptTarota.Api.Tests;

/// <summary>
/// Wolne Terminy: bloki „Dostępna” z Kalendarza Sesji podzielone na kolejne Terminy o długości Usługi, minus zajęte
/// okresy z głównego kalendarza Klary i opłacone Terminy. Strona nie dokłada przerw ani granic, poza tym, że Termin
/// zaczyna się w przyszłości. Każdy test bierze własne dni, bo baza (z opłaconymi Terminami) jest wspólna dla kolekcji.
/// </summary>
[Collection(ApiCollection.Name)]
public class AppointmentTests : IAsyncLifetime
{
    private static readonly object ServiceForm = new { name = "Anna", birthDate = "1990-04-12" };

    private readonly ApiFactory _factory;
    private readonly FakeProductCatalog _catalog = new();
    private readonly WebApplicationFactory<Program> _api;

    /// <summary>Dzisiejszy dzień w Warszawie; teraz jest w nim 10:00.</summary>
    private DateOnly _today;

    public AppointmentTests(ApiFactory factory)
    {
        _factory = factory;
        _api = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.ReplaceWithFake<IProductCatalog>(_catalog)));
    }

    /// <summary>
    /// Zegar jest wspólny dla kolekcji i nie cofa się, więc test przesuwa go na najbliższą 10:00 w Warszawie
    /// i liczy dni od dzisiaj.
    /// </summary>
    public async Task InitializeAsync()
    {
        var now = TimeZoneInfo.ConvertTime(_factory.Time.GetUtcNow(), Warsaw);
        _today = DateOnly.FromDateTime(now.Date).AddDays(1);
        _factory.Time.SetUtcNow(At(0, 10, 0).ToUniversalTime());
        await _factory.ConnectKlarasCalendar();
    }

    public Task DisposeAsync() => _api.DisposeAsync().AsTask();

    private FakeGoogleCalendar Calendar => _factory.GoogleCalendar;

    /// <summary>Godzina czasu polskiego <paramref name="days"/> dni od dzisiaj.</summary>
    private DateTimeOffset At(int days, int hour, int minute = 0) => InWarsaw(_today.AddDays(days), hour, minute);

    /// <summary>Dzień <paramref name="days"/> dni od dzisiaj w formacie zapytania.</summary>
    private string Day(int days) => _today.AddDays(days).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    [Fact]
    public async Task Available_blocks_are_split_into_back_to_back_Terminy_as_long_as_the_Session()
    {
        Calendar.AddAvailableBlock(At(18, 10, 0), At(18, 12, 30));

        var free = await FreeTerminy(FakeProductCatalog.SessionSlug, Day(18), Day(18));

        Assert.Equal(60, free.DurationMinutes);
        Assert.Equal([At(18, 10, 0), At(18, 11, 0)], free.Appointments.Select(a => a.Start));
        Assert.All(free.Appointments, a => Assert.Equal(a.Start.AddMinutes(60), a.End));
    }

    [Fact]
    public async Task A_Termin_is_as_long_as_its_Usluga()
    {
        Calendar.AddAvailableBlock(At(22, 10, 0), At(22, 13, 0));

        var free = await FreeTerminy(FakeProductCatalog.ServiceSlug, Day(22), Day(22));

        Assert.Equal(90, free.DurationMinutes);
        Assert.Equal([At(22, 10, 0), At(22, 11, 30)], free.Appointments.Select(a => a.Start));
        Assert.All(free.Appointments, a => Assert.Equal(a.Start.AddMinutes(90), a.End));
    }

    [Fact]
    public async Task Terminy_that_already_started_are_not_offered()
    {
        Calendar.AddAvailableBlock(At(0, 8, 0), At(0, 12, 0));

        var free = await FreeTerminy(FakeProductCatalog.SessionSlug, Day(0), Day(0));

        Assert.Equal([At(0, 11, 0)], free.Appointments.Select(a => a.Start));
    }

    [Fact]
    public async Task Klara_decides_how_far_ahead_she_takes_Terminy()
    {
        Calendar.AddAvailableBlock(At(370, 10, 0), At(370, 11, 0));

        var free = await FreeTerminy(FakeProductCatalog.SessionSlug, Day(351), Day(381));

        Assert.Equal([At(370, 10, 0)], free.Appointments.Select(a => a.Start));
    }

    [Fact]
    public async Task Busy_time_in_Klaras_main_calendar_is_not_offered_but_a_Termin_may_touch_it()
    {
        Calendar.AddAvailableBlock(At(19, 10, 0), At(19, 16, 0));
        Calendar.AddMainCalendarEvent(At(19, 12, 0), At(19, 13, 0));
        Calendar.AddMainCalendarEvent(At(19, 14, 30), At(19, 14, 45));

        var free = await FreeTerminy(FakeProductCatalog.SessionSlug, Day(19), Day(19));

        Assert.Equal(
            [At(19, 10, 0), At(19, 11, 0), At(19, 13, 0), At(19, 15, 0)],
            free.Appointments.Select(a => a.Start));
    }

    [Fact]
    public async Task Events_in_the_main_calendar_marked_as_free_do_not_take_Klaras_time()
    {
        Calendar.AddAvailableBlock(At(20, 10, 0), At(20, 11, 0));
        Calendar.AddMainCalendarEvent(At(20, 0, 0), At(21, 0, 0), busy: false);

        var free = await FreeTerminy(FakeProductCatalog.SessionSlug, Day(20), Day(20));

        Assert.Equal([At(20, 10, 0)], free.Appointments.Select(a => a.Start));
    }

    [Fact]
    public async Task Touching_available_blocks_make_one_longer_block()
    {
        Calendar.AddAvailableBlock(At(21, 10, 0), At(21, 10, 30));
        Calendar.AddAvailableBlock(At(21, 10, 30), At(21, 11, 0));

        var free = await FreeTerminy(FakeProductCatalog.SessionSlug, Day(21), Day(21));

        Assert.Equal([At(21, 10, 0)], free.Appointments.Select(a => a.Start));
    }

    [Fact]
    public async Task Only_Terminy_starting_in_the_requested_days_are_listed()
    {
        Calendar.AddAvailableBlock(At(23, 22, 0), At(24, 2, 0));

        var free = await FreeTerminy(FakeProductCatalog.SessionSlug, Day(24), Day(24));

        Assert.Equal([At(24, 0, 0), At(24, 1, 0)], free.Appointments.Select(a => a.Start));
    }

    [Fact]
    public async Task Terminy_have_the_Polish_winter_or_summer_offset()
    {
        // Pierwszy dzień czasu zimowego i letniego w nadchodzącym roku (dni 40–405 od dzisiaj).
        var winter = Enumerable.Range(40, 366).First(d => At(d, 10).Offset == TimeSpan.FromHours(1));
        var summer = Enumerable.Range(40, 366).First(d => At(d, 10).Offset == TimeSpan.FromHours(2));
        Calendar.AddAvailableBlock(At(winter, 10), At(winter, 11));
        Calendar.AddAvailableBlock(At(summer, 10), At(summer, 11));

        var inWinter = Assert.Single((await FreeTerminy(FakeProductCatalog.SessionSlug, Day(winter), Day(winter))).Appointments);
        var inSummer = Assert.Single((await FreeTerminy(FakeProductCatalog.SessionSlug, Day(summer), Day(summer))).Appointments);

        Assert.Equal((9, TimeSpan.FromHours(1)), (inWinter.Start.UtcDateTime.Hour, inWinter.Start.Offset));
        Assert.Equal((8, TimeSpan.FromHours(2)), (inSummer.Start.UtcDateTime.Hour, inSummer.Start.Offset));
    }

    [Fact]
    public async Task An_Ebook_or_unknown_Product_has_no_Terminy()
    {
        var client = _api.CreateClient();

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(AppointmentsUrl(FakeProductCatalog.EbookSlug, "2026-02-01", "2026-02-28"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(AppointmentsUrl("nie-ma-takiej-sesji", "2026-02-01", "2026-02-28"))).StatusCode);
    }

    [Theory]
    [InlineData(null, "2026-02-09")]
    [InlineData("2026-02-10", null)]
    [InlineData("10.02.2026", "2026-02-11")]
    [InlineData("2026-02-10", "2026-02-09")]
    [InlineData("2026-02-01", "2026-04-04")]
    public async Task Days_must_be_a_range_of_dates_up_to_62_days(string? from, string? to)
    {
        var response = await _api.CreateClient().GetAsync(AppointmentsUrl(FakeProductCatalog.SessionSlug, from, to));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Without_a_working_calendar_connection_Terminy_are_unavailable()
    {
        Calendar.RevokeAccess();

        var response = await _api.CreateClient().GetAsync(AppointmentsUrl(FakeProductCatalog.SessionSlug, Day(1), Day(30)));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task A_Session_with_a_free_Termin_can_be_bought()
    {
        Calendar.AddAvailableBlock(At(25, 10, 0), At(25, 12, 0));

        var cart = await ValidateCart(Session(At(25, 11, 0)));

        Assert.True(cart.IsValid);
        var line = Assert.Single(cart.Items);
        Assert.Empty(line.Errors);
        Assert.Equal(new AppointmentResponse(At(25, 11, 0), At(25, 12, 0)), line.Appointment);
        Assert.Equal(24900, cart.Total);
        Assert.Equal(["termsAndPrivacy"], cart.RequiredConsents.Select(c => c.Key));
    }

    [Fact]
    public async Task Several_Uslugi_and_Sessions_in_the_cart_can_have_back_to_back_Terminy()
    {
        Calendar.AddAvailableBlock(At(26, 10, 0), At(26, 16, 0));

        var cart = await ValidateCart(
            Session(At(26, 12, 0)),
            Session(At(26, 13, 0)),
            Service(At(26, 14, 30)));

        Assert.True(cart.IsValid);
        Assert.Equal(
            [At(26, 12, 0), At(26, 13, 0), At(26, 14, 30)],
            cart.Items.Select(l => l.Appointment!.Start));
    }

    [Fact]
    public async Task A_Usluga_or_Session_without_a_Termin_is_an_error()
    {
        var cart = await ValidateCart(
            new { slug = FakeProductCatalog.SessionSlug, price = 24900 },
            new { slug = FakeProductCatalog.ServiceSlug, price = 34900, form = ServiceForm });

        Assert.False(cart.IsValid);
        Assert.All(cart.Items, line => Assert.Equal("appointmentMissing", Assert.Single(line.Errors).Code));
        Assert.Equal(0, cart.Total);
    }

    [Fact]
    public async Task A_Termin_that_is_busy_past_or_not_one_Klara_offers_is_an_error()
    {
        Calendar.AddAvailableBlock(At(0, 8, 0), At(0, 12, 0));
        Calendar.AddAvailableBlock(At(27, 10, 0), At(27, 14, 0));
        Calendar.AddMainCalendarEvent(At(27, 12, 0), At(27, 13, 0));

        var cart = await ValidateCart(
            Session(At(0, 9, 0)),
            Session(At(27, 12, 0)),
            Session(At(27, 10, 30)),
            Session(At(27, 14, 0)),
            Session(At(28, 10, 0)));

        Assert.False(cart.IsValid);
        Assert.All(cart.Items, line => Assert.Equal("appointmentUnavailable", Assert.Single(line.Errors).Code));
        Assert.Equal(0, cart.Total);
    }

    [Fact]
    public async Task Two_cart_lines_cannot_take_the_same_time()
    {
        Calendar.AddAvailableBlock(At(28, 10, 0), At(28, 14, 0));

        var cart = await ValidateCart(
            Session(At(28, 10, 0)),
            Service(At(28, 10, 0)));

        Assert.False(cart.IsValid);
        Assert.Empty(cart.Items[0].Errors);
        Assert.Equal("appointmentUnavailable", Assert.Single(cart.Items[1].Errors).Code);
        Assert.Equal(24900, cart.Total);
    }

    [Fact]
    public async Task Absurd_dates_are_not_Terminy_and_do_not_break_the_API()
    {
        var cart = await ValidateCart(
            Session(DateTimeOffset.MinValue),
            Session(DateTimeOffset.MaxValue),
            Session(At(0, 10).AddYears(50)));
        var listed = await FreeTerminy(FakeProductCatalog.SessionSlug, "0001-01-01", "0001-01-31");
        var farListed = await FreeTerminy(FakeProductCatalog.SessionSlug, "9999-12-01", "9999-12-31");

        Assert.All(cart.Items, line => Assert.Equal("appointmentUnavailable", Assert.Single(line.Errors).Code));
        Assert.Empty(listed.Appointments);
        Assert.Empty(farListed.Appointments);
    }

    [Fact]
    public async Task When_the_calendar_cannot_be_read_the_Termin_cannot_be_checked()
    {
        Calendar.AddAvailableBlock(At(29, 10, 0), At(29, 12, 0));
        Calendar.RevokeAccess();

        var cart = await ValidateCart(Session(At(29, 10, 0)));

        Assert.False(cart.IsValid);
        Assert.Equal("appointmentUnverifiable", Assert.Single(Assert.Single(cart.Items).Errors).Code);
    }

    [Fact]
    public async Task A_paid_Termin_is_no_longer_free_and_Klara_gets_it_in_the_Order_email()
    {
        Calendar.AddAvailableBlock(At(32, 10, 0), At(32, 13, 0));
        var email = $"anna-{Guid.NewGuid():N}@example.com";
        var client = await _api.LoggedInClient(_factory.Emails, email);
        _factory.Payments.Reset();
        _factory.Emails.Reset();

        var checkout = await client.PostAsJsonAsync("/api/checkout", new
        {
            items = new[] { Session(At(32, 11, 0)) },
            email,
            name = "Anna Nowak",
            acceptedConsents = new[] { "termsAndPrivacy" },
        });
        Assert.Equal(HttpStatusCode.OK, checkout.StatusCode);
        await PayFor(Assert.Single(_factory.Payments.Sessions).Session.Id);

        var free = await FreeTerminy(FakeProductCatalog.SessionSlug, Day(32), Day(32));

        Assert.Equal([At(32, 10, 0), At(32, 12, 0)], free.Appointments.Select(a => a.Start));
        var toKlara = Assert.Single(_factory.Emails.Sent, m => m.To == ApiFactory.KlaraEmail).Html;
        var day = At(32, 11).ToString("d MMMM yyyy", CultureInfo.GetCultureInfo("pl-PL"));
        Assert.Contains($"Termin: {day}, 11:00–12:00", toKlara);
    }

    private static object Session(DateTimeOffset start) =>
        new { slug = FakeProductCatalog.SessionSlug, price = 24900, appointmentStart = start };

    private static object Service(DateTimeOffset start) =>
        new { slug = FakeProductCatalog.ServiceSlug, price = 34900, form = ServiceForm, appointmentStart = start };

    private async Task<FreeTerminyResponse> FreeTerminy(string slug, string from, string to)
    {
        var response = await _api.CreateClient().GetAsync(AppointmentsUrl(slug, from, to));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<FreeTerminyResponse>())!;
    }

    private static string AppointmentsUrl(string slug, string? from, string? to) =>
        QueryHelpers.AddQueryString(
            $"/api/products/{slug}/appointments",
            new Dictionary<string, string?> { ["from"] = from, ["to"] = to }.Where(p => p.Value is not null));

    private async Task<CartResponse> ValidateCart(params object[] items)
    {
        var response = await _api.CreateClient().PostAsJsonAsync("/api/cart/validate", new { items });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CartResponse>())!;
    }

    private async Task PayFor(string paymentSessionId)
    {
        var (payload, signature) = FakePaymentGateway.Webhook(paymentSessionId, PaymentOutcome.Paid);
        var response = await _api.CreateClient().SendAsync(new HttpRequestMessage(HttpMethod.Post, "/api/payments/stripe/webhook")
        {
            Content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json"),
            Headers = { { "Stripe-Signature", signature } },
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private sealed record FreeTerminyResponse(int DurationMinutes, List<AppointmentResponse> Appointments);

    private sealed record AppointmentResponse(DateTimeOffset Start, DateTimeOffset End);

    private sealed record CartResponse(bool IsValid, List<CartLineResponse> Items, int Total, List<ConsentResponse> RequiredConsents);

    private sealed record CartLineResponse(string Slug, List<ErrorResponse> Errors, AppointmentResponse? Appointment);

    private sealed record ErrorResponse(string Code, string Message);

    private sealed record ConsentResponse(string Key, string Text);
}
