import { Link, useSearchParams } from 'react-router'
import { dayKey, formatTime } from '../appointments.ts'
import { formatPrice, type ProductSummary } from '../catalog.ts'
import { panelOrderPath, panelOrdersUrl, type OrderFilter, type PanelOrder } from '../panel.ts'
import { useApi } from '../useApi.ts'

/** Parametry filtra w adresie panelu, żeby powrót ze szczegółów Zamówienia go zachował. */
const filterParams = { from: 'od', to: 'do', product: 'produkt' } as const satisfies Record<keyof OrderFilter, string>

const weekdayFormat = new Intl.DateTimeFormat('pl-PL', { timeZone: 'UTC', weekday: 'long' })
const monthFormat = new Intl.DateTimeFormat('pl-PL', { timeZone: 'UTC', month: 'long', year: 'numeric' })
const dateFormat = new Intl.DateTimeFormat('pl-PL', { timeZone: 'UTC', day: 'numeric', month: 'long', year: 'numeric' })

/**
 * Opłacone Zamówienia wszystkich Klientów, od najnowszego, pogrupowane po dniu zapłaty (czas polski), z filtrami po
 * dniu zapłaty i Produkcie. Cały wiersz Zamówienia prowadzi do jego szczegółów.
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

  const setFilter = (key: keyof OrderFilter, value: string) =>
    setSearchParams(
      (params) => {
        if (value) params.set(filterParams[key], value)
        else params.delete(filterParams[key])
        return params
      },
      { replace: true },
    )

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
              {dayOrders.map((order) => (
                <li key={order.id}>
                  <Link
                    className="panel-order-row"
                    to={panelOrderPath(order.id)}
                    state={{ listSearch: searchParams.toString() }}
                  >
                    <time className="panel-order-row__time" dateTime={order.paidAt}>
                      {formatTime(order.paidAt)}
                    </time>
                    <span className="panel-order-row__customer">
                      <span className="panel-order-row__name">{order.customerName}</span>
                      <span className="panel-order-row__email">{order.email}</span>
                    </span>
                    <span className="panel-order-row__products">
                      {order.items.map((line, index) => (
                        <span key={index}>
                          {line.quantity > 1 && `${line.quantity} × `}
                          {line.name}
                        </span>
                      ))}
                    </span>
                    <strong className="panel-order-row__total">{formatPrice(order.total)}</strong>
                    <span className="panel-order-row__chevron" aria-hidden="true">
                      ›
                    </span>
                  </Link>
                </li>
              ))}
            </ul>
          </section>
        ))}
    </div>
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
