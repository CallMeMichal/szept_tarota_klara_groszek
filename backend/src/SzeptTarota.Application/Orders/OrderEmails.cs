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

    /// <summary>
    /// Potwierdzenie dla Klienta: Terminy z linkami Meet, podsumowanie Zamówienia, a w załącznikach Ebooki i każdy
    /// Termin jako plik kalendarza do zapisania jednym kliknięciem.
    /// </summary>
    public static EmailMessage ToKlient(
        Order order, IReadOnlyList<EmailAttachment> ebooks, IReadOnlyList<OrderAppointment> appointments, string siteUrl)
    {
        var firstName = order.CustomerName.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        var body = new StringBuilder()
            .Append(Eyebrow("Zamówienie opłacone"))
            .Append(Heading("Dziękuję za zakup!"))
            .Append(Paragraph(
                (firstName is null ? "" : $"Cześć {HtmlEncode(firstName)}, ") + "Twoje Zamówienie jest opłacone."
                + (ebooks.Count > 0 ? " Zakupione Ebooki znajdziesz w załącznikach tego maila." : "")))
            .Append(TerminPanels(order, appointments, forKlara: false))
            .Append(order.AccountCreated ? AccountCreatedNote(siteUrl) : "")
            .Append(SectionHeading("Twoje Zamówienie"))
            .Append(LinesTable(order, forKlara: false))
            .Append(Paragraph(
                "<br>Ściskam,<br><span style=\"font-family:'Cormorant Garamond',Georgia,serif;font-style:italic;font-size:20px;color:#e2c17a;\">Klara</span>"))
            .ToString();

        var html = Page("Twoje Zamówienie jest opłacone.", body, $"Numer Zamówienia: {order.Id}");
        return new EmailMessage(
            order.Email,
            "Twoje Zamówienie w Szept Tarota",
            html,
            [.. ebooks, .. CalendarFiles(order, appointments)],
            $"zamowienie-{order.Id}-klient",
            Images(order));
    }

    /// <summary>Konto założone przy tym Zamówieniu na prośbę Klienta: jak się do niego dostać.</summary>
    private static string AccountCreatedNote(string siteUrl) =>
        Paragraph(
            "Założyłam Ci Konto na ten adres. Zaloguj się kodem z maila, żeby widzieć swoje Zamówienia i pobierać Ebooki: "
            + Link($"{siteUrl.TrimEnd('/')}/logowanie", "zaloguj się"));

    /// <summary>Grafiki układu i okładki Produktów z Zamówienia, każda raz.</summary>
    private static IReadOnlyList<EmailImage> Images(Order order) =>
    [
        .. EmailLayout.Images,
        .. order.Lines.Select(l => l.Slug).Distinct().Select(EmailImages.Product).OfType<EmailImage>(),
    ];

    /// <summary>Plik kalendarza każdego Terminu, który Klient ma; bez tych, które zajął w międzyczasie ktoś inny.</summary>
    private static IEnumerable<EmailAttachment> CalendarFiles(Order order, IReadOnlyList<OrderAppointment> appointments)
    {
        var kept = appointments.Where(a => a.ReleasedAt is null).OrderBy(a => a.LineIndex).ToList();
        return kept.Select((appointment, number) => TerminCalendarFile.Create(
            kept.Count == 1 ? "termin.ics" : $"termin-{number + 1}.ics",
            appointment.CalendarEventUid ?? $"{appointment.Id:N}@szept-tarota",
            $"{order.Lines[appointment.LineIndex].Name} (Szept Tarota)",
            appointment.Start,
            appointment.End,
            appointment.MeetLink,
            order.PaidAt ?? order.CreatedAt));
    }

    /// <summary>
    /// Powiadomienie dla Klary: kto kupił, Terminy z linkami Meet i tym, co ma z nimi zrobić, co i za ile, i odpowiedzi
    /// z formularzy Usług. Zgody zostają tylko w Zamówieniu.
    /// </summary>
    public static EmailMessage ToKlara(Order order, string klaraEmail, IReadOnlyList<OrderAppointment> appointments)
    {
        var body = new StringBuilder()
            .Append(Eyebrow($"Opłacone {FormatTime(order.PaidAt ?? order.CreatedAt)}"))
            .Append(Heading("Nowe Zamówienie"))
            .Append(Field("Klient", $"{HtmlEncode(order.CustomerName)}, {Link($"mailto:{order.Email}", order.Email)}"))
            .Append(TerminPanels(order, appointments, forKlara: true))
            .Append(SectionHeading("Zamówienie"))
            .Append(LinesTable(order, forKlara: true));

        var html = Page($"{order.CustomerName}: {FormatPrice(order.Total)}", body.ToString(), $"Numer Zamówienia: {order.Id}");
        return new EmailMessage(
            klaraEmail, $"Nowe Zamówienie: {FormatPrice(order.Total)}", html, [], $"zamowienie-{order.Id}-klara", Images(order));
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
            var body = new StringBuilder(Field("Kiedy", $"{HtmlEncode(FormatTermin(start, end))} <span style=\"color:#a7a3bd;\">(czas polski)</span>"));
            if (appointment is { ReleasedAt: null, MeetLink: { } link })
            {
                body.Append(Button(link, "Dołącz do spotkania w Google Meet"))
                    .Append(Field("Link do spotkania", Link(link)));
                if (!forKlara)
                {
                    body.Append(Note("Termin jest też w załączniku: zapisz go w swoim kalendarzu jednym kliknięciem."));
                }
            }
            else if (AppointmentProblem(appointment, forKlara) is { } problem)
            {
                body.Append(Warning(problem));
            }

            html.Append(Panel(forKlara ? "Termin" : "Twój Termin", line.Name, body.ToString(), EmailImages.Product(line.Slug)));
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
            rows.Append(Row(quantity + HtmlEncode(line.Name), FormatPrice(line.Price * line.Quantity), answers, EmailImages.Product(line.Slug)));
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
