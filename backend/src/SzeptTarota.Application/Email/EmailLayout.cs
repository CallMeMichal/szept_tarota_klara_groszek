using static System.Net.WebUtility;

namespace SzeptTarota.Application.Email;

/// <summary>
/// Wygląd maili w barwach strony: granatowy pasek z nazwą marki, biała karta na kremowym tle. Programy pocztowe
/// nie znają stylów strony, więc układ to tabele ze stylami w atrybutach. Parametry <c>html</c> muszą być już
/// zakodowane; zwykły tekst koduje sam układ.
/// </summary>
public static class EmailLayout
{
    // Kolory z frontend/src: granat nocnego nieba, złoto i kremowe tło.
    private const string Night = "#131c44";
    private const string NightLight = "#1e2a5c";
    private const string Gold = "#d4af61";
    private const string GoldDark = "#a8823c";
    private const string Cream = "#f3eee4";
    private const string Ivory = "#f5efe2";
    private const string Text = "#2b2f45";
    private const string Muted = "#626675";
    private const string Line = "#e4dccb";

    private const string Serif = "Georgia, 'Times New Roman', serif";
    private const string Sans = "'Segoe UI', Roboto, Helvetica, Arial, sans-serif";

    /// <summary>Cały mail; <paramref name="preview"/> to tekst, który program pocztowy pokazuje obok tematu.</summary>
    public static string Page(string preview, string bodyHtml, string? footer = null)
    {
        var footerHtml = footer is null
            ? ""
            : $"""<p style="margin:16px 0 0;font-family:{Sans};font-size:12px;color:{Muted};">{HtmlEncode(footer)}</p>""";
        return $"""
        <!doctype html>
        <html lang="pl"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width"></head>
        <body style="margin:0;padding:0;background:{Cream};">
        <div style="display:none;max-height:0;overflow:hidden;">{HtmlEncode(preview)}</div>
        <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="background:{Cream};">
        <tr><td align="center" style="padding:24px 12px;">
        <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="max-width:600px;background:#ffffff;border-radius:12px;overflow:hidden;">
        <tr><td style="background:{Night};padding:22px 32px;font-family:{Serif};font-size:20px;letter-spacing:4px;color:{Gold};text-transform:uppercase;">Szept Tarota</td></tr>
        <tr><td style="padding:32px;font-family:{Sans};font-size:15px;line-height:1.6;color:{Text};">{bodyHtml}</td></tr>
        </table>
        {footerHtml}
        </td></tr></table>
        </body></html>
        """;
    }

    public static string Heading(string text) =>
        $"""<h1 style="margin:0 0 16px;font-family:{Serif};font-size:30px;font-weight:normal;line-height:1.2;color:{Night};">{HtmlEncode(text)}</h1>""";

    public static string SectionHeading(string text) =>
        $"""<h2 style="margin:32px 0 12px;font-family:{Serif};font-size:22px;font-weight:normal;color:{Night};">{HtmlEncode(text)}</h2>""";

    public static string Paragraph(string html) => $"""<p style="margin:0 0 16px;">{html}</p>""";

    /// <summary>Wyróżniony blok, np. jeden Termin: złoty pasek z lewej i jasne tło, jak karta zaproszenia.</summary>
    public static string Panel(string label, string title, string bodyHtml) =>
        $"""
        <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="margin:0 0 16px;background:{Ivory};border-left:3px solid {Gold};border-radius:8px;">
        <tr><td style="padding:20px 24px;">
        <div style="font-size:11px;letter-spacing:2px;text-transform:uppercase;color:{GoldDark};">{HtmlEncode(label)}</div>
        <div style="margin:4px 0 12px;font-family:{Serif};font-size:22px;color:{Night};">{HtmlEncode(title)}</div>
        {bodyHtml}
        </td></tr></table>
        """;

    /// <summary>Podpisana wartość w bloku, np. „Kiedy” i data.</summary>
    public static string Field(string label, string valueHtml) =>
        $"""<div style="margin:0 0 12px;"><div style="font-size:12px;font-weight:600;color:{Muted};">{HtmlEncode(label)}</div><div>{valueHtml}</div></div>""";

    /// <summary>Przycisk z linkiem; zbudowany z komórki tabeli, żeby wyglądał tak samo także w Outlooku.</summary>
    public static string Button(string url, string text) =>
        $"""
        <table role="presentation" cellpadding="0" cellspacing="0" style="margin:4px 0 16px;"><tr>
        <td style="background:{NightLight};border-radius:6px;">
        <a href="{HtmlEncode(url)}" style="display:inline-block;padding:12px 22px;font-family:{Sans};font-size:15px;font-weight:600;color:#ffffff;text-decoration:none;">{HtmlEncode(text)}</a>
        </td></tr></table>
        """;

    /// <summary>Link z tekstem <paramref name="text"/>, domyślnie samym adresem.</summary>
    public static string Link(string url, string? text = null) =>
        $"""<a href="{HtmlEncode(url)}" style="color:{NightLight};">{HtmlEncode(text ?? url)}</a>""";

    /// <summary>Drobny tekst pomocniczy.</summary>
    public static string Note(string html) => $"""<p style="margin:0 0 8px;font-size:13px;color:{Muted};">{html}</p>""";

    /// <summary>Uwaga, przy której ktoś musi coś zrobić.</summary>
    public static string Warning(string html) =>
        $"""<p style="margin:0 0 8px;padding:10px 12px;background:#fbefd5;border-radius:6px;font-size:14px;color:{Text};">{html}</p>""";

    /// <summary>Tabela pozycji z sumą; <paramref name="rowsHtml"/> to wiersze z <see cref="Row"/>.</summary>
    public static string Table(string rowsHtml, string totalLabel, string total) =>
        $"""
        <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="border-collapse:collapse;">
        {rowsHtml}
        <tr><td style="padding:14px 0 0;font-family:{Serif};font-size:19px;color:{Night};">{HtmlEncode(totalLabel)}</td>
        <td align="right" style="padding:14px 0 0;font-family:{Serif};font-size:19px;color:{GoldDark};white-space:nowrap;">{HtmlEncode(total)}</td></tr>
        </table>
        """;

    /// <summary>Wiersz tabeli: nazwa, cena i pod nazwą opcjonalne szczegóły.</summary>
    public static string Row(string nameHtml, string price, string? detailsHtml = null)
    {
        var details = detailsHtml is null ? "" : $"""<div style="margin-top:6px;font-size:13px;color:{Muted};">{detailsHtml}</div>""";
        return $"""
            <tr><td style="padding:12px 16px 12px 0;border-bottom:1px solid {Line};vertical-align:top;">{nameHtml}{details}</td>
            <td align="right" style="padding:12px 0;border-bottom:1px solid {Line};vertical-align:top;white-space:nowrap;">{HtmlEncode(price)}</td></tr>
            """;
    }
}
