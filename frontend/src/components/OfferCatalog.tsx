import { useEffect } from 'react'
import { Link } from 'react-router'
import {
  categoryLabels,
  deliveryNotes,
  formatMinutes,
  formatPrice,
  kindLabels,
  productCategories,
  productPath,
  type ProductSummary,
} from '../catalog.ts'
import { useApi } from '../useApi.ts'
import { ProductImage } from './ProductImage.tsx'

type OfferCatalogProps = {
  /** Wywoływane, gdy Produkty z API są już na stronie (zmienia się jej wysokość). */
  onLoaded?: () => void
}

/** Produkty z katalogu w kategoriach sekcji Oferta; puste kategorie są pomijane. */
export function OfferCatalog({ onLoaded }: OfferCatalogProps) {
  const products = useApi<ProductSummary[]>('/api/products')
  const loaded = products.status === 'ok'

  useEffect(() => {
    if (loaded) onLoaded?.()
  }, [loaded, onLoaded])

  if (products.status === 'loading') {
    return <p className="section__lead">Rozkładam karty…</p>
  }

  if (products.status !== 'ok') {
    return (
      <p className="section__lead">
        Oferta jest chwilowo niedostępna. Zajrzyj tu za chwilę albo napisz do mnie na Instagramie.
      </p>
    )
  }

  const categories = productCategories
    .map((category) => ({
      category,
      products: products.data.filter((product) => product.category === category),
    }))
    .filter((group) => group.products.length > 0)

  return (
    <>
      <nav aria-label="Kategorie oferty">
        <ul className="offer-categories">
          {categories.map(({ category }) => (
            <li key={category}>
              <button
                type="button"
                onClick={() => document.getElementById(`oferta-${category}`)?.scrollIntoView()}
              >
                {categoryLabels[category]}
              </button>
            </li>
          ))}
        </ul>
      </nav>

      {categories.map(({ category, products }) => (
        <section
          key={category}
          id={`oferta-${category}`}
          className="offer-group"
          aria-labelledby={`oferta-${category}-title`}
        >
          <h3 id={`oferta-${category}-title`}>{categoryLabels[category]}</h3>
          <ul className="product-grid">
            {products.map((product) => (
              <li key={product.slug}>
                <ProductCard product={product} />
              </li>
            ))}
          </ul>
        </section>
      ))}
    </>
  )
}

function ProductCard({ product }: { product: ProductSummary }) {
  return (
    <Link className="product-card" to={productPath(product.slug)}>
      <ProductImage className="product-card__image" imageUrl={product.imageUrl} />
      <div className="product-card__body">
        <p className="product-card__kind">{kindLabels[product.kind]}</p>
        <h4>{product.name}</h4>
        <p className="product-card__tagline">{product.tagline}</p>
        <p className="product-card__meta">
          {deliveryNotes[product.kind]}
          {product.durationMinutes && ` · ${formatMinutes(product.durationMinutes)}`}
        </p>
        <p className="product-card__price">{formatPrice(product.price)}</p>
      </div>
    </Link>
  )
}
