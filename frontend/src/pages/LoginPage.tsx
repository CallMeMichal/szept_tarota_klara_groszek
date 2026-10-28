import { useEffect, useState, type FormEvent } from 'react'
import { Link, useSearchParams } from 'react-router'
import { accountPath, localPathOrNull, requestLoginLink, useSession, type LoginLinkResult } from '../account.ts'
import { usePageTitle } from '../usePageTitle.ts'

type State = { state: 'idle' } | { state: 'sending' } | { state: 'done'; email: string; result: LoginLinkResult }

/**
 * Logowanie bez hasła: Klient podaje e-mail i dostaje link logowania. Konto zakłada się samo
 * przy pierwszym logowaniu, więc ta sama strona służy też do „rejestracji”.
 */
export function LoginPage() {
  usePageTitle('Logowanie')
  const session = useSession()
  const [searchParams] = useSearchParams()
  const returnTo = localPathOrNull(searchParams.get('dalej'))
  const [status, setStatus] = useState<State>({ state: 'idle' })

  useEffect(() => {
    window.scrollTo({ top: 0 })
  }, [])

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const email = String(new FormData(event.currentTarget).get('email') ?? '').trim()
    setStatus({ state: 'sending' })
    setStatus({ state: 'done', email, result: await requestLoginLink(email, returnTo) })
  }

  if (session.status === 'logged-in') {
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

  if (status.state === 'done' && status.result.status === 'sent') {
    return (
      <section className="section login-page" aria-labelledby="login-title">
        <p className="eyebrow">Logowanie</p>
        <h1 id="login-title">Sprawdź skrzynkę</h1>
        <p className="section__lead" role="status">
          Wysłałam link logowania na adres {status.email}. Kliknij go w ciągu 15 minut, na tym samym urządzeniu. Jeśli
          nie widzisz maila, zajrzyj do spamu.
        </p>
        <button type="button" className="button button--ghost" onClick={() => setStatus({ state: 'idle' })}>
          Wyślij na inny adres
        </button>
      </section>
    )
  }

  const errors = status.state === 'done' && status.result.status === 'invalid' ? status.result.errors : {}
  const message = status.state === 'done' ? resultMessage(status.result) : null

  return (
    <section className="section login-page" aria-labelledby="login-title">
      <p className="eyebrow">Konto</p>
      <h1 id="login-title">Zaloguj się</h1>
      <p className="section__lead">
        Podaj swój adres email, a wyślę Ci link do logowania. Bez hasła: jeśli logujesz się pierwszy raz, Konto założy
        się samo.
      </p>
      <form className="checkout-form login-form" onSubmit={(event) => void submit(event)}>
        <label className="checkout-form__field">
          <span>Email</span>
          <input name="email" type="email" autoComplete="email" required maxLength={200} />
          {errors.email && (
            <span className="checkout-form__error" role="alert">
              {errors.email.join(' ')}
            </span>
          )}
        </label>
        <button className="button button--primary" type="submit" disabled={status.state === 'sending'}>
          {status.state === 'sending' ? 'Wysyłam link…' : 'Wyślij link logowania'}
        </button>
        {message && (
          <p className="checkout-form__error" role="alert">
            {message}
          </p>
        )}
      </form>
    </section>
  )
}

function resultMessage(result: LoginLinkResult): string | null {
  switch (result.status) {
    case 'too-many':
      return 'Wysłałam już kilka linków. Spróbuj ponownie za kilka minut.'
    case 'unavailable':
      return 'Logowanie nie jest jeszcze uruchomione. Napisz do mnie na Instagramie.'
    case 'error':
      return 'Nie udało się wysłać linku. Spróbuj ponownie za chwilę.'
    default:
      return null
  }
}
