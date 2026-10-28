import { useEffect, useState, type FormEvent } from 'react'
import { Link, useNavigate, useSearchParams } from 'react-router'
import {
  accountPath,
  googleLoginUrl,
  localPathOrNull,
  logIn,
  requestLoginCode,
  useSession,
  type LoginCodeResult,
} from '../account.ts'
import { usePageTitle } from '../usePageTitle.ts'

type Step =
  | { step: 'email'; sending: boolean; result: LoginCodeResult | null }
  /** `error` to komunikat o nieudanym logowaniu albo ponownym wysłaniu kodu. */
  | { step: 'code'; email: string; sending: boolean; error: string | null; resent: boolean }

/**
 * Logowanie bez hasła w dwóch krokach: Klient podaje e-mail, dostaje na niego kod i wpisuje go tutaj
 * (albo od razu loguje się przez Google).
 * Konto zakłada się samo przy pierwszym logowaniu, więc ta sama strona służy też do „rejestracji”.
 * Po zalogowaniu Klient wraca na `?dalej=` (np. do Koszyka), a bez niego trafia do Konta.
 */
export function LoginPage() {
  usePageTitle('Logowanie')
  const session = useSession()
  const navigate = useNavigate()
  const [searchParams] = useSearchParams()
  const returnTo = localPathOrNull(searchParams.get('dalej'))
  const [state, setState] = useState<Step>({ step: 'email', sending: false, result: null })

  useEffect(() => {
    window.scrollTo({ top: 0 })
  }, [])

  /** Wysyła kod; `resend` to ponowne wysłanie z kroku z kodem. */
  async function sendCode(email: string, resend: boolean) {
    const result = await requestLoginCode(email)
    if (result.status === 'sent') setState({ step: 'code', email, sending: false, error: null, resent: resend })
    else if (resend) setState({ step: 'code', email, sending: false, error: emailResultMessage(result), resent: false })
    else setState({ step: 'email', sending: false, result })
  }

  async function submitEmail(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const email = String(new FormData(event.currentTarget).get('email') ?? '').trim()
    setState({ step: 'email', sending: true, result: null })
    await sendCode(email, false)
  }

  async function submitCode(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (state.step !== 'code') return
    const code = String(new FormData(event.currentTarget).get('code') ?? '').replace(/\s/g, '')
    setState({ ...state, sending: true, error: null, resent: false })
    const result = await logIn(state.email, code)
    if (result === 'logged-in') {
      navigate(returnTo ?? accountPath, { replace: true })
      return
    }
    setState({
      ...state,
      sending: false,
      resent: false,
      error:
        result === 'invalid-code'
          ? 'Ten kod nie działa: jest błędny, wygasł albo był już użyty. Sprawdź go albo wyślij nowy.'
          : 'Nie udało się zalogować. Spróbuj ponownie za chwilę.',
    })
  }

  if (session.status === 'logged-in' && !(state.step === 'code' && state.sending)) {
    return (
      <section className="section login-page" aria-labelledby="login-title">
        <p className="eyebrow">Konto</p>
        <h1 id="login-title">Jesteś już na swoim Koncie</h1>
        <p className="section__lead">Zalogowano na adres {session.email}.</p>
        <Link className="button button--primary" to={returnTo ?? accountPath}>
          {returnTo ? 'Wróć' : 'Przejdź do Konta'}
        </Link>
      </section>
    )
  }

  if (state.step === 'code') {
    return (
      <section className="section login-page" aria-labelledby="login-title">
        <p className="eyebrow">Logowanie</p>
        <h1 id="login-title">Wpisz kod z maila</h1>
        <p className="section__lead" role="status">
          {state.resent ? 'Wysłałam nowy kod' : 'Wysłałam kod'} na adres {state.email}. Kod ma 6 cyfr i działa przez
          15 minut. Jeśli nie widzisz maila, zajrzyj do spamu.
        </p>
        {/* Osobne klucze formularzy, żeby React nie przeniósł wpisanego adresu do pola kodu. */}
        <form key="code" className="checkout-form login-form" onSubmit={(event) => void submitCode(event)}>
          <label className="checkout-form__field">
            <span>Kod logowania</span>
            <input
              name="code"
              inputMode="numeric"
              autoComplete="one-time-code"
              pattern="[0-9 ]{6,7}"
              maxLength={7}
              required
              autoFocus
              className="login-form__code"
            />
          </label>
          <button className="button button--primary" type="submit" disabled={state.sending}>
            {state.sending ? 'Loguję…' : 'Zaloguj się'}
          </button>
          {state.error && (
            <p className="checkout-form__error" role="alert">
              {state.error}
            </p>
          )}
          <div className="login-form__actions">
            <button
              type="button"
              className="button button--ghost"
              disabled={state.sending}
              onClick={() => {
                setState({ ...state, sending: true, error: null })
                void sendCode(state.email, true)
              }}
            >
              Wyślij nowy kod
            </button>
            <button
              type="button"
              className="button button--ghost"
              onClick={() => setState({ step: 'email', sending: false, result: null })}
            >
              Zmień adres
            </button>
          </div>
        </form>
      </section>
    )
  }

  const errors = state.result?.status === 'invalid' ? state.result.errors : {}
  const message = state.result
    ? emailResultMessage(state.result)
    : searchParams.get('google') === 'nieudane'
      ? 'Nie udało się zalogować przez Google. Spróbuj jeszcze raz albo zaloguj się kodem z maila.'
      : null

  return (
    <section className="section login-page" aria-labelledby="login-title">
      <h1 id="login-title">Zaloguj się</h1>
      <form key="email" className="checkout-form login-form" onSubmit={(event) => void submitEmail(event)}>
        <label className="checkout-form__field">
          <span>Email</span>
          <input name="email" type="email" autoComplete="email" required maxLength={200} />
          {errors.email && (
            <span className="checkout-form__error" role="alert">
              {errors.email.join(' ')}
            </span>
          )}
        </label>
        <button className="button button--primary" type="submit" disabled={state.sending}>
          {state.sending ? 'Wysyłam kod…' : 'Wyślij kod'}
        </button>
        {message && (
          <p className="checkout-form__error" role="alert">
            {message}
          </p>
        )}
        <p className="login-form__divider">albo</p>
        {/* Zwykły link, nie nawigacja Routera: przeglądarka ma przejść przez API do Google i z powrotem. */}
        <a className="button button--ghost" href={googleLoginUrl(returnTo)}>
          <GoogleLogo />
          Zaloguj przez Google
        </a>
      </form>
    </section>
  )
}

/** Wielokolorowe „G” Google, którego wymagają wytyczne Google dla przycisku logowania. */
function GoogleLogo() {
  return (
    <svg width="18" height="18" viewBox="0 0 48 48" aria-hidden="true" focusable="false">
      <path
        fill="#EA4335"
        d="M24 9.5c3.54 0 6.71 1.22 9.21 3.6l6.85-6.85C35.9 2.38 30.47 0 24 0 14.62 0 6.51 5.38 2.56 13.22l7.98 6.19C12.43 13.72 17.74 9.5 24 9.5z"
      />
      <path
        fill="#4285F4"
        d="M46.98 24.55c0-1.57-.15-3.09-.38-4.55H24v9.02h12.94c-.58 2.96-2.26 5.48-4.78 7.18l7.73 6c4.51-4.18 7.09-10.36 7.09-17.65z"
      />
      <path
        fill="#FBBC05"
        d="M10.53 28.59c-.48-1.45-.76-2.99-.76-4.59s.27-3.14.76-4.59l-7.98-6.19C.92 16.46 0 20.12 0 24c0 3.88.92 7.54 2.56 10.78l7.97-6.19z"
      />
      <path
        fill="#34A853"
        d="M24 48c6.48 0 11.93-2.13 15.89-5.81l-7.73-6c-2.15 1.45-4.92 2.3-8.16 2.3-6.26 0-11.57-4.22-13.47-9.91l-7.98 6.19C6.51 42.62 14.62 48 24 48z"
      />
    </svg>
  )
}

function emailResultMessage(result: LoginCodeResult): string | null {
  switch (result.status) {
    case 'too-many':
      return 'Wysłałam już kilka kodów. Spróbuj ponownie za kilka minut.'
    case 'unavailable':
      return 'Logowanie nie jest jeszcze uruchomione. Napisz do mnie na Instagramie.'
    case 'error':
      return 'Nie udało się wysłać kodu. Spróbuj ponownie za chwilę.'
    default:
      return null
  }
}
