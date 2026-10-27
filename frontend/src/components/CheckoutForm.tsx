import { useState, type FormEvent, type ReactElement } from 'react'
import arrowPrimary from '../assets/home/arrow-up-right-primary.svg'
import { startCheckout, type CartValidation } from '../cart.ts'
import { formatPrice } from '../catalog.ts'

type Status =
  | { state: 'idle' }
  | { state: 'sending' }
  | { state: 'invalid'; errors: Record<string, string[]> }
  | { state: 'unavailable' }
  | { state: 'error' }

type CheckoutFormProps = {
  /** Sprawdzony, poprawny Koszyk: płacimy za jego pozycje po cenach, które Klient widzi. */
  cart: CartValidation
  /** Serwer odrzucił Koszyk, bo coś się w nim zmieniło; trzeba go sprawdzić i pokazać ponownie. */
  onCartChanged: () => void
}

/** E-mail, dane do faktury i wymagane zgody; po wysłaniu przeglądarka przechodzi do Stripe Checkout. */
export function CheckoutForm({ cart, onCartChanged }: CheckoutFormProps) {
  const [status, setStatus] = useState<Status>({ state: 'idle' })
  const errors = status.state === 'invalid' ? status.errors : {}

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const data = new FormData(event.currentTarget)
    const text = (name: string) => String(data.get(name) ?? '').trim()
    setStatus({ state: 'sending' })

    const result = await startCheckout({
      items: cart.items.map((line) => ({ slug: line.slug, price: line.price ?? 0 })),
      email: text('email'),
      billing: {
        name: text('billingName'),
        street: text('billingStreet'),
        postalCode: text('billingPostalCode'),
        city: text('billingCity'),
        taxId: text('billingTaxId') || null,
      },
      acceptedConsents: data.getAll('consent').map(String),
    })

    switch (result.status) {
      case 'started':
        // Zostajemy w stanie „sending”, dopóki przeglądarka nie przejdzie do Stripe.
        window.location.assign(result.redirectUrl)
        return
      case 'cart-changed':
        setStatus({ state: 'idle' })
        onCartChanged()
        return
      case 'invalid':
        setStatus({ state: 'invalid', errors: result.errors })
        return
      default:
        setStatus({ state: result.status })
    }
  }

  const sending = status.state === 'sending'

  return (
    <form className="checkout-form" onSubmit={handleSubmit}>
      <h2>Dane do zamówienia</h2>

      <Field label="Email" name="email" error={errors.email} hint="Na ten adres wyślę Ebooki i potwierdzenie.">
        <input name="email" type="email" autoComplete="email" required maxLength={200} />
      </Field>

      <fieldset className="checkout-form__group">
        <legend>Dane do faktury</legend>
        <Field label="Imię i nazwisko albo nazwa firmy" name="billingName" error={errors['billing.name']}>
          <input name="billingName" autoComplete="name" required maxLength={200} />
        </Field>
        <Field label="Ulica i numer" name="billingStreet" error={errors['billing.street']}>
          <input name="billingStreet" autoComplete="street-address" required maxLength={200} />
        </Field>
        <div className="checkout-form__row">
          <Field label="Kod pocztowy" name="billingPostalCode" error={errors['billing.postalCode']}>
            <input
              name="billingPostalCode"
              autoComplete="postal-code"
              required
              pattern="[0-9]{2}-[0-9]{3}"
              placeholder="00-000"
              inputMode="numeric"
            />
          </Field>
          <Field label="Miejscowość" name="billingCity" error={errors['billing.city']}>
            <input name="billingCity" autoComplete="address-level2" required maxLength={200} />
          </Field>
        </div>
        <Field label="NIP (tylko przy zakupie na firmę)" name="billingTaxId" error={errors['billing.taxId']}>
          <input name="billingTaxId" inputMode="numeric" maxLength={20} />
        </Field>
      </fieldset>

      <fieldset className="checkout-form__group checkout-form__consents">
        <legend>Zgody</legend>
        {cart.requiredConsents.map((consent) => (
          <label key={consent.key} className="checkout-form__consent">
            <input type="checkbox" name="consent" value={consent.key} required />
            <span>{consent.text}</span>
          </label>
        ))}
        <FieldErrors id="consent-errors" errors={errors.acceptedConsents} />
      </fieldset>

      <button className="button button--primary" type="submit" disabled={sending}>
        {sending ? 'Przechodzę do płatności…' : `Zapłać ${formatPrice(cart.total)}`}
        <img src={arrowPrimary} width={16} height={16} alt="" />
      </button>
      <p className="checkout-form__note">Zapłacisz BLIK-iem, przelewem Przelewy24 albo kartą na stronie Stripe.</p>

      <p className="checkout-form__status" role="status">
        {status.state === 'unavailable' && 'Płatności nie są jeszcze uruchomione. Napisz do mnie na Instagramie.'}
        {status.state === 'error' && 'Nie udało się rozpocząć płatności. Spróbuj ponownie za chwilę.'}
        {status.state === 'invalid' && 'Popraw zaznaczone pola.'}
      </p>
    </form>
  )
}

type FieldProps = {
  label: string
  name: string
  error?: string[]
  hint?: string
  children: ReactElement
}

function Field({ label, name, error, hint, children }: FieldProps) {
  return (
    <label className="checkout-form__field">
      <span>{label}</span>
      {children}
      {hint && <small>{hint}</small>}
      <FieldErrors id={`${name}-errors`} errors={error} />
    </label>
  )
}

function FieldErrors({ id, errors }: { id: string; errors?: string[] }) {
  if (!errors?.length) return null
  return (
    <span id={id} className="checkout-form__error" role="alert">
      {errors.join(' ')}
    </span>
  )
}
