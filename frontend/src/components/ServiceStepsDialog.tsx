import { useId, useRef, useState, type FormEvent, type ReactNode } from 'react'
import { formatAppointment, type Appointment } from '../appointments.ts'
import type { FormField } from '../catalog.ts'
import { submitServiceForm, type FormAnswers } from '../serviceForm.ts'
import { AppointmentPicker } from './AppointmentPicker.tsx'
import { ServiceFormFields } from './ServiceFormFields.tsx'

type Step = 'details' | 'date'

type ServiceStepsDialogProps = {
  /** Produkt, którego wolne Terminy pokazuje kalendarz. */
  slug: string
  /** Nazwa Produktu pod tytułem okienka. */
  productName: string
  fields: FormField[]
  /** Dane wpisane wcześniej (przy poprawianiu w Koszyku). */
  initialAnswers?: FormAnswers
  /** Termin wybrany wcześniej (przy poprawianiu w Koszyku). */
  initialAppointment?: Appointment | null
  /** Błędy z ostatniego sprawdzenia Koszyka, pokazywane po otwarciu. */
  initialErrors?: Record<string, string>
  /** Klasa i treść przycisku otwierającego okienko. */
  triggerClassName: string
  triggerContent: ReactNode
  /** Napis na przycisku kończącym ostatni krok, np. „Dodaj do koszyka”. */
  finishLabel: string
  onFinish: (answers: FormAnswers, appointment: Appointment) => void
}

/**
 * Okienko z krokami dla Usługi albo Sesji: najpierw dane do formularza
 * (wszystkie można pominąć), potem wybór Terminu w kalendarzu wolnych godzin
 * Klary. To samo okienko służy do dodania do Koszyka i do poprawienia danych
 * albo Terminu w Koszyku.
 */
export function ServiceStepsDialog({
  slug,
  productName,
  fields,
  initialAnswers = {},
  initialAppointment = null,
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
  const [appointment, setAppointment] = useState<Appointment | null>(initialAppointment)
  // Kalendarz montujemy dopiero po otwarciu, żeby nie pytać serwera przy każdej podstronie i pozycji Koszyka.
  const [opened, setOpened] = useState(0)

  function open() {
    setStep(firstStep)
    setAnswers(initialAnswers)
    setErrors(initialErrors)
    setAppointment(initialAppointment)
    setOpened((count) => count + 1)
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
    if (!appointment) return
    onFinish(answers, appointment)
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
            {opened > 0 && (
              <AppointmentPicker key={opened} slug={slug} value={appointment} onChange={setAppointment} />
            )}
            <p className="add-dialog__chosen" role="status">
              {appointment ? `Wybrany Termin: ${formatAppointment(appointment)}` : 'Wybierz dzień i godzinę.'}
            </p>
            <div className="add-dialog__actions">
              {hasDetails && (
                <button type="button" className="button button--ghost" onClick={() => setStep('details')}>
                  Wstecz
                </button>
              )}
              <button type="button" className="button button--primary" onClick={finish} disabled={!appointment}>
                {finishLabel}
              </button>
            </div>
          </div>
        )}
      </dialog>
    </>
  )
}
