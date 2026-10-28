// Formularz Usługi, który Klient wypełnia przy dodawaniu jej do Koszyka.
// Sprawdzamy go tu tylko po to, żeby od razu pokazać braki; ostatecznie
// sprawdza go serwer przy każdym sprawdzeniu Koszyka i przed płatnością.

import type { FormEvent } from 'react'
import type { FormField } from './catalog.ts'

/** Odpowiedzi po kluczach pól. */
export type FormAnswers = Record<string, string>

/** Odczytuje odpowiedzi z formularza HTML, którego pola nazywają się kluczami pól Usługi. */
export function readServiceForm(fields: FormField[], form: HTMLFormElement): FormAnswers {
  const data = new FormData(form)
  const answers: FormAnswers = {}
  for (const field of fields) {
    const value = String(data.get(field.key) ?? '').trim()
    if (value) answers[field.key] = value
  }
  return answers
}

/**
 * Obsługuje wysłanie formularza Usługi: odczytuje i sprawdza odpowiedzi, a przy
 * błędach przenosi fokus na pierwsze błędne pole. Zwraca odpowiedzi i błędy;
 * odpowiedzi są poprawne, gdy błędów nie ma.
 */
export function submitServiceForm(event: FormEvent<HTMLFormElement>, fields: FormField[]) {
  event.preventDefault()
  const form = event.currentTarget
  const answers = readServiceForm(fields, form)
  const errors = checkServiceForm(fields, answers)
  const firstInvalid = fields.find((field) => errors[field.key])
  if (firstInvalid) form.querySelector<HTMLElement>(`[name="${firstInvalid.key}"]`)?.focus()
  return { answers, errors, valid: !firstInvalid }
}

const datePattern = /^\d{4}-\d{2}-\d{2}$/
const emailPattern = /^[^\s@]+@[^\s@]+\.[^\s@]+$/

/** Błędy po kluczach pól: braki w polach wymaganych i źle wpisane daty i adresy email. */
export function checkServiceForm(fields: FormField[], answers: FormAnswers): Record<string, string> {
  const errors: Record<string, string> = {}
  for (const field of fields) {
    const value = answers[field.key] ?? ''
    if (!value) {
      if (field.required) errors[field.key] = `Uzupełnij pole „${field.label}”.`
    } else if (hasControlCharacters(value, field.type === 'longText')) {
      errors[field.key] = `Pole „${field.label}” zawiera niedozwolone znaki.`
    } else if (field.type === 'date' && !(datePattern.test(value) && isRealDate(value))) {
      errors[field.key] = `Podaj poprawną datę w polu „${field.label}”.`
    } else if (field.type === 'email' && !emailPattern.test(value)) {
      errors[field.key] = `Podaj poprawny adres email w polu „${field.label}”.`
    }
  }
  return errors
}

/** Znaki sterujące jak na serwerze (char.IsControl); tekst długi może mieć nowe linie i tabulatory. */
function hasControlCharacters(value: string, allowsNewLines: boolean) {
  return [...value].some((character) => {
    const code = character.charCodeAt(0)
    const isControl = code < 0x20 || (code >= 0x7f && code <= 0x9f)
    return isControl && !(allowsNewLines && ['\n', '\r', '\t'].includes(character))
  })
}

function isRealDate(value: string) {
  const date = new Date(`${value}T00:00:00Z`)
  return !Number.isNaN(date.getTime()) && date.toISOString().startsWith(value)
}

/** Odpowiedź do pokazania w Koszyku, np. data urodzenia po polsku. */
export function formatAnswer(field: FormField, value: string) {
  if (field.type !== 'date' || !datePattern.test(value)) return value
  const [year, month, day] = value.split('-').map(Number)
  return new Date(year, month - 1, day).toLocaleDateString('pl-PL', {
    day: 'numeric',
    month: 'long',
    year: 'numeric',
  })
}
