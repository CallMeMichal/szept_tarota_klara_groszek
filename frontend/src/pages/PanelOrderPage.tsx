import { Link, useLocation, useParams } from 'react-router'
import { formatPaidAt } from '../account.ts'
import { formatAppointment } from '../appointments.ts'
import { formatPrice, kindLabels } from '../catalog.ts'
import { panelOrdersPath, type PanelOrderDetails as OrderDetails } from '../panel.ts'
import { useApi } from '../useApi.ts'

/** Szczegóły opłaconego Zamówienia w panelu Klary: Klient, Produkty, Terminy i odpowiedzi z formularzy Usług. */
export function PanelOrderDetails() {
  const { id = '' } = useParams()
  // Lista przekazuje swoje filtry, żeby powrót do niej ich nie gubił.
  const listSearch = (useLocation().state as { listSearch?: string } | null)?.listSearch
  const backTo = listSearch ? `${panelOrdersPath}?${listSearch}` : panelOrdersPath
  const order = useApi<OrderDetails>(`/api/panel/orders/${encodeURIComponent(id)}`)

  const back = (
    <Link className="panel-order-details__back" to={backTo}>
      <span aria-hidden="true">‹</span> Zamówienia
    </Link>
  )

  if (order.status !== 'ok') {
    return (
      <div className="panel-order-details">
        {back}
        <h2>
          {order.status === 'loading'
            ? 'Wczytuję Zamówienie…'
            : order.status === 'not-found'
              ? 'Nie ma takiego opłaconego Zamówienia'
              : 'Nie udało się wczytać Zamówienia'}
        </h2>
      </div>
    )
  }

  const { customerName, email, paidAt, items, total } = order.data
  return (
    <div className="panel-order-details">
      {back}
      <header className="panel-order-details__header">
        <h2>{customerName}</h2>
        <p>
          <a href={`mailto:${email}`}>{email}</a>
          <span>Zapłacone {formatPaidAt(paidAt)}</span>
        </p>
      </header>

      <ul className="panel-order-details__lines">
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

      <p className="panel-order-details__total">
        <span>Razem</span>
        <strong>{formatPrice(total)}</strong>
      </p>
    </div>
  )
}
