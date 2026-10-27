// Sekcje strony głównej: identyfikatory są też ścieżkami w adresie
// (/o-mnie, /oferta), więc zmiana któregoś psuje linki udostępnione wcześniej.
export const homeSections = {
  start: 'start',
  about: 'o-mnie',
  offer: 'oferta',
  testimonials: 'opinie',
  faq: 'faq',
  contact: 'kontakt',
} as const

export type HomeSectionId = (typeof homeSections)[keyof typeof homeSections]

const sectionIds = Object.values(homeSections) as HomeSectionId[]

/** Sekcje z własną ścieżką; Start to sama strona główna. */
export const linkedSectionIds = sectionIds.filter((id) => id !== homeSections.start)

export function isHomeSectionId(value: string): value is HomeSectionId {
  return (sectionIds as string[]).includes(value)
}

// Ścieżka sekcji w routerze (bez BASE_URL, który dokłada BrowserRouter).
export function homeSectionPath(sectionId: HomeSectionId) {
  return sectionId === homeSections.start ? '/' : `/${sectionId}`
}
