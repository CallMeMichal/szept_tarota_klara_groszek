import { useEffect, useRef, useState, type ReactNode } from 'react'
import { Link, Navigate, NavLink, Outlet, useLocation, useNavigate, useSearchParams } from 'react-router'
import { loginPathFor, logOut, useSession } from '../account.ts'
import moonStar from '../assets/home/moon-star.svg'
import { AvailabilityEditor } from '../components/AvailabilityEditor.tsx'
import {
  chooseSessionCalendar,
  connectCalendarUrl,
  panelAvailabilityPath,
  panelOrdersPath,
  type CalendarConnection,
  type ConnectResult,
} from '../panel.ts'
import { useApi } from '../useApi.ts'
import { usePageTitle } from '../usePageTitle.ts'

const sections = [
  { path: panelOrdersPath, label: 'Zamówienia' },
  { path: panelAvailabilityPath, label: 'Dostępność' },
]

/**
 * Panel Klary: własny układ z paskiem bocznym przy lewej krawędzi (logo, sekcje, powrót na stronę i wylogowanie),
 * bez nagłówka i stopki strony. Inni zalogowani widzą tylko informację, że to nie dla nich.
 */
export function PanelPage() {
  usePageTitle('Panel')
  const session = useSession()
  const location = useLocation()

  useEffect(() => {
    window.scrollTo({ top: 0 })
  }, [location.pathname])

  if (session.status === 'logged-out') {
    return <Navigate to={loginPathFor(location.pathname + location.search)} replace />
  }

  if (session.status !== 'logged-in' || !session.isKlara) {
    return (
      <main className="section account-page" aria-busy={session.status === 'loading'}>
        <h1>Panel</h1>
        {session.status === 'logged-in' && <p className="section__lead">Ta strona jest dostępna tylko dla Klary.</p>}
        {session.status === 'unavailable' && (
          <p className="section__lead">Nie udało się otworzyć panelu. Spróbuj ponownie za chwilę.</p>
        )}
        {session.status !== 'loading' && (
          <Link className="button button--ghost" to="/">
            Wróć na stronę
          </Link>
        )}
      </main>
    )
  }

  const account = { name: session.name, email: session.email, pictureUrl: session.pictureUrl }
  return (
    <div className="panel-shell">
      <aside className="panel-sidebar">
        <div className="panel-sidebar__top">
          <Link to="/" className="brand panel-sidebar__brand">
            <img className="brand__moon" src={moonStar} width={28} height={28} alt="" />
            <span>Szept Tarota</span>
          </Link>
          {/* Na telefonie Konto to zdjęcie w prawym górnym rogu, które rozwija menu. */}
          <AccountMenu key={location.pathname} account={account} placement="top" />
        </div>
        <nav className="panel-nav" aria-label="Sekcje panelu">
          {sections.map((section) => (
            <NavLink key={section.path} to={section.path} className="panel-nav__link">
              {section.label}
            </NavLink>
          ))}
        </nav>
        <div className="panel-sidebar__footer">
          <AccountMenu key={location.pathname} account={account} placement="bottom" />
        </div>
      </aside>
      <main className="panel-main">
        <Outlet />
      </main>
    </div>
  )
}

type ProfileAccount = { name: string | null; email: string; pictureUrl: string | null }

/** Zdjęcie profilu z Google albo, bez niego, pierwsza litera imienia lub adresu. */
function Avatar({ account }: { account: ProfileAccount }) {
  const [pictureFailed, setPictureFailed] = useState(false)

  return account.pictureUrl && !pictureFailed ? (
    <img
      className="panel-profile__picture"
      src={account.pictureUrl}
      alt=""
      width={40}
      height={40}
      // Google nie zawsze oddaje zdjęcie, gdy widzi adres strony, z której przyszło zapytanie.
      referrerPolicy="no-referrer"
      onError={() => setPictureFailed(true)}
    />
  ) : (
    <span className="panel-profile__picture panel-profile__picture--initial" aria-hidden="true">
      {(account.name ?? account.email).charAt(0).toUpperCase()}
    </span>
  )
}

/** Kto jest zalogowany: zdjęcie, imię i nazwisko i e-mail. */
function Profile({ account }: { account: ProfileAccount }) {
  return (
    <div className="panel-profile">
      <Avatar account={account} />
      <span className="panel-profile__text">
        {account.name && <span className="panel-profile__name">{account.name}</span>}
        <span className="panel-profile__email" title={account.email}>
          {account.email}
        </span>
      </span>
    </div>
  )
}

/**
 * Konto, które rozwija menu z powrotem na stronę i wylogowaniem; zamyka się po kliknięciu obok (a po przejściu
 * do innej sekcji przez `key`). Menu zaczyna się od imienia i nazwiska (jeśli jest, z Google) i e-maila. Na telefonie
 * (`top`) to samo zdjęcie w prawym górnym rogu; na komputerze (`bottom`) zdjęcie z imieniem i e-mailem na dole
 * paska, a menu otwiera się nad nim.
 */
function AccountMenu({ account, placement }: { account: ProfileAccount; placement: 'top' | 'bottom' }) {
  const ref = useRef<HTMLDetailsElement>(null)

  useEffect(() => {
    const closeOutside = (event: PointerEvent) => {
      if (ref.current?.open && !ref.current.contains(event.target as Node)) ref.current.open = false
    }
    document.addEventListener('pointerdown', closeOutside)
    return () => document.removeEventListener('pointerdown', closeOutside)
  }, [])

  return (
    <details ref={ref} className={`panel-account panel-account--${placement}`}>
      {placement === 'top' ? (
        <summary aria-label="Konto">
          <Avatar account={account} />
          <Chevron />
        </summary>
      ) : (
        <summary>
          <Profile account={account} />
          <Chevron />
        </summary>
      )}
      <div className="panel-account__menu">
        <Profile account={account} />
        <AccountActions />
      </div>
    </details>
  )
}

/** Strzałka podpowiadająca, że Konto się rozwija; obraca się, gdy menu jest otwarte. */
function Chevron() {
  return (
    <svg
      className="panel-account__chevron"
      viewBox="0 0 24 24"
      width={16}
      height={16}
      fill="none"
      stroke="currentColor"
      strokeWidth={2}
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden="true"
    >
      <path d="m6 9 6 6 6-6" />
    </svg>
  )
}

function AccountActions() {
  return (
    <>
      <Link to="/" className="panel-sidebar__action">
        ← Wróć na stronę
      </Link>
      <LogOutButton />
    </>
  )
}

function LogOutButton() {
  const navigate = useNavigate()
  const [failed, setFailed] = useState(false)

  return (
    <>
      <button
        type="button"
        className="panel-sidebar__action"
        onClick={() =>
          void logOut().then((ok) => {
            if (ok) navigate('/')
            else setFailed(true)
          })
        }
      >
        Wyloguj się
      </button>
      {failed && (
        <p className="checkout-form__error" role="alert">
          Nie udało się wylogować.
        </p>
      )}
    </>
  )
}

/**
 * Sam `/panel` otwiera Zamówienia. Po zgodzie w Google API odsyła na `/panel?kalendarz=…`
 * (PanelEndpoints.cs), więc wtedy z tym wynikiem do Dostępności, gdzie Klara wybiera Kalendarz Sesji.
 */
export function PanelHome() {
  const { search } = useLocation()
  const [searchParams] = useSearchParams()
  return <Navigate to={searchParams.has('kalendarz') ? panelAvailabilityPath + search : panelOrdersPath} replace />
}

const connectMessages: Record<ConnectResult, string> = {
  polaczono: 'Połączono z Google Calendar. Wybierz kalendarz, w którym zaznaczasz dostępność.',
  nieudane: 'Nie udało się połączyć z Google Calendar. Spróbuj ponownie.',
}

/**
 * Dostępność w jednym miejscu: połączenie z Google Calendar, wybór Kalendarza Sesji (zawsze do zmiany nad tygodniem)
 * i tydzień z blokami „Dostępna”.
 */
export function PanelAvailability() {
  const [searchParams] = useSearchParams()
  const result = searchParams.get('kalendarz') as ConnectResult | null
  const connection = useApi<CalendarConnection>('/api/panel/calendar')

  const message = result && result in connectMessages && (
    <p className={result === 'polaczono' ? 'checkout-form__note' : 'checkout-form__error'} role="status">
      {connectMessages[result]}
    </p>
  )

  if (connection.status === 'ok' && connection.data.status === 'connected') {
    return <ConnectedAvailability connection={connection.data} message={message} />
  }

  return (
    <div className="panel-availability">
      <h2 className="visually-hidden">Dostępność</h2>
      {message}
      {connection.status === 'loading' && <p>Sprawdzam połączenie z kalendarzem…</p>}
      {(connection.status === 'error' || connection.status === 'not-found') && (
        <p>Nie udało się sprawdzić połączenia z kalendarzem. Spróbuj ponownie za chwilę.</p>
      )}
      {connection.status === 'ok' && (
        <div className="panel-calendar__connect">
          <p>
            {connection.data.status === 'notConnected'
              ? 'Połącz stronę ze swoim kalendarzem Google, żeby zaznaczać dostępność i żeby Klienci widzieli Twoje wolne Terminy.'
              : 'Strona straciła dostęp do kalendarza (np. dostęp cofnięto w ustawieniach konta Google). Połącz go ponownie.'}
          </p>
          <a className="button button--primary" href={connectCalendarUrl}>
            {connection.data.status === 'notConnected' ? 'Połącz z Google Calendar' : 'Połącz ponownie'}
          </a>
        </div>
      )}
    </div>
  )
}

type SaveState = { status: 'idle' | 'saving' | 'saved' } | { status: 'failed'; error: string }

/** Wybór Kalendarza Sesji (zapisywany od razu po zmianie), a po wyborze tydzień do zaznaczania dostępności. */
function ConnectedAvailability({ connection, message }: { connection: CalendarConnection; message: ReactNode }) {
  // Panel dodaje w nim bloki „Dostępna”, więc tylko kalendarze, w których Klara może coś dodać (bez np. „Święta w Polsce”).
  const options = connection.calendars.filter((calendar) => !calendar.primary && calendar.writable)
  const [saved, setSaved] = useState(connection.sessionCalendarId)
  const [selected, setSelected] = useState(connection.sessionCalendarId ?? '')
  const [state, setState] = useState<SaveState>({ status: 'idle' })

  if (options.length === 0) {
    return (
      <div className="panel-availability">
        <h2 className="visually-hidden">Dostępność</h2>
        {message}
        <p className="checkout-form__note">
          Załóż w Google Calendar osobny kalendarz na swoją dostępność (po lewej przy „Inne kalendarze”: + → Utwórz nowy
          kalendarz, np. „Sesje”) i odśwież tę stronę, żeby go wybrać.
        </p>
      </div>
    )
  }

  const choose = (calendarId: string) => {
    const previous = selected
    setSelected(calendarId)
    setState({ status: 'saving' })
    void chooseSessionCalendar(calendarId).then((result) => {
      if (result.status === 'saved') {
        setSaved(calendarId)
        setState({ status: 'saved' })
      } else {
        setSelected(previous)
        setState({
          status: 'failed',
          error: result.status === 'invalid' ? result.error : 'Nie udało się zmienić kalendarza. Spróbuj ponownie.',
        })
      }
    })
  }

  const calendarPicker = (
    <div className="panel-availability__calendar">
      <span className="panel-availability__saved" role="status">
        {state.status === 'saving' ? 'Zapisuję…' : state.status === 'saved' ? 'Zapisano' : ''}
      </span>
      <label>
        <span>Kalendarz</span>
        <select value={selected} disabled={state.status === 'saving'} onChange={(event) => choose(event.target.value)}>
          {!saved && (
            <option value="" disabled>
              Wybierz…
            </option>
          )}
          {options.map((calendar) => (
            <option key={calendar.id} value={calendar.id}>
              {calendar.name}
            </option>
          ))}
        </select>
      </label>
    </div>
  )

  // Wybór kalendarza stoi w wierszu z tygodniem; dopóki go nie ma, sam nad informacją, co wybrać.
  return (
    <div className="panel-availability">
      <h2 className="visually-hidden">Dostępność</h2>
      {!saved && <div className="panel-availability__header">{calendarPicker}</div>}
      {message}
      {state.status === 'failed' && (
        <p className="checkout-form__error" role="alert">
          {state.error}
        </p>
      )}
      {saved ? (
        <AvailabilityEditor key={saved} actions={calendarPicker} />
      ) : (
        <div className="panel-calendar__connect">
          <p>Wybierz wyżej kalendarz Google, w którym zaznaczasz, kiedy przyjmujesz Usługi i Sesje.</p>
        </div>
      )}
    </div>
  )
}
