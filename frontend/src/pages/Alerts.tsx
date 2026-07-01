import { useEffect, useState } from 'react'
import { fetchAlerts } from '../api/borderApi'
import AlertList from '../components/AlertList'
import type { AlertEvent } from '../types'

export default function Alerts() {
  const [alerts, setAlerts] = useState<AlertEvent[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    const load = async () => {
      try {
        setError(null)
        const data = await fetchAlerts()
        setAlerts(data)
      } catch (err) {
        setError(err instanceof Error ? err.message : 'Erreur inconnue')
      } finally {
        setLoading(false)
      }
    }

    void load()
  }, [])

  return (
    <section className="page">
      <div className="page-header">
        <div>
          <h1>Alertes</h1>
          <p className="subtitle">Alertes récentes</p>
        </div>
      </div>

      {error && <div className="error-banner">{error}</div>}

      {loading ? <p className="empty-state">Chargement...</p> : <AlertList alerts={alerts} />}
    </section>
  )
}
