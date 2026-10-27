import { useState } from 'react'
import { Link } from 'react-router'
import moonStar from '../assets/home/moon-star.svg'
import { homeSectionPath, homeSections } from '../homeSections.ts'

const navItems = [
  { id: homeSections.about, label: 'O mnie' },
  { id: homeSections.offer, label: 'Oferta' },
  { id: homeSections.testimonials, label: 'Opinie' },
  { id: homeSections.faq, label: 'FAQ' },
  { id: homeSections.contact, label: 'Kontakt' },
]

export function SiteHeader() {
  const [menuOpen, setMenuOpen] = useState(false)

  return (
    <header className="site-header">
      <div className="site-header__inner">
        <Link to="/" className="brand" onClick={() => setMenuOpen(false)}>
          <img className="brand__moon" src={moonStar} width={32} height={32} alt="" />
          <span>Szept Tarota</span>
        </Link>

        <button
          type="button"
          className="menu-toggle"
          aria-expanded={menuOpen}
          aria-controls="site-nav"
          onClick={() => setMenuOpen((open) => !open)}
        >
          <span className="menu-toggle__bars" aria-hidden="true" />
          <span className="visually-hidden">
            {menuOpen ? 'Zamknij menu' : 'Otwórz menu'}
          </span>
        </button>

        <nav
          id="site-nav"
          className={menuOpen ? 'site-nav site-nav--open' : 'site-nav'}
          aria-label="Główna nawigacja"
        >
          <ul>
            {navItems.map((item) => (
              <li key={item.id}>
                <Link to={homeSectionPath(item.id)} onClick={() => setMenuOpen(false)}>
                  {item.label}
                </Link>
              </li>
            ))}
          </ul>
        </nav>
      </div>
    </header>
  )
}
