import { readFileSync } from 'node:fs'
import react from '@vitejs/plugin-react'
import { defineConfig, type Plugin } from 'vite'

// Na GitHub Pages nie ma backendu. Przy VITE_STATIC_CATALOG=true build dokłada
// odpowiedzi API katalogu jako pliki: api/products.json (lista) i
// api/products/<slug>.json (Produkt), w tym samym kształcie co ProductEndpoints.cs.
function staticCatalog(): Plugin {
  const catalogFile = new URL('../backend/src/SzeptTarota.Api/Catalog/products.json', import.meta.url)

  return {
    name: 'static-catalog',
    apply: 'build',
    generateBundle() {
      const { products } = JSON.parse(readFileSync(catalogFile, 'utf8')) as {
        products: Record<string, unknown>[]
      }
      const summary = (p: Record<string, unknown>) => ({
        slug: p.slug,
        kind: p.kind,
        category: p.category,
        name: p.name,
        tagline: p.tagline,
        price: p.price,
        durationMinutes: p.durationMinutes ?? null,
      })
      const emit = (fileName: string, data: unknown) =>
        this.emitFile({ type: 'asset', fileName, source: JSON.stringify(data) })

      emit('api/products.json', products.map(summary))
      for (const p of products) {
        emit(`api/products/${p.slug}.json`, {
          ...summary(p),
          description: p.description,
          form: p.form ?? [],
        })
      }
    },
  }
}

// https://vite.dev/config/
export default defineConfig({
  // Na GitHub Pages strona jest pod /<repo>/; workflow wdrożenia ustawia BASE_PATH.
  base: process.env.BASE_PATH ?? '/',
  plugins: [react(), process.env.VITE_STATIC_CATALOG === 'true' && staticCatalog()],
  server: {
    proxy: {
      '/api': 'http://localhost:5292',
    },
  },
})
