import { useEffect } from 'react'

/** Tytuł karty „<title> · Szept Tarota” na czas wyświetlania strony. */
export function usePageTitle(title: string) {
  useEffect(() => {
    const siteTitle = document.title
    document.title = `${title} · Szept Tarota`
    return () => {
      document.title = siteTitle
    }
  }, [title])
}
