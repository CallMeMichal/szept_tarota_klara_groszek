import { useState } from 'react'
import { Link } from 'react-router'
import moonStar from '../assets/home/moon-star.svg'
import { cartPath, quantityOf, useCart } from '../cart.ts'
import { homeNavItems, homeSectionPath } from '../homeSections.ts'

export function SiteHeader() {
  const [menuOpen, setMenuOpen] = useState(false)
  const cartCount = useCart().reduce((sum, item) => sum + quantityOf(item), 0)

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
            {homeNavItems.map((item) => (
              <li key={item.id}>
                <Link to={homeSectionPath(item.id)} onClick={() => setMenuOpen(false)}>
                  {item.label}
                </Link>
              </li>
            ))}
          </ul>
        </nav>

        <Link to={cartPath} className="cart-link" onClick={() => setMenuOpen(false)}>
          <svg
            className="cart-link__icon"
            viewBox="0 0 24 24"
            width={26}
            height={26}
            fill="none"
            stroke="currentColor"
            strokeWidth={1.5}
            strokeLinecap="round"
            strokeLinejoin="round"
            aria-hidden="true"
          >
            <path d="M3 4h2l2.4 11.2a1 1 0 0 0 1 .8h9.2a1 1 0 0 0 1-.8L20.5 8H6.2" />
            <circle cx="9.5" cy="19.5" r="1.25" />
            <circle cx="17" cy="19.5" r="1.25" />
          </svg>
          <span className="visually-hidden">Koszyk</span>
          {cartCount > 0 && (
            <span className="cart-link__count">
              {cartCount}
              <span className="visually-hidden"> {productsLabel(cartCount)}</span>
            </span>
          )}
        </Link>
      </div>
    </header>
  )
}

/** „1 produkt”, „2 produkty”, „5 produktów” (licząc sztuki): odmiana dla czytników ekranu. */
function productsLabel(count: number) {
  const lastDigit = count % 10
  const lastTwoDigits = count % 100
  if (count === 1) return 'produkt'
  if (lastDigit >= 2 && lastDigit <= 4 && (lastTwoDigits < 12 || lastTwoDigits > 14)) return 'produkty'
  return 'produktów'
}
