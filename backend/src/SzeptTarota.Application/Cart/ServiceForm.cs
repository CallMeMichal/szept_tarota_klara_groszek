using System.Globalization;
using System.Net.Mail;
using SzeptTarota.Domain.Catalog;
using SzeptTarota.Domain.Orders;

namespace SzeptTarota.Application.Cart;

/// <summary>
/// Sprawdza odpowiedzi z formularza Usługi względem jego definicji w katalogu. Odpowiedzi trafiają do maila Klary,
/// więc mają ograniczoną długość i nie mają znaków sterujących (poza nowymi liniami w tekście długim).
/// </summary>
public static class ServiceForm
{
    private const int MaxTextLength = 200;
    private const int MaxLongTextLength = 2000;

    /// <summary>
    /// Odpowiedzi w kolejności pól formularza, bez pustych pól nieobowiązkowych i bez kluczy spoza formularza,
    /// albo błędy po kluczach pól, gdy któraś odpowiedź jest niepoprawna.
    /// </summary>
    public static ServiceFormResult Check(IReadOnlyList<FormField> fields, IReadOnlyDictionary<string, string?>? form)
    {
        var answers = new List<FormAnswer>();
        var errors = new Dictionary<string, string>();

        foreach (var field in fields)
        {
            var value = form?.GetValueOrDefault(field.Key)?.Trim() ?? "";
            if (value.Length == 0)
            {
                if (field.Required)
                {
                    errors[field.Key] = $"Uzupełnij pole „{field.Label}”.";
                }

                continue;
            }

            if (ErrorIn(field, value) is { } error)
            {
                errors[field.Key] = error;
                continue;
            }

            answers.Add(new FormAnswer(field.Key, field.Label, value));
        }

        return new ServiceFormResult(answers, errors);
    }

    private static string? ErrorIn(FormField field, string value)
    {
        var maxLength = field.Type == FormFieldType.LongText ? MaxLongTextLength : MaxTextLength;
        if (value.Length > maxLength)
        {
            return $"Pole „{field.Label}” może mieć najwyżej {maxLength} znaków.";
        }

        var allowsNewLines = field.Type == FormFieldType.LongText;
        if (value.Any(c => char.IsControl(c) && !(allowsNewLines && c is '\n' or '\r' or '\t')))
        {
            return $"Pole „{field.Label}” zawiera niedozwolone znaki.";
        }

        return field.Type switch
        {
            FormFieldType.Date when !DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _) =>
                $"Podaj poprawną datę w polu „{field.Label}”.",
            FormFieldType.Email when !MailAddress.TryCreate(value, out var address) || address.Address != value =>
                $"Podaj poprawny adres email w polu „{field.Label}”.",
            _ => null,
        };
    }
}

/// <summary>Wynik sprawdzenia formularza Usługi: poprawne odpowiedzi i błędy po kluczach pól.</summary>
public sealed record ServiceFormResult(IReadOnlyList<FormAnswer> Answers, IReadOnlyDictionary<string, string> Errors);
