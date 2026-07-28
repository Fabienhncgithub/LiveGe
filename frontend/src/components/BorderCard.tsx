import StatusBadge from './StatusBadge'
import type { LiveBorderStatus } from '../types'
import { parseUtcDate } from '../utils/date'

interface BorderCardProps {
  status: LiveBorderStatus
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

export default function BorderCard({ status }: BorderCardProps) {
  const congestion = congestionMap[status.congestionLevel]
  const trend = trendMap[status.trend]
  const recordedAt = parseUtcDate(status.recordedAtUtc).toLocaleTimeString('fr-CH', {
    hour: '2-digit',
    minute: '2-digit'
  })

  return (
    <article className="card">
      <div className="card__header">
        <h3>{status.borderPointName}</h3>
        <StatusBadge label={congestion.label} variant={congestion.variant} />
      </div>

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
    </article>
  )
}
