using System.Globalization;
using System.Text;
using SzeptTarota.Application.Email;
using SzeptTarota.Domain.Orders;
using static System.Net.WebUtility;
using static SzeptTarota.Application.Email.EmailLayout;

namespace SzeptTarota.Application.Orders;

/// <summary>
/// Treść maili o opłaconym Zamówieniu. Każdy Termin ma własny blok jak zaproszenie z kalendarza: kiedy, przycisk
/// do Google Meet i link. Wszystko, co wpisał Klient, jest kodowane jako tekst HTML.
/// </summary>
public static class OrderEmails
{
    private static readonly CultureInfo Polish = CultureInfo.GetCultureInfo("pl-PL");
    private static readonly TimeZoneInfo Warsaw = TimeZoneInfo.FindSystemTimeZoneById("Europe/Warsaw");

    /// <summary>Potwierdzenie dla Klienta: Terminy z linkami Meet, podsumowanie Zamówienia i Ebooki w załączniku.</summary>
    public static EmailMessage ToKlient(Order order, IReadOnlyList<EmailAttachment> ebooks, IReadOnlyList<OrderAppointment> appointments)
    {
        var firstName = order.CustomerName.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        var body = new StringBuilder()
            .Append(Heading("Dziękuję za zakup!"))
            .Append(Paragraph(
                (firstName is null ? "" : $"Cześć {HtmlEncode(firstName)}, ") + "Twoje Zamówienie jest opłacone."
                + (ebooks.Count > 0 ? " Zakupione Ebooki znajdziesz w załącznikach tego maila." : "")))
            .Append(TerminPanels(order, appointments, forKlara: false))
            .Append(SectionHeading("Twoje Zamówienie"))
            .Append(LinesTable(order, forKlara: false))
            .Append(Paragraph("<br>Ściskam,<br>Klara, Szept Tarota"))
            .ToString();

        var html = Page("Twoje Zamówienie jest opłacone.", body, $"Numer Zamówienia: {order.Id}");
        return new EmailMessage(order.Email, "Twoje Zamówienie w Szept Tarota", html, ebooks, $"zamowienie-{order.Id}-klient");
    }

    /// <summary>
    /// Powiadomienie dla Klary: kto kupił, Terminy z linkami Meet i tym, co ma z nimi zrobić, co i za ile, i odpowiedzi
    /// z formularzy Usług. Zgody zostają tylko w Zamówieniu.
    /// </summary>
    public static EmailMessage ToKlara(Order order, string klaraEmail, IReadOnlyList<OrderAppointment> appointments)
    {
        var body = new StringBuilder()
            .Append(Heading("Nowe Zamówienie"))
            .Append(Field("Klient", $"{HtmlEncode(order.CustomerName)}, {Link($"mailto:{order.Email}", order.Email)}"))
            .Append(Field("Opłacone", HtmlEncode(FormatTime(order.PaidAt ?? order.CreatedAt))))
            .Append(TerminPanels(order, appointments, forKlara: true))
            .Append(SectionHeading("Zamówienie"))
            .Append(LinesTable(order, forKlara: true));

        var html = Page($"{order.CustomerName}: {FormatPrice(order.Total)}", body.ToString(), $"Numer Zamówienia: {order.Id}");
        return new EmailMessage(klaraEmail, $"Nowe Zamówienie: {FormatPrice(order.Total)}", html, [], $"zamowienie-{order.Id}-klara");
    }

    /// <summary>Blok każdego Terminu: kiedy, przycisk do Google Meet z linkiem albo co dalej, gdy linku nie ma.</summary>
    private static string TerminPanels(Order order, IReadOnlyList<OrderAppointment> appointments, bool forKlara)
    {
        var html = new StringBuilder();
        for (var index = 0; index < order.Lines.Count; index++)
        {
            if (order.Lines[index] is not { AppointmentStart: { } start, AppointmentEnd: { } end } line)
            {
                continue;
            }

            var appointment = appointments.FirstOrDefault(a => a.LineIndex == index);
            var body = new StringBuilder(Field("Kiedy", $"{HtmlEncode(FormatTermin(start, end))} <span style=\"color:#626675;\">(czas polski)</span>"));
            if (appointment is { ReleasedAt: null, MeetLink: { } link })
            {
                body.Append(Button(link, "Dołącz do spotkania w Google Meet"))
                    .Append(Field("Link do spotkania", Link(link)));
                if (!forKlara)
                {
                    body.Append(Note("Zaproszenie z przypomnieniami wyśle Ci też Kalendarz Google."));
                }
            }
            else if (AppointmentProblem(appointment, forKlara) is { } problem)
            {
                body.Append(Warning(problem));
            }

            html.Append(Panel(forKlara ? "Termin" : "Twój Termin", line.Name, body.ToString()));
        }

        return html.ToString();
    }

    /// <summary>Co z Terminem bez linku Meet; nic dla Terminów z Zamówień sprzed blokad.</summary>
    private static string? AppointmentProblem(OrderAppointment? appointment, bool forKlara) => appointment switch
    {
        null => null,
        { ReleasedAt: not null } => forKlara
            ? "Ten Termin zajął ktoś inny, zanim doszła płatność: ustal z Klientem nowy."
            : "Ten Termin zajął ktoś inny, zanim doszła płatność. Skontaktuję się z Tobą, żeby ustalić nowy.",
        _ when !forKlara => "Link do spotkania wyślę Ci osobno.",
        { CalendarEventId: null } =>
            "Nie udało się dodać tego Terminu do Twojego kalendarza Google: dodaj go ręcznie i wyślij Klientowi link do spotkania.",
        _ => "Google nie dodał jeszcze linku do spotkania: sprawdź wydarzenie w kalendarzu i wyślij Klientowi link.",
    };

    /// <summary>Pozycje z cenami i sumą; Klara widzi pod każdą Usługą odpowiedzi z formularza.</summary>
    private static string LinesTable(Order order, bool forKlara)
    {
        var rows = new StringBuilder();
        foreach (var line in order.Lines)
        {
            var quantity = line.Quantity > 1 ? $"{line.Quantity} × " : "";
            var answers = forKlara && line.FormAnswers.Count > 0
                ? string.Join("<br>", line.FormAnswers.Select(a => $"{HtmlEncode(a.Label)}: {MultilineText(a.Value)}"))
                : null;
            rows.Append(Row(quantity + HtmlEncode(line.Name), FormatPrice(line.Price * line.Quantity), answers));
        }

        return Table(rows.ToString(), "Suma", FormatPrice(order.Total));
    }

    /// <summary>Tekst Klienta jako HTML z zachowanymi nowymi liniami.</summary>
    private static string MultilineText(string text) =>
        string.Join("<br>", text.ReplaceLineEndings("\n").Split('\n').Select(HtmlEncode));

    /// <summary>Cena w groszach jako złote, np. 8800 → „88,00 zł”.</summary>
    private static string FormatPrice(int price) => (price / 100m).ToString("0.00", Polish) + " zł";

    private static string FormatTime(DateTimeOffset time) =>
        TimeZoneInfo.ConvertTime(time, Warsaw).ToString("d MMMM yyyy, HH:mm", Polish);

    /// <summary>Termin z dniem tygodnia, np. „czwartek, 30 października 2026, 08:00–09:00”.</summary>
    private static string FormatTermin(DateTimeOffset start, DateTimeOffset end) =>
        $"{TimeZoneInfo.ConvertTime(start, Warsaw).ToString("dddd, d MMMM yyyy, HH:mm", Polish)}–{TimeZoneInfo.ConvertTime(end, Warsaw):HH:mm}";
}
