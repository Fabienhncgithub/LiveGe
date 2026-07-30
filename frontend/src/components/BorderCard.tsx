import StatusBadge from './StatusBadge'
import type { LiveBorderStatus } from '../types'
import { parseUtcDate } from '../utils/date'

interface BorderCardProps { status: LiveBorderStatus; rank?: number }

const congestionMap = {
  Green: { label: 'Fluide', variant: 'green', message: 'Circulation normale' },
  Orange: { label: 'Chargé', variant: 'orange', message: 'Ralentissements modérés' },
  Red: { label: 'Saturé', variant: 'red', message: 'Forte attente' }
} as const

const trendMap = {
  Rising: { label: 'En hausse', variant: 'rising', arrow: '↗' },
  Stable: { label: 'Stable', variant: 'stable', arrow: '→' },
  Falling: { label: 'En baisse', variant: 'falling', arrow: '↘' }
} as const

export default function BorderCard({ status, rank }: BorderCardProps) {
  const congestion = congestionMap[status.congestionLevel]
  const trend = trendMap[status.trend]
  const recordedAt = parseUtcDate(status.recordedAtUtc).toLocaleTimeString('fr-CH', { hour: '2-digit', minute: '2-digit' })

  return (
    <article className={`card border-card border-card--${congestion.variant}`}>
      <div className="card__header">
        <div>
          {rank === 1 && <span className="most-loaded">Le plus chargé</span>}
          <h3>{status.borderPointName}</h3>
          <span className="traffic-description">{congestion.message}</span>
        </div>
        <StatusBadge label={congestion.label} variant={congestion.variant} />
      </div>
      <div className="card__metrics">
        <div className="delay-metric">
          <span className="metric-value">{status.estimatedDelayMinutes}</span>
          <span><strong>min</strong><small>d’attente</small></span>
        </div>
        <div className="speed-metric">
          <svg viewBox="0 0 24 24"><path d="M5 16a7 7 0 1 1 14 0M12 13l4-4M4 19h16" /></svg>
          <span><strong>{status.speedKmh} km/h</strong><small>vitesse estimée</small></span>
        </div>
      </div>
      <div className="card__meta">
        <StatusBadge label={`${trend.arrow} ${trend.label}`} variant={trend.variant} />
        <span className="prediction">Prévision : <strong>{status.predictedDelayMinutes ?? status.estimatedDelayMinutes} min</strong></span>
      </div>
      <div className="card__footer"><span className="freshness-dot" /> Estimation de {recordedAt}</div>
    </article>
  )
}
