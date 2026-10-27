import { instagram } from '../content/home.ts'

const currentYear = new Date().getFullYear()

export function SiteFooter() {
  return (
    <footer className="site-footer">
      <p className="site-footer__brand">Szept Tarota · Klara Groszek</p>
      <p>
        <a href={instagram.url} target="_blank" rel="noreferrer">
          @{instagram.handle}
        </a>
      </p>
      <p className="site-footer__copy">© {currentYear} Szept Tarota</p>
    </footer>
  )
}
