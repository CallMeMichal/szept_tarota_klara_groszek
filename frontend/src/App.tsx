import { useEffect, useState } from 'react'
import './App.css'

function App() {
  const [apiStatus, setApiStatus] = useState('…')

  useEffect(() => {
    fetch('/api/health')
      .then((res) => res.json())
      .then((data: { status: string }) => setApiStatus(data.status))
      .catch(() => setApiStatus('brak połączenia'))
  }, [])

  return (
    <main>
      <h1>Szept Tarota — Klara Groszek</h1>
      <p>API: {apiStatus}</p>
    </main>
  )
}

export default App
