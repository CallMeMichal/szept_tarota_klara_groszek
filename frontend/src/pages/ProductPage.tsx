import { useEffect } from 'react'
import { Link, useParams } from 'react-router'
import { ProductImage } from '../components/ProductImage.tsx'
import { AddToCartDialog } from '../components/AddToCartDialog.tsx'
import { addEbookToCart } from '../cart.ts'
import {
  categoryLabels,
  deliveryNotes,
  formatMinutes,
  formatPrice,
  type DescriptionBlock,
  type ProductDetails,
} from '../catalog.ts'
import { notifyAddedToCart } from '../cartNotice.ts'
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

/**
 * Układ jak w sklepach internetowych: po lewej grafika (na komputerze zostaje
 * w miejscu przy przewijaniu), po prawej nazwa, cena, zakup i opis.
 */
function ProductContent({ product }: { product: ProductDetails }) {
  return (
    <div className="product-layout">
      <div className="product-layout__media">
        <ProductImage className="product-page__image" slug={product.slug} />
      </div>

      <div className="product-layout__info">
        <p className="eyebrow">{categoryLabels[product.category]}</p>
        <h1>{product.name}</h1>
        <p className="product-page__tagline">{product.tagline}</p>
        <p className="product-page__price">{formatPrice(product.price)}</p>

        <ul className="product-page__highlights">
          <li>{deliveryNotes[product.kind]}</li>
          {product.durationMinutes && <li>Długość: {formatMinutes(product.durationMinutes)}</li>}
        </ul>

        <div className="product-buy">
          {product.kind === 'ebook' && <AddEbookToCart product={product} />}
          {product.kind !== 'ebook' && <AddToCartDialog key={product.slug} product={product} />}
        </div>

        <section className="product-page__description" aria-labelledby="product-description-title">
          <h2 id="product-description-title">Opis</h2>
          {product.description.map((block, index) => (
            <DescriptionBlockView key={index} block={block} />
          ))}
        </section>
      </div>
    </div>
  )
}

/** Każde kliknięcie dodaje kolejną sztukę Ebooka; liczbę sztuk można też zmienić w Koszyku. */
function AddEbookToCart({ product }: { product: ProductDetails }) {
  return (
    <p className="product-buy__actions">
      <button
        type="button"
        className="button button--primary product-buy__add"
        onClick={() => {
          addEbookToCart(product)
          notifyAddedToCart(product.name)
        }}
      >
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
