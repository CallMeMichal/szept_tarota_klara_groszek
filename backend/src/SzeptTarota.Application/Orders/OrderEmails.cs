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

    /// <summary>Potwierdzenie dla Klienta: podsumowanie Zamówienia i Ebooki w załączniku.</summary>
    public static EmailMessage ToKlient(Order order, IReadOnlyList<EmailAttachment> ebooks)
    {
        var html = new StringBuilder()
            .Append("<h1>Dziękuję za zakup!</h1>")
            .Append("<p>Twoje Zamówienie jest opłacone.")
            .Append(ebooks.Count > 0 ? " Zakupione Ebooki znajdziesz w załącznikach tego maila." : "")
            .Append("</p>")
            .Append(LinesTable(order))
            .Append("<p>Ściskam,<br>Klara, Szept Tarota</p>")
            .ToString();

        return new EmailMessage(order.Email, "Twoje Zamówienie w Szept Tarota", html, ebooks, $"zamowienie-{order.Id}-klient");
    }

    /// <summary>Powiadomienie dla Klary: kto kupił, co, za ile, odpowiedzi z formularzy Usług i zaakceptowane zgody.</summary>
    public static EmailMessage ToKlara(Order order, string klaraEmail)
    {
        var html = new StringBuilder()
            .Append("<h1>Nowe Zamówienie</h1>")
            .Append($"<p>Opłacone {FormatTime(order.PaidAt ?? order.CreatedAt)}. Numer: {order.Id}</p>")
            .Append($"<p>Klient: {HtmlEncode(order.CustomerName)}, {HtmlEncode(order.Email)}</p>")
            .Append(LinesTable(order, withFormAnswers: true))
            .Append("<h2>Zgody</h2><ul>");
        foreach (var consent in order.Consents)
        {
            html.Append($"<li>{HtmlEncode(consent.Text)} ({FormatTime(consent.AcceptedAt)})</li>");
        }

        html.Append("</ul>");
        return new EmailMessage(
            klaraEmail, $"Nowe Zamówienie: {FormatPrice(order.Total)}", html.ToString(), [], $"zamowienie-{order.Id}-klara");
    }

    private static string LinesTable(Order order, bool withFormAnswers = false)
    {
        var html = new StringBuilder("<table>");
        foreach (var line in order.Lines)
        {
            html.Append($"<tr><td>{HtmlEncode(line.Name)}</td><td>{FormatPrice(line.Price)}</td></tr>");
            if (withFormAnswers && line.FormAnswers.Count > 0)
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

    /// <summary>Tekst Klienta jako HTML z zachowanymi nowymi liniami.</summary>
    private static string MultilineText(string text) =>
        string.Join("<br>", text.ReplaceLineEndings("\n").Split('\n').Select(HtmlEncode));

    /// <summary>Cena w groszach jako złote, np. 8800 → „88,00 zł”.</summary>
    private static string FormatPrice(int price) => (price / 100m).ToString("0.00", Polish) + " zł";

    private static string FormatTime(DateTimeOffset time) =>
        TimeZoneInfo.ConvertTime(time, Warsaw).ToString("d MMMM yyyy, HH:mm", Polish);
}
