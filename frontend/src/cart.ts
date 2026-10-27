// Koszyk zapisany w pamięci przeglądarki (localStorage), więc przetrwa jej
// zamknięcie. Trzymamy tylko slug i cenę widzianą przy dodaniu: ceny, błędy i
// wymagane zgody liczy serwer (POST /api/cart/validate), a zapisana cena służy
// wyłącznie do pokazania Klientowi, że od tamtej pory się zmieniła.

import { useSyncExternalStore } from 'react'
import type { ProductKind } from './catalog.ts'

export type CartItem = {
  slug: string
  /** Nazwa z chwili dodania; pokazujemy ją, gdy serwer jest niedostępny. */
  name: string
  /** Cena w groszach widziana przy dodaniu. */
  price: number
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
    if (Array.isArray(parsed)) items = parsed.filter(isCartItem)
  } catch {
    // Uszkodzony zapis traktujemy jak pusty Koszyk.
  }
  cache = { raw, items }
  return items
}

function isCartItem(value: unknown): value is CartItem {
  const item = value as CartItem | null
  return (
    typeof item?.slug === 'string' && typeof item.name === 'string' && typeof item.price === 'number'
  )
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

/** Dodaje Ebook do Koszyka; Ebook może być w Koszyku najwyżej raz. */
export function addEbookToCart(product: { slug: string; name: string; price: number }) {
  const items = readItems()
  if (items.some((item) => item.slug === product.slug)) return
  writeItems([...items, { slug: product.slug, name: product.name, price: product.price }])
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
  /** Aktualna cena w groszach; brak dla Produktu spoza katalogu. */
  price: number | null
  /** Cena widziana przy dodaniu, gdy od tamtej pory się zmieniła. */
  previousPrice: number | null
  errors: CartError[]
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
    body: JSON.stringify({ items: items.map(({ slug, price }) => ({ slug, price })) }),
    signal,
  })
  if (!response.ok || !response.headers.get('Content-Type')?.includes('json')) {
    throw new Error(`HTTP ${response.status}`)
  }
  return (await response.json()) as CartValidation
}
