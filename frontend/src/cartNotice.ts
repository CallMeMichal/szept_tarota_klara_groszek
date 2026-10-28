// Powiadomienie o dodaniu Produktu do Koszyka: wyskakuje w rogu ekranu na
// kilka sekund, niezależnie od tego, która strona dodała Produkt
// (pokazuje je CartToast).

import { useSyncExternalStore } from 'react'

export type CartNotice = { id: number; productName: string }

let current: CartNotice | null = null
let nextId = 1
const listeners = new Set<() => void>()

function setNotice(notice: CartNotice | null) {
  current = notice
  listeners.forEach((listener) => listener())
}

/** Pokazuje powiadomienie, że Produkt trafił do Koszyka. */
export function notifyAddedToCart(productName: string) {
  setNotice({ id: nextId++, productName })
}

export function dismissCartNotice() {
  setNotice(null)
}

function subscribe(listener: () => void) {
  listeners.add(listener)
  return () => {
    listeners.delete(listener)
  }
}

export function useCartNotice(): CartNotice | null {
  return useSyncExternalStore(subscribe, () => current)
}
