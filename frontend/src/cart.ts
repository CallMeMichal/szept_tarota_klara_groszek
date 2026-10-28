// Koszyk zapisany w pamięci przeglądarki (localStorage), więc przetrwa jej
// zamknięcie. Trzymamy slug, cenę widzianą przy dodaniu, liczbę sztuk i
// odpowiedzi z formularza Usługi: ceny, błędy i wymagane zgody liczy serwer
// (POST /api/cart/validate), a zapisana cena służy wyłącznie do pokazania
// Klientowi, że od tamtej pory się zmieniła.

import { useSyncExternalStore } from 'react'
import type { FormField, ProductKind } from './catalog.ts'
import type { FormAnswers } from './serviceForm.ts'

export type CartItem = {
  /** Stały identyfikator pozycji: ta sama Usługa może być w Koszyku wiele razy. */
  id: string
  slug: string
  /** Nazwa z chwili dodania; pokazujemy ją, gdy serwer jest niedostępny. */
  name: string
  /** Cena jednej sztuki w groszach widziana przy dodaniu. */
  price: number
  /** Liczba sztuk; tylko Ebook może mieć więcej niż jedną (Usługa to zawsze osobna pozycja z formularzem). */
  quantity?: number
  /** Odpowiedzi z formularza Usługi po kluczach pól. */
  form?: FormAnswers
}

const storageKey = 'szept-tarota:koszyk'
const listeners = new Set<() => void>()

let cache: { raw: string | null; items: CartItem[] } = { raw: null, items: [] }

function readItems(): CartItem[] {
  const raw = localStorage.getItem(storageKey)
  if (raw === cache.raw) return cache.items

  let items: CartItem[] = []
  try {
    const parsed: unknown = raw ? JSON.parse(raw) : []
    // Pozycje zapisane, zanim Koszyk nadawał identyfikatory, dostają je przy odczycie.
    if (Array.isArray(parsed)) items = parsed.filter(isCartItem).map((item) => ({ ...item, id: item.id ?? newId() }))
  } catch {
    // Uszkodzony zapis traktujemy jak pusty Koszyk.
  }
  cache = { raw, items }
  return items
}

function isCartItem(value: unknown): value is Omit<CartItem, 'id'> & { id?: string } {
  const item = value as Partial<CartItem> | null
  return (
    typeof item?.slug === 'string' &&
    typeof item.name === 'string' &&
    typeof item.price === 'number' &&
    (item.id === undefined || typeof item.id === 'string') &&
    (item.quantity === undefined || typeof item.quantity === 'number') &&
    (item.form === undefined ||
      (typeof item.form === 'object' &&
        item.form !== null &&
        Object.values(item.form).every((answer) => typeof answer === 'string')))
  )
}

function newId() {
  // randomUUID działa tylko w bezpiecznym kontekście (https, localhost); np. przy
  // testach przez sieć lokalną po http wystarczy identyfikator losowy.
  return crypto.randomUUID?.() ?? `${Date.now().toString(36)}-${Math.random().toString(36).slice(2)}`
}

function writeItems(items: CartItem[]) {
  localStorage.setItem(storageKey, JSON.stringify(items))
  listeners.forEach((listener) => listener())
}

function subscribe(listener: () => void) {
  listeners.add(listener)
  // Zmiany Koszyka w innej karcie przeglądarki.
  const onStorage = (event: StorageEvent) => {
    if (event.key === storageKey) listener()
  }
  window.addEventListener('storage', onStorage)
  return () => {
    listeners.delete(listener)
    window.removeEventListener('storage', onStorage)
  }
}

export function useCart(): CartItem[] {
  return useSyncExternalStore(subscribe, readItems)
}

type ProductInCart = { slug: string; name: string; price: number }

/** Najwięcej sztuk w jednej pozycji (CartValidator.MaxQuantity). */
export const maxQuantity = 99

/** Liczba sztuk pozycji (zapisy bez liczby to jedna sztuka). */
export const quantityOf = (item: { quantity?: number }) => item.quantity ?? 1

/** Dodaje sztukę Ebooka; kolejne sztuki tego samego Ebooka trafiają do jednej pozycji. */
export function addEbookToCart(product: ProductInCart) {
  const items = readItems()
  const existing = items.find((item) => item.slug === product.slug && !item.form)
  if (existing) {
    setCartItemQuantity(existing.id, quantityOf(existing) + 1)
    return
  }
  writeItems([...items, { id: newId(), slug: product.slug, name: product.name, price: product.price, quantity: 1 }])
}

/** Zmienia liczbę sztuk pozycji (od 1 do `maxQuantity`). */
export function setCartItemQuantity(id: string, quantity: number) {
  const clamped = Math.min(Math.max(1, Math.round(quantity)), maxQuantity)
  writeItems(readItems().map((item) => (item.id === id ? { ...item, quantity: clamped } : item)))
}

/** Dodaje Usługę z wypełnionym formularzem; tę samą Usługę można dodać wiele razy. */
export function addServiceToCart(product: ProductInCart, form: FormAnswers) {
  writeItems([...readItems(), { id: newId(), slug: product.slug, name: product.name, price: product.price, form }])
}

/** Zapisuje poprawiony formularz Usługi w pozycji Koszyka. */
export function updateCartItemForm(id: string, form: FormAnswers) {
  writeItems(readItems().map((item) => (item.id === id ? { ...item, form } : item)))
}

/** Usuwa pozycję o danym miejscu w Koszyku. */
export function removeFromCart(index: number) {
  writeItems(readItems().filter((_, i) => i !== index))
}

export const cartPath = '/koszyk'

// Odpowiedź POST /api/cart/validate (CartEndpoints.cs).

export type CartError = { code: string; message: string }

export type CartLine = {
  slug: string
  name: string | null
  kind: ProductKind | null
  quantity: number
  /** Aktualna cena jednej sztuki w groszach; brak dla Produktu spoza katalogu. */
  price: number | null
  /** Cena widziana przy dodaniu, gdy od tamtej pory się zmieniła. */
  previousPrice: number | null
  errors: CartError[]
  /** Formularz Usługi (pusty dla Ebooka i Produktu spoza katalogu). */
  form: FormField[]
  /** Błędy formularza Usługi po kluczach pól. */
  formErrors: Record<string, string>
}

export type Consent = { key: string; text: string }

export type CartValidation = {
  isValid: boolean
  items: CartLine[]
  total: number
  requiredConsents: Consent[]
}

export async function validateCart(items: CartItem[], signal: AbortSignal): Promise<CartValidation> {
  const response = await fetch('/api/cart/validate', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', Accept: 'application/json' },
    body: JSON.stringify({
      items: items.map((item) => ({ slug: item.slug, price: item.price, quantity: quantityOf(item), form: item.form })),
    }),
    signal,
  })
  if (!response.ok || !response.headers.get('Content-Type')?.includes('json')) {
    throw new Error(`HTTP ${response.status}`)
  }
  return (await response.json()) as CartValidation
}

/** Opróżnia Koszyk, np. po opłaconym Zamówieniu. */
export function clearCart() {
  writeItems([])
}

// POST /api/checkout i GET /api/orders/{id} (OrderEndpoints.cs).

export type CheckoutForm = {
  /** Pozycje z cenami, które Klient widzi teraz w Koszyku, i formularzami Usług. */
  items: { slug: string; price: number; quantity: number; form?: FormAnswers }[]
  /** Imię i nazwisko Klienta. */
  name: string
  acceptedConsents: string[]
}

export type CheckoutResult =
  | { status: 'started'; redirectUrl: string }
  /** Ceny albo pozycje zmieniły się od ostatniego sprawdzenia Koszyka. */
  | { status: 'cart-changed'; cart: CartValidation }
  /** Błędy pól formularza: `name`, `acceptedConsents`. */
  | { status: 'invalid'; errors: Record<string, string[]> }
  /** Sesja wygasła; płacić można tylko po zalogowaniu. */
  | { status: 'logged-out' }
  | { status: 'unavailable' }
  | { status: 'error' }

/** Płatność zalogowanego Klienta; e-mail Zamówienia to adres jego Konta. */
export async function startCheckout(form: CheckoutForm): Promise<CheckoutResult> {
  try {
    const response = await fetch('/api/checkout', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json', Accept: 'application/json' },
      body: JSON.stringify(form),
    })
    const isJson = response.headers.get('Content-Type')?.includes('json')
    if (response.ok && isJson) {
      const { redirectUrl } = (await response.json()) as { redirectUrl: string }
      return { status: 'started', redirectUrl }
    }
    if (response.status === 409 && isJson) {
      return { status: 'cart-changed', cart: (await response.json()) as CartValidation }
    }
    if (response.status === 400 && isJson) {
      const { errors } = (await response.json()) as { errors?: Record<string, string[]> }
      return { status: 'invalid', errors: errors ?? {} }
    }
    if (response.status === 401) return { status: 'logged-out' }
    if (response.status === 503) return { status: 'unavailable' }
    return { status: 'error' }
  } catch {
    return { status: 'error' }
  }
}

export type OrderStatus = 'pendingPayment' | 'paid' | 'expired'

export type OrderSummary = {
  id: string
  status: OrderStatus
  /** `price` to cena jednej sztuki. */
  items: { slug: string; name: string; kind: ProductKind; quantity: number; price: number }[]
  total: number
}

/** Strona, na którą Stripe odsyła Klienta po płatności (Shop:SiteUrl + /zamowienie/<id>). */
export const orderPath = (id: string) => `/zamowienie/${id}`

/** Adres powrotu po przerwanej płatności to Koszyk z tym parametrem. */
export const interruptedPaymentParam = 'platnosc'
