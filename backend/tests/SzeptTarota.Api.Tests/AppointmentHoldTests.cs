using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using SzeptTarota.Api.Tests.Fakes;
using SzeptTarota.Api.Tests.Infrastructure;
using SzeptTarota.Application.Catalog;
using SzeptTarota.Application.Payments;

namespace SzeptTarota.Api.Tests;

/// <summary>
/// Blokada Terminu: rozpoczęcie płatności zajmuje wybrane Terminy na 15 minut, a po zapłacie Termin trafia do
/// kalendarza Klary jako wydarzenie z Klientem jako gościem i linkiem Meet. Każdy test bierze nowy dzień z Terminami.
/// </summary>
[Collection(ApiCollection.Name)]
public class AppointmentHoldTests : IAsyncLifetime
{
    private const string TakenMessage = "Ten termin jest już zajęty, wybierz inny.";

    private readonly ApiFactory _factory;
    private readonly FakeProductCatalog _catalog = new();
    private readonly WebApplicationFactory<Program> _api;

    /// <summary>Cztery wolne Terminy nowego dnia, pasujące do Sesji i Usługi.</summary>
    private DateTimeOffset[] _terminy = [];

    public AppointmentHoldTests(ApiFactory factory)
    {
        _factory = factory;
        _api = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.ReplaceWithFake<IProductCatalog>(_catalog)));
    }

    public async Task InitializeAsync()
    {
        _factory.Payments.Reset();
        _factory.Emails.Reset();
        _terminy = await _factory.OfferTerminy();
    }

    public Task DisposeAsync() => _api.DisposeAsync().AsTask();

    private FakeGoogleCalendar Calendar => _factory.GoogleCalendar;

    [Fact]
    public async Task Starting_the_payment_holds_the_Termin_for_other_Klienci()
    {
        var anna = await Klient();
        var maria = await Klient();

        var started = await anna.StartCheckout(Session(_terminy[0]));
        var free = await FreeStarts();
        var cart = await maria.ValidateCart(Session(_terminy[0]));

        Assert.Equal(HttpStatusCode.OK, started.StatusCode);
        Assert.DoesNotContain(_terminy[0], free);
        Assert.Contains(_terminy[1], free);
        var error = Assert.Single(Assert.Single(cart.Items).Errors);
        Assert.Equal(("appointmentTaken", TakenMessage), (error.Code, error.Message));
    }

    [Fact]
    public async Task Paying_for_a_held_Termin_tells_the_Klient_to_pick_another_and_creates_nothing()
    {
        var anna = await Klient();
        var maria = await Klient();
        await anna.StartCheckout(Session(_terminy[0]));

        var response = await maria.StartCheckout(Session(_terminy[1]), Session(_terminy[0]));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var cart = (await response.Content.ReadFromJsonAsync<CartResponse>())!;
        Assert.Empty(cart.Items[0].Errors);
        Assert.Equal(TakenMessage, Assert.Single(cart.Items[1].Errors).Message);
        Assert.Single(_factory.Payments.Sessions);
        // Druga pozycja Marii też nie została zablokowana.
        Assert.Contains(_terminy[1], await FreeStarts());
    }

    [Fact]
    public async Task Of_two_simultaneous_payments_for_the_same_Termin_only_one_goes_through()
    {
        var klienci = new[] { await Klient(), await Klient(), await Klient() };

        var responses = await Task.WhenAll(klienci.Select(k => k.StartCheckout(Session(_terminy[2]))));

        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.OK);
        Assert.Equal(2, responses.Count(r => r.StatusCode == HttpStatusCode.Conflict));
        Assert.Single(_factory.Payments.Sessions);
    }

    [Fact]
    public async Task A_Klient_who_interrupted_the_payment_can_pay_for_the_same_Termin_again()
    {
        var anna = await Klient();
        var maria = await Klient();
        await anna.StartCheckout(Session(_terminy[0]));

        var cart = await anna.ValidateCart(Session(_terminy[0]));
        var again = await anna.StartCheckout(Session(_terminy[0]));
        var forMaria = await maria.ValidateCart(Session(_terminy[0]));

        Assert.Empty(Assert.Single(cart.Items).Errors);
        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        Assert.Equal("appointmentTaken", Assert.Single(Assert.Single(forMaria.Items).Errors).Code);
    }

    [Fact]
    public async Task A_hold_expires_after_15_minutes()
    {
        var anna = await Klient();
        var maria = await Klient();
        await anna.StartCheckout(Session(_terminy[0]));

        _factory.Time.Advance(TimeSpan.FromMinutes(14));
        var heldAfter14 = await FreeStarts();
        _factory.Time.Advance(TimeSpan.FromMinutes(1));
        var freeAfter15 = await FreeStarts();
        var forMaria = await maria.StartCheckout(Session(_terminy[0]));

        Assert.DoesNotContain(_terminy[0], heldAfter14);
        Assert.Contains(_terminy[0], freeAfter15);
        Assert.Equal(HttpStatusCode.OK, forMaria.StatusCode);
    }

    [Fact]
    public async Task An_expired_payment_session_releases_the_Termin_at_once()
    {
        var anna = await Klient();
        var started = await anna.StartCheckout(Session(_terminy[0]));

        var webhook = await SendWebhook(await OrderIdOf(started), PaymentOutcome.Expired);

        Assert.Equal(HttpStatusCode.OK, webhook.StatusCode);
        Assert.Contains(_terminy[0], await FreeStarts());
    }

    [Fact]
    public async Task A_paid_Termin_goes_to_Klaras_calendar_with_the_Klient_as_guest_and_a_Meet_link()
    {
        var anna = await Klient();
        var orderId = await OrderIdOf(await anna.StartCheckout(
            Session(_terminy[0]),
            Service(_terminy[1], new { name = "Maria", birthDate = "1962-11-03", question = "Czy <zmienić> pracę?" })));

        var webhook = await SendWebhook(orderId, PaymentOutcome.Paid);

        Assert.Equal(HttpStatusCode.OK, webhook.StatusCode);
        var events = Calendar.AppointmentEvents;
        Assert.Equal(2, events.Count);
        var (session, service) = (events.Single(e => e.Request.Start == _terminy[0]), events.Single(e => e.Request.Start == _terminy[1]));
        Assert.Equal("Sesja Reiki: Anna Nowak", session.Request.Summary);
        Assert.Equal(_terminy[0].AddMinutes(60), session.Request.End);
        Assert.Equal((anna.Email, "Anna Nowak"), (session.Request.GuestEmail, session.Request.GuestName));
        Assert.Equal("Mapa Duszy: Anna Nowak", service.Request.Summary);
        Assert.Equal(_terminy[1].AddMinutes(90), service.Request.End);
        Assert.Contains("Imię: Maria", service.Request.Description);
        Assert.Contains("Data urodzenia: 1962-11-03", service.Request.Description);
        Assert.Contains("Pytanie do kart: Czy <zmienić> pracę?", service.Request.Description);
        Assert.Contains(anna.Email, service.Request.Description);
        // Opłacony Termin zajmuje czas Klary w jej głównym kalendarzu.
        Assert.Contains(Calendar.EventsIn(FakeGoogleCalendar.MainCalendar.Id), e => e.Start == _terminy[0] && e.Busy);

        var termin = $"{FormatDay(_terminy[0])}, 08:00–09:00";
        foreach (var to in new[] { anna.Email, ApiFactory.KlaraEmail })
        {
            var html = Assert.Single(_factory.Emails.Sent, m => m.To == to && m.Subject.Contains("Zamówienie")).Html;
            Assert.Contains(termin, html);
            Assert.Contains(session.MeetLink, html);
            Assert.Contains(service.MeetLink, html);
        }
    }

    [Fact]
    public async Task The_Klients_mail_carries_the_Termin_as_a_calendar_file_to_save_in_one_click()
    {
        var anna = await Klient();
        var orderId = await OrderIdOf(await anna.StartCheckout(Session(_terminy[0])));

        await SendWebhook(orderId, PaymentOutcome.Paid);

        var created = Assert.Single(Calendar.AppointmentEvents);
        var toAnna = Assert.Single(_factory.Emails.Sent, m => m.To == anna.Email && m.Subject.Contains("Zamówienie"));
        var file = Assert.Single(toAnna.Attachments);
        Assert.StartsWith("text/calendar", file.ContentType);
        Assert.EndsWith(".ics", file.FileName);
        // Długie linie pliku kalendarza są łamane: nowa linia i spacja.
        var ics = System.Text.Encoding.UTF8.GetString(file.Content).Replace("\r\n ", "");
        Assert.Contains("METHOD:PUBLISH", ics);
        // Ten sam identyfikator co wydarzenie w Google, więc kalendarz Klienta nie pokaże go dwa razy.
        Assert.Contains($"UID:{created.ICalUid}", ics);
        Assert.Contains($"DTSTART:{_terminy[0].UtcDateTime:yyyyMMdd'T'HHmmss'Z'}", ics);
        Assert.Contains($"DTEND:{_terminy[0].AddMinutes(60).UtcDateTime:yyyyMMdd'T'HHmmss'Z'}", ics);
        Assert.Contains("SUMMARY:Sesja Reiki", ics);
        Assert.Contains(created.MeetLink, ics);
        Assert.Empty(Assert.Single(_factory.Emails.Sent, m => m.To == ApiFactory.KlaraEmail).Attachments);
    }

    [Fact]
    public async Task Simultaneous_payment_notifications_are_handled_one_after_another()
    {
        // Przy BLIK-u Stripe wysyła „completed” i „async_payment_succeeded” naraz.
        var anna = await Klient();
        var orderId = await OrderIdOf(await anna.StartCheckout(Session(_terminy[0])));
        Calendar.AppointmentEventDelay = TimeSpan.FromMilliseconds(300);

        var simultaneous = await Task.WhenAll(Enumerable.Range(0, 3).Select(_ => SendWebhook(orderId, PaymentOutcome.Paid)));
        var later = await SendWebhook(orderId, PaymentOutcome.Paid);

        Assert.All(simultaneous.Append(later), r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
        Assert.Single(Calendar.AppointmentEvents);
        Assert.Equal(1, Calendar.AppointmentEventCalls);
        Assert.Single(_factory.Emails.Sent, m => m.To == anna.Email && m.Subject.Contains("Zamówienie"));
        Assert.Single(_factory.Emails.Sent, m => m.To == ApiFactory.KlaraEmail);
    }

    [Fact]
    public async Task A_payment_after_the_hold_expired_still_books_the_Termin_if_nobody_took_it()
    {
        var anna = await Klient();
        var orderId = await OrderIdOf(await anna.StartCheckout(Session(_terminy[0])));
        _factory.Time.Advance(TimeSpan.FromMinutes(20));

        await SendWebhook(orderId, PaymentOutcome.Paid);

        Assert.Single(Calendar.AppointmentEvents);
        Assert.DoesNotContain(_terminy[0], await FreeStarts());
    }

    [Fact]
    public async Task A_payment_after_someone_else_took_the_expired_Termin_is_kept_but_Klara_has_to_agree_a_new_one()
    {
        var anna = await Klient();
        var maria = await Klient();
        var annasOrder = await OrderIdOf(await anna.StartCheckout(Session(_terminy[0])));
        _factory.Time.Advance(TimeSpan.FromMinutes(16));
        var mariasOrder = await OrderIdOf(await maria.StartCheckout(Session(_terminy[0])));

        await SendWebhook(annasOrder, PaymentOutcome.Paid);
        await SendWebhook(mariasOrder, PaymentOutcome.Paid);

        var booked = Assert.Single(Calendar.AppointmentEvents);
        Assert.Equal(maria.Email, booked.Request.GuestEmail);
        var mailToAnna = Assert.Single(_factory.Emails.Sent, m => m.To == anna.Email && m.Subject.Contains("Zamówienie"));
        Assert.Empty(mailToAnna.Attachments);
        var toAnna = mailToAnna.Html;
        Assert.Contains("ktoś inny", toAnna);
        Assert.DoesNotContain("meet.google.com", toAnna);
        var toKlara = _factory.Emails.Sent.Where(m => m.To == ApiFactory.KlaraEmail).Select(m => m.Html).ToList();
        Assert.Contains(toKlara, html => html.Contains(anna.Email) && html.Contains("ustal z Klientem nowy"));
    }

    [Fact]
    public async Task Without_access_to_the_calendar_the_mails_still_go_and_Klara_is_asked_to_add_the_Termin_herself()
    {
        var anna = await Klient();
        var orderId = await OrderIdOf(await anna.StartCheckout(Session(_terminy[0])));
        Calendar.RevokeAccess();

        var webhook = await SendWebhook(orderId, PaymentOutcome.Paid);

        Assert.Equal(HttpStatusCode.OK, webhook.StatusCode);
        Assert.Empty(Calendar.AppointmentEvents);
        var toKlara = Assert.Single(_factory.Emails.Sent, m => m.To == ApiFactory.KlaraEmail).Html;
        Assert.Contains("dodaj go ręcznie", toKlara);
        var toAnna = Assert.Single(_factory.Emails.Sent, m => m.To == anna.Email && m.Subject.Contains("Zamówienie")).Html;
        Assert.Contains($"{FormatDay(_terminy[0])}, 08:00–09:00", toAnna);
        Assert.Contains("Link do spotkania wyślę Ci osobno", toAnna);
    }

    private static string FormatDay(DateTimeOffset time) =>
        TimeZoneInfo.ConvertTime(time, CalendarExtensions.Warsaw).ToString("d MMMM yyyy", System.Globalization.CultureInfo.GetCultureInfo("pl-PL"));

    private static object Service(DateTimeOffset start, object form) =>
        new { slug = FakeProductCatalog.ServiceSlug, price = 34900, form, appointmentStart = start };

    private static object Session(DateTimeOffset start) =>
        new { slug = FakeProductCatalog.SessionSlug, price = 24900, appointmentStart = start };

    private async Task<TestKlient> Klient()
    {
        var email = $"klient-{Guid.NewGuid():N}@example.com";
        return new TestKlient(await _api.LoggedInClient(_factory.Emails, email), email);
    }

    /// <summary>Początki wolnych Terminów Sesji w dniu z <see cref="_terminy"/>, tak jak widzi je niezalogowany odwiedzający.</summary>
    private async Task<List<DateTimeOffset>> FreeStarts()
    {
        var day = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(_terminy[0], CalendarExtensions.Warsaw).Date).ToString("yyyy-MM-dd");
        var response = await _api.CreateClient().GetAsync($"/api/products/{FakeProductCatalog.SessionSlug}/appointments?from={day}&to={day}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var free = (await response.Content.ReadFromJsonAsync<FreeTerminyResponse>())!;
        return free.Appointments.Select(a => a.Start).ToList();
    }

    private static async Task<Guid> OrderIdOf(HttpResponseMessage started)
    {
        Assert.Equal(HttpStatusCode.OK, started.StatusCode);
        return (await started.Content.ReadFromJsonAsync<CheckoutStartedResponse>())!.OrderId;
    }

    private Task<HttpResponseMessage> SendWebhook(Guid orderId, PaymentOutcome outcome)
    {
        var sessionId = _factory.Payments.Sessions.Single(s => s.Request.OrderId == orderId).Session.Id;
        var (payload, signature) = FakePaymentGateway.Webhook(sessionId, outcome);
        return _api.CreateClient().SendAsync(new HttpRequestMessage(HttpMethod.Post, "/api/payments/stripe/webhook")
        {
            Content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json"),
            Headers = { { "Stripe-Signature", signature } },
        });
    }

    private sealed record CheckoutStartedResponse(Guid OrderId, Uri RedirectUrl);

    private sealed record TestKlient(HttpClient Client, string Email)
    {
        public Task<HttpResponseMessage> StartCheckout(params object[] items) =>
            Client.PostAsJsonAsync("/api/checkout", new
            {
                items,
                email = Email,
                name = "Anna Nowak",
                acceptedConsents = new[] { "termsAndPrivacy" },
            });

        public async Task<CartResponse> ValidateCart(params object[] items)
        {
            var response = await Client.PostAsJsonAsync("/api/cart/validate", new { items });
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            return (await response.Content.ReadFromJsonAsync<CartResponse>())!;
        }
    }

    private sealed record FreeTerminyResponse(int DurationMinutes, List<AppointmentResponse> Appointments);

    private sealed record AppointmentResponse(DateTimeOffset Start, DateTimeOffset End);

    private sealed record CartResponse(bool IsValid, List<CartLineResponse> Items);

    private sealed record CartLineResponse(string Slug, List<ErrorResponse> Errors);

    private sealed record ErrorResponse(string Code, string Message);
}
