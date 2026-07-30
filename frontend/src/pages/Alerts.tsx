import { useCallback, useEffect, useState } from 'react'
import { fetchAlerts, fetchHereHistory, fetchHereQuota, fetchTrafficForecast } from '../api/borderApi'
import AlertList from '../components/AlertList'
import type { AlertEvent, HereHistoryEntry, HereQuotaStatus, TrafficForecast } from '../types'
import { parseUtcDate } from '../utils/date'

export default function Alerts() {
  const [alerts, setAlerts] = useState<AlertEvent[]>([])
  const [history, setHistory] = useState<HereHistoryEntry[]>([])
  const [quota, setQuota] = useState<HereQuotaStatus | null>(null)
  const [forecast, setForecast] = useState<TrafficForecast | null>(null)
  const [notificationsEnabled, setNotificationsEnabled] = useState(
    () => typeof Notification !== 'undefined' && Notification.permission === 'granted'
  )
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  const load = useCallback(async () => {
    try {
      setError(null)
      const [alertData, historyData, quotaData, forecastData] = await Promise.all([
        fetchAlerts(),
        fetchHereHistory(),
        fetchHereQuota(),
        fetchTrafficForecast()
      ])
      setAlerts(alertData)
      setHistory(historyData)
      setQuota(quotaData)
      setForecast(forecastData)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Erreur inconnue')
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => {
    void load()
  }, [load])

  useEffect(() => {
    if (!quota || quota.level === 'Normal' || !notificationsEnabled) return

    const notificationKey = `here-quota:${quota.dateUtc}:${quota.level}`
    if (localStorage.getItem(notificationKey)) return

    new Notification(`Quota HERE · ${quota.usagePercent}%`, {
      body: `${quota.requestsUsed}/${quota.dailyLimit} appels utilisés. ${quota.message}`
    })
    localStorage.setItem(notificationKey, 'sent')
  }, [notificationsEnabled, quota])

  const enableNotifications = async () => {
    if (typeof Notification === 'undefined') return
    const permission = await Notification.requestPermission()
    setNotificationsEnabled(permission === 'granted')
  }

  return (
    <section className="page alerts-dashboard">
      <div className="page-header">
        <div>
          <span className="section-kicker">Suivi & anticipation</span>
          <h1>Alertes trafic</h1>
          <p className="subtitle">Historique HERE, consommation du quota et créneaux à privilégier.</p>
        </div>
        <button className="refresh-button" onClick={() => void load()} disabled={loading}>Actualiser</button>
      </div>

      {error && <div className="error-banner">{error}</div>}

      {loading ? <div className="loading-panel"><span className="loader" /> Chargement…</div> : (
        <>
          {quota && (
            <section className={`quota-card quota-card--${quota.level.toLowerCase()}`}>
              <div>
                <span className="section-kicker">Garde-fou HERE</span>
                <h2>{quota.requestsUsed} <small>/ {quota.dailyLimit} appels aujourd’hui</small></h2>
                <p>{quota.message}</p>
                {!notificationsEnabled && (
                  <button className="quota-notification-button" onClick={() => void enableNotifications()}>
                    Activer les notifications quota
                  </button>
                )}
              </div>
              <div className="quota-gauge" style={{ '--quota': `${quota.usagePercent}%` } as React.CSSProperties}>
                <strong>{quota.usagePercent}%</strong>
                <span>{quota.requestsRemaining} restants</span>
              </div>
              <div className="quota-progress"><i style={{ width: `${quota.usagePercent}%` }} /></div>
            </section>
          )}

          {forecast && (
            <section className="forecast-panel">
              <div className="forecast-panel__header">
                <div><span className="section-kicker">Prévisions locales</span><h2>Quand passer&nbsp;?</h2></div>
                <span>{forecast.daysCovered}/{forecast.minimumDaysRequired} jours · {forecast.samplesCount} mesures</span>
              </div>
              {!forecast.isAvailable ? (
                <div className="forecast-learning"><span>↻</span><div><strong>Apprentissage en cours</strong><p>{forecast.message}</p></div></div>
              ) : (
                <div className="forecast-grid">
                  {forecast.suggestions.map((suggestion) => (
                    <article key={suggestion.direction}>
                      <span>{suggestion.directionLabel}</span>
                      <strong>{suggestion.bestDay} · {suggestion.bestHourStart}h–{suggestion.bestHourStart + 2}h</strong>
                      <p>{suggestion.averageDelayMinutes} min de retard moyen observé</p>
                      <small>{suggestion.confidencePercent}% confiance · {suggestion.sampleSize} mesures</small>
                    </article>
                  ))}
                </div>
              )}
              <p className="forecast-disclaimer">Les habitudes historiques aident à choisir, mais un accident ou des travaux peuvent modifier la situation.</p>
            </section>
          )}

          <section className="history-panel">
            <div className="history-panel__header">
              <div><span className="section-kicker">Mesures réelles</span><h2>Historique HERE</h2></div>
              <span>{history.length} derniers relevés</span>
            </div>
            {history.length === 0 ? (
              <p className="empty-state">La première mesure sera enregistrée au prochain cycle HERE.</p>
            ) : (
              <div className="history-table">
                {history.slice(0, 100).map((entry) => (
                  <div className="history-row" key={entry.id}>
                    <span className={`history-level history-level--${entry.congestionLevel.toLowerCase()}`} />
                    <strong>{entry.borderPointName}</strong>
                    <span>{entry.directionLabel}</span>
                    <b>+{entry.delayMinutes} min</b>
                    <time>{parseUtcDate(entry.observedAtUtc).toLocaleString('fr-CH', { dateStyle: 'short', timeStyle: 'short' })}</time>
                  </div>
                ))}
              </div>
            )}
          </section>

          <section>
            <div className="history-panel__header"><div><span className="section-kicker">Événements</span><h2>Alertes générées</h2></div></div>
            <AlertList alerts={alerts} />
          </section>
        </>
      )}
    </section>
  )
}
