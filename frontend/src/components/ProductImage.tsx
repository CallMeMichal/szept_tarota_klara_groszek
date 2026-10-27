import { Moon } from './Moon.tsx'

// Grafiki Produktów są tylko we frontendzie: plik src/assets/products/<slug>.webp
// należy do Produktu o tym slugu. Vite dokłada hash i BASE_URL do adresów.
const imagesBySlug = Object.fromEntries(
  Object.entries(
    import.meta.glob<string>('../assets/products/*.webp', {
      eager: true,
      query: '?url',
      import: 'default',
    }),
  ).map(([path, url]) => [path.slice(path.lastIndexOf('/') + 1, -'.webp'.length), url]),
)

type ProductImageProps = {
  slug: string
  className: string
}

/** Grafika Produktu albo księżyc marki, gdy Produkt nie ma grafiki. */
export function ProductImage({ slug, className }: ProductImageProps) {
  const imageUrl = imagesBySlug[slug]

  if (!imageUrl) {
    return (
      <div className={`${className} product-image--placeholder`} aria-hidden="true">
        <Moon className="product-image__moon" />
      </div>
    )
  }

  return <img className={className} src={imageUrl} alt="" loading="lazy" />
}
