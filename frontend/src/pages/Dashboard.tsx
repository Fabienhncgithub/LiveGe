import { useCallback, useEffect, useMemo, useState } from 'react'
import { fetchDirectionalTraffic, fetchLiveStatuses } from '../api/borderApi'
import TrafficMap from '../components/TrafficMap'
import type { DirectionalTraffic, LiveBorderStatus } from '../types'

type SelectedDirection = 'ToGeneva' | 'ToFrance'

const trendContent = {
  Rising: { icon: '↗', label: 'Se dégrade', className: 'rising' },
  Stable: { icon: '→', label: 'Stable', className: 'stable' },
  Falling: { icon: '↘', label: 'S’améliore', className: 'falling' },
  Unknown: { icon: '·', label: 'Tendance inconnue', className: 'unknown' }
} as const

const levelContent = {
  Green: { label: 'Bon choix', advice: 'Passage fluide' },
  Orange: { label: 'À surveiller', advice: 'Ralentissements' },
  Red: { label: 'À éviter', advice: 'Forte congestion' },
  Unknown: { label: 'Indisponible', advice: 'Aucune mesure réelle' }
} as const

export default function Dashboard() {
  const [statuses, setStatuses] = useState<LiveBorderStatus[]>([])
  const [directions, setDirections] = useState<DirectionalTraffic[]>([])
  const [selectedDirection, setSelectedDirection] = useState<SelectedDirection>('ToGeneva')
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [lastUpdated, setLastUpdated] = useState<Date | null>(null)

  const load = useCallback(async () => {
    try {
      setError(null)
      const [points, directionalData] = await Promise.all([fetchLiveStatuses(), fetchDirectionalTraffic()])
      setStatuses(points)
      setDirections(directionalData)
      setLastUpdated(new Date())
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Erreur inconnue')
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => {
    void load()
    const timer = setInterval(() => void load(), 60000)
    return () => clearInterval(timer)
  }, [load])

  const selectedReadings = useMemo(
    () => directions
      .filter((item) => item.direction === selectedDirection)
      .sort((a, b) => {
        if (a.isAvailable !== b.isAvailable) return a.isAvailable ? -1 : 1
        return (a.delayMinutes ?? Number.MAX_SAFE_INTEGER) - (b.delayMinutes ?? Number.MAX_SAFE_INTEGER)
      }),
    [directions, selectedDirection]
  )
  const available = selectedReadings.filter((item) => item.isAvailable)
  const best = available[0]
  const worst = available.length > 0 ? available[available.length - 1] : undefined
  const hasRealData = directions.some((item) => item.isAvailable)

  return (
    <section className="page decision-dashboard">
      <div className={`source-banner ${hasRealData ? 'source-banner--online' : 'source-banner--offline'}`}>
        <span className="source-banner__dot" />
        <strong>{hasRealData ? 'Trafic réel connecté' : 'Trafic réel non connecté'}</strong>
        <span>{hasRealData ? 'Source HERE Traffic · cache sécurisé de 30 minutes' : 'Ajoutez la clé HERE gratuite pour recevoir les mesures — aucune donnée simulée n’est affichée.'}</span>
      </div>

      <header className="decision-header">
        <div>
          <div className="eyebrow"><span /> Frontières du Grand Genève</div>
          <h1>Quel passage choisir&nbsp;?</h1>
          <p>Une réponse immédiate, séparée dans chaque sens.</p>
        </div>
        <div className="update-control">
          <span className="update-time">Actualisé à {lastUpdated?.toLocaleTimeString('fr-CH', { hour: '2-digit', minute: '2-digit' }) ?? '--:--'}</span>
          <button className="refresh-button" onClick={() => void load()} disabled={loading}>
            <svg viewBox="0 0 24 24"><path d="M20 11a8.1 8.1 0 0 0-15.5-3M4 4v4h4M4 13a8.1 8.1 0 0 0 15.5 3M20 20v-4h-4" /></svg>
            Actualiser
          </button>
        </div>
      </header>

      <div className="direction-switch" role="group" aria-label="Sens du trajet">
        <button className={selectedDirection === 'ToGeneva' ? 'active' : ''} onClick={() => setSelectedDirection('ToGeneva')}>
          <span>FR</span><i>→</i><span>GE</span>
          <small>Je vais vers Genève</small>
        </button>
        <button className={selectedDirection === 'ToFrance' ? 'active' : ''} onClick={() => setSelectedDirection('ToFrance')}>
          <span>GE</span><i>→</i><span>FR</span>
          <small>Je vais vers la France</small>
        </button>
      </div>

      {error && <div className="error-banner">{error}</div>}

      {loading ? (
        <div className="loading-panel"><span className="loader" /> Chargement du trafic réel…</div>
      ) : (
        <>
          {hasRealData && best && worst ? (
            <div className="instant-answer">
              <div className="instant-answer__best">
                <span>Meilleur choix maintenant</span>
                <strong>{best.borderPointName}</strong>
                <b>+{best.delayMinutes ?? 0} min</b>
              </div>
              <div className="instant-answer__avoid">
                <span>À éviter</span>
                <strong>{worst.borderPointName}</strong>
                <b>+{worst.delayMinutes ?? 0} min</b>
              </div>
            </div>
          ) : (
            <div className="no-live-data">
              <span className="no-live-data__icon">!</span>
              <div><strong>Aucune mesure de trafic réel disponible</strong><p>La carte reste utilisable, mais aucun passage n’est recommandé tant que HERE n’est pas connecté.</p></div>
            </div>
          )}

          <div className="map-stage">
            <TrafficMap statuses={statuses} directions={directions} selectedDirection={selectedDirection} />
          </div>

          <div className="ranking-heading">
            <div><span className="section-kicker">Décision rapide</span><h2>{selectedDirection === 'ToGeneva' ? 'Vers Genève' : 'Vers la France'}</h2></div>
            <div className="ranking-legend"><span className="green">Fluide</span><span className="orange">Chargé</span><span className="red">À éviter</span></div>
          </div>

          <div className="direction-ranking">
            {selectedReadings.map((reading, index) => {
              const level = levelContent[reading.congestionLevel]
              const trend = trendContent[reading.trend]
              return (
                <article className={`direction-result direction-result--${reading.congestionLevel.toLowerCase()}`} key={`${reading.borderPointName}-${reading.direction}`}>
                  <span className="direction-result__rank">{reading.isAvailable ? index + 1 : '—'}</span>
                  <div className="direction-result__name"><strong>{reading.borderPointName}</strong><span>{level.advice}</span></div>
                  <div className="direction-result__delay">
                    {reading.isAvailable ? <><strong>+{reading.delayMinutes ?? 0}</strong><span>min de retard</span></> : <><strong>—</strong><span>pas de mesure</span></>}
                  </div>
                  <div className={`direction-result__trend direction-result__trend--${trend.className}`}><b>{trend.icon}</b><span>{trend.label}</span></div>
                  <div className="direction-result__quality"><strong>{level.label}</strong><span>{reading.isAvailable ? `${reading.confidencePercent}% confiance` : 'HERE requis'}</span></div>
                </article>
              )
            })}
          </div>

          <section className="data-transparency">
            <strong>Comment lire ces résultats&nbsp;?</strong>
            <p>Le retard compare le temps de parcours actuel au temps sans congestion sur la route d’approche du poste. La tendance compare les relevés successifs. Une recommandation n’est produite que lorsque la source réelle répond.</p>
            <span>Source prévue : HERE Traffic · Incidents officiels OFROU à venir</span>
          </section>
        </>
      )}
    </section>
  )
}
