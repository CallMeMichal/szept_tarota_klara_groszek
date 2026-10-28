// Panel Klary (PanelEndpoints.cs): dostęp ma tylko Konto Klary.

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
