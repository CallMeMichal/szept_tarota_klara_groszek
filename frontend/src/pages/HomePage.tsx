import { useEffect, useState } from 'react'
import { Link, useLocation, useNavigate } from 'react-router'
import { HomeSection } from '../components/HomeSection.tsx'
import { OfferCatalog } from '../components/OfferCatalog.tsx'
import { Moon } from '../components/Moon.tsx'
import { PlaceholderNote } from '../components/PlaceholderNote.tsx'
import {
  about,
  contact,
  faq,
  hero,
  instagram,
  offer,
  testimonials,
} from '../content/home.ts'
import {
  homeSectionPath,
  homeSections,
  isHomeSectionId,
  type HomeSectionId,
} from '../homeSections.ts'

// Sekcje pod Ofertą: przesuwają się w dół, gdy Produkty dojdą z API.
const sectionsBelowOffer: HomeSectionId[] = [
  homeSections.testimonials,
  homeSections.faq,
  homeSections.contact,
]

type HomePageProps = {
  /** Sekcja z adresu (/oferta, /kontakt), do której przewijamy. */
  section: HomeSectionId
}

export function HomePage({ section }: HomePageProps) {
  const location = useLocation()
  const navigate = useNavigate()
  const [offerLoaded, setOfferLoaded] = useState(false)

  // Stare linki z kotwicą (/#o-mnie) przekierowujemy na ścieżkę sekcji.
  // Bez decodeURIComponent: identyfikatory sekcji są ASCII, a błędny
  // adres (np. #%E0) rzucałby wyjątek i wyłączał całą stronę.
  const legacySection = location.hash.slice(1)
  useEffect(() => {
    if (isHomeSectionId(legacySection)) {
      navigate(homeSectionPath(legacySection), { replace: true })
    }
  }, [legacySection, navigate])

  // Przewijamy przy każdym kliknięciu linku do sekcji (location.key), także
  // gdy adres się nie zmienia, bo użytkownik mógł w międzyczasie przewinąć.
  useEffect(() => {
    if (section === homeSections.start) {
      window.scrollTo({ top: 0 })
    } else {
      document.getElementById(section)?.scrollIntoView()
    }
  }, [section, location.key])

  // Wejście wprost na /kontakt: Produkty doładowują się nad sekcją i
  // spychają ją w dół, więc po ich wczytaniu przewijamy jeszcze raz.
  useEffect(() => {
    if (offerLoaded && sectionsBelowOffer.includes(section)) {
      document.getElementById(section)?.scrollIntoView()
    }
  }, [offerLoaded, section])

  return (
    <>
      <section id={homeSections.start} className="hero" aria-labelledby="start-title">
        <Moon className="hero__moon" />
        <p className="eyebrow">{hero.eyebrow}</p>
        <h1 id="start-title">{hero.title}</h1>
        <p className="hero__lead">{hero.lead}</p>
        <div className="hero__actions">
          <Link className="button button--primary" to={homeSectionPath(homeSections.offer)}>
            Zobacz ofertę
          </Link>
          <Link className="button button--ghost" to={homeSectionPath(homeSections.about)}>
            Poznaj mnie
          </Link>
        </div>
        <dl className="hero__stats">
          {hero.stats.map((stat) => (
            <div key={stat.label}>
              <dt>{stat.label}</dt>
              <dd>{stat.value}</dd>
            </div>
          ))}
        </dl>
        <PlaceholderNote show={hero.placeholder} />
      </section>

      <HomeSection
        id={homeSections.about}
        title={about.title}
        placeholder={about.placeholder}
        className="about"
      >
        <div className="about__portrait" aria-hidden="true">
          <Moon className="about__moon" />
        </div>
        <div className="about__text">
          {about.paragraphs.map((paragraph) => (
            <p key={paragraph}>{paragraph}</p>
          ))}
        </div>
      </HomeSection>

      <HomeSection id={homeSections.offer} title={offer.title}>
        <p className="section__lead">{offer.lead}</p>
        <OfferCatalog onLoaded={() => setOfferLoaded(true)} />
      </HomeSection>

      <HomeSection
        id={homeSections.testimonials}
        title={testimonials.title}
        placeholder={testimonials.placeholder}
      >
        <ul className="testimonials">
          {testimonials.items.map((item) => (
            <li key={item.author}>
              <figure>
                <blockquote>„{item.quote}”</blockquote>
                <figcaption>{item.author}</figcaption>
              </figure>
            </li>
          ))}
        </ul>
      </HomeSection>

      <HomeSection id={homeSections.faq} title={faq.title} placeholder={faq.placeholder}>
        <div className="faq">
          {faq.items.map((item) => (
            <details key={item.question}>
              <summary>{item.question}</summary>
              <p>{item.answer}</p>
            </details>
          ))}
        </div>
      </HomeSection>

      <HomeSection id={homeSections.contact} title={contact.title} placeholder={contact.placeholder}>
        <p className="section__lead">{contact.lead}</p>
        <p>
          <a
            className="button button--primary"
            href={instagram.url}
            target="_blank"
            rel="noreferrer"
          >
            Napisz na Instagramie @{instagram.handle}
          </a>
        </p>
      </HomeSection>
    </>
  )
}
