import { useState, type FormEvent, type ReactNode } from 'react'
import arrowPrimary from '../assets/home/arrow-up-right-primary.svg'

type Status = 'idle' | 'sending' | 'sent' | 'error'

type ContactFormProps = {
  /** Druga droga kontaktu, po lewej od przycisku wysyłki (np. link do Instagrama). */
  alternative?: ReactNode
}

/** Formularz w sekcji Kontakt: wiadomość trafia przez API na firmowy email. */
export function ContactForm({ alternative }: ContactFormProps) {
  const [status, setStatus] = useState<Status>('idle')

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const form = event.currentTarget
    const data = Object.fromEntries(new FormData(form))
    setStatus('sending')

    try {
      const response = await fetch('/api/contact', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(data),
      })
      if (!response.ok) throw new Error(`HTTP ${response.status}`)
      form.reset()
      setStatus('sent')
    } catch {
      setStatus('error')
    }
  }

  return (
    <form className="contact-form" onSubmit={handleSubmit}>
      <div className="contact-form__row">
        <label className="contact-form__field">
          <span>Imię</span>
          <input name="name" autoComplete="given-name" required maxLength={100} />
        </label>
        <label className="contact-form__field">
          <span>Email</span>
          <input name="email" type="email" autoComplete="email" required maxLength={200} />
        </label>
      </div>
      <label className="contact-form__field">
        <span>Wiadomość</span>
        <textarea name="message" rows={5} required maxLength={5000} />
      </label>

      {/* Pole-pułapka na boty, niewidoczne dla ludzi i czytników ekranu. */}
      <label className="contact-form__trap" aria-hidden="true">
        Strona internetowa
        <input name="website" tabIndex={-1} autoComplete="off" />
      </label>

      <div className="contact-form__actions">
        {alternative}
        <button className="button button--primary" type="submit" disabled={status === 'sending'}>
          {status === 'sending' ? 'Wysyłanie…' : 'Wyślij wiadomość'}
          <img src={arrowPrimary} width={16} height={16} alt="" />
        </button>
      </div>

      <p className="contact-form__status" role="status">
        {status === 'sent' && 'Dziękuję! Wiadomość dotarła, odpiszę najszybciej, jak to możliwe.'}
        {status === 'error' &&
          'Nie udało się wysłać wiadomości. Spróbuj ponownie za chwilę albo napisz do mnie na Instagramie.'}
      </p>
    </form>
  )
}
