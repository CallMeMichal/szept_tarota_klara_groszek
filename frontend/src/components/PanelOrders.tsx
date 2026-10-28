import { useState } from 'react'
import { useSearchParams } from 'react-router'
import { formatPaidAt } from '../account.ts'
import { dayKey, formatAppointment, formatTime } from '../appointments.ts'
import { formatPrice, kindLabels, type ProductSummary } from '../catalog.ts'
import { panelOrdersUrl, type OrderFilter, type PanelOrder, type PanelOrderDetails } from '../panel.ts'
import { useApi } from '../useApi.ts'

/** Parametry filtra w adresie panelu, żeby filtr przetrwał odświeżenie strony. */
const filterParams = { from: 'od', to: 'do', product: 'produkt' } as const satisfies Record<keyof OrderFilter, string>

const weekdayFormat = new Intl.DateTimeFormat('pl-PL', { timeZone: 'UTC', weekday: 'long' })
const monthFormat = new Intl.DateTimeFormat('pl-PL', { timeZone: 'UTC', month: 'long', year: 'numeric' })
const dateFormat = new Intl.DateTimeFormat('pl-PL', { timeZone: 'UTC', day: 'numeric', month: 'long', year: 'numeric' })

/**
 * Opłacone Zamówienia wszystkich Klientów, od najnowszego, pogrupowane po dniu zapłaty (czas polski), z filtrami po
 * dniu zapłaty i Produkcie. Kliknięty wiersz rozwija się w miejscu i pokazuje Terminy i odpowiedzi z formularzy.
 */
export function PanelOrders() {
  const [searchParams, setSearchParams] = useSearchParams()
  const filter: OrderFilter = {
    from: searchParams.get(filterParams.from) ?? '',
    to: searchParams.get(filterParams.to) ?? '',
    product: searchParams.get(filterParams.product) ?? '',
  }
  const orders = useApi<PanelOrder[]>(panelOrdersUrl(filter))
  const products = useApi<ProductSummary[]>('/api/products')
  const filtered = Object.values(filter).some((value) => value !== '')
  const [expanded, setExpanded] = useState<ReadonlySet<string>>(new Set())

  const setFilter = (key: keyof OrderFilter, value: string) =>
    setSearchParams(
      (params) => {
        if (value) params.set(filterParams[key], value)
        else params.delete(filterParams[key])
        return params
      },
      { replace: true },
    )

  const toggle = (id: string) =>
    setExpanded((open) => {
      const next = new Set(open)
      if (!next.delete(id)) next.add(id)
      return next
    })

  return (
    <div className="panel-orders">
      <h2 className="visually-hidden">Zamówienia</h2>
      <div className="panel-orders__toolbar">
        <div className="panel-orders__filters">
          <label className="panel-filter">
            <span>Od</span>
            <input
              type="date"
              value={filter.from}
              max={filter.to || undefined}
              onChange={(e) => setFilter('from', e.target.value)}
            />
          </label>
          <label className="panel-filter">
            <span>Do</span>
            <input
              type="date"
              value={filter.to}
              min={filter.from || undefined}
              onChange={(e) => setFilter('to', e.target.value)}
            />
          </label>
          <label className="panel-filter">
            <span>Produkt</span>
            <select value={filter.product} onChange={(e) => setFilter('product', e.target.value)}>
              <option value="">Wszystkie</option>
              {products.status === 'ok' &&
                [...products.data]
                  .sort((a, b) => a.name.localeCompare(b.name, 'pl'))
                  .map((product) => (
                    <option key={product.slug} value={product.slug}>
                      {product.name}
                    </option>
                  ))}
            </select>
          </label>
          {filtered && (
            <button type="button" className="panel-orders__clear" onClick={() => setSearchParams({}, { replace: true })}>
              Wyczyść
            </button>
          )}
        </div>
        {orders.status === 'ok' && orders.data.length > 0 && (
          <p className="panel-orders__summary">
            <span>{countLabel(orders.data.length)}</span>
            <strong>{formatPrice(sum(orders.data))}</strong>
          </p>
        )}
      </div>

      {orders.status === 'loading' && <p className="panel-orders__empty">Wczytuję Zamówienia…</p>}
      {(orders.status === 'error' || orders.status === 'not-found') && (
        <p className="panel-orders__empty">
          Nie udało się wczytać Zamówień. Sprawdź daty w filtrze albo spróbuj ponownie za chwilę.
        </p>
      )}
      {orders.status === 'ok' && orders.data.length === 0 && (
        <p className="panel-orders__empty">
          {filtered ? 'Żadne opłacone Zamówienie nie pasuje do filtrów.' : 'Nie ma jeszcze opłaconych Zamówień.'}
        </p>
      )}
      {orders.status === 'ok' && orders.data.length > 0 && (
        <div className="panel-orders__columns" aria-hidden="true">
          <span>Godzina</span>
          <span>Klient</span>
          <span>Produkty</span>
          <span>Kwota</span>
        </div>
      )}
      {orders.status === 'ok' &&
        groupByDay(orders.data).map(([day, dayOrders]) => (
          <section key={day} className="panel-orders__day" aria-label={dateFormat.format(utcDate(day))}>
            <header className="panel-orders__day-header">
              <span className="panel-orders__day-number">{Number(day.slice(8))}</span>
              <span className="panel-orders__day-name">
                <span>{weekdayFormat.format(utcDate(day))}</span>
                <span>{monthFormat.format(utcDate(day))}</span>
              </span>
              <span className="panel-orders__day-total">{formatPrice(sum(dayOrders))}</span>
            </header>
            <ul className="panel-orders__list">
              {dayOrders.map((order) => {
                const open = expanded.has(order.id)
                return (
                  <li key={order.id} className={`panel-order${open ? ' is-open' : ''}`}>
                    <button
                      type="button"
                      className="panel-order-row"
                      aria-expanded={open}
                      aria-controls={`zamowienie-${order.id}`}
                      onClick={() => toggle(order.id)}
                    >
                      <time className="panel-order-row__time" dateTime={order.paidAt}>
                        {formatTime(order.paidAt)}
                      </time>
                      <span className="panel-order-row__customer">
                        <span className="panel-order-row__name">{order.customerName}</span>
                        <span className="panel-order-row__email">{order.email}</span>
                      </span>
                      <span className="panel-order-row__products">
                        {order.items.map((line) => `${line.quantity > 1 ? `${line.quantity} × ` : ''}${line.name}`).join(', ')}
                      </span>
                      <strong className="panel-order-row__total">{formatPrice(order.total)}</strong>
                      <svg
                        className="panel-order-row__chevron"
                        viewBox="0 0 24 24"
                        width={18}
                        height={18}
                        fill="none"
                        stroke="currentColor"
                        strokeWidth={2}
                        strokeLinecap="round"
                        strokeLinejoin="round"
                        aria-hidden="true"
                      >
                        <path d="m6 9 6 6 6-6" />
                      </svg>
                    </button>
                    {open && (
                      <div id={`zamowienie-${order.id}`} className="panel-order__details">
                        <OrderDetails id={order.id} />
                      </div>
                    )}
                  </li>
                )
              })}
            </ul>
          </section>
        ))}
    </div>
  )
}

/** Rozwinięte Zamówienie: dane z płatności i każdy Produkt z Terminem i odpowiedziami z formularza Usługi. */
function OrderDetails({ id }: { id: string }) {
  const order = useApi<PanelOrderDetails>(`/api/panel/orders/${encodeURIComponent(id)}`)

  if (order.status === 'loading') return <p className="panel-order__status">Wczytuję szczegóły…</p>
  if (order.status !== 'ok') {
    return <p className="panel-order__status">Nie udało się wczytać szczegółów. Zwiń i rozwiń ponownie.</p>
  }

  const { email, paidAt, items } = order.data
  return (
    <>
      <p className="panel-order__meta">
        <a href={`mailto:${email}`}>{email}</a>
        <span>Zapłacone {formatPaidAt(paidAt)}</span>
      </p>
      <ul className="panel-order__items">
        {items.map((line, index) => (
          <li key={index} className="panel-order-item">
            <div className="panel-order-item__header">
              <span className="panel-order-item__name">
                {line.quantity > 1 && `${line.quantity} × `}
                {line.name}
              </span>
              <span className="panel-order-item__kind">{kindLabels[line.kind]}</span>
              <strong className="panel-order-item__price">{formatPrice(line.price * line.quantity)}</strong>
            </div>
            {line.appointment && (
              <p className="panel-order-item__appointment">
                <span>Termin</span>
                {formatAppointment(line.appointment)}
              </p>
            )}
            {line.formAnswers.length > 0 && (
              <dl className="panel-order-item__answers">
                {line.formAnswers.map((answer) => (
                  <div key={answer.key}>
                    <dt>{answer.label}</dt>
                    <dd>{answer.value}</dd>
                  </div>
                ))}
              </dl>
            )}
          </li>
        ))}
      </ul>
    </>
  )
}

/** Zamówienia po dniu zapłaty w czasie polskim, w kolejności z API (od najnowszego). */
function groupByDay(orders: PanelOrder[]): [string, PanelOrder[]][] {
  const days = new Map<string, PanelOrder[]>()
  for (const order of orders) {
    const day = dayKey(order.paidAt)
    days.set(day, [...(days.get(day) ?? []), order])
  }
  return [...days]
}

const sum = (orders: PanelOrder[]) => orders.reduce((total, order) => total + order.total, 0)

/** „1 Zamówienie”, „3 Zamówienia”, „5 Zamówień”. */
function countLabel(count: number) {
  const lastDigit = count % 10
  const lastTwoDigits = count % 100
  if (count === 1) return '1 Zamówienie'
  if (lastDigit >= 2 && lastDigit <= 4 && (lastTwoDigits < 12 || lastTwoDigits > 14)) return `${count} Zamówienia`
  return `${count} Zamówień`
}

const utcDate = (day: string) => new Date(`${day}T00:00:00Z`)
