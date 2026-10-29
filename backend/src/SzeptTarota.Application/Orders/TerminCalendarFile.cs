using System.Globalization;
using System.Text;
using SzeptTarota.Application.Email;

namespace SzeptTarota.Application.Orders;

/// <summary>
/// Termin jako plik kalendarza (iCalendar, RFC 5545) w załączniku maila do Klienta. Gmail pokazuje go jako kartę
/// wydarzenia z przyciskiem „Dodaj do kalendarza”, a Outlook i kalendarz Apple pozwalają go zapisać. To wydarzenie
/// „do zapisania” (<c>METHOD:PUBLISH</c>), nie zaproszenie z odpowiedzią: Klient nie musi niczego potwierdzać.
/// </summary>
public static class TerminCalendarFile
{
    /// <param name="uid">
    /// Identyfikator wydarzenia; ten sam co w Google Calendar Klary, więc kalendarz Google Klienta, w którym Termin
    /// już jest jako zaproszenie, nie pokaże go dwa razy.
    /// </param>
    public static EmailAttachment Create(
        string fileName, string uid, string summary, DateTimeOffset start, DateTimeOffset end, string? meetLink, DateTimeOffset stamp)
    {
        var lines = new List<string>
        {
            "BEGIN:VCALENDAR",
            "VERSION:2.0",
            "PRODID:-//Szept Tarota//Terminy//PL",
            "CALSCALE:GREGORIAN",
            "METHOD:PUBLISH",
            "BEGIN:VEVENT",
            $"UID:{Text(uid)}",
            $"DTSTAMP:{Utc(stamp)}",
            $"DTSTART:{Utc(start)}",
            $"DTEND:{Utc(end)}",
            $"SUMMARY:{Text(summary)}",
        };
        if (meetLink is not null)
        {
            lines.Add($"DESCRIPTION:{Text($"Dołącz do spotkania w Google Meet: {meetLink}")}");
            lines.Add($"LOCATION:{Text(meetLink)}");
            lines.Add($"URL:{meetLink}");
        }

        lines.AddRange(
        [
            // Przypomnienie godzinę wcześniej; kalendarz Google stosuje własne ustawienia Klienta.
            "BEGIN:VALARM",
            "ACTION:DISPLAY",
            $"DESCRIPTION:{Text(summary)}",
            "TRIGGER:-PT1H",
            "END:VALARM",
            "END:VEVENT",
            "END:VCALENDAR",
        ]);

        var content = string.Concat(lines.Select(line => Fold(line) + "\r\n"));
        return new EmailAttachment(fileName, "text/calendar; charset=utf-8; method=PUBLISH", Encoding.UTF8.GetBytes(content));
    }

    private static string Utc(DateTimeOffset time) =>
        time.UtcDateTime.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);

    /// <summary>Tekst z ukośnikami przed znakami, które w pliku kalendarza coś znaczą.</summary>
    private static string Text(string text) =>
        text.Replace("\\", "\\\\").Replace(";", "\\;").Replace(",", "\\,").ReplaceLineEndings("\\n");

    /// <summary>Linie dłuższe niż 75 bajtów łamie się: nowa linia i spacja; nie w środku znaku UTF-8.</summary>
    private static string Fold(string line)
    {
        var folded = new StringBuilder();
        var bytes = 0;
        foreach (var rune in line.EnumerateRunes())
        {
            var size = rune.Utf8SequenceLength;
            if (bytes + size > 75)
            {
                folded.Append("\r\n ");
                bytes = 1;
            }

            folded.Append(rune.ToString());
            bytes += size;
        }

        return folded.ToString();
    }
}
