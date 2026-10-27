import { useEffect } from 'react'
import { useParams } from 'react-router'
import { ProductImage } from '../components/ProductImage.tsx'
import {
  categoryLabels,
  deliveryNotes,
  formatMinutes,
  formatPrice,
  kindLabels,
  type DescriptionBlock,
  type ProductDetails,
} from '../catalog.ts'
import { homeSectionHref, homeSections } from '../homeSections.ts'
import { useApi } from '../useApi.ts'
import { NotFoundPage } from './NotFoundPage.tsx'

export function ProductPage() {
  const { slug = '' } = useParams()
  const product = useApi<ProductDetails>(`/api/products/${encodeURIComponent(slug)}`)
  const productName = product.status === 'ok' ? product.data.name : null

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
        <a href={homeSectionHref(homeSections.offer)}>← Cała oferta</a>
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
        <ProductImage className="product-page__image" imageUrl={product.imageUrl} />
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
