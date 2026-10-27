// Katalog Produktów z API (/api/products). Źródłem prawdy o cenach i
// Produktach jest backend; tu są tylko typy odpowiedzi i polskie etykiety.

export type ProductKind = 'ebook' | 'service' | 'session'

export type ProductCategory = 'ebooks' | 'analyses' | 'rituals' | 'sessions' | 'courses'

export type ProductSummary = {
  slug: string
  kind: ProductKind
  category: ProductCategory
  name: string
  tagline: string
  /** Cena w groszach. */
  price: number
  durationMinutes: number | null
}

export type DescriptionBlock =
  | { type: 'heading' | 'paragraph'; text: string }
  | { type: 'list'; items: string[] }

export type FormField = {
  key: string
  label: string
  type: 'text' | 'longText' | 'date' | 'email'
  required: boolean
}

export type ProductDetails = ProductSummary & {
  description: DescriptionBlock[]
  form: FormField[]
}

/** Kategorie w kolejności, w jakiej pokazuje je sekcja Oferta. */
export const categoryLabels: Record<ProductCategory, string> = {
  ebooks: 'Ebooki',
  analyses: 'Analizy',
  rituals: 'Rytuały',
  sessions: 'Sesje',
  courses: 'Kursy',
}

export const productCategories = Object.keys(categoryLabels) as ProductCategory[]

export const kindLabels: Record<ProductKind, string> = {
  ebook: 'Ebook',
  service: 'Usługa',
  session: 'Sesja',
}

/** Jak Klient dostaje Produkt po zakupie, zależnie od jego rodzaju. */
export const deliveryNotes: Record<ProductKind, string> = {
  ebook: 'Plik do pobrania od razu po zapłacie',
  service: 'Wykonuję ją indywidualnie dla Ciebie po zakupie',
  session: 'Spotkanie online przez Google Meet',
}

export function productPath(slug: string) {
  return `/oferta/${slug}`
}

const priceFormat = new Intl.NumberFormat('pl-PL', { style: 'currency', currency: 'PLN' })

export function formatPrice(grosze: number) {
  return priceFormat.format(grosze / 100)
}

export function formatMinutes(minutes: number) {
  const lastDigit = minutes % 10
  const lastTwoDigits = minutes % 100
  if (minutes === 1) return '1 minuta'
  if (lastDigit >= 2 && lastDigit <= 4 && (lastTwoDigits < 12 || lastTwoDigits > 14)) {
    return `${minutes} minuty`
  }
  return `${minutes} minut`
}
