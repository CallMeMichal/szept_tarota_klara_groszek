import { useId, useRef, useState, type FormEvent, type ReactNode } from 'react'
import type { FormField } from '../catalog.ts'
import { submitServiceForm, type FormAnswers } from '../serviceForm.ts'
import { ServiceFormFields } from './ServiceFormFields.tsx'

type Step = 'details' | 'date'

type ServiceStepsDialogProps = {
  /** Nazwa Produktu pod tytułem okienka. */
  productName: string
  fields: FormField[]
  /** Dane wpisane wcześniej (przy poprawianiu w Koszyku). */
  initialAnswers?: FormAnswers
  /** Błędy z ostatniego sprawdzenia Koszyka, pokazywane po otwarciu. */
  initialErrors?: Record<string, string>
  /** Klasa i treść przycisku otwierającego okienko. */
  triggerClassName: string
  triggerContent: ReactNode
  /** Napis na przycisku kończącym ostatni krok, np. „Dodaj do koszyka”. */
  finishLabel: string
  onFinish: (answers: FormAnswers) => void
}

/**
 * Okienko z krokami dla Usługi albo Sesji: najpierw dane do formularza
 * (wszystkie można pominąć), potem wybór Terminu w kalendarzu. Kalendarz
 * jeszcze nie działa, więc drugi krok jest na razie pusty. To samo okienko
 * służy do dodania do Koszyka i do poprawienia danych w Koszyku.
 */
export function ServiceStepsDialog({
  productName,
  fields,
  initialAnswers = {},
  initialErrors = {},
  triggerClassName,
  triggerContent,
  finishLabel,
  onFinish,
}: ServiceStepsDialogProps) {
  const dialog = useRef<HTMLDialogElement>(null)
  const id = useId()
  const hasDetails = fields.length > 0
  const firstStep: Step = hasDetails ? 'details' : 'date'
  const steps: Step[] = hasDetails ? ['details', 'date'] : ['date']
  const [step, setStep] = useState<Step>(firstStep)
  const [answers, setAnswers] = useState<FormAnswers>(initialAnswers)
  const [errors, setErrors] = useState<Record<string, string>>(initialErrors)

  function open() {
    setStep(firstStep)
    setAnswers(initialAnswers)
    setErrors(initialErrors)
    dialog.current?.showModal()
  }

  function onDetailsSubmit(event: FormEvent<HTMLFormElement>) {
    const result = submitServiceForm(event, fields)
    setErrors(result.errors)
    if (!result.valid) return
    setAnswers(result.answers)
    setStep('date')
  }

  function finish() {
    onFinish(answers)
    dialog.current?.close()
  }

  return (
    <>
      <button type="button" className={triggerClassName} onClick={open}>
        {triggerContent}
      </button>

      <dialog ref={dialog} className="add-dialog" aria-labelledby={`${id}-title`}>
        <header className="add-dialog__header">
          <p className="add-dialog__step">
            Krok {steps.indexOf(step) + 1} z {steps.length}
          </p>
          <h2 id={`${id}-title`}>{step === 'details' ? 'Twoje dane' : 'Wybierz Termin'}</h2>
          <p className="add-dialog__product">{productName}</p>
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
            <ServiceFormFields idPrefix={id} fields={fields} values={answers} errors={errors} />
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
              <button type="button" className="button button--primary" onClick={finish}>
                {finishLabel}
              </button>
            </div>
          </div>
        )}
      </dialog>
    </>
  )
}
