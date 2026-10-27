import { useEffect, useState } from 'react'

export type ApiState<T> =
  | { status: 'loading' }
  | { status: 'ok'; data: T }
  | { status: 'not-found' }
  | { status: 'error' }

/**
 * Pobiera JSON z API. Na podglądzie na GitHub Pages nie ma backendu,
 * więc stan 'error' jest tam normalny i strona musi go pokazać.
 */
export function useApi<T>(path: string): ApiState<T> {
  const [state, setState] = useState<{ path: string; result: ApiState<T> }>({
    path,
    result: { status: 'loading' },
  })

  useEffect(() => {
    const controller = new AbortController()

    fetch(path, { signal: controller.signal, headers: { Accept: 'application/json' } })
      .then(async (response): Promise<ApiState<T>> => {
        // 404 z HTML-em to nie odpowiedź API, tylko strona hostingu bez
        // backendu (404.html na GitHub Pages), czyli API jest niedostępne.
        const isHtml = response.headers.get('Content-Type')?.includes('text/html')
        if (response.status === 404 && !isHtml) return { status: 'not-found' }
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
