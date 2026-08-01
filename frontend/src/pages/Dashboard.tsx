import { useCallback, useEffect, useMemo, useState } from 'react'
import { fetchMobilityAdvice } from '../api/borderApi'
import TrafficMap from '../components/TrafficMap'
import type {
  AdviceRecommendation,
  DataSourceStatus,
  MobilityAdvice,
  RouteAdvice,
  SourceAvailability
} from '../types'

type SelectedDirection = 'ToGeneva' | 'ToFrance'

const trendContent = {
  Rising: { icon: '↗', label: 'Se dégrade', className: 'rising' },
  Stable: { icon: '→', label: 'Stable', className: 'stable' },
  Falling: { icon: '↘', label: 'S’améliore', className: 'falling' },
  Unknown: { icon: '·', label: 'Tendance inconnue', className: 'unknown' }
} as const

const recommendationContent: Record<AdviceRecommendation, {
  label: string
  shortLabel: string
  className: string
}> = {
  Recommended: { label: 'Passage recommandé', shortLabel: 'Recommandé', className: 'recommended' },
  Equivalent: { label: 'Passage équivalent', shortLabel: 'Équivalent', className: 'equivalent' },
  Alternative: { label: 'Alternative possible', shortLabel: 'Alternative', className: 'alternative' },
  Avoid: { label: 'Passage à éviter', shortLabel: 'À éviter', className: 'avoid' },
  Unavailable: { label: 'Données insuffisantes', shortLabel: 'Indisponible', className: 'unavailable' }
}

const sourceStatusContent: Record<SourceAvailability, { label: string; className: string }> = {
  Online: { label: 'À jour', className: 'online' },
  Stale: { label: 'En cache', className: 'stale' },
  Unavailable: { label: 'Indisponible', className: 'unavailable' }
}

const recommendationOrder: Record<AdviceRecommendation, number> = {
  Recommended: 0,
  Equivalent: 1,
  Alternative: 2,
  Avoid: 3,
  Unavailable: 4
}

const congestionClass = (level: RouteAdvice['congestionLevel']) => {
  if (level === 'Green' || level === 'Orange' || level === 'Red') return level.toLowerCase()
  return 'unknown'
}

const reasonSeverityClass = (severity: string) => {
  if (severity === 'Critical' || severity === 'Red') return 'critical'
  if (severity === 'Warning' || severity === 'Orange') return 'warning'
  if (severity === 'Green') return 'green'
  return 'info'
}

const sourceUrl = (source: DataSourceStatus) => {
  try {
    const url = new URL(source.sourceUrl)
    return url.protocol === 'https:' ? url.toString() : undefined
  } catch {
    return undefined
  }
}

const formattedObservation = (route: RouteAdvice) => {
  if (!route.observedAtUtc) return 'heure inconnue'
  const date = new Date(route.observedAtUtc)
  if (Number.isNaN(date.getTime())) return 'heure inconnue'
  return date.toLocaleTimeString('fr-CH', {
    hour: '2-digit',
    minute: '2-digit'
  })
}

const trafficLabel = (route: RouteAdvice) => {
  if (!route.isAvailable) return 'Pas de mesure'
  if (route.isStale) return 'Mesure en cache'
  if ((route.delayMinutes ?? 0) >= 15) return 'Fortement chargé'
  if ((route.delayMinutes ?? 0) >= 7) return 'Ralenti'
  return 'Fluide'
}

export default function Dashboard() {
  const [advice, setAdvice] = useState<MobilityAdvice | null>(null)
  const [selectedDirection, setSelectedDirection] = useState<SelectedDirection>('ToGeneva')
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  const load = useCallback(async () => {
    try {
      setLoading(true)
      setError(null)
      setAdvice(await fetchMobilityAdvice())
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

  const selectedRoutes = useMemo(
    () => (advice?.routes ?? [])
      .filter((route) => route.direction === selectedDirection)
      .sort((a, b) =>
        recommendationOrder[a.recommendation] - recommendationOrder[b.recommendation]
        || a.decisionCost - b.decisionCost
      ),
    [advice, selectedDirection]
  )

  const availableRoutes = selectedRoutes.filter((route) => route.isAvailable)
  const freshAvailableRoutes = availableRoutes.filter((route) => !route.isStale)
  const hasReliableComparison = freshAvailableRoutes.length >= 2
  const favorable = availableRoutes.find((route) => route.recommendation === 'Recommended')
    ?? availableRoutes.find((route) => route.recommendation === 'Equivalent')
    ?? availableRoutes.find((route) => route.recommendation === 'Alternative')
  const avoid = [...availableRoutes]
    .filter((route) => route.recommendation === 'Avoid')
    .sort((a, b) => b.decisionCost - a.decisionCost)[0]
  const activeSources = advice?.sources.filter((source) => source.status !== 'Unavailable').length ?? 0
  const totalSources = advice?.sources.length ?? 0
  const hasTrafficData = advice?.routes.some((route) => route.isAvailable) ?? false
  const generatedDate = advice ? new Date(advice.generatedAtUtc) : null
  const generatedAt = generatedDate && !Number.isNaN(generatedDate.getTime())
    ? generatedDate.toLocaleTimeString('fr-CH', { hour: '2-digit', minute: '2-digit' })
    : '--:--'

  return (
    <section className="page decision-dashboard">
      <div className={`source-banner ${hasTrafficData ? 'source-banner--online' : 'source-banner--offline'}`}>
        <span className="source-banner__dot" />
        <strong>{hasTrafficData ? 'Fusion multi-source active' : 'Mesures trafic indisponibles'}</strong>
        <span>
          {advice
            ? `${activeSources}/${totalSources} sources répondent · trafic, travaux, événements routiers et météo`
            : 'Connexion aux sources publiques en cours…'}
        </span>
      </div>

      <header className="decision-header">
        <div>
          <div className="eyebrow"><span /> Frontières du Grand Genève</div>
          <h1>Quel passage choisir&nbsp;?</h1>
          <p>Le passage le plus favorable sur l’approche, expliqué par des données réelles.</p>
        </div>
        <div className="update-control">
          <span className="update-time">Données générées à {generatedAt}</span>
          <button className="refresh-button" onClick={() => void load()} disabled={loading}>
            <svg viewBox="0 0 24 24" aria-hidden="true"><path d="M20 11a8.1 8.1 0 0 0-15.5-3M4 4v4h4M4 13a8.1 8.1 0 0 0 15.5 3M20 20v-4h-4" /></svg>
            Actualiser
          </button>
        </div>
      </header>

      <div className="direction-switch" role="group" aria-label="Sens du trajet">
        <button
          className={selectedDirection === 'ToGeneva' ? 'active' : ''}
          onClick={() => setSelectedDirection('ToGeneva')}
          aria-pressed={selectedDirection === 'ToGeneva'}
        >
          <span>FR</span><i>→</i><span>GE</span>
          <small>Je vais vers Genève</small>
        </button>
        <button
          className={selectedDirection === 'ToFrance' ? 'active' : ''}
          onClick={() => setSelectedDirection('ToFrance')}
          aria-pressed={selectedDirection === 'ToFrance'}
        >
          <span>GE</span><i>→</i><span>FR</span>
          <small>Je vais vers la France</small>
        </button>
      </div>

      <section className="traffic-glance" aria-label="État du trafic en un coup d’œil">
        <div className="traffic-glance__heading">
          <div>
            <span className="section-kicker">Maintenant</span>
            <strong>{selectedDirection === 'ToGeneva' ? 'Entrée vers Genève' : 'Sortie vers la France'}</strong>
          </div>
          <small>Retard par rapport à un trajet fluide · cliquer sur un passage dans la carte pour le détail</small>
        </div>
        <div className="traffic-glance__routes">
          {selectedRoutes.map((route) => {
            const trend = trendContent[route.trend]
            return (
              <article
                className={`traffic-glance__route traffic-glance__route--${congestionClass(route.congestionLevel)}`}
                key={`glance-${route.borderPointName}-${route.direction}`}
              >
                <span className="traffic-glance__level" aria-hidden="true" />
                <div>
                  <strong>{route.borderPointName}</strong>
                  <small>{trafficLabel(route)}</small>
                </div>
                <b>{route.isAvailable ? `+${route.delayMinutes ?? 0}` : '—'}<small> min</small></b>
                <span className={`traffic-glance__trend traffic-glance__trend--${trend.className}`}>
                  {trend.icon} {trend.label}
                </span>
              </article>
            )
          })}
        </div>
      </section>

      {error && <div className="error-banner">{error}</div>}

      {loading && !advice ? (
        <div className="loading-panel"><span className="loader" /> Fusion des sources en cours…</div>
      ) : advice ? (
        <>
          {favorable ? (
            <div className={`instant-answer ${avoid ? '' : 'instant-answer--single'}`}>
              <div className="instant-answer__best">
                <span>
                  {favorable.isStale
                    ? 'Dernière mesure disponible'
                    : hasReliableComparison
                      ? 'Passage le plus favorable sur l’approche'
                      : 'Seul passage avec une mesure récente'}
                </span>
                <strong>{favorable.borderPointName}</strong>
                <b>+{favorable.delayMinutes ?? 0} min</b>
                <small>
                  {hasReliableComparison
                    ? recommendationContent[favorable.recommendation].label
                    : 'Comparaison insuffisante pour désigner un gagnant'}
                  {' · '}{favorable.travelTimeMinutes ?? '—'} min de parcours mesuré
                  {' · '}{favorable.dataCoveragePercent}% de couverture
                  {favorable.isStale && ` · cache de ${favorable.ageMinutes ?? 'plusieurs'} min`}
                </small>
              </div>
              {avoid && (
                <div className="instant-answer__avoid">
                  <span>Conditions défavorables</span>
                  <strong>{avoid.borderPointName}</strong>
                  <b>+{avoid.delayMinutes ?? 0} min</b>
                  <small>Retard ou contexte routier pénalisant sur l’approche</small>
                </div>
              )}
            </div>
          ) : (
            <div className="no-live-data">
              <span className="no-live-data__icon">!</span>
              <div>
                <strong>Aucune comparaison fiable pour ce sens</strong>
                <p>Les signaux publics restent visibles, mais aucun passage n’est conseillé sans mesure de trafic disponible.</p>
              </div>
            </div>
          )}

          <div className="map-stage">
            <TrafficMap
              routes={advice.routes}
              signals={advice.signals}
              selectedDirection={selectedDirection}
            />
          </div>

          <div className="ranking-heading">
            <div>
              <span className="section-kicker">Décision expliquée</span>
              <h2>{selectedDirection === 'ToGeneva' ? 'France → Genève' : 'Genève → France'}</h2>
            </div>
            <div className="ranking-legend">
              <span className="green">Recommandé</span>
              <span className="orange">Équivalent</span>
              <span className="red">À éviter</span>
            </div>
          </div>

          <div className="direction-ranking">
            {selectedRoutes.map((route, index) => {
              const recommendation = recommendationContent[route.recommendation]
              const trend = trendContent[route.trend]
              return (
                <article
                  className={`direction-result direction-result--${congestionClass(route.congestionLevel)} direction-result--recommendation-${recommendation.className}`}
                  key={`${route.borderPointName}-${route.direction}`}
                >
                  <span className="direction-result__rank">{route.isAvailable ? index + 1 : '—'}</span>
                  <div className="direction-result__name">
                    <strong>{route.borderPointName}</strong>
                    <span className={`recommendation-pill recommendation-pill--${recommendation.className}`}>
                      {recommendation.shortLabel}
                    </span>
                  </div>
                  <div className="direction-result__times">
                    <div className="direction-result__delay">
                      {route.isAvailable
                        ? <><strong>+{route.delayMinutes ?? 0}</strong><span>min<br />de retard</span></>
                        : <><strong>—</strong><span>pas de<br />mesure</span></>}
                    </div>
                    <small>{route.travelTimeMinutes ?? '—'} min sur l’approche</small>
                  </div>
                  <div className={`direction-result__trend direction-result__trend--${trend.className}`}>
                    <b>{trend.icon}</b><span>{trend.label}</span>
                  </div>
                  <div className="direction-result__quality">
                    <strong>{route.isAvailable ? `${route.dataCoveragePercent}%` : '—'}</strong>
                    <span>couverture des données</span>
                    {route.isAvailable && (
                      <small className={route.isStale ? 'stale-reading' : ''}>
                        {route.isStale ? `cache de ${route.ageMinutes ?? 'plusieurs'} min` : `mesure à ${formattedObservation(route)}`}
                      </small>
                    )}
                  </div>
                  <div className="direction-result__reasons">
                    {route.reasons.length > 0 ? route.reasons.map((reason, reasonIndex) => (
                      <span
                        className={`advice-reason advice-reason--${reasonSeverityClass(reason.severity)}`}
                        key={`${reason.kind}-${reason.sourceName}-${reasonIndex}`}
                      >
                        <b>{reason.kind}</b>
                        {reason.label}
                        <small>{reason.sourceName}</small>
                      </span>
                    )) : (
                      <span className="advice-reason advice-reason--info">
                        {route.unavailableReason ?? 'Aucun signal contextuel pertinent à proximité.'}
                      </span>
                    )}
                  </div>
                </article>
              )
            })}
          </div>

          <section className="source-panel" aria-labelledby="source-panel-title">
            <div className="source-panel__header">
              <div>
                <span className="section-kicker">Traçabilité</span>
                <h2 id="source-panel-title">D’où viennent les informations&nbsp;?</h2>
              </div>
              <span>{activeSources}/{totalSources} sources disponibles</span>
            </div>
            <div className="source-grid">
              {advice.sources.map((source) => {
                const status = sourceStatusContent[source.status]
                const url = sourceUrl(source)
                return (
                  <article className={`source-card source-card--${status.className}`} key={source.id}>
                    <div className="source-card__title">
                      <strong>{source.name}</strong>
                      <span className={`source-state source-state--${status.className}`}>{status.label}</span>
                    </div>
                    <div className="source-card__tags">
                      {source.isOfficial && <span>Source officielle</span>}
                      {source.hasBillingRisk ? <span className="source-tag--cost">Quota surveillé</span> : <span>Sans clé payante</span>}
                    </div>
                    <p>{source.coverage}</p>
                    <small>
                      {source.recordsCount} enregistrements · {source.relevantSignalsCount} signaux retenus
                    </small>
                    {source.message && <em>{source.message}</em>}
                    {url
                      ? <a href={url} target="_blank" rel="noreferrer noopener">Voir la source ↗</a>
                      : <span className="source-card__attribution">{source.attribution}</span>}
                  </article>
                )
              })}
            </div>
          </section>

          <section className="data-transparency">
            <strong>Ce que le conseil compare</strong>
            <p>{advice.scopeNotice}</p>
            <span>
              Retard TomTom + proximité des travaux et événements publics + contexte météo ·
              algorithme {advice.algorithmVersion}
            </span>
          </section>
        </>
      ) : null}
    </section>
  )
}
