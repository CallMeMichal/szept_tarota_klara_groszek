import { useEffect, useState } from 'react'
import { Link, useNavigate, useSearchParams } from 'react-router'
import { accountPath, localPathOrNull, logIn, loginPath, type LogInResult } from '../account.ts'
import { usePageTitle } from '../usePageTitle.ts'

// Link działa raz, a StrictMode w developmencie uruchamia efekt dwa razy, więc każdy token
// wysyłamy najwyżej raz i oba uruchomienia czekają na tę samą odpowiedź.
const attempts = new Map<string, Promise<LogInResult>>()

function logInOnce(token: string) {
  let attempt = attempts.get(token)
  if (!attempt) {
    attempt = logIn(token)
    attempts.set(token, attempt)
  }
  return attempt
}

/** Strona z linku w mailu: loguje tokenem i przenosi tam, skąd Klient przyszedł (albo do Konta). */
export function LoginLinkPage() {
  usePageTitle('Logowanie')
  const [searchParams] = useSearchParams()
  const token = searchParams.get('token') ?? ''
  const navigate = useNavigate()
  const [failure, setFailure] = useState<'invalid-link' | 'error' | null>(null)

  useEffect(() => {
    let active = true
    void logInOnce(token).then((result) => {
      if (!active) return
      if (result.status === 'logged-in') navigate(localPathOrNull(result.returnTo) ?? accountPath, { replace: true })
      else setFailure(result.status)
    })
    return () => {
      active = false
    }
  }, [token, navigate])

  return (
    <section className="section login-page" aria-labelledby="login-title" aria-busy={failure === null}>
      <p className="eyebrow">Logowanie</p>
      <h1 id="login-title">
        {failure === null ? 'Loguję…' : failure === 'invalid-link' ? 'Ten link już nie działa' : 'Nie udało się zalogować'}
      </h1>
      {failure !== null && (
        <>
          <p className="section__lead">
            {failure === 'invalid-link'
              ? 'Link logowania działa raz i tylko przez 15 minut. Poproś o nowy.'
              : 'Spróbuj ponownie za chwilę albo poproś o nowy link.'}
          </p>
          <Link className="button button--primary" to={loginPath}>
            Poproś o nowy link
          </Link>
        </>
      )}
    </section>
  )
}
