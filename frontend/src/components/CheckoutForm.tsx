import type { FormEvent, ReactElement } from 'react'
import type { Consent } from '../cart.ts'
import type { CheckoutStatus } from '../checkout.ts'

type CheckoutFormProps = {
  /** Identyfikator formularza; przycisk „Zapłać” w podsumowaniu wskazuje go atrybutem `form`. */
  id: string
  consents: Consent[]
  /** Adres Konta zalogowanego Klienta; na niego przyjdzie potwierdzenie Zamówienia. */
  email: string
  status: CheckoutStatus
  onSubmit: (event: FormEvent<HTMLFormElement>) => void
}

/** Imię i nazwisko i wymagane zgody. Przycisk wysyłki jest w podsumowaniu Koszyka. */
export function CheckoutForm({ id, consents, email, status, onSubmit }: CheckoutFormProps) {
  const errors = status.state === 'invalid' ? status.errors : {}

  return (
    <form id={id} className="checkout-form" onSubmit={onSubmit} aria-labelledby={`${id}-title`}>
      <h2 id={`${id}-title`}>Dane do zamówienia</h2>

      <Field label="Imię i nazwisko" name="name" error={errors.name}>
        <input name="name" autoComplete="name" required maxLength={200} />
      </Field>
      <p className="checkout-form__note">Potwierdzenie i Ebooki wyślę na adres Twojego Konta: {email}.</p>

      <fieldset className="checkout-form__group">
        <legend>Zgody</legend>
        {consents.map((consent) => (
          <label key={consent.key} className="checkout-form__consent">
            <input type="checkbox" name="consent" value={consent.key} required />
            <span>{consent.text}</span>
          </label>
        ))}
        <FieldErrors id="consent-errors" errors={errors.acceptedConsents} />
      </fieldset>
    </form>
  )
}

type FieldProps = {
  label: string
  name: string
  error?: string[]
  children: ReactElement
}

function Field({ label, name, error, children }: FieldProps) {
  return (
    <label className="checkout-form__field">
      <span>{label}</span>
      {children}
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
