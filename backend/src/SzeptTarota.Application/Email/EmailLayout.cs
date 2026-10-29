using static System.Net.WebUtility;

namespace SzeptTarota.Application.Email;

/// <summary>
/// Wygląd maili jak strona: granat nocnego nieba, złoto i szeryfowe nagłówki, z nagłówkiem graficznym i księżycem
/// w stopce (<see cref="Images"/> idą razem z mailem). Programy pocztowe nie znają stylów strony, więc układ to tabele
/// ze stylami w atrybutach. Parametry <c>html</c> muszą być już zakodowane; zwykły tekst koduje sam układ.
/// </summary>
public static class EmailLayout
{
    // Kolory z frontend/src.
    private const string Night950 = "#070b1f";
    private const string Night900 = "#0b1230";
    private const string Night800 = "#131c44";
    private const string Border = "#27306a";
    private const string Gold = "#d4af61";
    private const string GoldLight = "#e2c17a";
    private const string Cream = "#f3eee4";
    private const string Text = "#d8d3e6";
    private const string Muted = "#a7a3bd";

    // Cormorant Garamond i Inter jak na stronie, gdzie program pocztowy pobiera czcionki; inaczej najbliższe systemowe.
    private const string Serif = "'Cormorant Garamond', Georgia, 'Times New Roman', serif";
    private const string Sans = "Inter, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif";

    private const string InstagramUrl = "https://www.instagram.com/szept_tarota/";

    /// <summary>Grafiki, których używa sam układ (nagłówek i stopka).</summary>
    public static IReadOnlyList<EmailImage> Images => [EmailImages.Header, EmailImages.Moon];

    /// <summary>
    /// Cały mail; <paramref name="preview"/> to tekst, który program pocztowy pokazuje obok tematu, a
    /// <paramref name="footnote"/> to drobny dopisek w stopce (np. numer Zamówienia).
    /// </summary>
    public static string Page(string preview, string bodyHtml, string? footnote = null)
    {
        var footnoteHtml = footnote is null
            ? ""
            : $"""<div style="margin-top:14px;font-size:11px;color:{Muted};">{HtmlEncode(footnote)}</div>""";
        return $"""
            <!doctype html>
            <html lang="pl"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width">
            <meta name="color-scheme" content="dark"><meta name="supported-color-schemes" content="dark">
            <link href="https://fonts.googleapis.com/css2?family=Cormorant+Garamond:ital,wght@0,400;0,500;1,400&family=Inter:wght@400;600&display=swap" rel="stylesheet">
            </head>
            <body style="margin:0;padding:0;background:{Night950};">
            <div style="display:none;max-height:0;overflow:hidden;">{HtmlEncode(preview)}</div>
            <table role="presentation" width="100%" cellpadding="0" cellspacing="0" bgcolor="{Night950}" style="background:{Night950};">
            <tr><td align="center" style="padding:24px 12px 32px;">
            <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="max-width:600px;">
            <tr><td><img src="cid:{EmailImages.Header.ContentId}" width="600" alt="Szept Tarota. Karty nie mówią głośno." style="display:block;width:100%;max-width:600px;height:auto;border:0;border-radius:14px 14px 0 0;"></td></tr>
            <tr><td bgcolor="{Night900}" style="background:{Night900};padding:36px 32px 32px;border-radius:0 0 14px 14px;font-family:{Sans};font-size:15px;line-height:1.65;color:{Text};">{bodyHtml}</td></tr>
            <tr><td align="center" style="padding:28px 16px 0;font-family:{Sans};font-size:13px;color:{Muted};">
            <img src="cid:{EmailImages.Moon.ContentId}" width="40" height="40" alt="" style="display:block;margin:0 auto 10px;border:0;border-radius:50%;">
            <div style="font-family:{Serif};font-size:20px;color:{Cream};">Szept Tarota</div>
            <div style="font-family:{Serif};font-style:italic;font-size:16px;color:{GoldLight};margin:2px 0 12px;">Karty nie mówią głośno.</div>
            <a href="{InstagramUrl}" style="color:{Muted};text-decoration:underline;">Instagram @szept_tarota</a>
            {footnoteHtml}
            </td></tr>
            </table>
            </td></tr></table>
            </body></html>
            """;
    }

    /// <summary>Mały złoty napis nad nagłówkiem, np. „Zamówienie opłacone”.</summary>
    public static string Eyebrow(string text) =>
        $"""<div style="font-size:11px;font-weight:600;letter-spacing:3px;text-transform:uppercase;color:{Gold};margin:0 0 10px;">{HtmlEncode(text)}</div>""";

    public static string Heading(string text) =>
        $"""<h1 style="margin:0 0 14px;font-family:{Serif};font-size:36px;font-weight:400;line-height:1.15;color:{Cream};">{HtmlEncode(text)}</h1>""";

    /// <summary>Nagłówek sekcji ze złotą kreską, jak „Zatrzymaj się. Posłuchaj siebie.” na stronie.</summary>
    public static string SectionHeading(string text) =>
        $"""
        <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="margin:36px 0 8px;"><tr>
        <td width="32" valign="middle"><div style="width:32px;height:1px;background:{Gold};font-size:1px;line-height:1px;">&nbsp;</div></td>
        <td valign="middle" style="padding-left:12px;font-family:{Serif};font-size:24px;color:{Cream};white-space:nowrap;">{HtmlEncode(text)}</td>
        <td width="100%"></td>
        </tr></table>
        """;

    public static string Paragraph(string html) => $"""<p style="margin:0 0 18px;">{html}</p>""";

    /// <summary>Wyróżniony blok, np. jeden Termin, z okładką Produktu z lewej (gdy jest), jak karta zaproszenia.</summary>
    public static string Panel(string label, string title, string bodyHtml, EmailImage? image = null)
    {
        var cover = image is null
            ? ""
            : $"""<td width="96" valign="top" style="padding:0 20px 0 0;"><img src="cid:{image.ContentId}" width="96" alt="" style="display:block;width:96px;height:auto;border:0;border-radius:8px;"></td>""";
        return $"""
            <table role="presentation" width="100%" cellpadding="0" cellspacing="0" bgcolor="{Night800}" style="margin:8px 0 16px;background:{Night800};border:1px solid {Border};border-radius:12px;">
            <tr><td style="padding:24px;">
            <table role="presentation" width="100%" cellpadding="0" cellspacing="0"><tr>{cover}<td valign="top">
            <div style="font-size:11px;font-weight:600;letter-spacing:3px;text-transform:uppercase;color:{Gold};">{HtmlEncode(label)}</div>
            <div style="margin:4px 0 14px;font-family:{Serif};font-size:26px;line-height:1.2;color:{Cream};">{HtmlEncode(title)}</div>
            {bodyHtml}
            </td></tr></table>
            </td></tr></table>
            """;
    }

    /// <summary>Podpisana wartość w bloku, np. „Kiedy” i data.</summary>
    public static string Field(string label, string valueHtml) =>
        $"""<div style="margin:0 0 14px;"><div style="font-size:11px;letter-spacing:1px;text-transform:uppercase;color:{Muted};">{HtmlEncode(label)}</div><div style="color:{Cream};">{valueHtml}</div></div>""";

    /// <summary>Złoty przycisk jak „Zobacz ofertę” na stronie; z komórki tabeli, żeby wyglądał tak samo w Outlooku.</summary>
    public static string Button(string url, string text) =>
        $"""
        <table role="presentation" cellpadding="0" cellspacing="0" style="margin:6px 0 18px;"><tr>
        <td bgcolor="{GoldLight}" style="background:{GoldLight};border-radius:4px;">
        <a href="{HtmlEncode(url)}" style="display:inline-block;padding:14px 26px;font-family:{Sans};font-size:15px;font-weight:600;color:{Night900};text-decoration:none;">{HtmlEncode(text)}</a>
        </td></tr></table>
        """;

    /// <summary>Link z tekstem <paramref name="text"/>, domyślnie samym adresem.</summary>
    public static string Link(string url, string? text = null) =>
        $"""<a href="{HtmlEncode(url)}" style="color:{GoldLight};text-decoration:underline;">{HtmlEncode(text ?? url)}</a>""";

    /// <summary>Drobny tekst pomocniczy.</summary>
    public static string Note(string html) => $"""<p style="margin:0;font-size:13px;color:{Muted};">{html}</p>""";

    /// <summary>Uwaga, przy której ktoś musi coś zrobić.</summary>
    public static string Warning(string html) =>
        $"""<p style="margin:0;padding:12px 14px;background:#2c2614;border:1px solid #6b5527;border-radius:8px;font-size:14px;color:{Cream};">{html}</p>""";

    /// <summary>Tabela pozycji z sumą; <paramref name="rowsHtml"/> to wiersze z <see cref="Row"/>.</summary>
    public static string Table(string rowsHtml, string totalLabel, string total) =>
        $"""
        <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="border-collapse:collapse;">
        {rowsHtml}
        <tr><td colspan="2" style="padding:18px 0 0;font-family:{Serif};font-size:22px;color:{Cream};">{HtmlEncode(totalLabel)}</td>
        <td align="right" style="padding:18px 0 0;font-family:{Serif};font-size:24px;color:{GoldLight};white-space:nowrap;">{HtmlEncode(total)}</td></tr>
        </table>
        """;

    /// <summary>Wiersz tabeli: miniatura okładki (gdy jest), nazwa z opcjonalnymi szczegółami pod nią i cena.</summary>
    public static string Row(string nameHtml, string price, string? detailsHtml = null, EmailImage? image = null)
    {
        var details = detailsHtml is null ? "" : $"""<div style="margin-top:6px;font-size:13px;line-height:1.6;color:{Muted};">{detailsHtml}</div>""";
        var cover = image is null ? "" : $"""<img src="cid:{image.ContentId}" width="48" alt="" style="display:block;width:48px;height:auto;border:0;border-radius:6px;">""";
        return $"""
            <tr><td width="64" valign="top" style="padding:14px 16px 14px 0;border-bottom:1px solid {Border};">{cover}</td>
            <td valign="top" style="padding:14px 16px 14px 0;border-bottom:1px solid {Border};color:{Cream};">{nameHtml}{details}</td>
            <td align="right" valign="top" style="padding:14px 0;border-bottom:1px solid {Border};white-space:nowrap;color:{Cream};">{HtmlEncode(price)}</td></tr>
            """;
    }
}
