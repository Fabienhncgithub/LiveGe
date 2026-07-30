import { divIcon } from 'leaflet'
import { Fragment } from 'react'
import { CircleMarker, MapContainer, Marker, Popup, TileLayer, ZoomControl } from 'react-leaflet'
import type { DirectionalTraffic, LiveBorderStatus } from '../types'

interface TrafficMapProps {
  statuses: LiveBorderStatus[]
  directions: DirectionalTraffic[]
  selectedDirection: 'ToGeneva' | 'ToFrance'
}

const locations: Record<string, [number, number]> = {
  Bardonnex: [46.1406, 6.1279],
  Perly: [46.1083, 6.0754],
  Moillesulaz: [46.1876, 6.2101],
  'Thônex-Vallard': [46.1935, 6.2156],
  Anières: [46.276, 6.222],
  Meyrin: [46.234, 6.079],
  'Ferney-Voltaire': [46.255, 6.108]
}

const lane = (direction: DirectionalTraffic | undefined, label: string) => {
  const level = direction?.congestionLevel.toLowerCase() ?? 'unknown'
  const value = direction?.isAvailable ? `+${direction.delayMinutes ?? 0} min` : '—'
  return `<span class="real-map-lane real-map-lane--${level}"><b>${label}</b><em>${value}</em></span>`
}

export default function TrafficMap({ statuses, directions, selectedDirection }: TrafficMapProps) {
  return (
    <div className="real-map">
      <MapContainer
        center={[46.198, 6.145]}
        zoom={11.5}
        zoomSnap={0.5}
        minZoom={10}
        maxZoom={16}
        zoomControl={false}
      >
        <TileLayer
          attribution='&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a>'
          url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png"
        />
        <ZoomControl position="bottomright" />
        {statuses.map((status) => {
          const position = locations[status.borderPointName]
          if (!position) return null

          const matches = directions.filter((item) => item.borderPointName === status.borderPointName)
          const toGeneva = matches.find((item) => item.direction === 'ToGeneva')
          const toFrance = matches.find((item) => item.direction === 'ToFrance')
          const selected = selectedDirection === 'ToGeneva' ? toGeneva : toFrance
          const selectedLabel = selectedDirection === 'ToGeneva' ? '→ GE' : '→ FR'
          const isMoillesulaz = status.borderPointName === 'Moillesulaz'
          const isThonex = status.borderPointName === 'Thônex-Vallard'
          const icon = divIcon({
            className: 'real-map-marker',
            iconSize: [118, 58],
            iconAnchor: isMoillesulaz ? [125, 45] : isThonex ? [-7, 45] : [59, 58],
            html: `<div class="real-map-marker__card"><strong>${status.borderPointName}</strong><div>${lane(selected, selectedLabel)}</div></div><i></i>`
          })

          return (
            <Fragment key={status.borderPointId}>
              <CircleMarker center={position} radius={5} pathOptions={{ color: '#ffffff', weight: 2, fillColor: '#0b4638', fillOpacity: 1 }} />
              <Marker position={position} icon={icon}>
                <Popup>
                  <strong>{status.borderPointName}</strong>
                  <p>France → Genève : {toGeneva?.isAvailable ? `+${toGeneva.delayMinutes ?? 0} min` : 'donnée indisponible'}</p>
                  <p>Genève → France : {toFrance?.isAvailable ? `+${toFrance.delayMinutes ?? 0} min` : 'donnée indisponible'}</p>
                  <small>Source : {toGeneva?.sourceName ?? 'HERE Traffic'}</small>
                </Popup>
              </Marker>
            </Fragment>
          )
        })}
      </MapContainer>
      <div className="real-map__key">
        <strong>Sens affiché</strong>
        <span>{selectedDirection === 'ToGeneva' ? <><b>FR → GE</b> vers Genève</> : <><b>GE → FR</b> vers la France</>}</span>
        {!directions.some((item) => item.isAvailable) && <small>Clé HERE requise pour afficher les délais réels.</small>}
      </div>
    </div>
  )
}
