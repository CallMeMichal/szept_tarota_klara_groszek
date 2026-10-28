import { useEffect, useState } from 'react'
import { Link, Navigate, useNavigate } from 'react-router'
import { accountPath, formatPaidAt, loginPathFor, logOut, panelPath, useSession, type MyOrder } from '../account.ts'
import { formatPrice, productPath } from '../catalog.ts'
import { homeSectionPath, homeSections } from '../homeSections.ts'
import { useApi } from '../useApi.ts'
import { usePageTitle } from '../usePageTitle.ts'

/** Konto zalogowanego Klienta: adres, wylogowanie i Moje Zamówienia. */
export function AccountPage() {
  usePageTitle('Konto')
  const session = useSession()

  useEffect(() => {
    window.scrollTo({ top: 0 })
  }, [])

  if (session.status === 'logged-out') return <Navigate to={loginPathFor(accountPath)} replace />

  return (
    <section className="section account-page" aria-labelledby="account-title" aria-busy={session.status === 'loading'}>
      <p className="eyebrow">Konto</p>
      <h1 id="account-title">Moje Konto</h1>
      {session.status === 'logged-in' && (
        <>
          <p className="section__lead">Zalogowano na adres {session.email}.</p>
          {session.isKlara && (
            <Link className="button button--primary" to={panelPath}>
              Panel Klary
            </Link>
          )}
          <LogOutButton />
          <MyOrders />
        </>
      )}
      {session.status === 'unavailable' && (
        <p className="section__lead">Nie udało się otworzyć Konta. Spróbuj ponownie za chwilę.</p>
      )}
    </section>
  )
}

function LogOutButton() {
  const navigate = useNavigate()
  const [failed, setFailed] = useState(false)

  return (
    <>
      <button
        type="button"
        className="button button--ghost"
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
          Nie udało się wylogować. Spróbuj ponownie.
        </p>
      )}
    </>
  )
}

function MyOrders() {
  const orders = useApi<MyOrder[]>('/api/account/orders')

  return (
    <div className="my-orders">
      <h2>Moje Zamówienia</h2>
      {orders.status === 'loading' && <p>Wczytuję Zamówienia…</p>}
      {(orders.status === 'error' || orders.status === 'not-found') && (
        <p>Nie udało się wczytać Zamówień. Spróbuj ponownie za chwilę.</p>
      )}
      {orders.status === 'ok' && orders.data.length === 0 && (
        <>
          <p>Nie masz jeszcze opłaconych Zamówień.</p>
          <Link className="button button--primary" to={homeSectionPath(homeSections.offer)}>
            Zobacz ofertę
          </Link>
        </>
      )}
      {orders.status === 'ok' && orders.data.length > 0 && (
        <ul className="my-orders__list">
          {orders.data.map((order) => (
            <li key={order.id} className="my-order">
              <p className="my-order__header">
                <span>{formatPaidAt(order.paidAt)}</span>
                <strong>{formatPrice(order.total)}</strong>
              </p>
              <ul className="my-order__lines">
                {order.items.map((line, index) => (
                  <li key={index}>
                    <Link to={productPath(line.slug)}>
                      {line.quantity > 1 && `${line.quantity} × `}
                      {line.name}
                    </Link>
                    <span>{formatPrice(line.price * line.quantity)}</span>
                  </li>
                ))}
              </ul>
            </li>
          ))}
        </ul>
      )}
    </div>
  )
}
