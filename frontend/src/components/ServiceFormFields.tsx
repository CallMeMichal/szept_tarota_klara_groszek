import type { FormField } from '../catalog.ts'
import type { FormAnswers } from '../serviceForm.ts'

type ServiceFormFieldsProps = {
  /** Przedrostek identyfikatorów pól, różny dla każdego formularza na stronie. */
  idPrefix: string
  fields: FormField[]
  /** Odpowiedzi wpisane wcześniej, np. przy poprawianiu formularza w Koszyku. */
  values?: FormAnswers
  errors: Record<string, string>
}

const inputTypes = { text: 'text', date: 'date', email: 'email' } as const

/**
 * Pola formularza Usługi; nazwy pól to ich klucze z katalogu. Pola wymagane
 * (jeśli katalog jakieś ma) są oznaczone gwiazdką, a błędy pokazujemy pod polem.
 */
export function ServiceFormFields({ idPrefix, fields, values, errors }: ServiceFormFieldsProps) {
  return fields.map((field) => {
    const id = `${idPrefix}-${field.key}`
    const error = errors[field.key]
    const inputProps = {
      id,
      name: field.key,
      defaultValue: values?.[field.key] ?? '',
      required: field.required,
      'aria-invalid': error ? true : undefined,
      'aria-describedby': error ? `${id}-error` : undefined,
    }

    return (
      <div key={field.key} className="checkout-form__field">
        <label htmlFor={id}>
          {field.label}
          {field.required && <span aria-hidden="true"> *</span>}
        </label>
        {field.type === 'longText' ? (
          <textarea {...inputProps} rows={4} maxLength={2000} />
        ) : (
          <input
            {...inputProps}
            type={inputTypes[field.type]}
            maxLength={200}
            autoComplete={field.key === 'name' ? 'given-name' : 'off'}
          />
        )}
        {error && (
          <span id={`${id}-error`} className="checkout-form__error" role="alert">
            {error}
          </span>
        )}
      </div>
    )
  })
}
