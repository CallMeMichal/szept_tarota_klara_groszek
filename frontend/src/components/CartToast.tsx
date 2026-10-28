import { useEffect, useState } from 'react'
import { Link } from 'react-router'
import { cartPath } from '../cart.ts'
import { dismissCartNotice, useCartNotice } from '../cartNotice.ts'

/** Jak długo powiadomienie jest widoczne. */
const visibleMs = 5000

/** Powiadomienie o dodaniu do Koszyka w rogu ekranu, z linkiem do Koszyka. */
export function CartToast() {
  const notice = useCartNotice()
  // Najechanie myszą albo fokus wewnątrz wstrzymują znikanie, żeby zdążyć kliknąć.
  const [paused, setPaused] = useState(false)

  useEffect(() => {
    if (!notice || paused) return
    const timeout = window.setTimeout(() => dismissCartNotice(), visibleMs)
    return () => window.clearTimeout(timeout)
  }, [notice, paused])

  return (
    <div className="cart-toast-region" role="status" aria-live="polite">
      {notice && (
        <div
          key={notice.id}
          className="cart-toast"
          onMouseEnter={() => setPaused(true)}
          onMouseLeave={() => setPaused(false)}
          onFocus={() => setPaused(true)}
          onBlur={() => setPaused(false)}
        >
          <p className="cart-toast__text">
            <strong>Dodano do Koszyka</strong>
            <span>{notice.productName}</span>
          </p>
          <Link className="button button--primary cart-toast__link" to={cartPath} onClick={() => dismissCartNotice()}>
            Przejdź do Koszyka
          </Link>
          <button type="button" className="cart-toast__close" onClick={() => dismissCartNotice()}>
            <span aria-hidden="true">×</span>
            <span className="visually-hidden">Zamknij powiadomienie</span>
          </button>
        </div>
      )}
    </div>
  )
}
