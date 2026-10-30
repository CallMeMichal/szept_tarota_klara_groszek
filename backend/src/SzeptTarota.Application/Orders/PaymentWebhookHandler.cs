using Microsoft.Extensions.Options;
using SzeptTarota.Application.Appointments;
using SzeptTarota.Application.Calendar;
using SzeptTarota.Application.Ebooks;
using SzeptTarota.Application.Email;
using SzeptTarota.Application.Payments;
using SzeptTarota.Domain.Catalog;
using SzeptTarota.Domain.Orders;

namespace SzeptTarota.Application.Orders;

/// <summary>
/// Powiadomienie z bramki płatności o sesji Zamówienia. Bramka może wysłać to samo powiadomienie
/// kilka razy (także równocześnie), więc każdy krok robi się tylko raz.
/// </summary>
public sealed class PaymentWebhookHandler(
    IPaymentGateway paymentGateway,
    IOrderStore orders,
    IAppointmentStore appointments,
    ICalendarConnectionStore calendarConnections,
    IGoogleCalendar google,
    IEbookStorage ebooks,
    IEmailSender emails,
    TimeProvider time,
    IOptions<ShopOptions> shopOptions)
{
    /// <exception cref="InvalidPaymentWebhookException">Gdy podpis się nie zgadza.</exception>
    /// <exception cref="PaymentGatewayNotConfiguredException">Gdy nie ustawiono sekretu webhooków.</exception>
    public async Task HandleAsync(string payload, string? signature, CancellationToken cancellationToken)
    {
        if (paymentGateway.ReadWebhook(payload, signature) is not { } paymentEvent
            || await orders.FindByPaymentSessionAsync(paymentEvent.SessionId, cancellationToken) is not { } order)
        {
            return;
        }

        var now = time.GetUtcNow();
        switch (paymentEvent.Outcome)
        {
            case PaymentOutcome.Paid:
                await using (await orders.LockAsync(order.Id, cancellationToken))
                {
                    await orders.MarkPaidAsync(order.Id, now, cancellationToken);
                    // Czekając, inne powiadomienie mogło już dodać Terminy do kalendarza i wysłać maile.
                    await NotifyAboutPaidOrderAsync(await orders.FindAsync(order.Id, cancellationToken) ?? order, now, cancellationToken);
                }

                break;
            case PaymentOutcome.Expired:
                await orders.MarkExpiredAsync(order.Id, now, cancellationToken);
                await appointments.ReleaseAsync(order.Id, now, cancellationToken);
                break;
        }
    }

    /// <summary>
    /// Terminy opłaconego Zamówienia w kalendarzu Klary i maile o nim. Zamówienie jest oznaczane jako powiadomione dopiero po wysłaniu obu,
    /// więc awaria (także w połowie) kończy webhook błędem, a bramka ponowi powiadomienie. Każdy mail ma
    /// stały klucz idempotencji, więc ponowienie, także równoczesne, nie wyśle tego samego maila drugi raz.
    /// </summary>
    private async Task NotifyAboutPaidOrderAsync(Order order, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (order.NotificationsSentAt is not null)
        {
            return;
        }

        var booked = await appointments.ConfirmAsync(order.Id, cancellationToken);
        await AddToCalendarAsync(order, booked, cancellationToken);

        var attachments = new List<EmailAttachment>();
        foreach (var line in order.Lines.Where(l => l.Kind == ProductKind.Ebook && l.EbookFileKey is not null))
        {
            var fileName = Path.GetFileName(line.EbookFileKey!);
            var content = await ebooks.ReadAsync(line.EbookFileKey!, cancellationToken);
            attachments.Add(new EmailAttachment(fileName, EbookFile.ContentTypeOf(fileName), content));
        }

        await emails.SendAsync(OrderEmails.ToKlient(order, attachments, booked, shopOptions.Value.SiteUrl), cancellationToken);
        await emails.SendAsync(OrderEmails.ToKlara(order, shopOptions.Value.KlaraEmail, booked), cancellationToken);
        await orders.MarkNotificationsSentAsync(order.Id, now, cancellationToken);
    }

    /// <summary>
    /// Dodaje opłacone Terminy do głównego kalendarza Klary z Klientem jako gościem i linkiem Meet. Wydarzenie ma
    /// identyfikator Terminu, więc ponowione powiadomienie go nie zdubluje. Bez działającego kalendarza maile i tak
    /// idą: Klara dowiaduje się z nich, że ma dodać Termin sama, zamiast czekać na ponowienia bramki przez kilka dni.
    /// </summary>
    private async Task AddToCalendarAsync(Order order, IReadOnlyList<OrderAppointment> booked, CancellationToken cancellationToken)
    {
        var missing = booked.Where(b => b.IsPaid && b.CalendarEventId is null).ToList();
        if (missing.Count == 0
            || await calendarConnections.FindAsync(cancellationToken) is not { RefreshToken: { } refreshToken })
        {
            return;
        }

        foreach (var appointment in missing)
        {
            var line = order.Lines[appointment.LineIndex];
            var request = new AppointmentEventRequest(
                appointment.Id.ToString("N"),
                $"{line.Name}: {order.CustomerName}",
                EventDescription(order, line),
                appointment.Start,
                appointment.End,
                order.Email,
                order.CustomerName);
            GoogleAppointmentEvent created;
            try
            {
                created = await google.CreateAppointmentEventAsync(refreshToken, request, cancellationToken);
            }
            catch (Exception e) when (e is GoogleCalendarAccessLostException or GoogleCalendarNotConfiguredException or HttpRequestException)
            {
                return;
            }

            appointment.AddedToCalendar(created.Id, created.MeetLink, created.ICalUid);
            await appointments.SaveCalendarEventAsync(appointment.Id, created.Id, created.MeetLink, created.ICalUid, cancellationToken);
        }
    }

    /// <summary>Opis wydarzenia: Klient i odpowiedzi z formularza Usługi, jako zwykły tekst.</summary>
    private static string EventDescription(Order order, OrderLine line) =>
        string.Join('\n', line.FormAnswers.Select(a => $"{a.Label}: {a.Value}").Prepend($"Klient: {order.CustomerName}, {order.Email}"));
}
