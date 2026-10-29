using System.Globalization;
using System.Text;
using SzeptTarota.Application.Email;
using SzeptTarota.Domain.Orders;
using static System.Net.WebUtility;

namespace SzeptTarota.Application.Orders;

/// <summary>Treść maili o opłaconym Zamówieniu. Wszystko, co wpisał Klient, jest kodowane jako tekst HTML.</summary>
public static class OrderEmails
{
    private static readonly CultureInfo Polish = CultureInfo.GetCultureInfo("pl-PL");
    private static readonly TimeZoneInfo Warsaw = TimeZoneInfo.FindSystemTimeZoneById("Europe/Warsaw");

    /// <summary>Potwierdzenie dla Klienta: podsumowanie Zamówienia z Terminami i linkami Meet i Ebooki w załączniku.</summary>
    public static EmailMessage ToKlient(Order order, IReadOnlyList<EmailAttachment> ebooks, IReadOnlyList<OrderAppointment> appointments)
    {
        var html = new StringBuilder()
            .Append("<h1>Dziękuję za zakup!</h1>")
            .Append("<p>Twoje Zamówienie jest opłacone.")
            .Append(ebooks.Count > 0 ? " Zakupione Ebooki znajdziesz w załącznikach tego maila." : "")
            .Append("</p>")
            .Append(LinesTable(order, appointments, forKlara: false))
            .Append("<p>Ściskam,<br>Klara, Szept Tarota</p>")
            .ToString();

        return new EmailMessage(order.Email, "Twoje Zamówienie w Szept Tarota", html, ebooks, $"zamowienie-{order.Id}-klient");
    }

    /// <summary>
    /// Powiadomienie dla Klary: kto kupił, co, za ile, Terminy z linkami Meet, odpowiedzi z formularzy Usług i
    /// zaakceptowane zgody.
    /// </summary>
    public static EmailMessage ToKlara(Order order, string klaraEmail, IReadOnlyList<OrderAppointment> appointments)
    {
        var html = new StringBuilder()
            .Append("<h1>Nowe Zamówienie</h1>")
            .Append($"<p>Opłacone {FormatTime(order.PaidAt ?? order.CreatedAt)}. Numer: {order.Id}</p>")
            .Append($"<p>Klient: {HtmlEncode(order.CustomerName)}, {HtmlEncode(order.Email)}</p>")
            .Append(LinesTable(order, appointments, forKlara: true))
            .Append("<h2>Zgody</h2><ul>");
        foreach (var consent in order.Consents)
        {
            html.Append($"<li>{HtmlEncode(consent.Text)} ({FormatTime(consent.AcceptedAt)})</li>");
        }

        html.Append("</ul>");
        return new EmailMessage(
            klaraEmail, $"Nowe Zamówienie: {FormatPrice(order.Total)}", html.ToString(), [], $"zamowienie-{order.Id}-klara");
    }

    /// <summary>Pozycje Zamówienia; Klara widzi też odpowiedzi z formularzy i co ma zrobić z Terminem.</summary>
    private static string LinesTable(Order order, IReadOnlyList<OrderAppointment> appointments, bool forKlara)
    {
        var html = new StringBuilder("<table>");
        for (var index = 0; index < order.Lines.Count; index++)
        {
            var line = order.Lines[index];
            var quantity = line.Quantity > 1 ? $"{line.Quantity} × " : "";
            html.Append($"<tr><td>{quantity}{HtmlEncode(line.Name)}</td><td>{FormatPrice(line.Price * line.Quantity)}</td></tr>");
            if (line is { AppointmentStart: { } start, AppointmentEnd: { } end })
            {
                html.Append($"<tr><td colspan=\"2\">Termin: {FormatTime(start)}–{TimeZoneInfo.ConvertTime(end, Warsaw):HH:mm}</td></tr>");
                if (AppointmentNote(appointments.FirstOrDefault(a => a.LineIndex == index), forKlara) is { } note)
                {
                    html.Append($"<tr><td colspan=\"2\">{note}</td></tr>");
                }
            }

            if (forKlara && line.FormAnswers.Count > 0)
            {
                html.Append("<tr><td colspan=\"2\"><ul>");
                foreach (var answer in line.FormAnswers)
                {
                    html.Append($"<li>{HtmlEncode(answer.Label)}: {MultilineText(answer.Value)}</li>");
                }

                html.Append("</ul></td></tr>");
            }
        }

        return html.Append($"<tr><th>Suma</th><th>{FormatPrice(order.Total)}</th></tr></table>").ToString();
    }

    /// <summary>Link Meet Terminu albo co z nim dalej, gdy linku nie ma; nic dla Terminów z Zamówień sprzed blokad.</summary>
    private static string? AppointmentNote(OrderAppointment? appointment, bool forKlara) => appointment switch
    {
        null => null,
        { ReleasedAt: not null } => forKlara
            ? "Ten Termin zajął ktoś inny, zanim doszła płatność: ustal z Klientem nowy."
            : "Ten Termin zajął ktoś inny, zanim doszła płatność. Skontaktuję się z Tobą, żeby ustalić nowy.",
        { MeetLink: { } link } =>
            $"Link do spotkania (Google Meet): <a href=\"{HtmlEncode(link)}\">{HtmlEncode(link)}</a>",
        _ when !forKlara => "Link do spotkania wyślę Ci osobno.",
        { CalendarEventId: null } =>
            "Nie udało się dodać tego Terminu do Twojego kalendarza Google: dodaj go ręcznie i wyślij Klientowi link do spotkania.",
        _ => "Google nie dodał jeszcze linku do spotkania: sprawdź wydarzenie w kalendarzu i wyślij Klientowi link.",
    };

    /// <summary>Tekst Klienta jako HTML z zachowanymi nowymi liniami.</summary>
    private static string MultilineText(string text) =>
        string.Join("<br>", text.ReplaceLineEndings("\n").Split('\n').Select(HtmlEncode));

    /// <summary>Cena w groszach jako złote, np. 8800 → „88,00 zł”.</summary>
    private static string FormatPrice(int price) => (price / 100m).ToString("0.00", Polish) + " zł";

    private static string FormatTime(DateTimeOffset time) =>
        TimeZoneInfo.ConvertTime(time, Warsaw).ToString("d MMMM yyyy, HH:mm", Polish);
}
