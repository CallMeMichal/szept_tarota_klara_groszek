// Konto Klienta (AccountEndpoints.cs). Nie ma haseł: Klient podaje e-mail, dostaje
// jednorazowy link logowania, a strona pod tym linkiem wysyła token do API, które
// ustawia ciasteczko sesji. Stan logowania trzymamy w jednym miejscu, żeby nagłówek,
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
/** Strona, na którą prowadzi link z maila (LoginService.cs). */
export const loginLinkPath = '/logowanie/link'
export const accountPath = '/konto'

/**
 * Ścieżka tej strony (np. `/koszyk`) albo `null`, gdy to adres innej strony; ta sama zasada co
 * w LoginService.cs, żeby `dalej` z adresu nie wyprowadził Klienta poza stronę.
 */
export function localPathOrNull(path: string | null | undefined): string | null {
  if (!path || path.length > 200 || !path.startsWith('/') || path.startsWith('//') || path.includes('\\')) return null
  // oxlint-disable-next-line no-control-regex -- właśnie znaków sterujących szukamy
  return /[\u0000-\u001f\u007f]/.test(path) ? null : path
}

/** Logowanie z powrotem na `returnTo`, np. do Koszyka. */
export const loginPathFor = (returnTo: string) => `${loginPath}?dalej=${encodeURIComponent(returnTo)}`

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
  return () => listeners.delete(listener)
}

/** Czy Klient jest zalogowany; sesję sprawdza raz, przy pierwszym użyciu. */
export function useSession(): Session {
  return useSyncExternalStore(subscribe, () => session)
}

export type LoginLinkResult =
  | { status: 'sent' }
  | { status: 'invalid'; errors: Record<string, string[]> }
  | { status: 'too-many' }
  | { status: 'unavailable' }
  | { status: 'error' }

/** Prosi o link logowania na `email`; po zalogowaniu Klient wróci na `returnTo`. */
export async function requestLoginLink(email: string, returnTo: string | null): Promise<LoginLinkResult> {
  try {
    const response = await fetch('/api/account/login-link', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json', Accept: 'application/json' },
      body: JSON.stringify({ email, returnTo }),
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

export type LogInResult = { status: 'logged-in'; returnTo: string | null } | { status: 'invalid-link' } | { status: 'error' }

/** Loguje tokenem z linku; link działa raz i przez 15 minut. */
export async function logIn(token: string): Promise<LogInResult> {
  try {
    const response = await fetch('/api/account/login', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json', Accept: 'application/json' },
      body: JSON.stringify({ token }),
    })
    if (response.status === 401) return { status: 'invalid-link' }
    if (!response.ok || !response.headers.get('Content-Type')?.includes('json')) return { status: 'error' }
    const { email, returnTo } = (await response.json()) as { email: string; returnTo: string | null }
    setSession({ status: 'logged-in', email })
    return { status: 'logged-in', returnTo }
  } catch {
    return { status: 'error' }
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
