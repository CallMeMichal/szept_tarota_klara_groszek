import { Link, useSearchParams } from 'react-router'
import { formatPrice, type ProductSummary } from '../catalog.ts'
import { formatPaidAt } from '../account.ts'
import { panelOrderPath, panelOrdersUrl, type OrderFilter, type PanelOrder } from '../panel.ts'
import { useApi } from '../useApi.ts'

/** Parametry filtra w adresie panelu, żeby powrót ze szczegółów Zamówienia go zachował. */
const filterParams = { from: 'od', to: 'do', product: 'produkt' } as const satisfies Record<keyof OrderFilter, string>

/** Opłacone Zamówienia wszystkich Klientów, od najnowszego, z filtrami po dniu zapłaty i Produkcie. */
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
    <section className="panel-orders" aria-labelledby="panel-orders-title">
      <h2 id="panel-orders-title">Zamówienia</h2>
      <div className="panel-orders__filters">
        <label className="checkout-form__field">
          Zapłacone od
          <input type="date" value={filter.from} max={filter.to || undefined} onChange={(e) => setFilter('from', e.target.value)} />
        </label>
        <label className="checkout-form__field">
          Zapłacone do
          <input type="date" value={filter.to} min={filter.from || undefined} onChange={(e) => setFilter('to', e.target.value)} />
        </label>
        <label className="checkout-form__field">
          Produkt
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
          <button type="button" className="button button--ghost" onClick={() => setSearchParams({}, { replace: true })}>
            Wyczyść filtry
          </button>
        )}
      </div>

      {orders.status === 'loading' && <p>Wczytuję Zamówienia…</p>}
      {(orders.status === 'error' || orders.status === 'not-found') && (
        <p>Nie udało się wczytać Zamówień. Sprawdź daty w filtrze albo spróbuj ponownie za chwilę.</p>
      )}
      {orders.status === 'ok' && orders.data.length === 0 && (
        <p>{filtered ? 'Żadne opłacone Zamówienie nie pasuje do filtrów.' : 'Nie ma jeszcze opłaconych Zamówień.'}</p>
      )}
      {orders.status === 'ok' && orders.data.length > 0 && (
        <ul className="my-orders__list">
          {orders.data.map((order) => (
            <li key={order.id} className="my-order">
              <p className="my-order__header">
                <span>{formatPaidAt(order.paidAt)}</span>
                <strong>{formatPrice(order.total)}</strong>
              </p>
              <p className="panel-orders__customer">
                {order.customerName} · <a href={`mailto:${order.email}`}>{order.email}</a>
              </p>
              <ul className="my-order__lines">
                {order.items.map((line, index) => (
                  <li key={index}>
                    <span>
                      {line.quantity > 1 && `${line.quantity} × `}
                      {line.name}
                    </span>
                    <span>{formatPrice(line.price * line.quantity)}</span>
                  </li>
                ))}
              </ul>
              <Link className="panel-orders__details" to={panelOrderPath(order.id)}>
                Szczegóły Zamówienia
              </Link>
            </li>
          ))}
        </ul>
      )}
    </section>
  )
}
