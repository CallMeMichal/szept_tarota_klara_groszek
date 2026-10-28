import { useEffect, useState } from 'react'
import { Link, useParams } from 'react-router'
import { cartPath, clearCart, type OrderSummary } from '../cart.ts'
import { formatPrice, kindLabels, productPath } from '../catalog.ts'
import { homeSectionPath, homeSections } from '../homeSections.ts'

/** Co ile i ile razy pytamy o Zamówienie, zanim potwierdzenie płatności dotrze ze Stripe. */
const pollIntervalMs = 2000
const maxPolls = 30

type State =
  | { status: 'loading' }
  | { status: 'ok'; order: OrderSummary; polls: number }
  | { status: 'not-found' }
  | { status: 'error' }

/**
 * Strona, na którą Stripe odsyła Klienta po płatności. Potwierdzenie płatności (webhook) może dotrzeć
 * chwilę po powrocie, więc dopóki Zamówienie czeka na płatność, pytamy o nie ponownie.
 */
export function OrderPage() {
  const { id = '' } = useParams()
  const state = useOrder(id)
  const paid = state.status === 'ok' && state.order.status === 'paid'

  useEffect(() => {
    window.scrollTo({ top: 0 })
    const siteTitle = document.title
    document.title = 'Zamówienie · Szept Tarota'
    return () => {
      document.title = siteTitle
    }
  }, [])

  // Kupione Produkty nie powinny czekać w Koszyku na drugą płatność.
  useEffect(() => {
    if (paid) clearCart()
  }, [paid])

  return (
    <section className="section order-page" aria-labelledby="order-title" aria-busy={state.status === 'loading'}>
      {state.status === 'ok' ? (
        <OrderContent order={state.order} gaveUpWaiting={state.polls >= maxPolls} />
      ) : (
        <>
          <p className="eyebrow">Zamówienie</p>
          <h1 id="order-title">
            {state.status === 'loading' ? 'Sprawdzam Zamówienie…' : 'Nie znalazłam tego Zamówienia'}
          </h1>
          {state.status === 'error' && (
            <p className="section__lead">
              Nie udało się sprawdzić Zamówienia. Jeśli zapłaciłaś lub zapłaciłeś, potwierdzenie przyjdzie mailem.
            </p>
          )}
          {state.status !== 'loading' && (
            <Link className="button button--primary" to={homeSectionPath(homeSections.offer)}>
              Zobacz ofertę
            </Link>
          )}
        </>
      )}
    </section>
  )
}

function OrderContent({ order, gaveUpWaiting }: { order: OrderSummary; gaveUpWaiting: boolean }) {
  if (order.status === 'expired') {
    return (
      <>
        <p className="eyebrow">Zamówienie</p>
        <h1 id="order-title">Płatność nie doszła do skutku</h1>
        <p className="section__lead">Nic nie zostało pobrane. Produkty czekają w Koszyku, możesz spróbować ponownie.</p>
        <Link className="button button--primary" to={cartPath}>
          Wróć do Koszyka
        </Link>
      </>
    )
  }

  const paid = order.status === 'paid'
  return (
    <>
      <p className="eyebrow">{paid ? 'Zamówienie opłacone' : 'Zamówienie'}</p>
      <h1 id="order-title">{paid ? 'Dziękuję za zakup!' : 'Czekam na potwierdzenie płatności'}</h1>
      <p className="section__lead" role="status">
        {paid
          ? 'Potwierdzenie i zakupione Ebooki wysłałam na Twój adres email. Jeśli nie widzisz maila, zajrzyj do spamu.'
          : gaveUpWaiting
            ? 'Potwierdzenie płatności jeszcze nie dotarło. Gdy tylko dotrze, dostaniesz maila z Ebookami.'
            : 'To zwykle trwa kilka sekund. Nie zamykaj tej strony.'}
      </p>

      <ul className="order-lines">
        {order.items.map((line, index) => (
          <li key={index} className="order-line">
            <span className="order-line__product">
              <Link to={productPath(line.slug)}>
                {line.quantity > 1 && `${line.quantity} × `}
                {line.name}
              </Link>
              <span className="cart-line__kind">{kindLabels[line.kind]}</span>
            </span>
            <strong>{formatPrice(line.price * line.quantity)}</strong>
          </li>
        ))}
      </ul>
      <p className="cart-total">
        <span>Suma</span>
        <strong>{formatPrice(order.total)}</strong>
      </p>
    </>
  )
}

/** Pobiera Zamówienie i ponawia zapytanie, dopóki czeka ono na płatność (najwyżej `maxPolls` razy). */
function useOrder(id: string): State {
  const [state, setState] = useState<{ id: string; result: State }>({ id, result: { status: 'loading' } })

  useEffect(() => {
    const controller = new AbortController()
    let timer: ReturnType<typeof setTimeout> | undefined

    async function poll(polls: number) {
      const result = await fetch(`/api/orders/${encodeURIComponent(id)}`, {
        signal: controller.signal,
        headers: { Accept: 'application/json' },
      })
        .then(async (response): Promise<State> => {
          const isJson = response.headers.get('Content-Type')?.includes('json')
          if (response.status === 404) return { status: 'not-found' }
          if (!response.ok || !isJson) return { status: 'error' }
          return { status: 'ok', order: (await response.json()) as OrderSummary, polls }
        })
        .catch((): State => ({ status: 'error' }))
      if (controller.signal.aborted) return

      setState({ id, result })
      if (result.status === 'ok' && result.order.status === 'pendingPayment' && polls < maxPolls) {
        timer = setTimeout(() => void poll(polls + 1), pollIntervalMs)
      }
    }

    void poll(1)
    return () => {
      controller.abort()
      clearTimeout(timer)
    }
  }, [id])

  return state.id === id ? state.result : { status: 'loading' }
}
