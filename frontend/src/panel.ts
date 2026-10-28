// Panel Klary (PanelEndpoints.cs): dostęp ma tylko Konto Klary.

import type { Appointment } from './appointments.ts'
import type { ProductKind } from './catalog.ts'

/** Sekcje panelu (menu po lewej). */
export const panelOrdersPath = '/panel/zamowienia'
export const panelAvailabilityPath = '/panel/dostepnosc'

/** Przycisk „Połącz z Google Calendar” to zwykłe przejście pod ten adres: API odsyła do zgody w Google, a po powrocie na `/panel?kalendarz=…`. */
export const connectCalendarUrl = '/api/panel/calendar/connect'

/** Wynik powrotu ze zgody w Google, z `?kalendarz=` w adresie panelu. */
export type ConnectResult = 'polaczono' | 'nieudane'

export type CalendarConnectionStatus = 'notConnected' | 'connected' | 'broken'

/** `writable`: Klara może w nim dodawać wydarzenia, więc nadaje się na Kalendarz Sesji. */
export type GoogleCalendar = { id: string; name: string; primary: boolean; writable: boolean }

// GET /api/panel/calendar
export type CalendarConnection = {
  status: CalendarConnectionStatus
  /** Kalendarze z konta Google Klary; puste, gdy połączenie nie działa. */
  calendars: GoogleCalendar[]
  /** Kalendarz z blokami „Dostępna” (Kalendarz Sesji). */
  sessionCalendarId: string | null
}

export type ChooseCalendarResult = { status: 'saved' } | { status: 'invalid'; error: string } | { status: 'error' }

/** Ustawia Kalendarz Sesji; główny kalendarz jest zawsze odczytywany jako zajętość i nie może nim być. */
export async function chooseSessionCalendar(calendarId: string): Promise<ChooseCalendarResult> {
  try {
    const response = await fetch('/api/panel/calendar/session-calendar', {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json', Accept: 'application/json' },
      body: JSON.stringify({ calendarId }),
    })
    if (response.status === 204) return { status: 'saved' }
    if (response.status === 400 && response.headers.get('Content-Type')?.includes('json')) {
      const { errors } = (await response.json()) as { errors?: Record<string, string[]> }
      return { status: 'invalid', error: errors?.calendarId?.[0] ?? 'Nie udało się wybrać kalendarza.' }
    }
    return { status: 'error' }
  } catch {
    return { status: 'error' }
  }
}

// Dostępność zaznaczana w panelu: bloki „Dostępna” w Kalendarzu Sesji (GET/POST/DELETE /api/panel/availability).

/** Okres z przesunięciem czasu polskiego (ISO 8601). */
export type Period = { start: string; end: string }

/** Blok „Dostępna”; `id` to identyfikator wydarzenia w Google. */
export type AvailabilityBlock = Period & { id: string }

export type AvailabilityWeek = { blocks: AvailabilityBlock[]; busy: Period[] }

export type AvailabilityFailure =
  /** Strona nie jest połączona z Google albo nie ma wybranego Kalendarza Sesji. */
  | { status: 'no-calendar' }
  /** Google nie odpowiada albo strona straciła dostęp. */
  | { status: 'unavailable' }
  | { status: 'error' }

const failureFor = (response: Response): AvailabilityFailure =>
  response.status === 409
    ? { status: 'no-calendar' }
    : response.status === 503
      ? { status: 'unavailable' }
      : { status: 'error' }

/** Bloki i zajęte okresy w dniach od `from` do `to` (RRRR-MM-DD, czas polski, włącznie). */
export async function fetchAvailability(
  from: string,
  to: string,
  signal: AbortSignal,
): Promise<{ status: 'ok'; week: AvailabilityWeek } | AvailabilityFailure> {
  try {
    const response = await fetch(`/api/panel/availability?${new URLSearchParams({ from, to })}`, {
      headers: { Accept: 'application/json' },
      signal,
    })
    if (response.ok) return { status: 'ok', week: (await response.json()) as AvailabilityWeek }
    return failureFor(response)
  } catch (error) {
    if (signal.aborted) throw error
    return { status: 'error' }
  }
}

/** Dodaje blok; `start` i `end` to czas polski bez strefy, np. `2026-11-03T10:00` (północ: `00:00` następnego dnia). */
export async function addAvailabilityBlock(
  start: string,
  end: string,
): Promise<{ status: 'added'; block: AvailabilityBlock } | { status: 'invalid'; error: string } | AvailabilityFailure> {
  try {
    const response = await fetch('/api/panel/availability', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json', Accept: 'application/json' },
      body: JSON.stringify({ start, end }),
    })
    if (response.status === 201) return { status: 'added', block: (await response.json()) as AvailabilityBlock }
    if (response.status === 400 && response.headers.get('Content-Type')?.includes('json')) {
      const { errors } = (await response.json()) as { errors?: Record<string, string[]> }
      return { status: 'invalid', error: Object.values(errors ?? {})[0]?.[0] ?? 'Nie udało się dodać dostępności.' }
    }
    return failureFor(response)
  } catch {
    return { status: 'error' }
  }
}

export async function removeAvailabilityBlock(id: string): Promise<{ status: 'removed' } | AvailabilityFailure> {
  try {
    const response = await fetch(`/api/panel/availability/${encodeURIComponent(id)}`, { method: 'DELETE' })
    return response.status === 204 ? { status: 'removed' } : failureFor(response)
  } catch {
    return { status: 'error' }
  }
}

// Zamówienia w panelu: opłacone Zamówienia wszystkich Klientów (PanelOrderEndpoints.cs).

/** Filtr listy Zamówień: dni zapłaty (RRRR-MM-DD, czas polski, włącznie) i slug Produktu; pusty to bez ograniczenia. */
export type OrderFilter = { from: string; to: string; product: string }

/** Adres listy Zamówień z filtrem (GET /api/panel/orders). */
export function panelOrdersUrl({ from, to, product }: OrderFilter) {
  const query = new URLSearchParams(
    Object.entries({ from, to, product }).filter(([, value]) => value !== ''),
  ).toString()
  return query ? `/api/panel/orders?${query}` : '/api/panel/orders'
}


/** Opłacone Zamówienie na liście; `email` to adres podany przy płatności, `price` to cena jednej sztuki. */
export type PanelOrder = {
  id: string
  paidAt: string
  customerName: string
  email: string
  items: { slug: string; name: string; kind: ProductKind; quantity: number; price: number }[]
  total: number
}

/** Odpowiedź z formularza Usługi z etykietą pola z chwili zakupu. */
export type LabelledFormAnswer = { key: string; label: string; value: string }

// GET /api/panel/orders/<id>
export type PanelOrderDetails = Omit<PanelOrder, 'items'> & {
  items: (PanelOrder['items'][number] & { formAnswers: LabelledFormAnswer[]; appointment: Appointment | null })[]
}
