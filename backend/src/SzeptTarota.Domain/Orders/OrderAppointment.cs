namespace SzeptTarota.Domain.Orders;

/// <summary>
/// Termin pozycji Zamówienia. Rozpoczęcie płatności zakłada go jako blokadę do <see cref="HeldUntil"/>; po zapłacie
/// blokada staje się Terminem bez wygaśnięcia. Zwolniony (<see cref="ReleasedAt"/>) już nikogo nie blokuje. Baza nie
/// pozwala, żeby dwa niezwolnione Terminy na siebie nachodziły.
/// </summary>
public sealed class OrderAppointment
{
    public OrderAppointment(
        Guid id, Guid orderId, Guid? accountId, int lineIndex, DateTimeOffset start, DateTimeOffset end, DateTimeOffset? heldUntil)
    {
        Id = id;
        OrderId = orderId;
        AccountId = accountId;
        LineIndex = lineIndex;
        Start = start;
        End = end;
        HeldUntil = heldUntil;
    }

    // Dla EF Core.
    private OrderAppointment()
    {
    }

    /// <summary>Identyfikator, także wydarzenia w kalendarzu Klary, żeby ponowione powiadomienie nie dodało go drugi raz.</summary>
    public Guid Id { get; private set; }

    public Guid OrderId { get; private set; }

    /// <summary>Konto Klienta; jego własne blokady nie zajmują mu Terminów, gdy wraca z przerwanej płatności.</summary>
    public Guid? AccountId { get; private set; }

    /// <summary>Numer pozycji w <see cref="Order.Lines"/>, od zera.</summary>
    public int LineIndex { get; private set; }

    public DateTimeOffset Start { get; private set; }

    public DateTimeOffset End { get; private set; }

    /// <summary>Do kiedy trwa blokada; brak, gdy Termin jest opłacony.</summary>
    public DateTimeOffset? HeldUntil { get; private set; }

    /// <summary>Kiedy Termin wrócił do puli (wygasła blokada albo sesja płatności, zajął go ktoś inny).</summary>
    public DateTimeOffset? ReleasedAt { get; private set; }

    /// <summary>Wydarzenie Terminu w głównym kalendarzu Klary, po zapłacie.</summary>
    public string? CalendarEventId { get; private set; }

    /// <summary>Link do spotkania Google Meet z wydarzenia w kalendarzu.</summary>
    public string? MeetLink { get; private set; }

    public bool IsPaid => HeldUntil is null && ReleasedAt is null;

    public void AddedToCalendar(string calendarEventId, string? meetLink)
    {
        CalendarEventId = calendarEventId;
        MeetLink = meetLink;
    }
}
