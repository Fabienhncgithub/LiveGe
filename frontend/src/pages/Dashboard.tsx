import { useCallback, useEffect, useState } from 'react'
import { fetchLiveStatuses } from '../api/borderApi'
import BorderCard from '../components/BorderCard'
import type { LiveBorderStatus } from '../types'

export default function Dashboard() {
  const [statuses, setStatuses] = useState<LiveBorderStatus[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [lastUpdated, setLastUpdated] = useState<Date | null>(null)

  const load = useCallback(async () => {
    try {
      setError(null)
      const data = await fetchLiveStatuses()
      setStatuses(data)
      setLastUpdated(new Date())
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Erreur inconnue')
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => {
    void load()
    const timer = setInterval(() => {
      void load()
    }, 30000)
    return () => clearInterval(timer)
  }, [load])

  const sortedStatuses = [...statuses].sort((a, b) => b.estimatedDelayMinutes - a.estimatedDelayMinutes)

  return (
    <section className="page">
      <div className="page-header">
        <div>
          <h1>Frontière Live GE</h1>
          <p className="subtitle">Surveillance en temps réel des principaux passages frontaliers genevois</p>
        </div>
        <div className="page-meta">
          <span>
            MAJ:{' '}
            {lastUpdated ? lastUpdated.toLocaleTimeString('fr-CH', { hour: '2-digit', minute: '2-digit' }) : '--'}
          </span>
          <button className="btn btn-secondary" onClick={() => void load()} disabled={loading}>
            MAJ
          </button>
        </div>
      </div>

      {error && <div className="error-banner">{error}</div>}

      {loading ? (
        <p className="empty-state">Chargement des données...</p>
      ) : (
        <div className="grid">
          {sortedStatuses.map((status) => (
            <BorderCard key={status.borderPointId} status={status} />
          ))}
        </div>
      )}
    </section>
  )
}
