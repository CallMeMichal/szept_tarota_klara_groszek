import { Route, Routes } from 'react-router'
import './App.css'
import { accountPath, loginPath } from './account.ts'
import { cartPath, orderPath } from './cart.ts'
import { CartToast } from './components/CartToast.tsx'
import { SiteFooter } from './components/SiteFooter.tsx'
import { SiteHeader } from './components/SiteHeader.tsx'
import { homeSections, linkedSectionIds } from './homeSections.ts'
import { AccountPage } from './pages/AccountPage.tsx'
import { CartPage } from './pages/CartPage.tsx'
import { HomePage } from './pages/HomePage.tsx'
import { LoginPage } from './pages/LoginPage.tsx'
import { NotFoundPage } from './pages/NotFoundPage.tsx'
import { OrderPage } from './pages/OrderPage.tsx'
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
          <Route path={orderPath(':id')} element={<OrderPage />} />
          <Route path={loginPath} element={<LoginPage />} />
          <Route path={accountPath} element={<AccountPage />} />
          <Route path="*" element={<NotFoundPage />} />
        </Routes>
      </main>
      <SiteFooter />
      <CartToast />
    </>
  )
}

export default App
