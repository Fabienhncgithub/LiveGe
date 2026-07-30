import * as maplibregl from 'maplibre-gl'
import type { Map as MapLibreMap, Marker as MapLibreMarker } from 'maplibre-gl'
import maplibreWorkerUrl from 'maplibre-gl/dist/maplibre-gl-worker.mjs?worker&url'
import { useEffect, useMemo, useRef, useState } from 'react'
import type { AdviceRecommendation, RoadSignal, RouteAdvice } from '../types'

maplibregl.setWorkerUrl(maplibreWorkerUrl)

interface TrafficMapProps {
  routes: RouteAdvice[]
  signals: RoadSignal[]
  selectedDirection: 'ToGeneva' | 'ToFrance'
}

interface MapLocation {
  coordinates: [number, number]
  labelOffset: [number, number]
}

const locations: Record<string, MapLocation> = {
  Bardonnex: { coordinates: [6.1279, 46.1406], labelOffset: [0, 0] },
  Perly: { coordinates: [6.0754, 46.1083], labelOffset: [0, 0] },
  Moillesulaz: { coordinates: [6.2101, 46.1876], labelOffset: [-62, 18] },
  'Thônex-Vallard': { coordinates: [6.2156, 46.1935], labelOffset: [65, -16] },
  Anières: { coordinates: [6.222, 46.276], labelOffset: [0, 0] },
  Meyrin: { coordinates: [6.079, 46.234], labelOffset: [-55, 10] },
  'Ferney-Voltaire': { coordinates: [6.108, 46.255], labelOffset: [58, -12] }
}

const recommendationLabel: Record<AdviceRecommendation, string> = {
  Recommended: 'Recommandé',
  Equivalent: 'Équivalent',
  Alternative: 'Alternative',
  Avoid: 'À éviter',
  Unavailable: 'Indisponible'
}

const markerClass = (route: RouteAdvice | undefined) => {
  if (!route?.isAvailable) return 'unavailable'
  return route.recommendation.toLowerCase()
}

const signalSeverity = (severity: string) => {
  if (severity === 'Critical') return { className: 'critical', label: 'Important' }
  if (severity === 'Warning') return { className: 'warning', label: 'À surveiller' }
  return { className: 'info', label: 'Information' }
}

const validCoordinate = (value: number | null | undefined): value is number =>
  typeof value === 'number' && Number.isFinite(value)

const safeDetailsUrl = (value: string | null | undefined) => {
  if (!value) return undefined
  try {
    const url = new URL(value)
    return url.protocol === 'https:' ? url.toString() : undefined
  } catch {
    return undefined
  }
}

const signalDate = (signal: RoadSignal) => {
  const value = signal.observedAtUtc ?? signal.startsAtUtc
  if (!value) return null
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return null
  return date.toLocaleString('fr-CH', {
    day: '2-digit',
    month: '2-digit',
    hour: '2-digit',
    minute: '2-digit'
  })
}

const appendText = <K extends keyof HTMLElementTagNameMap>(
  parent: HTMLElement,
  tag: K,
  text: string,
  className?: string
) => {
  const element = document.createElement(tag)
  element.textContent = text
  if (className) element.className = className
  parent.append(element)
  return element
}

const createSignalPopup = (signal: RoadSignal) => {
  const content = document.createElement('div')
  content.className = 'map-popup'
  const severity = signalSeverity(signal.severity)
  appendText(content, 'span', signal.category, `map-signal-label map-signal-label--${severity.className}`)
  appendText(content, 'h3', signal.title, 'map-popup-title')
  if (signal.description) appendText(content, 'p', signal.description)

  const metadata = `Source : ${signal.sourceName}${signalDate(signal) ? ` · ${signalDate(signal)}` : ''}`
  appendText(content, 'small', metadata)

  const detailsUrl = safeDetailsUrl(signal.detailsUrl)
  if (detailsUrl) {
    const paragraph = document.createElement('p')
    const link = document.createElement('a')
    link.href = detailsUrl
    link.target = '_blank'
    link.rel = 'noreferrer noopener'
    link.textContent = 'Détails officiels ↗'
    paragraph.append(link)
    content.append(paragraph)
  }

  return content
}

const createRoutePopup = (name: string, route: RouteAdvice | undefined) => {
  const content = document.createElement('div')
  content.className = 'map-popup'
  appendText(content, 'strong', name)

  if (!route?.isAvailable) {
    appendText(content, 'p', route?.unavailableReason ?? 'Aucune mesure de trafic disponible.')
    return content
  }

  appendText(
    content,
    'p',
    `${route.directionLabel} : ${route.travelTimeMinutes ?? '—'} min, dont +${route.delayMinutes ?? 0} min de retard.`
  )
  appendText(
    content,
    'p',
    `${recommendationLabel[route.recommendation]} · ${route.dataCoveragePercent}% de couverture des données.`
  )
  if (route.isStale) {
    appendText(content, 'p', `Mesure en cache depuis ${route.ageMinutes ?? 'plusieurs'} minutes.`)
  }
  route.reasons.slice(0, 2).forEach((reason) => {
    appendText(content, 'small', `${reason.label} — ${reason.sourceName}`, 'map-popup-reason')
  })
  return content
}

const createRouteLabel = (name: string, route: RouteAdvice | undefined) => {
  const element = document.createElement('button')
  element.type = 'button'
  element.className = `route-map-label route-map-label--${markerClass(route)}`
  element.setAttribute(
    'aria-label',
    `${name}, ${route?.isAvailable ? `retard ${route.delayMinutes ?? 0} minutes, ${recommendationLabel[route.recommendation]}` : 'données indisponibles'}`
  )
  appendText(element, 'strong', name)
  appendText(element, 'span', route?.isAvailable ? `+${route.delayMinutes ?? 0} min` : '—')
  appendText(element, 'small', route ? recommendationLabel[route.recommendation] : 'Indisponible')
  return element
}

export default function TrafficMap({ routes, signals, selectedDirection }: TrafficMapProps) {
  const containerRef = useRef<HTMLDivElement>(null)
  const mapRef = useRef<MapLibreMap | null>(null)
  const markersRef = useRef<MapLibreMarker[]>([])
  const [mapUnavailable, setMapUnavailable] = useState(false)
  const selectedRoutes = useMemo(
    () => routes.filter((route) => route.direction === selectedDirection),
    [routes, selectedDirection]
  )
  const globalSignals = signals.filter((signal) => signal.appliesToAllRoutes)

  useEffect(() => {
    if (!containerRef.current || mapRef.current) return

    let map: MapLibreMap
    try {
      map = new maplibregl.Map({
        container: containerRef.current,
        style: 'https://tiles.openfreemap.org/styles/liberty',
        center: [6.145, 46.198],
        zoom: 10.4,
        minZoom: 9.5,
        maxZoom: 17,
        attributionControl: false
      })
    } catch {
      setMapUnavailable(true)
      return
    }
    map.addControl(new maplibregl.NavigationControl({ showCompass: false }), 'bottom-right')
    map.addControl(new maplibregl.AttributionControl({ compact: true }), 'bottom-right')
    mapRef.current = map

    return () => {
      markersRef.current.forEach((marker) => marker.remove())
      markersRef.current = []
      map.remove()
      mapRef.current = null
    }
  }, [])

  useEffect(() => {
    const map = mapRef.current
    if (!map) return

    markersRef.current.forEach((marker) => marker.remove())
    const nextMarkers: MapLibreMarker[] = []

    signals.forEach((signal) => {
      if (!validCoordinate(signal.latitude) || !validCoordinate(signal.longitude)) return

      const severity = signalSeverity(signal.severity)
      const element = document.createElement('button')
      element.type = 'button'
      element.className = `map-signal-marker map-signal-marker--${severity.className}`
      element.title = `${signal.title} · ${severity.label}`
      element.setAttribute('aria-label', `${signal.title}, ${severity.label}, source ${signal.sourceName}`)

      const marker = new maplibregl.Marker({ element, anchor: 'center' })
        .setLngLat([signal.longitude, signal.latitude])
        .setPopup(new maplibregl.Popup({ offset: 14, maxWidth: '320px' }).setDOMContent(createSignalPopup(signal)))
        .addTo(map)
      nextMarkers.push(marker)
    })

    Object.entries(locations).forEach(([name, location]) => {
      const route = selectedRoutes.find((item) => item.borderPointName === name)

      const dot = document.createElement('span')
      dot.className = 'route-crossing-dot'
      dot.setAttribute('aria-hidden', 'true')
      nextMarkers.push(
        new maplibregl.Marker({ element: dot, anchor: 'center' })
          .setLngLat(location.coordinates)
          .addTo(map)
      )

      const label = createRouteLabel(name, route)
      const popup = new maplibregl.Popup({ offset: 16, maxWidth: '320px' })
        .setDOMContent(createRoutePopup(name, route))
      nextMarkers.push(
        new maplibregl.Marker({
          element: label,
          anchor: 'bottom',
          offset: location.labelOffset
        })
          .setLngLat(location.coordinates)
          .setPopup(popup)
          .addTo(map)
      )
    })

    markersRef.current = nextMarkers
    return () => {
      nextMarkers.forEach((marker) => marker.remove())
      if (markersRef.current === nextMarkers) markersRef.current = []
    }
  }, [routes, selectedDirection, signals, selectedRoutes])

  return (
    <div className="real-map">
      <div
        className="real-map__canvas"
        ref={containerRef}
        role="application"
        aria-label="Carte interactive des passages frontaliers et signaux routiers"
      />
      {mapUnavailable && (
        <div className="map-unavailable" role="status">
          <strong>Fond de carte indisponible</strong>
          <span>Votre navigateur doit prendre en charge WebGL2. Les conseils détaillés restent disponibles sous la carte.</span>
        </div>
      )}

      {globalSignals.length > 0 && (
        <div className="map-context">
          <strong>Contexte commun</strong>
          {globalSignals.slice(0, 2).map((signal) => (
            <span key={signal.id}>
              <i className={`map-context__dot map-context__dot--${signalSeverity(signal.severity).className}`} />
              <b>{signal.title}</b>
              <small>{signal.sourceName}</small>
            </span>
          ))}
        </div>
      )}

      <div className="real-map__key">
        <strong>{selectedDirection === 'ToGeneva' ? 'FR → GE · vers Genève' : 'GE → FR · vers la France'}</strong>
        <span><i className="map-key-dot map-key-dot--recommended" /> Recommandé</span>
        <span><i className="map-key-dot map-key-dot--equivalent" /> Équivalent</span>
        <span><i className="map-key-dot map-key-dot--alternative" /> Alternative</span>
        <span><i className="map-key-dot map-key-dot--avoid" /> À éviter</span>
        <span><i className="map-key-dot map-key-dot--signal" /> Travaux / événement</span>
        {!selectedRoutes.some((route) => route.isAvailable) && (
          <small>Aucune mesure de trafic en cache pour ce sens.</small>
        )}
      </div>
    </div>
  )
}
