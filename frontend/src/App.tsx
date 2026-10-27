import { Route, Routes } from 'react-router'
import './App.css'
import { SiteFooter } from './components/SiteFooter.tsx'
import { SiteHeader } from './components/SiteHeader.tsx'
import { HomePage } from './pages/HomePage.tsx'
import { NotFoundPage } from './pages/NotFoundPage.tsx'

function App() {
  return (
    <>
      <div className="night-sky" aria-hidden="true" />
      <SiteHeader />
      <main>
        <Routes>
          <Route index element={<HomePage />} />
          <Route path="*" element={<NotFoundPage />} />
        </Routes>
      </main>
      <SiteFooter />
    </>
  )
}

export default App
