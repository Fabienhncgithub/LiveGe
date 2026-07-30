import { useEffect, useState } from 'react'
import Layout from './components/Layout'
import Dashboard from './pages/Dashboard'
import Alerts from './pages/Alerts'

const normalizePath = (path: string) => (path === '/' ? '/dashboard' : path)

export default function App() {
  const [path, setPath] = useState(() => normalizePath(window.location.pathname))

  useEffect(() => {
    if (window.location.pathname === '/') {
      window.history.replaceState(null, '', '/dashboard')
    }

    const handlePopState = () => setPath(normalizePath(window.location.pathname))
    window.addEventListener('popstate', handlePopState)
    return () => window.removeEventListener('popstate', handlePopState)
  }, [])

  const navigate = (nextPath: string) => {
    if (nextPath === path) {
      return
    }

    window.history.pushState(null, '', nextPath)
    setPath(nextPath)
  }

  const page = (() => {
    switch (path) {
      case '/alerts':
        return <Alerts />
      default:
        return <Dashboard />
    }
  })()

  return (
    <Layout currentPath={path} onNavigate={navigate}>
      {page}
    </Layout>
  )
}
