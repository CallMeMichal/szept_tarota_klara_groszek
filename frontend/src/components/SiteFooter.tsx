import { Link } from 'react-router'
import arrowUp from '../assets/home/arrow-up.svg'
import instagramIcon from '../assets/home/instagram-small.svg'
import moonStar from '../assets/home/moon-star-footer.svg'
import { instagram } from '../content/home.ts'
import { homeNavItems, homeSectionPath } from '../homeSections.ts'

const currentYear = new Date().getFullYear()

export function SiteFooter() {
  return (
    <footer className="site-footer">
      <div className="site-footer__inner">
        <div className="site-footer__top">
          <div className="site-footer__brand">
            <Link to="/" className="site-footer__logo">
              <img src={moonStar} width={46} height={46} alt="" />
              <span>Szept Tarota</span>
            </Link>
          </div>

          <nav aria-label="Nawigacja w stopce">
            <ul className="site-footer__nav">
              {homeNavItems.map((item) => (
                <li key={item.id}>
                  <Link to={homeSectionPath(item.id)}>{item.label}</Link>
                </li>
              ))}
            </ul>
          </nav>
        </div>

        <div className="site-footer__bottom">
          <p>© {currentYear} Szept Tarota</p>
          <a
            className="site-footer__instagram"
            href={instagram.url}
            target="_blank"
            rel="noreferrer"
          >
            <img src={instagramIcon} width={15} height={15} alt="" />@{instagram.handle}
          </a>
          <Link to="/" className="site-footer__top-link">
            Na początek
            <img src={arrowUp} width={15} height={15} alt="" />
          </Link>
        </div>
      </div>
    </footer>
  )
}
