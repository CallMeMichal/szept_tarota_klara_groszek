import { Outlet, Route, Routes } from 'react-router'
import './App.css'
import { accountPath, loginPath, panelPath } from './account.ts'
import { cartPath, orderPath } from './cart.ts'
import { CartToast } from './components/CartToast.tsx'
import { SiteFooter } from './components/SiteFooter.tsx'
import { SiteHeader } from './components/SiteHeader.tsx'
import { homeSections, linkedSectionIds } from './homeSections.ts'
import { panelAvailabilityPath, panelOrderPath, panelOrdersPath } from './panel.ts'
import { AccountPage } from './pages/AccountPage.tsx'
import { CartPage } from './pages/CartPage.tsx'
import { HomePage } from './pages/HomePage.tsx'
import { LoginPage } from './pages/LoginPage.tsx'
import { NotFoundPage } from './pages/NotFoundPage.tsx'
import { OrderPage } from './pages/OrderPage.tsx'
import { PanelOrders } from './components/PanelOrders.tsx'
import { PanelOrderDetails } from './pages/PanelOrderPage.tsx'
import { PanelAvailability, PanelHome, PanelPage } from './pages/PanelPage.tsx'
import { ProductPage } from './pages/ProductPage.tsx'

function App() {
  return (
    <>
      <div className="night-sky" aria-hidden="true" />
      <Routes>
        {/* Panel Klary ma własny układ z paskiem bocznym, bez nagłówka i stopki strony. */}
        <Route path={panelPath} element={<PanelPage />}>
          <Route index element={<PanelHome />} />
          <Route path={panelOrdersPath} element={<PanelOrders />} />
          <Route path={panelOrderPath(':id')} element={<PanelOrderDetails />} />
          <Route path={panelAvailabilityPath} element={<PanelAvailability />} />
        </Route>
        <Route element={<SiteLayout />}>
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
        </Route>
      </Routes>
      <CartToast />
    </>
  )
}

/** Strona dla Klientów: nagłówek, wybrana podstrona i stopka. */
function SiteLayout() {
  return (
    <>
      <SiteHeader />
      <main>
        <Outlet />
      </main>
      <SiteFooter />
    </>
  )
}

export default App
