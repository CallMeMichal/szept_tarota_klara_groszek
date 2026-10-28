// Rozpoczęcie płatności za sprawdzony Koszyk: formularz z danymi do zamówienia
// leży w Koszyku po lewej, a przycisk płatności w podsumowaniu po prawej, więc
// stan płatności żyje w CartPage i trafia do obu przez ten hook.

import { useState, type FormEvent } from 'react'
import { sessionExpired } from './account.ts'
import { startCheckout, type CartItem, type CartValidation } from './cart.ts'

export type CheckoutStatus =
  | { state: 'idle' }
  | { state: 'sending' }
  | { state: 'invalid'; errors: Record<string, string[]> }
  | { state: 'unavailable' }
  | { state: 'error' }

/**
 * Wysyła formularz płatności i przenosi przeglądarkę do Stripe Checkout.
 * `onCartChanged` jest wołane, gdy serwer odrzuci Koszyk, bo coś się w nim zmieniło.
 */
export function useCheckout(onCartChanged: () => void) {
  const [status, setStatus] = useState<CheckoutStatus>({ state: 'idle' })

  /** `items` to Koszyk z przeglądarki, z którego powstał `cart` (te same pozycje w tej samej kolejności). */
  async function submit(event: FormEvent<HTMLFormElement>, cart: CartValidation, items: CartItem[]) {
    event.preventDefault()
    const data = new FormData(event.currentTarget)
    const text = (name: string) => String(data.get(name) ?? '').trim()
    setStatus({ state: 'sending' })

    const result = await startCheckout({
      items: cart.items.map((line, index) => ({
        slug: line.slug,
        price: line.price ?? 0,
        quantity: line.quantity,
        form: items[index]?.form,
        appointmentStart: items[index]?.appointment?.start,
      })),
      email: text('email'),
      name: text('name'),
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
      case 'logged-out':
        // Koszyk zamiast formularza pokaże wtedy zaproszenie do logowania.
        setStatus({ state: 'idle' })
        sessionExpired()
        return
      default:
        setStatus({ state: result.status })
    }
  }

  return { status, submit }
}

/** Komunikat o wyniku próby płatności, pokazywany przy przycisku płatności. */
export function checkoutStatusMessage(status: CheckoutStatus): string | null {
  switch (status.state) {
    case 'unavailable':
      return 'Płatności nie są jeszcze uruchomione. Napisz do mnie na Instagramie.'
    case 'error':
      return 'Nie udało się rozpocząć płatności. Spróbuj ponownie za chwilę.'
    case 'invalid':
      return 'Popraw zaznaczone pola w danych do zamówienia.'
    default:
      return null
  }
}
