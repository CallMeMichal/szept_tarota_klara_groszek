import { useEffect, useRef, useState, type PointerEvent as ReactPointerEvent } from 'react'
import { dayKey, formatTime, todayKey } from '../appointments.ts'
import {
  addAvailabilityBlock,
  fetchAvailability,
  removeAvailabilityBlock,
  type AvailabilityFailure,
  type AvailabilityWeek,
  type Period,
} from '../panel.ts'

/** Długość jednej kratki siatki w minutach. */
const slotMinutes = 30
const slotsPerDay = (24 * 60) / slotMinutes
/** Godzina, do której siatka przewija się po otwarciu. */
const firstVisibleHour = 7

const weekdayFormat = new Intl.DateTimeFormat('pl-PL', { timeZone: 'UTC', weekday: 'short', day: 'numeric' })
const dayFormat = new Intl.DateTimeFormat('pl-PL', { timeZone: 'UTC', weekday: 'long', day: 'numeric', month: 'long' })
const rangeFormat = new Intl.DateTimeFormat('pl-PL', { timeZone: 'UTC', day: 'numeric', month: 'long', year: 'numeric' })

type Selection = { day: string; from: number; to: number }

type Week =
  | { status: 'loading' }
  | { status: 'ok'; week: AvailabilityWeek }
  | AvailabilityFailure

/**
 * Tydzień, w którym Klara zaznacza godziny, kiedy przyjmuje. Myszką
 * przeciąga po kratkach dnia; palcem (i klawiaturą) dotyka początku,
 * a potem końca. Każde zaznaczenie to blok „Dostępna” w jej Kalendarzu
 * Sesji w Google. Godziny zajęte w głównym kalendarzu są wyszarzone.
 * Na telefonie widać jeden dzień, wybierany nad siatką.
 */
export function AvailabilityEditor() {
  const today = todayKey()
  const [weekStart, setWeekStart] = useState(() => mondayOf(today))
  const [selectedDay, setSelectedDay] = useState(today)
  const [reload, setReload] = useState(0)
  const week = useAvailability(weekStart, reload)
  const [pending, setPending] = useState<Selection | null>(null)
  const [drag, setDrag] = useState<Selection | null>(null)
  const [saving, setSaving] = useState(false)
  const [message, setMessage] = useState<string | null>(null)
  const lastPointer = useRef<string>('mouse')
  const scroller = useRef<HTMLDivElement>(null)

  const days = Array.from({ length: 7 }, (_, i) => addDays(weekStart, i))
  const now = localNow()

  useEffect(() => {
    scroller.current?.scrollTo({ top: (firstVisibleHour * 60 * scroller.current.scrollHeight) / (24 * 60) })
  }, [])

  // Przeciąganie myszką kończy się puszczeniem przycisku także poza siatką.
  useEffect(() => {
    if (!drag) return
    const finish = () => {
      setDrag(null)
      void save(drag)
    }
    window.addEventListener('pointerup', finish)
    return () => window.removeEventListener('pointerup', finish)
  })

  function changeWeek(by: number) {
    const start = addDays(weekStart, 7 * by)
    setWeekStart(start)
    setSelectedDay(start)
    setPending(null)
  }

  async function save(selection: Selection) {
    const from = Math.min(selection.from, selection.to)
    const to = Math.max(selection.from, selection.to) + 1
    setSaving(true)
    setMessage(null)
    const result = await addAvailabilityBlock(localTime(selection.day, from), localTime(selection.day, to))
    setSaving(false)
    if (result.status === 'added') {
      setReload((r) => r + 1)
    } else {
      setMessage(
        result.status === 'invalid' ? result.error : 'Nie udało się zapisać w Google Calendar. Spróbuj ponownie.',
      )
    }
  }

  async function remove(id: string) {
    setMessage(null)
    const result = await removeAvailabilityBlock(id)
    if (result.status === 'removed') setReload((r) => r + 1)
    else setMessage('Nie udało się usunąć w Google Calendar. Spróbuj ponownie.')
  }

  function onSlotPointerDown(event: ReactPointerEvent, day: string, slot: number) {
    lastPointer.current = event.pointerType
    if (event.pointerType !== 'mouse' || event.button !== 0) return
    event.preventDefault()
    setPending(null)
    setDrag({ day, from: slot, to: slot })
  }

  /** Dotyk i klawiatura: pierwsze dotknięcie to początek, drugie (w tym samym dniu) koniec. */
  function onSlotClick(isKeyboard: boolean, day: string, slot: number) {
    if (!isKeyboard && lastPointer.current === 'mouse') return
    if (pending && pending.day === day) {
      setPending(null)
      void save({ day, from: pending.from, to: slot })
    } else {
      setPending({ day, from: slot, to: slot })
    }
  }

  const blocks = week.status === 'ok' ? week.week.blocks : []
  const busy = week.status === 'ok' ? week.week.busy : []
  const selection = drag ?? pending

  return (
    <div className="availability-editor">
      <div className="availability-editor__toolbar">
        <button type="button" className="appointment-picker__nav" onClick={() => changeWeek(-1)}>
          <span aria-hidden="true">‹</span>
          <span className="visually-hidden">Poprzedni tydzień</span>
        </button>
        <h3 aria-live="polite">
          {rangeFormat.formatRange(utcDate(days[0]), utcDate(days[6]))}
        </h3>
        <button type="button" className="appointment-picker__nav" onClick={() => changeWeek(1)}>
          <span aria-hidden="true">›</span>
          <span className="visually-hidden">Następny tydzień</span>
        </button>
      </div>

      <p className="checkout-form__note">
        Zaznacz godziny, w których przyjmujesz: myszką przeciągnij po kratkach dnia, a na telefonie dotknij godziny
        początku i potem godziny końca. Bloki zapisują się w Twoim Kalendarzu Sesji w Google.
      </p>

      <div className="availability-editor__days" role="group" aria-label="Dzień">
        {days.map((day) => (
          <button
            key={day}
            type="button"
            className="availability-editor__day-tab"
            aria-pressed={day === selectedDay}
            onClick={() => setSelectedDay(day)}
          >
            {weekdayFormat.format(utcDate(day))}
          </button>
        ))}
      </div>

      {week.status === 'loading' && <p role="status">Pobieram dostępność z Google Calendar…</p>}
      {week.status !== 'loading' && week.status !== 'ok' && (
        <p className="checkout-form__error" role="alert">
          {week.status === 'no-calendar'
            ? 'Najpierw wybierz Kalendarz Sesji.'
            : 'Nie udało się pobrać kalendarza z Google. Spróbuj ponownie za chwilę.'}
        </p>
      )}
      {pending && (
        <p className="checkout-form__note" role="status">
          Początek: {dayFormat.format(utcDate(pending.day))}, {slotLabel(pending.from)}. Dotknij godziny końca.
        </p>
      )}
      {saving && <p role="status">Zapisuję…</p>}
      {message && (
        <p className="checkout-form__error" role="alert">
          {message}
        </p>
      )}

      <div className="availability-editor__scroller" ref={scroller}>
        <div className="availability-editor__grid" aria-busy={week.status === 'loading'}>
          <div className="availability-editor__hours" aria-hidden="true">
            {Array.from({ length: 24 }, (_, hour) => (
              <span key={hour}>{`${hour}:00`}</span>
            ))}
          </div>
          {days.map((day) => (
            <div
              key={day}
              className={`availability-editor__column${day === selectedDay ? ' is-selected' : ''}`}
              role="group"
              aria-label={dayFormat.format(utcDate(day))}
            >
              <span className="availability-editor__column-title" aria-hidden="true">
                {weekdayFormat.format(utcDate(day))}
              </span>
              <div className="availability-editor__slots">
                {Array.from({ length: slotsPerDay }, (_, slot) => {
                  const past = day < now.day || (day === now.day && (slot + 1) * slotMinutes <= now.minutes)
                  const selected =
                    selection?.day === day &&
                    slot >= Math.min(selection.from, selection.to) &&
                    slot <= Math.max(selection.from, selection.to)
                  return (
                    <button
                      key={slot}
                      type="button"
                      className={`availability-editor__slot${selected ? ' is-selected' : ''}`}
                      disabled={past || saving || week.status !== 'ok'}
                      aria-label={`${slotLabel(slot)}–${slotLabel(slot + 1)}`}
                      onPointerDown={(event) => onSlotPointerDown(event, day, slot)}
                      onPointerEnter={() => {
                        if (drag?.day === day) setDrag({ ...drag, to: slot })
                      }}
                      onClick={(event) => onSlotClick(event.detail === 0, day, slot)}
                    />
                  )
                })}
                {segmentsOn(day, busy).map((segment) => (
                  <div
                    key={`busy-${segment.from}`}
                    className="availability-editor__busy"
                    style={position(segment)}
                    aria-hidden="true"
                  />
                ))}
                {blocks.flatMap((block) =>
                  segmentsOn(day, [block]).map((segment) => (
                    <div key={block.id} className="availability-editor__block" style={position(segment)}>
                      <span>
                        {formatTime(block.start)}–{formatTime(block.end)}
                      </span>
                      <button
                        type="button"
                        className="availability-editor__remove"
                        onClick={() => void remove(block.id)}
                      >
                        <span aria-hidden="true">×</span>
                        <span className="visually-hidden">
                          Usuń dostępność {dayFormat.format(utcDate(day))}, {formatTime(block.start)}–{formatTime(block.end)}
                        </span>
                      </button>
                    </div>
                  )),
                )}
              </div>
            </div>
          ))}
        </div>
      </div>
    </div>
  )
}

function useAvailability(weekStart: string, reload: number): Week {
  const [state, setState] = useState<{ key: string; week: Week } | null>(null)
  const key = `${weekStart}:${reload}`

  useEffect(() => {
    const controller = new AbortController()
    fetchAvailability(weekStart, addDays(weekStart, 6), controller.signal)
      .then((result) => setState({ key, week: result }))
      .catch(() => {
        // Przerwane przy zmianie tygodnia.
      })
    return () => controller.abort()
  }, [weekStart, key])

  // Po zapisie pokazujemy poprzedni stan tygodnia, dopóki nie przyjdzie nowy.
  if (state?.key === key) return state.week
  return state?.key.startsWith(`${weekStart}:`) ? state.week : { status: 'loading' }
}

/** Część okresu przypadająca na dzień `day`, w minutach od północy czasu polskiego. */
function segmentsOn(day: string, periods: Period[]) {
  const segments: { from: number; to: number }[] = []
  for (const period of periods) {
    const start = toLocal(period.start)
    const end = toLocal(period.end)
    if (start.day > day || end.day < day || (end.day === day && end.minutes === 0)) continue
    const from = start.day < day ? 0 : start.minutes
    const to = end.day > day ? 24 * 60 : end.minutes
    if (to > from) segments.push({ from, to })
  }
  return segments
}

const position = ({ from, to }: { from: number; to: number }) => ({
  top: `${(from / (24 * 60)) * 100}%`,
  height: `${((to - from) / (24 * 60)) * 100}%`,
})

/** Dzień i minuty od północy w czasie polskim. */
function toLocal(iso: string) {
  const [hours, minutes] = formatTime(iso).split(':').map(Number)
  return { day: dayKey(iso), minutes: hours * 60 + minutes }
}

function localNow() {
  return toLocal(new Date().toISOString())
}

/** Czas polski bez strefy dla API, np. `2026-11-03T10:00`; kratka 48 to północ następnego dnia. */
function localTime(day: string, slot: number) {
  const minutes = slot * slotMinutes
  if (minutes >= 24 * 60) return `${addDays(day, 1)}T00:00`
  return `${day}T${String(Math.floor(minutes / 60)).padStart(2, '0')}:${String(minutes % 60).padStart(2, '0')}`
}

function slotLabel(slot: number) {
  const minutes = (slot * slotMinutes) % (24 * 60)
  return `${Math.floor(minutes / 60)}:${String(minutes % 60).padStart(2, '0')}`
}

const utcDate = (day: string) => new Date(`${day}T00:00:00Z`)

function addDays(day: string, days: number) {
  const date = utcDate(day)
  date.setUTCDate(date.getUTCDate() + days)
  return date.toISOString().slice(0, 10)
}

function mondayOf(day: string) {
  return addDays(day, -((utcDate(day).getUTCDay() + 6) % 7))
}
