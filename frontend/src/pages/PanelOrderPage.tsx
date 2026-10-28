import { useEffect } from 'react'
import { Link, Navigate, useParams } from 'react-router'
import { formatPaidAt, loginPathFor, panelPath, useSession } from '../account.ts'
import { formatAppointment } from '../appointments.ts'
import { formatPrice, kindLabels } from '../catalog.ts'
import { panelOrderPath, type PanelOrderDetails } from '../panel.ts'
import { useApi } from '../useApi.ts'
import { usePageTitle } from '../usePageTitle.ts'

/** Szczegóły opłaconego Zamówienia w panelu Klary: Klient, Produkty, Terminy i odpowiedzi z formularzy Usług. */
export function PanelOrderPage() {
  usePageTitle('Zamówienie')
  const { id = '' } = useParams()
  const session = useSession()

  useEffect(() => {
    window.scrollTo({ top: 0 })
  }, [])

  if (session.status === 'logged-out') return <Navigate to={loginPathFor(panelOrderPath(id))} replace />

  return (
    <section className="section account-page" aria-labelledby="panel-order-title" aria-busy={session.status === 'loading'}>
      <p className="eyebrow">
        <Link to={panelPath}>Panel</Link> · Zamówienie
      </p>
      {session.status === 'logged-in' && session.isKlara && <OrderDetails id={id} />}
      {session.status === 'logged-in' && !session.isKlara && (
        <>
          <h1 id="panel-order-title">Panel Klary</h1>
          <p className="section__lead">Ta strona jest dostępna tylko dla Klary.</p>
        </>
      )}
      {session.status === 'unavailable' && (
        <p className="section__lead">Nie udało się otworzyć panelu. Spróbuj ponownie za chwilę.</p>
      )}
    </section>
  )
}

function OrderDetails({ id }: { id: string }) {
  const order = useApi<PanelOrderDetails>(`/api/panel/orders/${encodeURIComponent(id)}`)

  if (order.status !== 'ok') {
    return (
      <>
        <h1 id="panel-order-title">
          {order.status === 'loading'
            ? 'Wczytuję Zamówienie…'
            : order.status === 'not-found'
              ? 'Nie ma takiego opłaconego Zamówienia'
              : 'Nie udało się wczytać Zamówienia'}
        </h1>
        {order.status !== 'loading' && (
          <Link className="button button--ghost" to={panelPath}>
            Wróć do panelu
          </Link>
        )}
      </>
    )
  }

  const { customerName, email, paidAt, items, total } = order.data
  return (
    <>
      <h1 id="panel-order-title">{customerName}</h1>
      <p className="section__lead">
        <a href={`mailto:${email}`}>{email}</a> · zapłacone {formatPaidAt(paidAt)}
      </p>

      <ul className="order-lines">
        {items.map((line, index) => (
          <li key={index} className="panel-order-line">
            <div className="order-line">
              <span className="order-line__product">
                <span>
                  {line.quantity > 1 && `${line.quantity} × `}
                  {line.name}
                </span>
                <span className="cart-line__kind">{kindLabels[line.kind]}</span>
              </span>
              <strong>{formatPrice(line.price * line.quantity)}</strong>
            </div>
            {line.appointment && <p className="panel-order-line__appointment">Termin: {formatAppointment(line.appointment)}</p>}
            {line.formAnswers.length > 0 && (
              <dl className="panel-order-line__answers">
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
      <p className="cart-total">
        <span>Suma</span>
        <strong>{formatPrice(total)}</strong>
      </p>
      <Link className="button button--ghost" to={panelPath}>
        Wróć do listy Zamówień
      </Link>
    </>
  )
}
