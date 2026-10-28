import { useEffect, useId, useState } from 'react'
import {
  dayKey,
  fetchFreeAppointments,
  formatTime,
  todayKey,
  type Appointment,
  type FreeAppointments,
} from '../appointments.ts'

type Month = { year: number; month: number }

type AppointmentPickerProps = {
  slug: string
  /** Wybrany Termin (np. zapisany wcześniej w Koszyku). */
  value: Appointment | null
  onChange: (appointment: Appointment) => void
}

const weekdays = ['pn', 'wt', 'śr', 'czw', 'pt', 'sob', 'nd']
const monthFormat = new Intl.DateTimeFormat('pl-PL', { timeZone: 'UTC', month: 'long', year: 'numeric' })
const dayFormat = new Intl.DateTimeFormat('pl-PL', { timeZone: 'UTC', weekday: 'long', day: 'numeric', month: 'long' })

/**
 * Kalendarz wolnych Terminów Klary: miesiąc z dniami, w których są wolne
 * godziny, a pod nim godziny wybranego dnia. Daty i godziny są w czasie
 * polskim; miesiąc pobieramy z serwera przy każdym przejściu.
 */
export function AppointmentPicker({ slug, value, onChange }: AppointmentPickerProps) {
  const id = useId()
  const today = todayKey()
  // Termin zapisany w Koszyku mógł już minąć; wtedy kalendarz zaczyna od dzisiaj.
  const valueDay = value && dayKey(value.start) >= today ? dayKey(value.start) : null
  const [month, setMonth] = useState<Month>(() => monthOf(valueDay ?? today))
  const [day, setDay] = useState<string | null>(valueDay)
  const free = useFreeAppointments(slug, month, today)

  const isCurrentMonth = monthKey(month) === today.slice(0, 7)
  const byDay = new Map<string, Appointment[]>()
  if (free?.status === 'ok') {
    for (const appointment of free.appointments) {
      const key = dayKey(appointment.start)
      byDay.set(key, [...(byDay.get(key) ?? []), appointment])
    }
  }
  const dayAppointments = day ? (byDay.get(day) ?? []) : []

  function changeMonth(by: number) {
    setMonth(({ year, month }) => {
      const next = new Date(Date.UTC(year, month - 1 + by, 1))
      return { year: next.getUTCFullYear(), month: next.getUTCMonth() + 1 }
    })
    setDay(null)
  }

  return (
    <div className="appointment-picker">
      <div className="appointment-picker__month">
        <button
          type="button"
          className="appointment-picker__nav"
          onClick={() => changeMonth(-1)}
          disabled={isCurrentMonth}
        >
          <span aria-hidden="true">‹</span>
          <span className="visually-hidden">Poprzedni miesiąc</span>
        </button>
        <h3 id={`${id}-month`} aria-live="polite">
          {monthFormat.format(new Date(Date.UTC(month.year, month.month - 1, 1)))}
        </h3>
        <button type="button" className="appointment-picker__nav" onClick={() => changeMonth(1)}>
          <span aria-hidden="true">›</span>
          <span className="visually-hidden">Następny miesiąc</span>
        </button>
      </div>

      <div className="appointment-picker__days" role="group" aria-labelledby={`${id}-month`} aria-busy={!free}>
        {weekdays.map((weekday) => (
          <span key={weekday} className="appointment-picker__weekday" aria-hidden="true">
            {weekday}
          </span>
        ))}
        {daysOf(month).map((key, index) =>
          key === null ? (
            <span key={`empty-${index}`} aria-hidden="true" />
          ) : (
            <button
              key={key}
              type="button"
              className="appointment-picker__day"
              disabled={!byDay.has(key)}
              aria-pressed={key === day}
              onClick={() => setDay(key)}
            >
              {Number(key.slice(8))}
              <span className="visually-hidden">
                {' '}
                {dayFormat.format(new Date(`${key}T00:00:00Z`))}
                {byDay.has(key) ? ', są wolne Terminy' : ''}
              </span>
            </button>
          ),
        )}
      </div>

      {!free ? (
        <p className="appointment-picker__note" role="status">
          Sprawdzam wolne Terminy…
        </p>
      ) : free.status === 'unavailable' ? (
        <p className="appointment-picker__note" role="alert">
          Nie udało się teraz pobrać wolnych Terminów. Spróbuj ponownie za chwilę.
        </p>
      ) : byDay.size === 0 ? (
        <p className="appointment-picker__note" role="status">
          W tym miesiącu nie ma już wolnych Terminów. Sprawdź kolejny miesiąc.
        </p>
      ) : !day ? (
        <p className="appointment-picker__note">Wybierz dzień, żeby zobaczyć wolne godziny.</p>
      ) : (
        <div className="appointment-picker__times" role="group" aria-label={`Wolne godziny, ${dayFormat.format(new Date(`${day}T00:00:00Z`))}`}>
          {dayAppointments.map((appointment) => (
            <button
              key={appointment.start}
              type="button"
              className="appointment-picker__time"
              aria-pressed={value?.start === appointment.start}
              onClick={() => onChange(appointment)}
            >
              {formatTime(appointment.start)}–{formatTime(appointment.end)}
            </button>
          ))}
        </div>
      )}
    </div>
  )
}

/** Wolne Terminy w miesiącu, od dzisiaj; `null` w trakcie pobierania. */
function useFreeAppointments(slug: string, month: Month, today: string): FreeAppointments | null {
  const [state, setState] = useState<{ key: string; result: FreeAppointments } | null>(null)
  const key = `${slug}:${monthKey(month)}`

  useEffect(() => {
    const controller = new AbortController()
    const days = daysOf(month).filter((day) => day !== null)
    const from = days[0] < today ? today : days[0]
    fetchFreeAppointments(slug, from, days[days.length - 1], controller.signal)
      .then((result) => setState({ key, result }))
      .catch(() => {
        // Przerwane przy zmianie miesiąca albo zamknięciu okienka.
      })
    return () => controller.abort()
  }, [slug, month, today, key])

  return state?.key === key ? state.result : null
}

const monthKey = ({ year, month }: Month) => `${year}-${String(month).padStart(2, '0')}`

function monthOf(day: string): Month {
  return { year: Number(day.slice(0, 4)), month: Number(day.slice(5, 7)) }
}

/** Dni miesiąca (RRRR-MM-DD) z pustymi miejscami przed pierwszym, tak żeby tydzień zaczynał się w poniedziałek. */
function daysOf({ year, month }: Month): (string | null)[] {
  const first = new Date(Date.UTC(year, month - 1, 1))
  const count = new Date(Date.UTC(year, month, 0)).getUTCDate()
  const leading = (first.getUTCDay() + 6) % 7
  return [
    ...Array.from({ length: leading }, () => null),
    ...Array.from({ length: count }, (_, i) => `${monthKey({ year, month })}-${String(i + 1).padStart(2, '0')}`),
  ]
}
