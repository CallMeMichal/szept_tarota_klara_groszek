import { addServiceToCart } from '../cart.ts'
import { notifyAddedToCart } from '../cartNotice.ts'
import type { ProductDetails } from '../catalog.ts'
import { ServiceStepsDialog } from './ServiceStepsDialog.tsx'

/** „Dodaj do koszyka” przy Usłudze albo Sesji: otwiera okienko z danymi i Terminem. */
export function AddToCartDialog({ product }: { product: ProductDetails }) {
  return (
    <div className="product-buy__actions">
      <ServiceStepsDialog
        productName={product.name}
        fields={product.form}
        triggerClassName="button button--primary product-buy__add"
        triggerContent="Dodaj do koszyka"
        finishLabel="Dodaj do koszyka"
        onFinish={(answers) => {
          addServiceToCart(product, answers)
          notifyAddedToCart(product.name)
        }}
      />
    </div>
  )
}
