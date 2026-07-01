import { useCallback, useEffect, useState } from 'react'
import { fetchLiveStatuses } from '../api/borderApi'
import BorderCard from '../components/BorderCard'
import { g7Event, g7OpenBorders, getG7BorderInfo } from '../data/g7Event'
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

  const sortedStatuses = [...statuses].sort((a, b) => {
    const aInfo = getG7BorderInfo(a.borderPointName)
    const bInfo = getG7BorderInfo(b.borderPointName)
    const aPriority = aInfo?.priority ?? 999
    const bPriority = bInfo?.priority ?? 999

    if (aPriority !== bPriority) {
      return aPriority - bPriority
    }

    return b.estimatedDelayMinutes - a.estimatedDelayMinutes
  })

  const openStatusCount = sortedStatuses.filter((status) => getG7BorderInfo(status.borderPointName)).length

  return (
    <section className="page">
      <div className="page-header">
        <div>
          <h1>Live G7</h1>
          <p className="subtitle">Surveillance des passages ouverts pendant le dispositif special</p>
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

      <div className="event-banner">
        <div>
          <span className="event-banner__eyebrow">{g7Event.title} · {g7Event.periodLabel}</span>
          <h2>{g7Event.openCount} passages ouverts, {g7Event.closedCount} douanes fermees</h2>
          <p>
            Franchissement autorise uniquement aux points listes. Controles permanents 24h/24 et delais
            accrus a prevoir.
          </p>
        </div>
        <div className="event-banner__stats">
          <span>{openStatusCount}/{g7Event.openCount}</span>
          <small>points suivis</small>
        </div>
      </div>

      <div className="event-checklist" aria-label="Passages ouverts G7">
        {g7OpenBorders.map((point) => (
          <span key={point.name}>{point.aliases?.[0] ?? point.name}</span>
        ))}
      </div>

      {loading ? (
        <p className="empty-state">Chargement des données...</p>
      ) : (
        <div className="grid">
          {sortedStatuses.map((status) => (
            <BorderCard key={status.borderPointId} status={status} g7Info={getG7BorderInfo(status.borderPointName)} />
          ))}
        </div>
      )}
    </section>
  )
}
