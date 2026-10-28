import { useRef, useState, type FormEvent } from 'react'
import { addServiceToCart } from '../cart.ts'
import { notifyAddedToCart } from '../cartNotice.ts'
import type { ProductDetails } from '../catalog.ts'
import { submitServiceForm, type FormAnswers } from '../serviceForm.ts'
import { ServiceFormFields } from './ServiceFormFields.tsx'

type Step = 'details' | 'date'

/**
 * Dodanie Usługi albo Sesji do Koszyka w okienku z krokami: najpierw dane do
 * formularza (wszystkie można pominąć), potem wybór Terminu w kalendarzu.
 * Kalendarz jeszcze nie działa, więc drugi krok jest na razie pusty.
 */
export function AddToCartDialog({ product }: { product: ProductDetails }) {
  const dialog = useRef<HTMLDialogElement>(null)
  const hasDetails = product.form.length > 0
  const firstStep: Step = hasDetails ? 'details' : 'date'
  const [step, setStep] = useState<Step>(firstStep)
  const [answers, setAnswers] = useState<FormAnswers>({})
  const [errors, setErrors] = useState<Record<string, string>>({})
  const steps: Step[] = hasDetails ? ['details', 'date'] : ['date']

  function open() {
    setStep(firstStep)
    setAnswers({})
    setErrors({})
    dialog.current?.showModal()
  }

  function onDetailsSubmit(event: FormEvent<HTMLFormElement>) {
    const result = submitServiceForm(event, product.form)
    setErrors(result.errors)
    if (!result.valid) return
    setAnswers(result.answers)
    setStep('date')
  }

  function addToCart() {
    addServiceToCart(product, answers)
    notifyAddedToCart(product.name)
    dialog.current?.close()
  }

  const titleId = `dodaj-${product.slug}-tytul`

  return (
    <>
      <p className="product-buy__actions">
        <button type="button" className="button button--primary product-buy__add" onClick={open}>
          Dodaj do koszyka
        </button>
      </p>

      <dialog ref={dialog} className="add-dialog" aria-labelledby={titleId}>
        <header className="add-dialog__header">
          <p className="add-dialog__step">
            Krok {steps.indexOf(step) + 1} z {steps.length}
          </p>
          <h2 id={titleId}>{step === 'details' ? 'Twoje dane' : 'Wybierz Termin'}</h2>
          <p className="add-dialog__product">{product.name}</p>
          <button type="button" className="add-dialog__close" onClick={() => dialog.current?.close()}>
            <span aria-hidden="true">×</span>
            <span className="visually-hidden">Zamknij</span>
          </button>
        </header>

        {step === 'details' ? (
          <form className="service-form" onSubmit={onDetailsSubmit} noValidate>
            <p className="service-form__lead">
              Te informacje pomogą mi przygotować Usługę dla Ciebie. Możesz je też uzupełnić później w Koszyku.
            </p>
            <ServiceFormFields idPrefix={`dodaj-${product.slug}`} fields={product.form} values={answers} errors={errors} />
            <div className="add-dialog__actions">
              <button type="submit" className="button button--primary">
                Dalej
              </button>
            </div>
          </form>
        ) : (
          <div className="add-dialog__date">
            <div className="add-dialog__calendar" role="note">
              Tu pojawi się kalendarz z wolnymi Terminami.
            </div>
            <div className="add-dialog__actions">
              {hasDetails && (
                <button type="button" className="button button--ghost" onClick={() => setStep('details')}>
                  Wstecz
                </button>
              )}
              <button type="button" className="button button--primary" onClick={addToCart}>
                Dodaj do koszyka
              </button>
            </div>
          </div>
        )}
      </dialog>
    </>
  )
}
