import { Moon } from './Moon.tsx'

type ProductImageProps = {
  imageUrl: string | null
  className: string
}

/** Grafika Produktu albo księżyc marki, gdy Produkt nie ma grafiki. */
export function ProductImage({ imageUrl, className }: ProductImageProps) {
  if (!imageUrl) {
    return (
      <div className={`${className} product-image--placeholder`} aria-hidden="true">
        <Moon className="product-image__moon" />
      </div>
    )
  }

  return <img className={className} src={imageUrl} alt="" loading="lazy" />
}
