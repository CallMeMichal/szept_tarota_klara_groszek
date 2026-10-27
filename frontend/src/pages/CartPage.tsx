import { useEffect, useState } from 'react'
import { Link, useSearchParams } from 'react-router'
import {
  interruptedPaymentParam,
  removeFromCart,
  useCart,
  validateCart,
  type CartItem,
  type CartValidation,
} from '../cart.ts'
import { CheckoutForm } from '../components/CheckoutForm.tsx'
import { ProductImage } from '../components/ProductImage.tsx'
import { formatPrice, kindLabels, productPath } from '../catalog.ts'
import { homeSectionPath, homeSections } from '../homeSections.ts'

type Validation =
  | { status: 'loading' }
  | { status: 'ok'; data: CartValidation }
  | { status: 'error' }

export function CartPage() {
  const items = useCart()
  // Zwiększenie wymusza ponowne sprawdzenie Koszyka, gdy serwer odrzuci płatność, bo coś się zmieniło.
  const [revision, setRevision] = useState(0)
  const [cartChanged, setCartChanged] = useState(false)
  const validation = useCartValidation(items, revision)
  const [searchParams] = useSearchParams()
  const paymentInterrupted = searchParams.get(interruptedPaymentParam) === 'przerwana'

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

      {paymentInterrupted && items.length > 0 && (
        <p className="cart-page__notice" role="status">
          Płatność została przerwana. Twój Koszyk czeka, możesz spróbować ponownie.
        </p>
      )}
      {cartChanged && (
        <p className="cart-page__notice" role="alert">
          Koszyk zmienił się od ostatniego sprawdzenia. Zobacz aktualne ceny i zapłać ponownie.
        </p>
      )}

      {items.length === 0 ? (
        <>
          <p className="section__lead">Koszyk jest pusty. Zajrzyj do oferty i wybierz coś dla siebie.</p>
          <Link className="button button--primary" to={homeSectionPath(homeSections.offer)}>
            Zobacz ofertę
          </Link>
        </>
      ) : validation.status === 'ok' ? (
        <>
          <CheckedCart cart={validation.data} />
          {validation.data.isValid ? (
            <CheckoutForm
              cart={validation.data}
              onCartChanged={() => {
                setCartChanged(true)
                setRevision((r) => r + 1)
              }}
            />
          ) : (
            <p className="cart-page__status">Usuń z Koszyka pozycje z błędami, żeby przejść do płatności.</p>
          )}
        </>
      ) : (
        <UncheckedCart items={items} checking={validation.status === 'loading'} />
      )}
    </section>
  )
}

/** Sprawdza Koszyk na serwerze przy każdej jego zmianie i przy każdej zmianie `revision`. */
function useCartValidation(items: CartItem[], revision: number): Validation {
  const [state, setState] = useState<{ items: CartItem[]; revision: number; result: Validation }>({
    items,
    revision,
    result: { status: 'loading' },
  })

  useEffect(() => {
    if (items.length === 0) return
    const controller = new AbortController()
    validateCart(items, controller.signal)
      .then((data): Validation => ({ status: 'ok', data }))
      .catch((): Validation => ({ status: 'error' }))
      .then((result) => {
        if (!controller.signal.aborted) setState({ items, revision, result })
      })
    return () => controller.abort()
  }, [items, revision])

  // Wynik dla poprzedniej zawartości Koszyka mógłby pokazać usuniętą pozycję.
  return state.items === items && state.revision === revision ? state.result : { status: 'loading' }
}

function CheckedCart({ cart }: { cart: CartValidation }) {
  return (
    <>
      <ul className="cart-lines">
        {cart.items.map((line, index) => (
          <li key={index} className="cart-line">
            <CartLineImage slug={line.slug} linked={line.name !== null} />
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
            <CartLineImage slug={item.slug} linked />
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

/** Miniatura grafiki Produktu; prowadzi na jego podstronę, o ile Produkt jest w ofercie. */
function CartLineImage({ slug, linked }: { slug: string; linked: boolean }) {
  const image = <ProductImage className="cart-line__image" slug={slug} />
  // Nazwa obok już jest linkiem, więc miniatura nie trafia drugi raz do kolejności Tab.
  return linked ? (
    <Link to={productPath(slug)} tabIndex={-1} aria-hidden="true">
      {image}
    </Link>
  ) : (
    image
  )
}

function RemoveButton({ index, name }: { index: number; name: string }) {
  return (
    <button type="button" className="cart-line__remove" onClick={() => removeFromCart(index)}>
      Usuń<span className="visually-hidden"> {name} z Koszyka</span>
    </button>
  )
}
