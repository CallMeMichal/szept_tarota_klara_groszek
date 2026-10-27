namespace SzeptTarota.Domain.Catalog;

/// <summary>Wszystko, co można kupić na stronie: Ebook, Usługa albo Sesja (Sesja to Usługa z Terminem).</summary>
public sealed record Product(
    string Slug,
    ProductKind Kind,
    ProductCategory Category,
    string Name,
    string Tagline,
    IReadOnlyList<DescriptionBlock> Description,
    int Price,
    int? DurationMinutes,
    IReadOnlyList<FormField> Form,
    string? EbookFileKey);

public enum ProductKind
{
    Ebook,
    Service,
    Session,
}

/// <summary>Kategoria, w której Produkt jest pokazywany w sekcji Oferta.</summary>
public enum ProductCategory
{
    Ebooks,
    Analyses,
    Rituals,
    Sessions,
    Courses,
}

/// <summary>Pole formularza, który Klient wypełnia, dodając Usługę do Koszyka.</summary>
public sealed record FormField(string Key, string Label, FormFieldType Type, bool Required);

public enum FormFieldType
{
    Text,
    LongText,
    Date,
    Email,
}

/// <summary>Akapit, nagłówek albo lista w opisie Produktu.</summary>
public sealed record DescriptionBlock(DescriptionBlockType Type, string? Text, IReadOnlyList<string>? Items);

public enum DescriptionBlockType
{
    Heading,
    Paragraph,
    List,
}
