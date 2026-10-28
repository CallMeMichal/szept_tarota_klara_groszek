// Terminy Usług i Sesji: wolne godziny Klary z API (GET /api/products/<slug>/appointments,
// ProductEndpoints.cs). Dostępność liczy wyłącznie serwer z kalendarza Klary; tu tylko
// pobieramy ją po miesiącu i pokazujemy daty i godziny zawsze w czasie polskim.

/** Termin: początek i koniec w ISO 8601 z przesunięciem czasu polskiego. */
export type Appointment = { start: string; end: string }

export type FreeAppointments =
  | { status: 'ok'; durationMinutes: number; appointments: Appointment[] }
  /** Kalendarz Klary chwilowo nie działa (albo nie ma backendu, np. na GitHub Pages). */
  | { status: 'unavailable' }

const timeZone = 'Europe/Warsaw'

/** Wolne Terminy Produktu zaczynające się w dniach od `from` do `to` (RRRR-MM-DD, włącznie). */
export async function fetchFreeAppointments(
  slug: string,
  from: string,
  to: string,
  signal: AbortSignal,
): Promise<FreeAppointments> {
  const query = new URLSearchParams({ from, to })
  try {
    const response = await fetch(`/api/products/${encodeURIComponent(slug)}/appointments?${query}`, {
      headers: { Accept: 'application/json' },
      signal,
    })
    if (!response.ok || !response.headers.get('Content-Type')?.includes('json')) return { status: 'unavailable' }
    const data = (await response.json()) as { durationMinutes: number; appointments: Appointment[] }
    return { status: 'ok', ...data }
  } catch (error) {
    if (signal.aborted) throw error
    return { status: 'unavailable' }
  }
}

const dayKeyFormat = new Intl.DateTimeFormat('en-CA', { timeZone, year: 'numeric', month: '2-digit', day: '2-digit' })

/** Dzień (RRRR-MM-DD) w czasie polskim, w którym zaczyna się Termin. */
export function dayKey(iso: string) {
  return dayKeyFormat.format(new Date(iso))
}

/** Dzisiejszy dzień w czasie polskim (RRRR-MM-DD). */
export function todayKey() {
  return dayKeyFormat.format(new Date())
}

const timeFormat = new Intl.DateTimeFormat('pl-PL', { timeZone, hour: '2-digit', minute: '2-digit' })

/** Godzina w czasie polskim, np. „10:00”. */
export function formatTime(iso: string) {
  return timeFormat.format(new Date(iso))
}

const dateFormat = new Intl.DateTimeFormat('pl-PL', { timeZone, weekday: 'long', day: 'numeric', month: 'long', year: 'numeric' })

/** Termin po polsku, np. „poniedziałek, 16 lutego 2026, 10:00–11:00”. */
export function formatAppointment(appointment: Appointment) {
  return `${dateFormat.format(new Date(appointment.start))}, ${formatTime(appointment.start)}–${formatTime(appointment.end)}`
}
