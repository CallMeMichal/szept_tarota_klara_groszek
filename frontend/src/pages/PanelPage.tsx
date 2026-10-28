import { useEffect, useState } from 'react'
import { Navigate, useSearchParams } from 'react-router'
import { loginPathFor, panelPath, useSession } from '../account.ts'
import { AvailabilityEditor } from '../components/AvailabilityEditor.tsx'
import {
  chooseSessionCalendar,
  connectCalendarUrl,
  type CalendarConnection,
  type ConnectResult,
} from '../panel.ts'
import { useApi } from '../useApi.ts'
import { usePageTitle } from '../usePageTitle.ts'

/** Panel Klary; inni zalogowani widzą tylko informację, że to nie dla nich. */
export function PanelPage() {
  usePageTitle('Panel')
  const session = useSession()

  useEffect(() => {
    window.scrollTo({ top: 0 })
  }, [])

  if (session.status === 'logged-out') return <Navigate to={loginPathFor(panelPath)} replace />

  return (
    <section className="section account-page" aria-labelledby="panel-title" aria-busy={session.status === 'loading'}>
      <p className="eyebrow">Panel</p>
      <h1 id="panel-title">Panel Klary</h1>
      {session.status === 'logged-in' && session.isKlara && <CalendarSection />}
      {session.status === 'logged-in' && !session.isKlara && (
        <p className="section__lead">Ta strona jest dostępna tylko dla Klary.</p>
      )}
      {session.status === 'unavailable' && (
        <p className="section__lead">Nie udało się otworzyć panelu. Spróbuj ponownie za chwilę.</p>
      )}
    </section>
  )
}

const connectMessages: Record<ConnectResult, string> = {
  polaczono: 'Połączono z Google Calendar.',
  nieudane: 'Nie udało się połączyć z Google Calendar. Spróbuj ponownie.',
}

function CalendarSection() {
  const [searchParams] = useSearchParams()
  const result = searchParams.get('kalendarz') as ConnectResult | null
  const connection = useApi<CalendarConnection>('/api/panel/calendar')

  return (
    <div className="panel-calendar">
      <h2>Google Calendar</h2>
      {result && result in connectMessages && (
        <p className={result === 'polaczono' ? 'checkout-form__note' : 'checkout-form__error'} role="status">
          {connectMessages[result]}
        </p>
      )}
      {connection.status === 'loading' && <p>Sprawdzam połączenie…</p>}
      {(connection.status === 'error' || connection.status === 'not-found') && (
        <p>Nie udało się sprawdzić połączenia. Spróbuj ponownie za chwilę.</p>
      )}
      {connection.status === 'ok' && <CalendarConnectionDetails connection={connection.data} />}
    </div>
  )
}

const statusLabels: Record<CalendarConnection['status'], string> = {
  notConnected: 'Nie połączono',
  connected: 'Połączono',
  broken: 'Połączenie nie działa',
}

function CalendarConnectionDetails({ connection }: { connection: CalendarConnection }) {
  const mainCalendar = connection.calendars.find((calendar) => calendar.primary)

  return (
    <>
      <p className={`panel-calendar__status panel-calendar__status--${connection.status}`}>
        {statusLabels[connection.status]}
      </p>
      {connection.status === 'connected' ? (
        <>
          {mainCalendar && (
            <p className="checkout-form__note">
              Zajęte godziny są odczytywane z głównego kalendarza „{mainCalendar.name}”.
            </p>
          )}
          <SessionCalendarChoice connection={connection} />
        </>
      ) : (
        // W miejscu kalendarza dostępności: bez połączenia z Google nie ma czego pokazać.
        <div className="panel-calendar__connect">
          <p>
            {connection.status === 'notConnected'
              ? 'Połącz stronę ze swoim kalendarzem Google, żeby zaznaczać dostępność i żeby Klienci widzieli Twoje wolne Terminy.'
              : 'Strona straciła dostęp do kalendarza (np. dostęp cofnięto w ustawieniach konta Google). Połącz go ponownie.'}
          </p>
          <a className="button button--primary" href={connectCalendarUrl}>
            {connection.status === 'notConnected' ? 'Połącz z Google Calendar' : 'Połącz ponownie'}
          </a>
        </div>
      )}
    </>
  )
}

function SessionCalendarChoice({ connection }: { connection: CalendarConnection }) {
  // Panel dodaje w nim bloki „Dostępna”, więc tylko kalendarze, w których Klara może coś dodać (bez np. „Święta w Polsce”).
  const options = connection.calendars.filter((calendar) => !calendar.primary && calendar.writable)
  const [saved, setSaved] = useState(connection.sessionCalendarId)
  const [selected, setSelected] = useState(connection.sessionCalendarId ?? options[0]?.id ?? '')
  const [state, setState] = useState<{ status: 'idle' | 'saving' | 'saved' } | { status: 'failed'; error: string }>({
    status: 'idle',
  })

  if (options.length === 0) {
    return (
      <p className="checkout-form__note">
        Załóż w Google Calendar osobny kalendarz na swoją dostępność (po lewej przy „Inne kalendarze”: + → Utwórz nowy
        kalendarz, np. „Sesje”) i odśwież tę stronę, żeby go wybrać.
      </p>
    )
  }

  return (
    <>
      <form
        className="panel-calendar__choice"
        onSubmit={(event) => {
          event.preventDefault()
          setState({ status: 'saving' })
          void chooseSessionCalendar(selected).then((result) => {
            if (result.status === 'saved') {
              setSaved(selected)
              setState({ status: 'saved' })
            } else {
              setState({
                status: 'failed',
                error: result.status === 'invalid' ? result.error : 'Nie udało się zapisać. Spróbuj ponownie.',
              })
            }
          })
        }}
      >
        <label className="checkout-form__field">
          Kalendarz z blokami „Dostępna” (Kalendarz Sesji)
          <select value={selected} onChange={(event) => setSelected(event.target.value)}>
            {options.map((calendar) => (
              <option key={calendar.id} value={calendar.id}>
                {calendar.name}
              </option>
            ))}
          </select>
        </label>
        {!saved && <p className="checkout-form__error">Wybierz Kalendarz Sesji, żeby strona wiedziała, kiedy przyjmujesz Sesje.</p>}
        <button type="submit" className="button button--primary" disabled={state.status === 'saving' || selected === saved}>
          Zapisz
        </button>
        {state.status === 'saved' && (
          <p className="checkout-form__note" role="status">
            Zapisano.
          </p>
        )}
        {state.status === 'failed' && (
          <p className="checkout-form__error" role="alert">
            {state.error}
          </p>
        )}
      </form>
      {saved && (
        <section className="panel-availability" aria-labelledby="panel-availability-title">
          <h2 id="panel-availability-title">Dostępność</h2>
          <AvailabilityEditor key={saved} />
        </section>
      )}
    </>
  )
}
