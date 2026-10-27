import { useEffect, useState } from 'react'

export type ApiState<T> =
  | { status: 'loading' }
  | { status: 'ok'; data: T }
  | { status: 'not-found' }
  | { status: 'error' }

// Podgląd na GitHub Pages nie ma backendu: build dokłada tam katalog jako
// statyczne pliki JSON (vite.config.ts), a zapytania idą do nich.
const staticCatalog = import.meta.env.VITE_STATIC_CATALOG === 'true'

/** Adres odpowiedzi API: /api/products/x albo statyczny plik <base>api/products/x.json. */
function apiUrl(path: string) {
  return staticCatalog ? `${import.meta.env.BASE_URL}${path.slice(1)}.json` : path
}

/** Pobiera JSON z API. */
export function useApi<T>(path: string): ApiState<T> {
  const [state, setState] = useState<{ path: string; result: ApiState<T> }>({
    path,
    result: { status: 'loading' },
  })

  useEffect(() => {
    const controller = new AbortController()

    fetch(apiUrl(path), { signal: controller.signal, headers: { Accept: 'application/json' } })
      .then(async (response): Promise<ApiState<T>> => {
        // 404 z HTML-em to nie odpowiedź API, tylko strona hostingu bez
        // backendu, czyli API jest niedostępne. Przy statycznym katalogu
        // brak pliku znaczy po prostu, że nie ma takiego Produktu.
        const isHtml = response.headers.get('Content-Type')?.includes('text/html')
        if (response.status === 404 && (staticCatalog || !isHtml)) return { status: 'not-found' }
        if (!response.ok) return { status: 'error' }
        return { status: 'ok', data: (await response.json()) as T }
      })
      .catch((): ApiState<T> => ({ status: 'error' }))
      .then((result) => {
        if (!controller.signal.aborted) setState({ path, result })
      })

    return () => controller.abort()
  }, [path])

  // Po zmianie adresu (np. przejście na inną podstronę Produktu) nie
  // pokazujemy wyniku poprzedniego zapytania.
  return state.path === path ? state.result : { status: 'loading' }
}
