import { useEffect } from 'react'
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
import { homeSectionHref, homeSections } from '../homeSections.ts'

export function HomePage() {
  // Przeglądarka szuka kotwicy z adresu, zanim React wyrenderuje sekcje,
  // więc wejście na /#o-mnie przewijamy ręcznie po pierwszym renderze.
  useEffect(() => {
    // Bez decodeURIComponent: identyfikatory sekcji są ASCII, a błędny
    // adres (np. #%E0) rzucałby wyjątek i wyłączał całą stronę.
    const sectionId = window.location.hash.slice(1)
    if (sectionId) document.getElementById(sectionId)?.scrollIntoView()
  }, [])

  return (
    <>
      <section id={homeSections.start} className="hero" aria-labelledby="start-title">
        <Moon className="hero__moon" />
        <p className="eyebrow">{hero.eyebrow}</p>
        <h1 id="start-title">{hero.title}</h1>
        <p className="hero__lead">{hero.lead}</p>
        <div className="hero__actions">
          <a className="button button--primary" href={homeSectionHref(homeSections.offer)}>
            Zobacz ofertę
          </a>
          <a className="button button--ghost" href={homeSectionHref(homeSections.about)}>
            Poznaj mnie
          </a>
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
        <OfferCatalog />
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
