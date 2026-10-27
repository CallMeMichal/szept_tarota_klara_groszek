import { Link } from 'react-router'
import { Moon } from '../components/Moon.tsx'

export function NotFoundPage() {
  return (
    <section className="section not-found" aria-labelledby="not-found-title">
      <Moon className="not-found__moon" />
      <h1 id="not-found-title">Tej strony tu nie ma</h1>
      <p className="section__lead">
        Karty milczą w tej sprawie. Wróć na stronę główną i zacznij od nowa.
      </p>
      <Link className="button button--primary" to="/">
        Strona główna
      </Link>
    </section>
  )
}
