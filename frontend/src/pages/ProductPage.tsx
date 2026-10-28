import { useEffect, useState, type FormEvent } from 'react'
import { Link, useParams } from 'react-router'
import { ProductImage } from '../components/ProductImage.tsx'
import { ServiceFormFields } from '../components/ServiceFormFields.tsx'
import { addEbookToCart, addServiceToCart, useCart } from '../cart.ts'
import {
  categoryLabels,
  deliveryNotes,
  formatMinutes,
  formatPrice,
  kindLabels,
  type DescriptionBlock,
  type ProductDetails,
} from '../catalog.ts'
import { notifyAddedToCart } from '../cartNotice.ts'
import { homeSectionPath, homeSections } from '../homeSections.ts'
import { submitServiceForm } from '../serviceForm.ts'
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
          {product.kind === 'service' && <AddServiceToCart key={product.slug} product={product} />}
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

  // Ebook może być w Koszyku najwyżej raz, więc po dodaniu przycisk jest nieaktywny.
  return (
    <p className="product-page__cart">
      <button
        type="button"
        className="button button--primary"
        disabled={inCart}
        onClick={() => {
          addEbookToCart(product)
          notifyAddedToCart(product.name)
        }}
      >
        {inCart ? 'Już w Koszyku' : 'Dodaj do koszyka'}
      </button>
    </p>
  )
}

/**
 * Formularz Usługi z przyciskiem dodania do Koszyka. Po dodaniu wyskakuje
 * powiadomienie, a formularz się czyści, więc tę samą Usługę można od razu
 * dodać dla kolejnej osoby.
 */
function AddServiceToCart({ product }: { product: ProductDetails }) {
  const [errors, setErrors] = useState<Record<string, string>>({})

  function onSubmit(event: FormEvent<HTMLFormElement>) {
    const { answers, errors, valid } = submitServiceForm(event, product.form)
    setErrors(errors)
    if (!valid) return

    addServiceToCart(product, answers)
    notifyAddedToCart(product.name)
    event.currentTarget.reset()
  }

  return (
    <form className="service-form" onSubmit={onSubmit} noValidate aria-labelledby="service-form-title">
      <h2 id="service-form-title">Formularz do Usługi</h2>
      {product.form.length > 0 && (
        <p className="service-form__lead">
          Tych informacji potrzebuję, żeby przygotować Usługę. Pola z gwiazdką są wymagane.
        </p>
      )}
      <ServiceFormFields idPrefix="usluga" fields={product.form} errors={errors} />
      <button type="submit" className="button button--primary">
        Dodaj do koszyka
      </button>
    </form>
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
