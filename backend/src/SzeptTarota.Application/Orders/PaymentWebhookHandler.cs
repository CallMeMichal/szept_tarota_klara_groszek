using Microsoft.Extensions.Options;
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
                await orders.MarkPaidAsync(order.Id, now, cancellationToken);
                await NotifyAboutPaidOrderAsync(order, now, cancellationToken);
                break;
            case PaymentOutcome.Expired:
                await orders.MarkExpiredAsync(order.Id, now, cancellationToken);
                break;
        }
    }

    /// <summary>
    /// Maile o opłaconym Zamówieniu. Zamówienie jest oznaczane jako powiadomione dopiero po wysłaniu obu,
    /// więc awaria (także w połowie) kończy webhook błędem, a bramka ponowi powiadomienie. Każdy mail ma
    /// stały klucz idempotencji, więc ponowienie, także równoczesne, nie wyśle tego samego maila drugi raz.
    /// </summary>
    private async Task NotifyAboutPaidOrderAsync(Order order, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (order.NotificationsSentAt is not null)
        {
            return;
        }

        var attachments = new List<EmailAttachment>();
        foreach (var line in order.Lines.Where(l => l.Kind == ProductKind.Ebook && l.EbookFileKey is not null))
        {
            var fileName = Path.GetFileName(line.EbookFileKey!);
            var content = await ebooks.ReadAsync(line.EbookFileKey!, cancellationToken);
            attachments.Add(new EmailAttachment(fileName, ContentTypeOf(fileName), content));
        }

        await emails.SendAsync(OrderEmails.ToKlient(order, attachments), cancellationToken);
        await emails.SendAsync(OrderEmails.ToKlara(order, shopOptions.Value.KlaraEmail), cancellationToken);
        await orders.MarkNotificationsSentAsync(order.Id, now, cancellationToken);
    }

    private static string ContentTypeOf(string fileName) =>
        Path.GetExtension(fileName).Equals(".pdf", StringComparison.OrdinalIgnoreCase)
            ? "application/pdf"
            : "application/octet-stream";
}
