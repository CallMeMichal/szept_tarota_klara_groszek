import { useEffect, useState } from 'react'
import { Link } from 'react-router'
import {
  removeFromCart,
  useCart,
  validateCart,
  type CartItem,
  type CartValidation,
} from '../cart.ts'
import { formatPrice, kindLabels, productPath } from '../catalog.ts'
import { homeSectionPath, homeSections } from '../homeSections.ts'

type Validation =
  | { status: 'loading' }
  | { status: 'ok'; data: CartValidation }
  | { status: 'error' }

export function CartPage() {
  const items = useCart()
  const validation = useCartValidation(items)

  useEffect(() => {
    window.scrollTo({ top: 0 })
    const siteTitle = document.title
    document.title = 'Koszyk · Szept Tarota'
    return () => {
      document.title = siteTitle
    }
  }, [])

  return (
    <section className="section cart-page" aria-labelledby="cart-title" aria-busy={validation.status === 'loading'}>
      <p className="eyebrow">Twój wybór</p>
      <h1 id="cart-title">Koszyk</h1>

      {items.length === 0 ? (
        <>
          <p className="section__lead">Koszyk jest pusty. Zajrzyj do oferty i wybierz coś dla siebie.</p>
          <Link className="button button--primary" to={homeSectionPath(homeSections.offer)}>
            Zobacz ofertę
          </Link>
        </>
      ) : validation.status === 'ok' ? (
        <CheckedCart cart={validation.data} />
      ) : (
        <UncheckedCart items={items} checking={validation.status === 'loading'} />
      )}
    </section>
  )
}

/** Sprawdza Koszyk na serwerze przy każdej jego zmianie. */
function useCartValidation(items: CartItem[]): Validation {
  const [state, setState] = useState<{ items: CartItem[]; result: Validation }>({
    items,
    result: { status: 'loading' },
  })

  useEffect(() => {
    if (items.length === 0) return
    const controller = new AbortController()
    validateCart(items, controller.signal)
      .then((data): Validation => ({ status: 'ok', data }))
      .catch((): Validation => ({ status: 'error' }))
      .then((result) => {
        if (!controller.signal.aborted) setState({ items, result })
      })
    return () => controller.abort()
  }, [items])

  // Wynik dla poprzedniej zawartości Koszyka mógłby pokazać usuniętą pozycję.
  return state.items === items ? state.result : { status: 'loading' }
}

function CheckedCart({ cart }: { cart: CartValidation }) {
  return (
    <>
      <ul className="cart-lines">
        {cart.items.map((line, index) => (
          <li key={index} className="cart-line">
            <div className="cart-line__product">
              {line.name ? (
                <Link to={productPath(line.slug)}>{line.name}</Link>
              ) : (
                <span>Produkt niedostępny</span>
              )}
              {line.kind && <span className="cart-line__kind">{kindLabels[line.kind]}</span>}
              {line.errors.map((error) => (
                <span key={error.code} className="cart-line__error" role="alert">
                  {error.message}
                </span>
              ))}
            </div>
            <div className="cart-line__price">
              {line.previousPrice !== null && line.price !== null && (
                <>
                  <s aria-label={`Cena przy dodaniu ${formatPrice(line.previousPrice)}`}>
                    {formatPrice(line.previousPrice)}
                  </s>
                  <span className="cart-line__note">Cena zmieniła się od dodania do Koszyka</span>
                </>
              )}
              {line.price !== null && <strong>{formatPrice(line.price)}</strong>}
            </div>
            <RemoveButton index={index} name={line.name ?? line.slug} />
          </li>
        ))}
      </ul>

      <p className="cart-total">
        <span>Suma</span>
        <strong>{formatPrice(cart.total)}</strong>
      </p>

      {cart.requiredConsents.length > 0 && (
        <div className="cart-consents">
          <h2>Przed płatnością poproszę Cię o zgody</h2>
          <ul>
            {cart.requiredConsents.map((consent) => (
              <li key={consent.key}>{consent.text}</li>
            ))}
          </ul>
        </div>
      )}
    </>
  )
}

/** Koszyk z cenami z chwili dodania, gdy nie udało się go sprawdzić na serwerze. */
function UncheckedCart({ items, checking }: { items: CartItem[]; checking: boolean }) {
  return (
    <>
      <ul className="cart-lines">
        {items.map((item, index) => (
          <li key={index} className="cart-line">
            <div className="cart-line__product">
              <Link to={productPath(item.slug)}>{item.name}</Link>
            </div>
            <div className="cart-line__price">
              <strong>{formatPrice(item.price)}</strong>
            </div>
            <RemoveButton index={index} name={item.name} />
          </li>
        ))}
      </ul>
      <p className="cart-total">
        <span>Suma</span>
        <strong>{formatPrice(items.reduce((sum, item) => sum + item.price, 0))}</strong>
      </p>
      <p className="cart-page__status" role="status">
        {checking
          ? 'Sprawdzam aktualne ceny…'
          : 'Nie udało się sprawdzić aktualnych cen. Pokazuję ceny z chwili dodania; sprawdzę je ponownie przed płatnością.'}
      </p>
    </>
  )
}

function RemoveButton({ index, name }: { index: number; name: string }) {
  return (
    <button type="button" className="cart-line__remove" onClick={() => removeFromCart(index)}>
      Usuń<span className="visually-hidden"> {name} z Koszyka</span>
    </button>
  )
}
