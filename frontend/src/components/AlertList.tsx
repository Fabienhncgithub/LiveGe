import StatusBadge from './StatusBadge'
import type { AlertEvent } from '../types'

interface AlertListProps {
  alerts: AlertEvent[]
}

const severityMap = {
  Info: { label: 'Info', variant: 'info' },
  Warning: { label: 'Warning', variant: 'warning' },
  Critical: { label: 'Critical', variant: 'critical' }
} as const

const trendMap = {
  Rising: { label: 'Rising', variant: 'rising' },
  Stable: { label: 'Stable', variant: 'stable' },
  Falling: { label: 'Falling', variant: 'falling' }
} as const

export default function AlertList({ alerts }: AlertListProps) {
  if (alerts.length === 0) {
    return <p className="empty-state">Aucune alerte.</p>
  }

  return (
    <div className="alert-list">
      {alerts.map((alert) => {
        const severity = severityMap[alert.severity]
        const trend = trendMap[alert.trend]
        const createdAt = new Date(alert.createdAtUtc).toLocaleString('fr-CH', {
          dateStyle: 'medium',
          timeStyle: 'short'
        })

        return (
          <div key={alert.id} className="alert-item">
            <div className="alert-item__header">
              <h4>{alert.borderPointName}</h4>
              <div className="alert-badges">
                <StatusBadge label={severity.label} variant={severity.variant} />
                <StatusBadge label={trend.label} variant={trend.variant} />
              </div>
            </div>
            <p className="alert-message">{alert.message}</p>
            <div className="alert-item__footer">
              <span>{createdAt}</span>
              <span className={alert.isPosted ? 'posted' : 'not-posted'}>
                {alert.isPosted ? 'Publié' : 'Non'}
              </span>
            </div>
          </div>
        )
      })}
    </div>
  )
}
