import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

// https://vite.dev/config/
export default defineConfig({
  // Na GitHub Pages strona jest pod /<repo>/; workflow wdrożenia ustawia BASE_PATH.
  base: process.env.BASE_PATH ?? '/',
  plugins: [react()],
  server: {
    proxy: {
      '/api': 'http://localhost:5292',
    },
  },
})
