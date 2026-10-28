// Konto Klienta (AccountEndpoints.cs). Nie ma haseł: Klient podaje e-mail, dostaje
// na niego jednorazowy kod i wpisuje go na stronie logowania, albo loguje się przez
// Google, a API ustawia ciasteczko sesji. Stan logowania trzymamy w jednym miejscu, żeby nagłówek,
// Koszyk i Konto widziały to samo i odświeżały się razem po zalogowaniu i wylogowaniu.

import { useSyncExternalStore } from 'react'
import type { ProductKind } from './catalog.ts'

export type Session =
  | { status: 'loading' }
  | { status: 'logged-in'; email: string }
  | { status: 'logged-out' }
  /** API nie odpowiada (np. podgląd na GitHub Pages bez backendu). */
  | { status: 'unavailable' }

export const loginPath = '/logowanie'
export const accountPath = '/konto'

/** Ścieżka tej strony (np. `/koszyk`) albo `null`, gdy to adres innej strony, żeby `dalej` z adresu nie wyprowadził Klienta poza stronę. */
export function localPathOrNull(path: string | null | undefined): string | null {
  if (!path || path.length > 200 || !path.startsWith('/') || path.startsWith('//') || path.includes('\\')) return null
  // oxlint-disable-next-line no-control-regex -- właśnie znaków sterujących szukamy
  return /[\u0000-\u001f\u007f]/.test(path) ? null : path
}

/** Logowanie z powrotem na `returnTo`, np. do Koszyka. */
export const loginPathFor = (returnTo: string) => `${loginPath}?dalej=${encodeURIComponent(returnTo)}`

/**
 * Adres, pod który przeglądarka przechodzi, żeby zalogować się przez Google. API odsyła ją do Google, a po
 * powrocie na `returnTo` (bez niego do Konta); gdy się nie uda, na stronę logowania z `?google=nieudane`.
 */
export const googleLoginUrl = (returnTo: string | null) =>
  returnTo ? `/api/account/google?returnUrl=${encodeURIComponent(returnTo)}` : '/api/account/google'

let session: Session = { status: 'loading' }
let loaded = false
const listeners = new Set<() => void>()

function setSession(next: Session) {
  session = next
  listeners.forEach((listener) => listener())
}

async function loadSession() {
  try {
    const response = await fetch('/api/account', { headers: { Accept: 'application/json' } })
    const isJson = response.headers.get('Content-Type')?.includes('json')
    if (response.status === 401) setSession({ status: 'logged-out' })
    else if (response.ok && isJson) setSession({ status: 'logged-in', email: ((await response.json()) as { email: string }).email })
    else setSession({ status: 'unavailable' })
  } catch {
    setSession({ status: 'unavailable' })
  }
}

function subscribe(listener: () => void) {
  listeners.add(listener)
  if (!loaded) {
    loaded = true
    void loadSession()
  }
  return () => {
    listeners.delete(listener)
  }
}

/** Czy Klient jest zalogowany; sesję sprawdza raz, przy pierwszym użyciu. */
export function useSession(): Session {
  return useSyncExternalStore(subscribe, () => session)
}

export type LoginCodeResult =
  | { status: 'sent' }
  | { status: 'invalid'; errors: Record<string, string[]> }
  | { status: 'too-many' }
  | { status: 'unavailable' }
  | { status: 'error' }

/** Prosi o kod logowania na `email`. */
export async function requestLoginCode(email: string): Promise<LoginCodeResult> {
  try {
    const response = await fetch('/api/account/login-code', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json', Accept: 'application/json' },
      body: JSON.stringify({ email }),
    })
    if (response.status === 202) return { status: 'sent' }
    if (response.status === 400 && response.headers.get('Content-Type')?.includes('json')) {
      const { errors } = (await response.json()) as { errors?: Record<string, string[]> }
      return { status: 'invalid', errors: errors ?? {} }
    }
    if (response.status === 429) return { status: 'too-many' }
    if (response.status === 503) return { status: 'unavailable' }
    return { status: 'error' }
  } catch {
    return { status: 'error' }
  }
}

export type LogInResult = 'logged-in' | 'invalid-code' | 'error'

/** Loguje kodem z maila wysłanego na `email`; kod działa raz, przez 15 minut. */
export async function logIn(email: string, code: string): Promise<LogInResult> {
  try {
    const response = await fetch('/api/account/login', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json', Accept: 'application/json' },
      body: JSON.stringify({ email, code }),
    })
    if (response.status === 401) return 'invalid-code'
    if (!response.ok || !response.headers.get('Content-Type')?.includes('json')) return 'error'
    setSession({ status: 'logged-in', email: ((await response.json()) as { email: string }).email })
    return 'logged-in'
  } catch {
    return 'error'
  }
}

export async function logOut(): Promise<boolean> {
  try {
    const response = await fetch('/api/account/logout', { method: 'POST' })
    if (!response.ok) return false
    setSession({ status: 'logged-out' })
    return true
  } catch {
    return false
  }
}

/** Sesja wygasła po stronie serwera (API odpowiedziało 401). */
export function sessionExpired() {
  if (session.status === 'logged-in') setSession({ status: 'logged-out' })
}

// GET /api/account/orders

export type MyOrder = {
  id: string
  paidAt: string
  /** `price` to cena jednej sztuki. */
  items: { slug: string; name: string; kind: ProductKind; quantity: number; price: number }[]
  total: number
}
