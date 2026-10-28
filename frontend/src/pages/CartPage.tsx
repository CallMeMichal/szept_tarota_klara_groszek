import { useEffect, useState, type FormEvent } from 'react'
import { Link, useSearchParams } from 'react-router'
import arrowPrimary from '../assets/home/arrow-up-right-primary.svg'
import {
  interruptedPaymentParam,
  maxQuantity,
  quantityOf,
  removeFromCart,
  setCartItemQuantity,
  updateCartItemForm,
  useCart,
  validateCart,
  type CartItem,
  type CartValidation,
} from '../cart.ts'
import { checkoutStatusMessage, useCheckout } from '../checkout.ts'
import { CheckoutForm } from '../components/CheckoutForm.tsx'
import { ProductImage } from '../components/ProductImage.tsx'
import { ServiceFormFields } from '../components/ServiceFormFields.tsx'
import { formatPrice, kindLabels, productPath, type FormField } from '../catalog.ts'
import { homeSectionPath, homeSections } from '../homeSections.ts'
import { formatAnswer, submitServiceForm, type FormAnswers } from '../serviceForm.ts'

type Validation =
  | { status: 'loading' }
  | { status: 'ok'; data: CartValidation }
  | { status: 'error' }

/** Formularz z danymi do zamówienia; przycisk płatności w podsumowaniu wysyła go atrybutem `form`. */
const checkoutFormId = 'dane-zamowienia'

export function CartPage() {
  const items = useCart()
  // Zwiększenie wymusza ponowne sprawdzenie Koszyka, gdy serwer odrzuci płatność, bo coś się zmieniło.
  const [revision, setRevision] = useState(0)
  const [cartChanged, setCartChanged] = useState(false)
  const validation = useCartValidation(items, revision)
  const checkout = useCheckout(() => {
    setCartChanged(true)
    setRevision((r) => r + 1)
  })
  const [searchParams] = useSearchParams()
  const paymentInterrupted = searchParams.get(interruptedPaymentParam) === 'przerwana'
  const cart = validation.status === 'ok' ? validation.data : null

  useEffect(() => {
    window.scrollTo({ top: 0 })
    const siteTitle = document.title
    document.title = 'Koszyk · Szept Tarota'
    return () => {
      document.title = siteTitle
    }
  }, [])

  if (items.length === 0) {
    return (
      <section className="section cart-page" aria-labelledby="cart-title">
        <p className="eyebrow">Twój wybór</p>
        <h1 id="cart-title">Koszyk</h1>
        <p className="section__lead">Koszyk jest pusty. Zajrzyj do oferty i wybierz coś dla siebie.</p>
        <Link className="button button--primary" to={homeSectionPath(homeSections.offer)}>
          Zobacz ofertę
        </Link>
      </section>
    )
  }

  return (
    <section
      className="section cart-page cart-page--filled"
      aria-labelledby="cart-title"
      aria-busy={validation.status === 'loading'}
    >
      <p className="eyebrow">Twój wybór</p>
      <h1 id="cart-title">Koszyk</h1>

      {paymentInterrupted && (
        <p className="cart-page__notice" role="status">
          Płatność została przerwana. Twój Koszyk czeka, możesz spróbować ponownie.
        </p>
      )}
      {cartChanged && (
        <p className="cart-page__notice" role="alert">
          Koszyk zmienił się od ostatniego sprawdzenia. Zobacz aktualne ceny i zapłać ponownie.
        </p>
      )}

      <div className="cart-layout">
        <div className="cart-layout__main">
          <div className="cart-panel">
            <h2 className="cart-panel__title">Produkty</h2>
            {cart ? <CheckedCartLines cart={cart} items={items} /> : <UncheckedCartLines items={items} />}
          </div>
          {cart?.isValid && (
            <CheckoutForm
              id={checkoutFormId}
              consents={cart.requiredConsents}
              status={checkout.status}
              onSubmit={(event) => void checkout.submit(event, cart, items)}
            />
          )}
        </div>

        <CartSummary
          count={items.reduce((sum, item) => sum + quantityOf(item), 0)}
          total={cart ? cart.total : items.reduce((sum, item) => sum + item.price * quantityOf(item), 0)}
          payable={cart?.isValid ?? false}
          sending={checkout.status.state === 'sending'}
          message={
            validation.status === 'loading'
              ? 'Sprawdzam aktualne ceny…'
              : validation.status === 'error'
                ? 'Nie udało się sprawdzić aktualnych cen. Pokazuję ceny z chwili dodania; sprawdzę je ponownie przed płatnością.'
                : !validation.data.isValid
                  ? 'Popraw albo usuń z Koszyka pozycje z błędami, żeby przejść do płatności.'
                  : checkoutStatusMessage(checkout.status)
          }
        />
      </div>
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

type CartSummaryProps = {
  count: number
  total: number
  /** Koszyk jest sprawdzony i bez błędów, więc można przejść do płatności. */
  payable: boolean
  sending: boolean
  /** Stan Koszyka albo wynik próby płatności, pokazywany pod przyciskiem. */
  message: string | null
}

/** Podsumowanie obok Produktów (na telefonie pod nimi): suma do zapłaty i przejście do płatności. */
function CartSummary({ count, total, payable, sending, message }: CartSummaryProps) {
  return (
    <aside className="cart-summary" aria-labelledby="cart-summary-title">
      <h2 id="cart-summary-title">Podsumowanie</h2>
      <dl className="cart-summary__rows">
        <div>
          <dt>Wartość produktów ({count})</dt>
          <dd>{formatPrice(total)}</dd>
        </div>
      </dl>
      <p className="cart-summary__total">
        <span>Do zapłaty</span>
        <strong>{formatPrice(total)}</strong>
      </p>
      <button
        className="button button--primary cart-summary__pay"
        type="submit"
        form={checkoutFormId}
        disabled={!payable || sending}
      >
        {sending ? 'Przechodzę do płatności…' : 'Przejdź do płatności'}
        <img src={arrowPrimary} width={16} height={16} alt="" />
      </button>
      <p className="cart-summary__note">BLIK, Przelewy24 albo karta. Płatność obsługuje Stripe.</p>
      {message && (
        <p className="cart-summary__message" role="status">
          {message}
        </p>
      )}
    </aside>
  )
}

/** Pozycje sprawdzone na serwerze; `items` to Koszyk z przeglądarki, z którego powstał `cart`. */
function CheckedCartLines({ cart, items }: { cart: CartValidation; items: CartItem[] }) {
  return (
    <ul className="cart-lines">
      {cart.items.map((line, index) => (
        <li key={items[index]?.id ?? index} className="cart-line">
          <CartLineImage slug={line.slug} linked={line.name !== null} />
          <div className="cart-line__product">
            {line.name ? <Link to={productPath(line.slug)}>{line.name}</Link> : <span>Produkt niedostępny</span>}
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
                <s aria-label={`Cena przy dodaniu ${formatPrice(line.previousPrice * line.quantity)}`}>
                  {formatPrice(line.previousPrice * line.quantity)}
                </s>
                <span className="cart-line__note">Cena zmieniła się od dodania do Koszyka</span>
              </>
            )}
            {line.price !== null && <strong>{formatPrice(line.price * line.quantity)}</strong>}
            {line.price !== null && line.quantity > 1 && (
              <span className="cart-line__note">{formatPrice(line.price)} za sztukę</span>
            )}
          </div>
          <div className="cart-line__actions">
            {line.kind === 'ebook' && items[index] && (
              <QuantityStepper item={items[index]} name={line.name ?? line.slug} />
            )}
            <RemoveButton index={index} name={line.name ?? line.slug} />
          </div>
          {line.form.length > 0 && items[index] && (
            <CartLineForm
              item={items[index]}
              name={line.name ?? line.slug}
              fields={line.form}
              serverErrors={line.formErrors}
            />
          )}
        </li>
      ))}
    </ul>
  )
}

type CartLineFormProps = {
  item: CartItem
  name: string
  fields: FormField[]
  /** Błędy formularza z ostatniego sprawdzenia Koszyka na serwerze. */
  serverErrors: Record<string, string>
}

/**
 * Odpowiedzi z formularza Usługi w pozycji Koszyka i odnośnik do ich poprawienia.
 * Błędy z serwera pokazujemy w formularzu po jego otwarciu.
 */
function CartLineForm({ item, name, fields, serverErrors }: CartLineFormProps) {
  const [editing, setEditing] = useState(false)
  const [errors, setErrors] = useState<Record<string, string> | null>(null)
  const answers: FormAnswers = item.form ?? {}
  const idPrefix = `pozycja-${item.id}`

  function onSubmit(event: FormEvent<HTMLFormElement>) {
    const { answers: corrected, errors: formErrors, valid } = submitServiceForm(event, fields)
    setErrors(formErrors)
    if (!valid) return

    updateCartItemForm(item.id, corrected)
    setErrors(null)
    setEditing(false)
  }

  if (editing) {
    return (
      <form
        className="cart-line__form service-form"
        onSubmit={onSubmit}
        noValidate
        aria-label={`Formularz: ${name}`}
      >
        <ServiceFormFields idPrefix={idPrefix} fields={fields} values={answers} errors={errors ?? serverErrors} />
        <div className="cart-line__form-actions">
          <button type="submit" className="button button--primary">
            Zapisz formularz
          </button>
          <button
            type="button"
            className="button button--ghost"
            onClick={() => {
              setErrors(null)
              setEditing(false)
            }}
          >
            Anuluj
          </button>
        </div>
      </form>
    )
  }

  const filled = fields.filter((field) => answers[field.key])
  return (
    <div className="cart-line__form">
      {filled.length > 0 && (
        <dl className="cart-line__answers">
          {filled.map((field) => (
            <div key={field.key}>
              <dt>{field.label}</dt>
              <dd>{formatAnswer(field, answers[field.key])}</dd>
            </div>
          ))}
        </dl>
      )}
      <button type="button" className="cart-line__edit" onClick={() => setEditing(true)}>
        {filled.length > 0 ? 'Popraw dane' : 'Uzupełnij dane'}
        <span className="visually-hidden"> {name}</span>
      </button>
    </div>
  )
}

/** Pozycje z cenami z chwili dodania, gdy Koszyk nie jest (jeszcze) sprawdzony na serwerze. */
function UncheckedCartLines({ items }: { items: CartItem[] }) {
  return (
    <ul className="cart-lines">
      {items.map((item, index) => (
        <li key={item.id} className="cart-line">
          <CartLineImage slug={item.slug} linked />
          <div className="cart-line__product">
            <Link to={productPath(item.slug)}>{item.name}</Link>
          </div>
          <div className="cart-line__price">
            <strong>{formatPrice(item.price * quantityOf(item))}</strong>
          </div>
          <div className="cart-line__actions">
            {!item.form && <QuantityStepper item={item} name={item.name} />}
            <RemoveButton index={index} name={item.name} />
          </div>
        </li>
      ))}
    </ul>
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

/** Liczba sztuk Ebooka: przyciski − i + oraz pole do wpisania liczby. */
function QuantityStepper({ item, name }: { item: CartItem; name: string }) {
  const quantity = quantityOf(item)
  return (
    <div className="quantity" role="group" aria-label={`Liczba sztuk: ${name}`}>
      <button
        type="button"
        className="quantity__button"
        onClick={() => setCartItemQuantity(item.id, quantity - 1)}
        disabled={quantity <= 1}
      >
        <span aria-hidden="true">−</span>
        <span className="visually-hidden">Jedna sztuka mniej</span>
      </button>
      <input
        className="quantity__input"
        type="number"
        inputMode="numeric"
        min={1}
        max={maxQuantity}
        value={quantity}
        aria-label="Liczba sztuk"
        onChange={(event) => {
          const value = Number(event.target.value)
          if (Number.isFinite(value) && value >= 1) setCartItemQuantity(item.id, value)
        }}
      />
      <button
        type="button"
        className="quantity__button"
        onClick={() => setCartItemQuantity(item.id, quantity + 1)}
        disabled={quantity >= maxQuantity}
      >
        <span aria-hidden="true">+</span>
        <span className="visually-hidden">Jedna sztuka więcej</span>
      </button>
    </div>
  )
}

function RemoveButton({ index, name }: { index: number; name: string }) {
  return (
    <button type="button" className="cart-line__remove" onClick={() => removeFromCart(index)}>
      Usuń<span className="visually-hidden"> {name} z Koszyka</span>
    </button>
  )
}
