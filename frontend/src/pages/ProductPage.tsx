import { useEffect } from 'react'
import { Link, useParams } from 'react-router'
import { ProductImage } from '../components/ProductImage.tsx'
import { addEbookToCart, cartPath, useCart } from '../cart.ts'
import {
  categoryLabels,
  deliveryNotes,
  formatMinutes,
  formatPrice,
  kindLabels,
  type DescriptionBlock,
  type ProductDetails,
} from '../catalog.ts'
import { homeSectionPath, homeSections } from '../homeSections.ts'
import { useApi } from '../useApi.ts'
import { NotFoundPage } from './NotFoundPage.tsx'

export function ProductPage() {
  const { slug = '' } = useParams()
  const product = useApi<ProductDetails>(`/api/products/${encodeURIComponent(slug)}`)
  const productName = product.status === 'ok' ? product.data.name : null

  // Router nie przewija sam: Produkt otwarty z połowy strony głównej
  // zaczynamy od góry.
  useEffect(() => {
    window.scrollTo({ top: 0 })
  }, [slug])

  useEffect(() => {
    if (!productName) return
    const siteTitle = document.title
    document.title = `${productName} · Szept Tarota`
    return () => {
      document.title = siteTitle
    }
  }, [productName])

  if (product.status === 'not-found') return <NotFoundPage />

  return (
    <article className="section product-page" aria-busy={product.status === 'loading'}>
      <p className="product-page__back">
        <Link to={homeSectionPath(homeSections.offer)}>← Cała oferta</Link>
      </p>

      {product.status === 'loading' && <p className="section__lead">Rozkładam karty…</p>}

      {product.status === 'error' && (
        <p className="section__lead">
          Nie udało się wczytać tego Produktu. Odśwież stronę za chwilę albo napisz do mnie na
          Instagramie.
        </p>
      )}

      {product.status === 'ok' && <ProductContent product={product.data} />}
    </article>
  )
}

function ProductContent({ product }: { product: ProductDetails }) {
  return (
    <>
      <header className="product-page__header">
        <ProductImage className="product-page__image" slug={product.slug} />
        <div className="product-page__summary">
          <p className="eyebrow">{categoryLabels[product.category]}</p>
          <h1>{product.name}</h1>
          <p className="product-page__tagline">{product.tagline}</p>
          <p className="product-page__price">{formatPrice(product.price)}</p>
          <dl className="product-page__facts">
            <div>
              <dt>Rodzaj</dt>
              <dd>{kindLabels[product.kind]}</dd>
            </div>
            {product.durationMinutes && (
              <div>
                <dt>Długość</dt>
                <dd>{formatMinutes(product.durationMinutes)}</dd>
              </div>
            )}
          </dl>
          <p className="product-page__kind-note">{deliveryNotes[product.kind]}</p>
          {product.kind === 'ebook' && <AddEbookToCart product={product} />}
        </div>
      </header>

      <div className="product-page__description">
        {product.description.map((block, index) => (
          <DescriptionBlockView key={index} block={block} />
        ))}
      </div>
    </>
  )
}

function AddEbookToCart({ product }: { product: ProductDetails }) {
  const inCart = useCart().some((item) => item.slug === product.slug)

  // Ebook może być w Koszyku najwyżej raz, więc po dodaniu zamiast przycisku
  // prowadzimy do Koszyka.
  return inCart ? (
    <p className="product-page__cart">
      <span>Ten Ebook jest już w Koszyku.</span>
      <Link className="button button--ghost" to={cartPath}>
        Przejdź do Koszyka
      </Link>
    </p>
  ) : (
    <p className="product-page__cart">
      <button type="button" className="button button--primary" onClick={() => addEbookToCart(product)}>
        Dodaj do koszyka
      </button>
    </p>
  )
}

function DescriptionBlockView({ block }: { block: DescriptionBlock }) {
  switch (block.type) {
    case 'heading':
      return <h2>{block.text}</h2>
    case 'paragraph':
      return <p>{block.text}</p>
    case 'list':
      return (
        <ul>
          {block.items.map((item) => (
            <li key={item}>{item}</li>
          ))}
        </ul>
      )
  }
}
