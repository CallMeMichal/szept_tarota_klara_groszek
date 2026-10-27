// Sekcje strony głównej: identyfikatory są kotwicami w adresie (#o-mnie),
// więc zmiana któregoś psuje linki udostępnione wcześniej.
export const homeSections = {
  start: 'start',
  about: 'o-mnie',
  offer: 'oferta',
  testimonials: 'opinie',
  faq: 'faq',
  contact: 'kontakt',
} as const

export type HomeSectionId = (typeof homeSections)[keyof typeof homeSections]

// Kotwice sekcji strony głównej działają z każdej podstrony: z innej trasy
// przeładowują stronę główną i przewijają do sekcji, na stronie głównej
// tylko przewijają. BASE_URL uwzględnia ścieżkę repo na GitHub Pages.
export function homeSectionHref(sectionId: HomeSectionId) {
  return `${import.meta.env.BASE_URL}#${sectionId}`
}
