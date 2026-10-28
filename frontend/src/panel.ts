// Panel Klary (PanelEndpoints.cs): dostęp ma tylko Konto Klary.

/** Przycisk „Połącz z Google Calendar” to zwykłe przejście pod ten adres: API odsyła do zgody w Google, a po powrocie na `/panel?kalendarz=…`. */
export const connectCalendarUrl = '/api/panel/calendar/connect'

/** Wynik powrotu ze zgody w Google, z `?kalendarz=` w adresie panelu. */
export type ConnectResult = 'polaczono' | 'nieudane'

export type CalendarConnectionStatus = 'notConnected' | 'connected' | 'broken'

export type GoogleCalendar = { id: string; name: string; primary: boolean }

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
