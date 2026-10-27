import { Route, Routes } from 'react-router'
import './App.css'
import { cartPath } from './cart.ts'
import { SiteFooter } from './components/SiteFooter.tsx'
import { SiteHeader } from './components/SiteHeader.tsx'
import { homeSections, linkedSectionIds } from './homeSections.ts'
import { CartPage } from './pages/CartPage.tsx'
import { HomePage } from './pages/HomePage.tsx'
import { NotFoundPage } from './pages/NotFoundPage.tsx'
import { ProductPage } from './pages/ProductPage.tsx'

function App() {
  return (
    <>
      <div className="night-sky" aria-hidden="true" />
      <SiteHeader />
      <main>
        <Routes>
          <Route index element={<HomePage section={homeSections.start} />} />
          {linkedSectionIds.map((id) => (
            <Route key={id} path={id} element={<HomePage section={id} />} />
          ))}
          <Route path="oferta/:slug" element={<ProductPage />} />
          <Route path={cartPath} element={<CartPage />} />
          <Route path="*" element={<NotFoundPage />} />
        </Routes>
      </main>
      <SiteFooter />
    </>
  )
}

export default App
