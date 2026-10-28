import { useEffect, useState } from 'react'
import { Link, useLocation, useNavigate } from 'react-router'
import arrowGhost from '../assets/home/arrow-up-right-ghost.svg'
import aboutRitual from '../assets/home/about-ritual.jpg'
import arrowPrimary from '../assets/home/arrow-up-right-primary.svg'
import globeIcon from '../assets/home/globe.svg'
import heroTarot from '../assets/home/hero-tarot.jpg'
import intuitionSeal from '../assets/home/intuition-seal.svg'
import orbit from '../assets/home/orbit.svg'
import plusIcon from '../assets/home/plus.svg'
import { ContactForm } from '../components/ContactForm.tsx'
import { HomeSection } from '../components/HomeSection.tsx'
import { OfferCatalog } from '../components/OfferCatalog.tsx'
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
        <div className="hero__inner">
          <div className="hero__intro">
            <h1 id="start-title">
              {hero.titleLines.map((line) => (
                <span key={line}>{line}</span>
              ))}
            </h1>
            <p className="hero__tagline">{hero.tagline}</p>
            <p className="hero__lead">{hero.lead}</p>
            <div className="hero__actions">
              <Link className="button button--primary" to={homeSectionPath(homeSections.offer)}>
                Zobacz ofertę
                <img src={arrowPrimary} width={16} height={16} alt="" />
              </Link>
              <Link className="button button--ghost" to={homeSectionPath(homeSections.about)}>
                Poznaj mnie
                <img src={arrowGhost} width={16} height={16} alt="" />
              </Link>
            </div>
            <p className="hero__availability">
              <img src={globeIcon} width={15} height={15} alt="" />
              {hero.availability}
            </p>
          </div>

          <figure className="hero__visual">
            <img className="hero__orbit" src={orbit} width={640} height={640} alt="" />
            <div className="hero__arch">
              <img src={heroTarot} width={1024} height={1024} alt={hero.imageAlt} />
            </div>
            <img className="hero__seal" src={intuitionSeal} width={116} height={116} alt="" />
            <figcaption className="hero__caption">{hero.imageCaption}</figcaption>
          </figure>
        </div>
      </section>

      <section id={homeSections.about} className="about" aria-labelledby="o-mnie-title">
        <div className="about__inner">
          <figure className="about__portrait">
            <img src={aboutRitual} width={928} height={1152} alt={about.imageAlt} />
            <blockquote className="about__thought">
              <span className="about__thought-symbol" aria-hidden="true">
                ✧
              </span>
              <p>
                {about.thoughtLines.map((line) => (
                  <span key={line}>{line}</span>
                ))}
              </p>
            </blockquote>
          </figure>

          <div className="about__story">
            <h2 id="o-mnie-title">
              {about.titleLines.map((line) => (
                <span key={line}>{line}</span>
              ))}
            </h2>
            {about.paragraphs.map((paragraph) => (
              <p key={paragraph} className="about__text">
                {paragraph}
              </p>
            ))}
            <p className="about__signature">{about.signature}</p>
          </div>
        </div>
      </section>

      <HomeSection id={homeSections.offer} title={offer.title}>
        <p className="section__lead">{offer.lead}</p>
        <OfferCatalog onLoaded={() => setOfferLoaded(true)} />
      </HomeSection>

      <HomeSection
        id={homeSections.testimonials}
        title={testimonials.title}
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

      <section id={homeSections.faq} className="faq" aria-labelledby="faq-title">
        <div className="faq__inner">
          <div className="faq__intro">
            <p className="section-eyebrow">{faq.eyebrow}</p>
            <h2 id="faq-title">
              {faq.titleLines.map((line) => (
                <span key={line}>{line}</span>
              ))}
            </h2>
          </div>
          <div className="faq__list">
            {faq.items.map((item) => (
              <details key={item.question}>
                <summary>
                  {item.question}
                  <img src={plusIcon} width={18} height={18} alt="" />
                </summary>
                <p>{item.answer}</p>
              </details>
            ))}
          </div>
        </div>
      </section>

      <section id={homeSections.contact} className="contact" aria-labelledby="kontakt-title">
        <div className="contact__card">
          <div className="contact__invite">
            <h2 id="kontakt-title">
              {contact.titleLines.map((line) => (
                <span key={line}>{line}</span>
              ))}
            </h2>
            <p className="contact__lead">{contact.lead}</p>
            <ContactForm
              alternative={
                <a
                  className="button button--ghost"
                  href={instagram.url}
                  target="_blank"
                  rel="noreferrer"
                >
                  Napisz na Instagramie @{instagram.handle}
                  <img src={arrowGhost} width={16} height={16} alt="" />
                </a>
              }
            />
          </div>
        </div>
      </section>
    </>
  )
}
