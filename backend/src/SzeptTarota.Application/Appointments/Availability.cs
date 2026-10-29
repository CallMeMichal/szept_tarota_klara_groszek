namespace SzeptTarota.Application.Appointments;

/// <summary>Termin: początek i koniec spotkania Klary z Klientem.</summary>
public sealed record Appointment(DateTimeOffset Start, DateTimeOffset End);

/// <summary>
/// Dostępność Klary w wczytanym okresie: bloki „Dostępna”, zajęte okresy z głównego kalendarza i Terminy zajęte przez
/// Klientów (opłacone i zablokowane na czas płatności).
/// Strona niczego nie dokłada od siebie: blok dzieli się od jego początku na kolejne Terminy o długości Usługi, a Termin
/// jest wolny, gdy zaczyna się w przyszłości i nie nachodzi na żaden zajęty okres (stykać się może, np. 12–13 i 13–14).
/// </summary>
public sealed class Availability
{
    private readonly IReadOnlyList<Appointment> _blocks;
    private readonly List<Appointment> _busy;
    private readonly IReadOnlyList<Appointment> _taken;
    private readonly DateTimeOffset _now;

    public Availability(IEnumerable<Appointment> blocks, IEnumerable<Appointment> busy, IEnumerable<Appointment> taken, DateTimeOffset now)
    {
        _blocks = Merge(blocks);
        _busy = busy.ToList();
        _taken = taken.ToList();
        _now = now;
    }

    /// <summary>Wolne Terminy o długości <paramref name="duration"/> zaczynające się od <paramref name="from"/> do przed <paramref name="to"/>.</summary>
    public IReadOnlyList<Appointment> Free(TimeSpan duration, DateTimeOffset from, DateTimeOffset to)
    {
        var free = new List<Appointment>();
        foreach (var block in _blocks)
        {
            for (var start = block.Start; start + duration <= block.End && start < to; start += duration)
            {
                var appointment = new Appointment(start, start + duration);
                if (start >= from && IsFree(appointment))
                {
                    free.Add(appointment);
                }
            }
        }

        return free;
    }

    /// <summary>Czy Termin jest jednym z wolnych Terminów, które pokazuje <see cref="Free"/>.</summary>
    public bool IsFree(Appointment appointment)
    {
        var duration = appointment.End - appointment.Start;
        return duration > TimeSpan.Zero
            && appointment.Start > _now
            && _blocks.Any(b => b.Start <= appointment.Start && appointment.End <= b.End && (appointment.Start - b.Start).Ticks % duration.Ticks == 0)
            && !_busy.Any(b => Overlap(appointment, b))
            && !IsTaken(appointment);
    }

    /// <summary>Czy Termin nachodzi na Termin, który opłacił albo właśnie opłaca inny Klient.</summary>
    public bool IsTaken(Appointment appointment) => _taken.Any(t => Overlap(appointment, t));

    private static bool Overlap(Appointment a, Appointment b) => a.Start < b.End && b.Start < a.End;

    /// <summary>Zajmuje Termin, np. wybrany już przy innej pozycji tego samego Koszyka.</summary>
    public void Occupy(Appointment appointment) => _busy.Add(appointment);

    /// <summary>Bloki, które na siebie nachodzą albo się stykają, to jeden dłuższy blok.</summary>
    private static List<Appointment> Merge(IEnumerable<Appointment> blocks)
    {
        var merged = new List<Appointment>();
        foreach (var block in blocks.Where(b => b.End > b.Start).OrderBy(b => b.Start))
        {
            if (merged.Count > 0 && block.Start <= merged[^1].End)
            {
                merged[^1] = merged[^1] with { End = block.End > merged[^1].End ? block.End : merged[^1].End };
            }
            else
            {
                merged.Add(block);
            }
        }

        return merged;
    }
}
