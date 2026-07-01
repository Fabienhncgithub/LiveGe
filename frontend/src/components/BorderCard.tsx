import StatusBadge from './StatusBadge'
import type { G7BorderInfo } from '../data/g7Event'
import type { LiveBorderStatus } from '../types'

interface BorderCardProps {
  status: LiveBorderStatus
  g7Info?: G7BorderInfo
}

const congestionMap = {
  Green: { label: 'Green', variant: 'green' },
  Orange: { label: 'Orange', variant: 'orange' },
  Red: { label: 'Red', variant: 'red' }
} as const

const trendMap = {
  Rising: { label: 'Rising', variant: 'rising' },
  Stable: { label: 'Stable', variant: 'stable' },
  Falling: { label: 'Falling', variant: 'falling' }
} as const

export default function BorderCard({ status, g7Info }: BorderCardProps) {
  const congestion = congestionMap[status.congestionLevel]
  const trend = trendMap[status.trend]
  const recordedAt = new Date(status.recordedAtUtc).toLocaleTimeString('fr-CH', {
    hour: '2-digit',
    minute: '2-digit'
  })

  return (
    <article className="card">
      <div className="card__header">
        <h3>{status.borderPointName}</h3>
        <StatusBadge label={congestion.label} variant={congestion.variant} />
      </div>

      {g7Info && (
        <div className="g7-card-note">
          <span className="g7-card-note__label">Ouvert G7</span>
          <span>{g7Info.corridor}</span>
        </div>
      )}

      <div className="card__metrics">
        <div>
          <span className="metric-label">Délai</span>
          <span className="metric-value">{status.estimatedDelayMinutes} min</span>
        </div>
        <div>
          <span className="metric-label">Vitesse</span>
          <span className="metric-value">{status.speedKmh} km/h</span>
        </div>
      </div>

      <div className="card__meta">
        <StatusBadge label={trend.label} variant={trend.variant} />
        <span className="prediction">
          {status.predictedDelayMinutes ?? status.estimatedDelayMinutes} min · {status.predictionLabel}
        </span>
      </div>

      <div className="card__footer">Mesure: {recordedAt}</div>

      {g7Info && <p className="card__guidance">{g7Info.guidance}</p>}
    </article>
  )
}
