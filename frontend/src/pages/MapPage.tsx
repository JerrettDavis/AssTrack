import { Fragment, useCallback, useEffect, useMemo, useRef, useState, type ReactNode } from 'react'
import { useSearchParams } from 'react-router-dom'
import { Circle, CircleMarker, MapContainer, Marker, Pane, Polygon, Polyline, Popup, TileLayer, useMap, useMapEvents } from 'react-leaflet'
import L from 'leaflet'
import 'leaflet/dist/leaflet.css'
import markerIcon2x from 'leaflet/dist/images/marker-icon-2x.png'
import markerIcon from 'leaflet/dist/images/marker-icon.png'
import markerShadow from 'leaflet/dist/images/marker-shadow.png'
import { getGeofenceBreaches, getGeofences, type Geofence, type GeofenceBreach } from '../api/geofences'
import { type Observation, type ObservationTimeline, getLatestPositions, getDeviceTrail, getObservationHistory, getObservationTimeline } from '../api/observations'
import { getDevices, getDeviceSummary, updateDevice, type DeviceSummary } from '../api/devices'
import { createAsset, getAssets, type Asset, type Device } from '../api/assets'
import { getIntegrationFeeds, type IntegrationFeed } from '../api/integrations'
import { useLiveEvents } from '../hooks/useLiveEvents'
import { getSseStatus, type ObservationEvent } from '../api/sseClient'
import { useAppearance, type ThemeStyle } from '../context/AppearanceContext'

delete (L.Icon.Default.prototype as unknown as Record<string, unknown>)._getIconUrl
L.Icon.Default.mergeOptions({
  iconRetinaUrl: markerIcon2x,
  iconUrl: markerIcon,
  shadowUrl: markerShadow,
})

function ageTimestamp(observedAt: string, receivedAt?: string | null): string {
  const observedMs = new Date(observedAt).getTime()
  if (Number.isFinite(observedMs) && observedMs <= Date.now()) return observedAt
  return receivedAt ?? observedAt
}

type MapBaseLayer = 'theme' | 'street' | 'satellite' | 'terrain'
type TimeFilterMinutes = 'all' | '5' | '15' | '30' | '60' | '360' | '1440'
type MapEntityMode = 'assets' | 'trackers'
type SeparationRangeMeters = '100' | '250' | '500' | '1000'
type TimelineWindowHours = '24' | '48' | '72'
type SelectedMapNode =
  | { type: 'asset'; assetId: string; observation: Observation }
  | { type: 'device'; deviceId: string; observation: Observation }
  | { type: 'geofence'; geofenceId: string }

const timeFilterOptions: Array<{ value: TimeFilterMinutes; label: string }> = [
  { value: 'all', label: 'Any time' },
  { value: '5', label: 'Last 5 minutes' },
  { value: '15', label: 'Last 15 minutes' },
  { value: '30', label: 'Last 30 minutes' },
  { value: '60', label: 'Last hour' },
  { value: '360', label: 'Last 6 hours' },
  { value: '1440', label: 'Last 24 hours' },
]

const separationRangeOptions: Array<{ value: SeparationRangeMeters; label: string }> = [
  { value: '100', label: '100 m' },
  { value: '250', label: '250 m' },
  { value: '500', label: '500 m' },
  { value: '1000', label: '1000 m' },
]

function optionOrDefault<T extends string>(value: string | null, allowed: readonly T[], fallback: T): T {
  return allowed.includes(value as T) ? value as T : fallback
}

function boolParam(value: string | null, fallback: boolean): boolean {
  if (value === '1' || value === 'true') return true
  if (value === '0' || value === 'false') return false
  return fallback
}

function numberParam(value: string | null, fallback: number, allowed?: readonly number[]): number {
  const parsed = Number(value)
  if (!Number.isFinite(parsed)) return fallback
  return allowed && !allowed.includes(parsed) ? fallback : parsed
}

function coordinateParam(value: string | null): number | null {
  const parsed = Number(value)
  return Number.isFinite(parsed) ? parsed : null
}

function localDateInput(date = new Date()): string {
  const year = date.getFullYear()
  const month = `${date.getMonth() + 1}`.padStart(2, '0')
  const day = `${date.getDate()}`.padStart(2, '0')
  return `${year}-${month}-${day}`
}

function endOfLocalDay(value: string): Date {
  const [year, month, day] = value.split('-').map(Number)
  return new Date(year, month - 1, day, 23, 59, 59, 999)
}

function formatTimelineLabel(value: number): string {
  return new Date(value).toLocaleString([], {
    month: 'short',
    day: 'numeric',
    hour: 'numeric',
    minute: '2-digit',
  })
}

function makeMarkerIcon(freshnessClass: string, provider?: string | null, pulse = false) {
  const providerClass = provider ? ` provider-${provider.replace(/[^a-z0-9-]/gi, '-').toLowerCase()}` : ''
  return L.divIcon({
    className: `device-marker ${freshnessClass}${providerClass}${pulse ? ' pulse' : ''}`,
    html: '',
    iconSize: [20, 20],
    iconAnchor: [10, 10],
    popupAnchor: [0, -10],
  })
}

function makeClusterIcon(count: number, pulse = false) {
  const size = count >= 100 ? 48 : count >= 10 ? 42 : 36
  return L.divIcon({
    className: `device-cluster-marker${pulse ? ' pulse' : ''}`,
    html: `<span>${count}</span>`,
    iconSize: [size, size],
    iconAnchor: [size / 2, size / 2],
  })
}

function providerDisplayName(device?: Device | null, fallback?: string | null): string {
  return device?.providerLongName || device?.providerLabel || device?.providerShortName || device?.label || fallback || device?.identifier || 'Unknown tracker'
}

function parsedJson(value: unknown): Record<string, unknown> | null {
  if (typeof value !== 'string' || value.trim() === '') return null
  try {
    const parsed = JSON.parse(value) as unknown
    return parsed != null && typeof parsed === 'object' && !Array.isArray(parsed) ? parsed as Record<string, unknown> : null
  } catch {
    return null
  }
}

function numberValue(value: unknown): number | null {
  if (typeof value === 'number' && Number.isFinite(value)) return value
  if (typeof value === 'string') {
    const parsed = Number(value)
    return Number.isFinite(parsed) ? parsed : null
  }
  return null
}

function estimateAccuracyMeters(precisionBits: number | null, pdop: number | null, satsInView: number | null): number | null {
  if (precisionBits != null) {
    const bits = Math.min(32, Math.max(0, precisionBits))
    const quantizationMeters = Math.pow(2, 32 - bits) * 0.011132
    return Math.round(Math.max(1.6, quantizationMeters) * 100) / 100
  }
  if (pdop != null) {
    const normalizedPdop = pdop > 50 ? pdop / 100 : pdop
    const satellitePenalty = satsInView != null && satsInView > 0 && satsInView < 6 ? 2 : 1
    return Math.round(Math.max(1.6, normalizedPdop * 5 * satellitePenalty) * 100) / 100
  }
  return null
}

function metadataAccuracyMeters(position: Observation): number | null {
  const root = parsedJson(position.metadata)
  if (!root) return null
  const sourceMetadata = parsedJson(root.sourceMetadata)
  const candidates = [root, sourceMetadata].filter((item): item is Record<string, unknown> => item != null)

  for (const item of candidates) {
    const explicit = numberValue(item.accuracyMeters ?? item.accuracy_meters ?? item.accuracy)
    if (explicit != null && explicit > 0) return explicit

    const precisionBits = numberValue(item.precisionBits ?? item.precision_bits)
    const pdop = numberValue(item.pdop ?? item.PDOP)
    const satsInView = numberValue(item.satsInView ?? item.sats_in_view)
    const estimated = estimateAccuracyMeters(precisionBits, pdop, satsInView)
    if (estimated != null) return estimated

    const raw = item.raw != null && typeof item.raw === 'object' && !Array.isArray(item.raw) ? item.raw as Record<string, unknown> : null
    const payload = raw?.payload != null && typeof raw.payload === 'object' && !Array.isArray(raw.payload) ? raw.payload as Record<string, unknown> : null
    if (payload) {
      const payloadEstimated = estimateAccuracyMeters(
        numberValue(payload.precisionBits ?? payload.precision_bits),
        numberValue(payload.pdop ?? payload.PDOP),
        numberValue(payload.satsInView ?? payload.sats_in_view),
      )
      if (payloadEstimated != null) return payloadEstimated
    }
  }

  return null
}

function displayAccuracyMeters(position: Observation, device?: Device | null): number | null {
  if (position.accuracyMeters != null && position.accuracyMeters > 0) return position.accuracyMeters
  if ((device?.provider ?? '').toLowerCase() === 'meshtastic') return metadataAccuracyMeters(position)
  return metadataAccuracyMeters(position)
}

function formatRelativeTime(observedAt: string, receivedAt?: string | null): string {
  const diffMs = Math.max(0, Date.now() - signalTimeMs({ observedAt, receivedAt }))
  const diffSec = Math.floor(diffMs / 1000)
  if (diffSec < 60) return `${diffSec}s ago`
  const diffMin = Math.floor(diffSec / 60)
  if (diffMin < 60) return `${diffMin}m ago`
  const diffHr = Math.floor(diffMin / 60)
  if (diffHr < 24) return `${diffHr}h ago`
  return `${Math.floor(diffHr / 24)}d ago`
}

function signalTimeMs(position: { observedAt: string; receivedAt?: string | null }, nowMs = Date.now()): number {
  const observedMs = new Date(position.observedAt).getTime()
  const receivedMs = position.receivedAt ? new Date(position.receivedAt).getTime() : NaN
  const candidates = [observedMs, receivedMs].filter((value) => Number.isFinite(value) && value <= nowMs)
  return candidates.length > 0 ? Math.max(...candidates) : nowMs
}

function observationAgeMs(position: { observedAt: string; receivedAt?: string | null }, nowMs = Date.now()): number {
  return Math.max(0, nowMs - signalTimeMs(position, nowMs))
}

function getFreshnessState(position: { observedAt: string; receivedAt?: string | null }, nowMs: number, liveMinutes: number, idleMinutes: number): 'live' | 'idle' | 'offline' {
  const ageMinutes = observationAgeMs(position, nowMs) / 60000
  if (ageMinutes <= liveMinutes) return 'live'
  if (ageMinutes <= idleMinutes) return 'idle'
  return 'offline'
}

function freshnessLabel(state: 'live' | 'idle' | 'offline') {
  if (state === 'live') return 'Live'
  if (state === 'idle') return 'Idle'
  return 'Offline'
}

function positionTimeMs(position: Pick<Observation, 'observedAt' | 'receivedAt'>): number {
  return new Date(ageTimestamp(position.observedAt, position.receivedAt)).getTime()
}

function distanceMeters(a: Pick<Observation, 'latitude' | 'longitude'>, b: Pick<Observation, 'latitude' | 'longitude'>): number {
  const earthRadiusMeters = 6_371_000
  const dLat = (b.latitude - a.latitude) * Math.PI / 180
  const dLon = (b.longitude - a.longitude) * Math.PI / 180
  const lat1 = a.latitude * Math.PI / 180
  const lat2 = b.latitude * Math.PI / 180
  const h =
    Math.sin(dLat / 2) * Math.sin(dLat / 2) +
    Math.cos(lat1) * Math.cos(lat2) * Math.sin(dLon / 2) * Math.sin(dLon / 2)
  return earthRadiusMeters * 2 * Math.atan2(Math.sqrt(h), Math.sqrt(1 - h))
}

function interpolateNumber(a: number | null | undefined, b: number | null | undefined, progress: number): number | null {
  if (a == null || b == null) return a ?? b ?? null
  return a + (b - a) * progress
}

function interpolateObservation(from: Observation, to: Observation, cursorMs: number): Observation {
  const fromMs = positionTimeMs(from)
  const toMs = positionTimeMs(to)
  if (!Number.isFinite(fromMs) || !Number.isFinite(toMs) || toMs <= fromMs) return from

  const progress = Math.max(0, Math.min(1, (cursorMs - fromMs) / (toMs - fromMs)))
  return {
    ...from,
    id: `${from.id}:${to.id}:${Math.round(cursorMs)}`,
    observedAt: new Date(cursorMs).toISOString(),
    latitude: interpolateNumber(from.latitude, to.latitude, progress) ?? from.latitude,
    longitude: interpolateNumber(from.longitude, to.longitude, progress) ?? from.longitude,
    altitude: interpolateNumber(from.altitude, to.altitude, progress),
    accuracyMeters: interpolateNumber(from.accuracyMeters, to.accuracyMeters, progress),
    speedKmh: interpolateNumber(from.speedKmh, to.speedKmh, progress),
    headingDegrees: interpolateNumber(from.headingDegrees, to.headingDegrees, progress),
  }
}

function observationAtCursor(ordered: Observation[], cursorMs: number): Observation | null {
  if (ordered.length === 0) return null
  const firstMs = positionTimeMs(ordered[0])
  if (cursorMs < firstMs) return null

  for (let index = 0; index < ordered.length - 1; index += 1) {
    const current = ordered[index]
    const next = ordered[index + 1]
    const currentMs = positionTimeMs(current)
    const nextMs = positionTimeMs(next)
    if (cursorMs === currentMs) return current
    if (cursorMs > currentMs && cursorMs < nextMs) return interpolateObservation(current, next, cursorMs)
  }

  return ordered[ordered.length - 1]
}

type DisplayPosition = Observation & {
  groupedTrackers?: Observation[]
  groupedTrackerCount?: number
  displayMode?: MapEntityMode
  displayKey?: string
}

type SeparationAlert = {
  assetId: string
  assetName: string
  distanceMeters: number
  thresholdMeters: number
  primary: Observation
  secondary: Observation
}

type LiveLocationUpdate = {
  id: string
  deviceId: string
  deviceIdentifier: string
  assetName?: string | null
  observedAt: string
  receivedAt: string
  latitude: number
  longitude: number
  speedKmh?: number | null
}

type MarkerCluster = {
  center: [number, number]
  id: string
  point: L.Point
  positions: DisplayPosition[]
}

function displayPositionKey(position: Pick<DisplayPosition, 'deviceId' | 'assetId' | 'assetName' | 'displayKey' | 'displayMode'>): string {
  if (position.displayKey) return position.displayKey
  if (position.displayMode === 'assets' && position.assetId) return `asset:${position.assetId}`
  if (position.displayMode === 'assets' && position.assetName) return `asset-name:${position.assetName.toLocaleLowerCase()}`
  return `device:${position.deviceId}`
}

function groupPositionsByAsset(positions: Observation[], deviceById: Map<string, Device>): DisplayPosition[] {
  const groupedByAsset = new Map<string, Observation[]>()
  const ungrouped: DisplayPosition[] = []

  for (const position of positions) {
    const device = deviceById.get(position.deviceId)
    const assetKey = position.assetId ?? device?.assetId ?? (position.assetName ? `name:${position.assetName.toLocaleLowerCase()}` : null)
    if (!assetKey) {
      ungrouped.push({ ...position, displayKey: `device:${position.deviceId}` })
      continue
    }
    groupedByAsset.set(assetKey, [...(groupedByAsset.get(assetKey) ?? []), position])
  }

  const result: DisplayPosition[] = [...ungrouped]
  for (const assetPositions of groupedByAsset.values()) {
    const ordered = [...assetPositions].sort((a, b) => positionTimeMs(b) - positionTimeMs(a))
    result.push({
      ...ordered[0],
      displayKey: assetPositions[0].assetId ? `asset:${assetPositions[0].assetId}` : assetPositions[0].assetName ? `asset-name:${assetPositions[0].assetName.toLocaleLowerCase()}` : `asset:${assetPositions.map((position) => position.deviceId).sort().join('-')}`,
      displayMode: 'assets',
      groupedTrackers: ordered,
      groupedTrackerCount: ordered.length,
    })
  }

  return result
}

function clusterMapPositions(map: L.Map, positions: DisplayPosition[], pixelRange = 46): MarkerCluster[] {
  const zoom = map.getZoom()
  const maxZoom = map.getMaxZoom() === Infinity ? 19 : map.getMaxZoom()
  if (zoom >= maxZoom) {
    return positions.map((position) => ({
      center: [position.latitude, position.longitude],
      id: displayPositionKey(position),
      point: map.project([position.latitude, position.longitude], zoom),
      positions: [position],
    }))
  }

  const clusters: MarkerCluster[] = []

  for (const position of positions) {
    const point = map.project([position.latitude, position.longitude], zoom)
    const cluster = clusters.find((item) => item.point.distanceTo(point) <= pixelRange)
    if (!cluster) {
      clusters.push({
        center: [position.latitude, position.longitude],
        id: displayPositionKey(position),
        point,
        positions: [position],
      })
      continue
    }

    const nextCount = cluster.positions.length + 1
    cluster.point = L.point(
      (cluster.point.x * cluster.positions.length + point.x) / nextCount,
      (cluster.point.y * cluster.positions.length + point.y) / nextCount,
    )
    cluster.center = [
      (cluster.center[0] * cluster.positions.length + position.latitude) / nextCount,
      (cluster.center[1] * cluster.positions.length + position.longitude) / nextCount,
    ]
    cluster.positions.push(position)
    cluster.id = [...cluster.positions.map(displayPositionKey)].sort().join('-')
  }

  return clusters
}

function DeviceMarkerPopup({ device, position }: { device?: Device | null; position: DisplayPosition }) {
  return (
    <Popup>
      <strong>{position.assetName ?? providerDisplayName(device, position.deviceIdentifier)}</strong>
      <br />
      <span>{device?.provider ?? 'manual'}{device?.integrationFeedName ? ` / ${device.integrationFeedName}` : ''}</span>
      {(device?.providerShortName || device?.providerHardwareModel) && (
        <>
          <br />
          <span>{[device.providerShortName, device.providerHardwareModel].filter(Boolean).join(' / ')}</span>
        </>
      )}
      {position.groupedTrackerCount != null && position.groupedTrackerCount > 1 && (
        <>
          <br />
          Grouped trackers: {position.groupedTrackerCount}
          {position.groupedTrackers?.map((tracker) => (
            <span className="grouped-tracker-line" key={tracker.id}>
              {tracker.deviceIdentifier}: {formatRelativeTime(tracker.observedAt, tracker.receivedAt)}
            </span>
          ))}
        </>
      )}
      <br />
      {position.latitude.toFixed(4)}, {position.longitude.toFixed(4)}
      <br />
      {position.speedKmh != null ? `${position.speedKmh.toFixed(1)} km/h` : 'Speed N/A'}
      <br />
      {new Date(position.observedAt).toLocaleString()}
    </Popup>
  )
}

function AnimatedMarker({
  children,
  eventHandlers,
  icon,
  position,
}: {
  children?: ReactNode
  eventHandlers?: L.LeafletEventHandlerFnMap
  icon: L.Icon | L.DivIcon
  position: [number, number]
}) {
  const [displayPosition, setDisplayPosition] = useState<[number, number]>(position)
  const previousTargetRef = useRef(position)

  useEffect(() => {
    const previousTarget = previousTargetRef.current
    previousTargetRef.current = position
    if (previousTarget[0] === position[0] && previousTarget[1] === position[1]) return

    let frameId = 0
    const startMs = performance.now()
    const durationMs = 1200
    setDisplayPosition((from) => {
      const start = from
      const tick = (frameMs: number) => {
        const progress = Math.min(1, (frameMs - startMs) / durationMs)
        const eased = progress < 0.5 ? 2 * progress * progress : 1 - Math.pow(-2 * progress + 2, 2) / 2
        setDisplayPosition([
          interpolateNumber(start[0], position[0], eased) ?? position[0],
          interpolateNumber(start[1], position[1], eased) ?? position[1],
        ])
        if (progress < 1) frameId = window.requestAnimationFrame(tick)
      }
      frameId = window.requestAnimationFrame(tick)
      return from
    })

    return () => window.cancelAnimationFrame(frameId)
  }, [position])

  return (
    <Marker eventHandlers={eventHandlers} icon={icon} position={displayPosition}>
      {children}
    </Marker>
  )
}

function DeviceMarkerLayer({
  deviceById,
  idleMinutes,
  liveMinutes,
  nowMs,
  positions,
  recentPingByDeviceId,
  selectAssetNode,
  selectDeviceNode,
}: {
  deviceById: Map<string, Device>
  idleMinutes: number
  liveMinutes: number
  nowMs: number
  positions: DisplayPosition[]
  recentPingByDeviceId: Map<string, number>
  selectAssetNode: (position: DisplayPosition) => void
  selectDeviceNode: (position: Observation) => void
}) {
  const map = useMap()
  const [viewportVersion, setViewportVersion] = useState(0)

  useMapEvents({
    moveend: () => setViewportVersion((version) => version + 1),
    zoomend: () => setViewportVersion((version) => version + 1),
  })

  const clusters = useMemo(() => clusterMapPositions(map, positions), [map, positions, viewportVersion])

  function zoomIntoCluster(cluster: MarkerCluster) {
    const bounds = L.latLngBounds(cluster.positions.map((position) => [position.latitude, position.longitude] as [number, number]))
    const maxZoom = map.getMaxZoom() === Infinity ? 19 : map.getMaxZoom()
    const currentZoom = map.getZoom()
    const boundsZoom = bounds.isValid() ? map.getBoundsZoom(bounds, false, L.point(80, 80)) : currentZoom + 2
    const nextZoom = Math.min(maxZoom, Math.max(currentZoom + 2, boundsZoom, 13))

    if (!bounds.isValid() || bounds.getNorthEast().equals(bounds.getSouthWest())) {
      map.flyTo(cluster.center, nextZoom)
      return
    }

    map.flyToBounds(bounds, {
      maxZoom: nextZoom,
      padding: [80, 80],
    })
  }

  return (
    <>
      {clusters.map((cluster) => {
        if (cluster.positions.length > 1) {
          const clusterPulse = cluster.positions.some((position) => (recentPingByDeviceId.get(position.deviceId) ?? 0) > nowMs - 6500)
          return (
            <AnimatedMarker
              eventHandlers={{ click: () => zoomIntoCluster(cluster) }}
              icon={makeClusterIcon(cluster.positions.length, clusterPulse)}
              key={`cluster-${cluster.id}`}
              position={cluster.center}
            />
          )
        }

        const position = cluster.positions[0]
        const device = deviceById.get(position.deviceId)
        const freshnessClass = getFreshnessState(position, nowMs, liveMinutes, idleMinutes)
        const pulse = (recentPingByDeviceId.get(position.deviceId) ?? 0) > nowMs - 6500
        const onClick = position.displayMode === 'assets' && position.assetId
          ? () => void selectAssetNode(position)
          : () => void selectDeviceNode(position)
        return (
          <AnimatedMarker
            eventHandlers={{ click: onClick }}
            icon={makeMarkerIcon(freshnessClass, device?.provider, pulse)}
            key={displayPositionKey(position)}
            position={[position.latitude, position.longitude]}
          >
            <DeviceMarkerPopup device={device} position={position} />
          </AnimatedMarker>
        )
      })}
    </>
  )
}

function TimelineScrubber({
  cursorMs,
  error,
  isPlaying,
  isLive,
  loading,
  maxMs,
  minMs,
  onCursorChange,
  onLive,
  onDateChange,
  onPlayToggle,
  onWindowChange,
  scoped,
  timeline,
  timelineDate,
  windowHours,
}: {
  cursorMs: number | null
  error: string | null
  isPlaying: boolean
  isLive: boolean
  loading: boolean
  maxMs: number
  minMs: number
  onCursorChange: (value: number) => void
  onLive: () => void
  onDateChange: (value: string) => void
  onPlayToggle: () => void
  onWindowChange: (value: TimelineWindowHours) => void
  scoped: boolean
  timeline: ObservationTimeline | null
  timelineDate: string
  windowHours: TimelineWindowHours
}) {
  const maxBucketCount = Math.max(1, ...(timeline?.buckets.map((bucket) => bucket.count) ?? [1]))
  const histogramWidth = 100
  const histogramHeight = 34

  return (
    <div className="map-timeline" aria-label="Map timeline scrubber">
      <div className="timeline-controls">
        <button className="timeline-play-button" onClick={onPlayToggle} type="button" title={isPlaying ? 'Pause playback' : 'Play timeline'}>
          {isPlaying ? 'Pause' : 'Play'}
        </button>
        <button className={`timeline-live-button${isLive ? ' active' : ''}`} onClick={onLive} type="button" title="Return to live map positions">
          Live
        </button>
        <label>
          <span>Day</span>
          <input type="date" value={timelineDate} onChange={(event) => onDateChange(event.target.value)} />
        </label>
        <label>
          <span>Window</span>
          <select value={windowHours} onChange={(event) => onWindowChange(event.target.value as TimelineWindowHours)} disabled={!scoped}>
            <option value="24">24h</option>
            <option value="48">48h</option>
            <option value="72">72h</option>
          </select>
        </label>
        <div className="timeline-readout">
          <strong>{isLive ? 'LIVE' : cursorMs == null ? error ? 'Timeline unavailable' : 'Loading timeline' : formatTimelineLabel(cursorMs)}</strong>
          <span>
            {timeline
              ? `${timeline.totalCount}${timeline.truncated ? '+' : ''} observations${scoped ? ' scoped' : ' across map'}`
              : loading ? 'Loading observations' : 'No observations'}
          </span>
        </div>
      </div>
      <div className="timeline-track-shell">
        <svg className="timeline-histogram" viewBox={`0 0 ${histogramWidth} ${histogramHeight}`} preserveAspectRatio="none" aria-hidden="true">
          {(timeline?.buckets ?? []).map((bucket, index) => {
            const x = index * (histogramWidth / Math.max(1, timeline?.buckets.length ?? 1))
            const width = Math.max(0.45, histogramWidth / Math.max(1, timeline?.buckets.length ?? 1) - 0.18)
            const height = Math.max(1, (bucket.count / maxBucketCount) * histogramHeight)
            return <rect key={`${bucket.start}-${index}`} x={x} y={histogramHeight - height} width={width} height={height} rx="0.3" />
          })}
        </svg>
        <input
          aria-label="Timeline position"
          disabled={timeline === null}
          max={maxMs}
          min={minMs}
          onChange={(event) => onCursorChange(Number(event.target.value))}
          step={60_000}
          type="range"
          value={cursorMs ?? maxMs}
        />
      </div>
      <div className="timeline-footer">
        <span>{formatTimelineLabel(minMs)}</span>
        {error && <strong>{error}</strong>}
        {loading && <strong>Loading...</strong>}
        {timeline?.truncated && <strong>Point capped for performance</strong>}
        <span>{formatTimelineLabel(maxMs)}</span>
      </div>
    </div>
  )
}

function getMapTheme(colorMode: 'light' | 'dark', themeStyle: ThemeStyle) {
  const dark = colorMode === 'dark'
  const cartoAttribution = '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> &copy; <a href="https://carto.com/attributions">CARTO</a>'
  const osmAttribution = '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a>'

  if (themeStyle === 'classic' && !dark) {
    return {
      key: `${colorMode}-${themeStyle}`,
      attribution: osmAttribution,
      url: 'https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png',
      geofenceColor: '#25436d',
      geofenceFill: '#8eb4e3',
      trailColor: '#a15c12',
    }
  }

  if (themeStyle === 'contrast') {
    return {
      key: `${colorMode}-${themeStyle}`,
      attribution: cartoAttribution,
      url: dark
        ? 'https://{s}.basemaps.cartocdn.com/dark_all/{z}/{x}/{y}{r}.png'
        : 'https://{s}.basemaps.cartocdn.com/light_all/{z}/{x}/{y}{r}.png',
      geofenceColor: dark ? '#94eeff' : '#005f73',
      geofenceFill: dark ? '#94eeff' : '#005f73',
      trailColor: dark ? '#ffd08a' : '#8a4b00',
    }
  }

  return {
    key: `${colorMode}-${themeStyle}`,
    attribution: cartoAttribution,
    url: dark
      ? 'https://{s}.basemaps.cartocdn.com/dark_all/{z}/{x}/{y}{r}.png'
      : 'https://{s}.basemaps.cartocdn.com/light_all/{z}/{x}/{y}{r}.png',
    geofenceColor: dark ? '#58d0bf' : '#14534e',
    geofenceFill: dark ? '#58d0bf' : '#14534e',
    trailColor: dark ? '#f2b45f' : '#a15c12',
  }
}

function getBaseLayer(layer: MapBaseLayer, colorMode: 'light' | 'dark', themeStyle: ThemeStyle) {
  const themed = getMapTheme(colorMode, themeStyle)
  const osmAttribution = '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a>'

  if (layer === 'street') {
    return {
      ...themed,
      key: `street-${colorMode}-${themeStyle}`,
      url: 'https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png',
      attribution: osmAttribution,
    }
  }

  if (layer === 'satellite') {
    return {
      ...themed,
      key: `satellite-${colorMode}-${themeStyle}`,
      url: 'https://server.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer/tile/{z}/{y}/{x}',
      attribution: 'Tiles &copy; Esri',
      geofenceColor: colorMode === 'dark' ? '#94eeff' : '#005f73',
      geofenceFill: colorMode === 'dark' ? '#94eeff' : '#005f73',
      trailColor: '#ffcc66',
    }
  }

  if (layer === 'terrain') {
    return {
      ...themed,
      key: `terrain-${colorMode}-${themeStyle}`,
      url: 'https://{s}.tile.opentopomap.org/{z}/{x}/{y}.png',
      attribution: `${osmAttribution}, SRTM | &copy; <a href="https://opentopomap.org">OpenTopoMap</a>`,
      geofenceColor: colorMode === 'dark' ? '#58d0bf' : '#14534e',
      geofenceFill: colorMode === 'dark' ? '#58d0bf' : '#14534e',
      trailColor: colorMode === 'dark' ? '#ffcc66' : '#8a4b00',
    }
  }

  return themed
}

function MapViewportUpdater({
  selectedCenter,
  selectedFocusKey,
  hasInitialViewport,
  onViewportChange,
}: {
  selectedCenter: [number, number] | null
  selectedFocusKey: string | null
  hasInitialViewport: boolean
  onViewportChange: (viewport: { lat: number; lng: number; zoom: number }) => void
}) {
  const map = useMap()
  const hasUserMoved = useRef(hasInitialViewport)
  const prevSelectedFocusKey = useRef<string | null>(null)

  useEffect(() => {
    const handler = () => { hasUserMoved.current = true }
    const moveEndHandler = () => {
      const nextCenter = map.getCenter()
      onViewportChange({
        lat: Number(nextCenter.lat.toFixed(6)),
        lng: Number(nextCenter.lng.toFixed(6)),
        zoom: map.getZoom(),
      })
    }
    map.on('dragstart', handler)
    map.on('zoomstart', handler)
    map.on('moveend', moveEndHandler)
    return () => {
      map.off('dragstart', handler)
      map.off('zoomstart', handler)
      map.off('moveend', moveEndHandler)
    }
  }, [map, onViewportChange])

  useEffect(() => {
    if (selectedCenter === null || selectedFocusKey === null) {
      prevSelectedFocusKey.current = null
      return
    }

    const isNewFocus = prevSelectedFocusKey.current !== selectedFocusKey
    prevSelectedFocusKey.current = selectedFocusKey

    if (isNewFocus) {
      hasUserMoved.current = false
      map.setView(selectedCenter, Math.max(map.getZoom(), 13))
      return
    }

    if (hasUserMoved.current) {
      return
    }

    if (!map.getBounds().pad(-0.2).contains(selectedCenter)) {
      map.panTo(selectedCenter)
    }
  }, [selectedCenter, selectedFocusKey, map])

  return null
}

export default function MapPage() {
  const { effectiveColorMode, themeStyle } = useAppearance()
  const [searchParams, setSearchParams] = useSearchParams()
  const initialViewport = useMemo(() => {
    const lat = coordinateParam(searchParams.get('lat'))
    const lng = coordinateParam(searchParams.get('lng'))
    const zoom = numberParam(searchParams.get('z'), 10)
    return lat === null || lng === null ? null : { lat, lng, zoom }
    // The query string should only seed component state on initial mount.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])
  const initialSelectedGeofenceId = useMemo(() => searchParams.get('geofence'), [])
  const [positions, setPositions] = useState<Observation[]>([])
  const [geofences, setGeofences] = useState<Geofence[]>([])
  const [devices, setDevices] = useState<Device[]>([])
  const [assets, setAssets] = useState<Asset[]>([])
  const [feeds, setFeeds] = useState<IntegrationFeed[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [lastUpdated, setLastUpdated] = useState<string | null>(null)
  const [selectedDeviceId, setSelectedDeviceId] = useState<string | null>(() => searchParams.get('device'))
  const [trailPoints, setTrailPoints] = useState<[number, number][]>([])
  const [trailLength, setTrailLength] = useState<number>(() => numberParam(searchParams.get('trail'), 50, [20, 50, 100]))
  const [deviceSummary, setDeviceSummary] = useState<DeviceSummary | null>(null)
  const [baseLayer, setBaseLayer] = useState<MapBaseLayer>(() => optionOrDefault(searchParams.get('layer'), ['theme', 'street', 'satellite', 'terrain'] as const, 'theme'))
  const [showDevices, setShowDevices] = useState(() => boolParam(searchParams.get('devices'), true))
  const [showGeofences, setShowGeofences] = useState(() => boolParam(searchParams.get('geofences'), true))
  const [showTrail, setShowTrail] = useState(() => boolParam(searchParams.get('trailVisible'), true))
  const [showAccuracy, setShowAccuracy] = useState(() => boolParam(searchParams.get('accuracy'), false))
  const [showTimeline, setShowTimeline] = useState(() => boolParam(searchParams.get('timeline'), true))
  const [providerFilter, setProviderFilter] = useState(() => searchParams.get('provider') || 'all')
  const [feedFilter, setFeedFilter] = useState(() => searchParams.get('feed') || 'all')
  const [timeFilterMinutes, setTimeFilterMinutes] = useState<TimeFilterMinutes>(() => optionOrDefault(searchParams.get('age'), ['all', '5', '15', '30', '60', '360', '1440'] as const, '1440'))
  const [mapMode, setMapMode] = useState<MapEntityMode>(() => optionOrDefault(searchParams.get('mode'), ['assets', 'trackers'] as const, 'assets'))
  const [liveMinutes, setLiveMinutes] = useState(() => numberParam(searchParams.get('live'), 15))
  const [idleMinutes, setIdleMinutes] = useState(() => numberParam(searchParams.get('idle'), 60))
  const effectiveLiveMinutes = Math.max(1, liveMinutes)
  const effectiveIdleMinutes = Math.max(effectiveLiveMinutes, idleMinutes)
  const [separationRangeMeters, setSeparationRangeMeters] = useState<SeparationRangeMeters>(() => optionOrDefault(searchParams.get('separation'), ['100', '250', '500', '1000'] as const, '100'))
  const [mapViewport, setMapViewport] = useState<{ lat: number; lng: number; zoom: number } | null>(initialViewport)
  const [nowMs, setNowMs] = useState(() => Date.now())
  const [timelineDate, setTimelineDate] = useState(() => searchParams.get('timelineDate') || localDateInput())
  const [timelineWindowHours, setTimelineWindowHours] = useState<TimelineWindowHours>(() => optionOrDefault(searchParams.get('timelineWindow'), ['24', '48', '72'] as const, '24'))
  const [timeline, setTimeline] = useState<ObservationTimeline | null>(null)
  const [timelineCursorMs, setTimelineCursorMs] = useState<number | null>(null)
  const [timelineLoading, setTimelineLoading] = useState(false)
  const [timelineError, setTimelineError] = useState<string | null>(null)
  const [timelineLive, setTimelineLive] = useState(() => !searchParams.has('timelineAt'))
  const [timelinePlaying, setTimelinePlaying] = useState(false)
  const [selectedNode, setSelectedNode] = useState<SelectedMapNode | null>(null)
  const [nodeHistory, setNodeHistory] = useState<Observation[]>([])
  const [nodeBreaches, setNodeBreaches] = useState<GeofenceBreach[]>([])
  const [nodeLoading, setNodeLoading] = useState(false)
  const [nodeError, setNodeError] = useState<string | null>(null)
  const [attachAssetId, setAttachAssetId] = useState('')
  const [newAssetName, setNewAssetName] = useState('')
  const [nodeSubmitting, setNodeSubmitting] = useState(false)
  const [recentPingByDeviceId, setRecentPingByDeviceId] = useState<Map<string, number>>(() => new Map())
  const [liveLocationUpdates, setLiveLocationUpdates] = useState<LiveLocationUpdate[]>([])
  const [liveUpdateStreamCollapsed, setLiveUpdateStreamCollapsed] = useState(false)
  const pollRef = useRef<number | null>(null)
  const restoredSelectionRef = useRef(false)
  const timelineDateUserSetRef = useRef(searchParams.has('timelineDate'))

  async function loadTrailAndSummary(deviceId: string, length: number) {
    const [trail, summary] = await Promise.all([
      getDeviceTrail(deviceId, length),
      getDeviceSummary(deviceId),
    ])
    const points: [number, number][] = trail
      .slice()
      .reverse()
      .map((o) => [o.latitude, o.longitude])
    setTrailPoints(points)
    setDeviceSummary(summary)
  }

  useEffect(() => {
    async function load() {
      try {
        setError(null)
        const [latestPositions, geofenceItems, deviceList, feedItems, assetItems] = await Promise.all([
          getLatestPositions(),
          getGeofences(),
          getDevices(),
          getIntegrationFeeds(),
          getAssets(),
        ])
        setPositions(latestPositions)
        setGeofences(geofenceItems)
        setDevices(deviceList)
        setFeeds(feedItems)
        setAssets(assetItems)
        setLastUpdated(new Date().toLocaleTimeString())
      } catch (e: unknown) {
        setError(e instanceof Error ? e.message : String(e))
      } finally {
        setLoading(false)
      }
    }

    void load()
    pollRef.current = window.setInterval(() => {
      void load()
    }, 30000)

    return () => {
      if (pollRef.current != null) window.clearInterval(pollRef.current)
    }
  }, [])

  useEffect(() => {
    const timer = window.setInterval(() => setNowMs(Date.now()), 1000)
    return () => window.clearInterval(timer)
  }, [])

  useEffect(() => {
    if (timelineDateUserSetRef.current || positions.length === 0) return
    const selectedPosition = selectedNode?.type === 'device'
      ? positions.find((position) => position.deviceId === selectedNode.deviceId)
      : null
    const sourcePosition = selectedPosition ?? [...positions].sort((a, b) => positionTimeMs(b) - positionTimeMs(a))[0]
    if (!sourcePosition) return

    const nextDate = localDateInput(new Date(ageTimestamp(sourcePosition.observedAt, sourcePosition.receivedAt)))
    if (nextDate !== timelineDate) {
      setTimelineDate(nextDate)
    }
  }, [positions, selectedNode, timelineDate])

  useEffect(() => {
    const params = new URLSearchParams()
    if (baseLayer !== 'theme') params.set('layer', baseLayer)
    if (providerFilter !== 'all') params.set('provider', providerFilter)
    if (feedFilter !== 'all') params.set('feed', feedFilter)
    if (timeFilterMinutes !== '1440') params.set('age', timeFilterMinutes)
    if (mapMode !== 'assets') params.set('mode', mapMode)
    if (effectiveLiveMinutes !== 15) params.set('live', String(effectiveLiveMinutes))
    if (effectiveIdleMinutes !== 60) params.set('idle', String(effectiveIdleMinutes))
    if (separationRangeMeters !== '100') params.set('separation', separationRangeMeters)
    if (!showDevices) params.set('devices', '0')
    if (!showGeofences) params.set('geofences', '0')
    if (!showTrail) params.set('trailVisible', '0')
    if (showAccuracy) params.set('accuracy', '1')
    if (!showTimeline) params.set('timeline', '0')
    if (trailLength !== 50) params.set('trail', String(trailLength))
    if (timelineDate !== localDateInput()) params.set('timelineDate', timelineDate)
    if (timelineWindowHours !== '24') params.set('timelineWindow', timelineWindowHours)
    if (!timelineLive && timelineCursorMs != null) params.set('timelineAt', new Date(timelineCursorMs).toISOString())
    if (selectedNode?.type === 'device') params.set('device', selectedNode.deviceId)
    else if (selectedDeviceId) params.set('device', selectedDeviceId)
    if (selectedNode?.type === 'geofence') params.set('geofence', selectedNode.geofenceId)
    if (mapViewport) {
      params.set('lat', mapViewport.lat.toFixed(6))
      params.set('lng', mapViewport.lng.toFixed(6))
      params.set('z', String(mapViewport.zoom))
    }

    setSearchParams(params, { replace: true })
  }, [
    baseLayer,
    feedFilter,
    effectiveIdleMinutes,
    effectiveLiveMinutes,
    mapViewport,
    mapMode,
    providerFilter,
    selectedDeviceId,
    separationRangeMeters,
    selectedNode,
    setSearchParams,
    showAccuracy,
    showDevices,
    showGeofences,
    showTrail,
    showTimeline,
    timeFilterMinutes,
    timelineCursorMs,
    timelineDate,
    timelineLive,
    timelineWindowHours,
    trailLength,
  ])

  useEffect(() => {
    if (selectedDeviceId === null) return
    void loadTrailAndSummary(selectedDeviceId, trailLength)
    const refreshInterval = window.setInterval(() => {
      void loadTrailAndSummary(selectedDeviceId, trailLength)
    }, 30000)
    return () => window.clearInterval(refreshInterval)
  }, [selectedDeviceId, trailLength])

  useEffect(() => {
    if (restoredSelectionRef.current || loading) return
    if (initialSelectedGeofenceId) {
      const geofence = geofences.find((item) => item.id === initialSelectedGeofenceId)
      if (geofence) {
        restoredSelectionRef.current = true
        void selectGeofenceNode(geofence)
      }
      return
    }
    if (selectedDeviceId) {
      const position = positions.find((item) => item.deviceId === selectedDeviceId)
      if (position) {
        restoredSelectionRef.current = true
        void selectDeviceNode(position)
      }
    }
  }, [geofences, initialSelectedGeofenceId, loading, positions, selectedDeviceId])

  useLiveEvents((type, data) => {
    if (type === 'observation') {
      const obs = data as ObservationEvent
      const receivedAt = new Date().toISOString()
      const now = Date.now()
      setPositions(prev => {
        const idx = prev.findIndex(p => p.deviceId === obs.deviceId)
        const existing = idx >= 0 ? prev[idx] : undefined
        const newPos = {
          id: obs.id,
          deviceId: obs.deviceId,
          assetId: obs.assetId ?? existing?.assetId ?? undefined,
          latitude: obs.latitude,
          longitude: obs.longitude,
          speedKmh: obs.speedKmh ?? undefined,
          observedAt: obs.observedAt,
          receivedAt,
          assetName: existing?.assetName,
          deviceIdentifier: existing?.deviceIdentifier ?? obs.deviceId,
        }
        if (idx >= 0) {
          const current = existing!
          if (signalTimeMs(newPos, now) <= signalTimeMs(current, now)) return prev
          const updated = [...prev]
          updated[idx] = { ...current, ...newPos }
          return updated
        }
        return [...prev, newPos]
      })
      setRecentPingByDeviceId((current) => {
        const next = new Map(current)
        next.set(obs.deviceId, now)
        for (const [deviceId, pingMs] of next) {
          if (pingMs < now - 30000) next.delete(deviceId)
        }
        return next
      })
      setLiveLocationUpdates((current) => {
        const existing = positions.find((position) => position.deviceId === obs.deviceId)
        const nextUpdate: LiveLocationUpdate = {
          id: obs.id,
          deviceId: obs.deviceId,
          deviceIdentifier: existing?.deviceIdentifier ?? obs.deviceId,
          assetName: existing?.assetName,
          observedAt: obs.observedAt,
          receivedAt,
          latitude: obs.latitude,
          longitude: obs.longitude,
          speedKmh: obs.speedKmh,
        }
        return [nextUpdate, ...current.filter((item) => item.id !== obs.id)].slice(0, 40)
      })
      setNowMs(now)
      setLastUpdated(new Date().toLocaleTimeString())
      if (obs.deviceId === selectedDeviceId) {
        void loadTrailAndSummary(obs.deviceId, trailLength)
      }
    }
    if (type === 'speed_alert' || type === 'geofence_breach') {
      if (selectedDeviceId !== null) {
        void loadTrailAndSummary(selectedDeviceId, trailLength)
      }
    }
  })

  function handleDeviceSelect(deviceId: string) {
    if (deviceId === '') {
      setSelectedDeviceId(null)
      if (selectedNode?.type === 'device') setSelectedNode(null)
      setTrailPoints([])
      setDeviceSummary(null)
    } else {
      setSelectedDeviceId(deviceId)
    }
  }

  async function selectDeviceNode(position: Observation) {
    setSelectedNode({ type: 'device', deviceId: position.deviceId, observation: position })
    setSelectedDeviceId(position.deviceId)
    setAttachAssetId('')
    const device = deviceById.get(position.deviceId)
    setNewAssetName(position.assetName ?? providerDisplayName(device, position.deviceIdentifier))
    setNodeLoading(true)
    setNodeError(null)
    try {
      const [summary, history, breaches] = await Promise.all([
        getDeviceSummary(position.deviceId),
        getObservationHistory({ deviceId: position.deviceId, page: 1, pageSize: 12 }),
        getGeofenceBreaches({ deviceId: position.deviceId, limit: 12 }),
      ])
      setDeviceSummary(summary)
      setNodeHistory(history.items)
      setNodeBreaches(breaches)
      const trail = await getDeviceTrail(position.deviceId, trailLength)
      setTrailPoints(trail.slice().reverse().map((item) => [item.latitude, item.longitude]))
    } catch (err) {
      setNodeError(err instanceof Error ? err.message : 'Unable to load node details.')
    } finally {
      setNodeLoading(false)
    }
  }

  async function selectAssetNode(position: DisplayPosition) {
    const assetId = position.assetId ?? deviceById.get(position.deviceId)?.assetId
    if (!assetId) {
      await selectDeviceNode(position)
      return
    }

    setSelectedNode({ type: 'asset', assetId, observation: position })
    setSelectedDeviceId(position.deviceId)
    setAttachAssetId('')
    setNewAssetName(position.assetName ?? 'Tracked asset')
    setNodeLoading(true)
    setNodeError(null)
    try {
      const [summary, history, breaches] = await Promise.all([
        getDeviceSummary(position.deviceId),
        getObservationHistory({ assetId, page: 1, pageSize: 20 }),
        getGeofenceBreaches({ deviceId: position.deviceId, limit: 12 }),
      ])
      setDeviceSummary(summary)
      setNodeHistory(history.items)
      setNodeBreaches(breaches)
      const trail = await getDeviceTrail(position.deviceId, trailLength)
      setTrailPoints(trail.slice().reverse().map((item) => [item.latitude, item.longitude]))
    } catch (err) {
      setNodeError(err instanceof Error ? err.message : 'Unable to load asset details.')
    } finally {
      setNodeLoading(false)
    }
  }

  async function selectGeofenceNode(geofence: Geofence) {
    setSelectedNode({ type: 'geofence', geofenceId: geofence.id })
    setNodeHistory([])
    setNodeLoading(true)
    setNodeError(null)
    try {
      setNodeBreaches(await getGeofenceBreaches({ geofenceId: geofence.id, limit: 20 }))
    } catch (err) {
      setNodeError(err instanceof Error ? err.message : 'Unable to load geofence details.')
    } finally {
      setNodeLoading(false)
    }
  }

  async function assignSelectedDeviceToAsset(assetId: string) {
    if (!assetId || selectedNode?.type !== 'device') return
    const device = deviceById.get(selectedNode.deviceId)
    if (!device) return
    setNodeSubmitting(true)
    setNodeError(null)
    try {
      await updateDevice(device.id, {
        identifier: device.identifier,
        label: device.label ?? null,
        protocol: device.protocol ?? null,
        provider: device.provider ?? null,
        externalId: device.externalId ?? null,
        tags: device.tags ?? null,
        integrationFeedId: device.integrationFeedId ?? null,
        assetId,
      })
      const [deviceList, assetItems, latestPositions] = await Promise.all([getDevices(), getAssets(), getLatestPositions()])
      setDevices(deviceList)
      setAssets(assetItems)
      setPositions(latestPositions)
      setAttachAssetId('')
      const nextObservation = latestPositions.find((item) => item.deviceId === device.id) ?? selectedNode.observation
      await selectDeviceNode(nextObservation)
    } catch (err) {
      setNodeError(err instanceof Error ? err.message : 'Unable to attach tracker.')
    } finally {
      setNodeSubmitting(false)
    }
  }

  async function createAssetFromSelectedDevice() {
    if (selectedNode?.type !== 'device') return
    const device = deviceById.get(selectedNode.deviceId)
    if (!device) return
    setNodeSubmitting(true)
    setNodeError(null)
    try {
      const asset = await createAsset({
        name: newAssetName.trim() || providerDisplayName(device),
        description: `Created from ${device.provider || 'manual'} map node ${device.identifier}.`,
        category: device.provider === 'meshtastic' ? 'Mesh tracker' : 'Tracked asset',
      })
      await assignSelectedDeviceToAsset(asset.id)
    } catch (err) {
      setNodeError(err instanceof Error ? err.message : 'Unable to create tracked asset.')
      setNodeSubmitting(false)
    }
  }

  const deviceById = useMemo(() => new Map(devices.map((device) => [device.id, device])), [devices])
  const selectedDevice = selectedNode?.type === 'device' ? deviceById.get(selectedNode.deviceId) : null
  const selectedAsset = selectedNode?.type === 'asset' ? assets.find((asset) => asset.id === selectedNode.assetId) : null

  const timelineScope = useMemo(() => {
    if (selectedAsset) return { scoped: true as const, deviceId: undefined, assetId: selectedAsset.id, label: selectedAsset.name }
    if (!selectedDevice) return { scoped: false as const, deviceId: undefined, assetId: undefined, label: 'All map nodes' }
    if (selectedDevice.assetId) {
      return { scoped: true as const, deviceId: undefined, assetId: selectedDevice.assetId, label: selectedDevice.assetName ?? providerDisplayName(selectedDevice) }
    }
    return { scoped: true as const, deviceId: selectedDevice.id, assetId: undefined, label: providerDisplayName(selectedDevice) }
  }, [selectedAsset, selectedDevice])

  const timelineBounds = useMemo(() => {
    const end = endOfLocalDay(timelineDate)
    const hours = timelineScope.scoped ? Number(timelineWindowHours) : 24
    const start = new Date(end.getTime() - hours * 60 * 60 * 1000)
    return { from: start, to: end }
  }, [timelineDate, timelineScope.scoped, timelineWindowHours])

  useEffect(() => {
    let cancelled = false
    async function loadTimeline() {
      if (!showTimeline) {
        setTimeline(null)
        setTimelineCursorMs(null)
        setTimelineLoading(false)
        setTimelineError(null)
        setTimelineLive(true)
        setTimelinePlaying(false)
        return
      }

      setTimelineLoading(true)
      setTimelineError(null)
      try {
        const result = await getObservationTimeline({
          assetId: timelineScope.assetId,
          deviceId: timelineScope.deviceId,
          fromDate: timelineBounds.from.toISOString(),
          toDate: timelineBounds.to.toISOString(),
          maxPoints: timelineScope.scoped ? 5000 : 2500,
        })
        if (cancelled) return
        setTimeline(result)
        const toMs = new Date(result.to).getTime()
        const requested = searchParams.get('timelineAt')
        const requestedMs = requested ? new Date(requested).getTime() : NaN
        const nextCursorMs = Number.isFinite(requestedMs) && requestedMs >= new Date(result.from).getTime() && requestedMs <= toMs ? requestedMs : toMs
        setTimelineCursorMs(nextCursorMs)
      } catch (err) {
        if (!cancelled) {
          setTimeline(null)
          setTimelineError(err instanceof Error ? err.message : 'Unable to load timeline.')
        }
      } finally {
        if (!cancelled) setTimelineLoading(false)
      }
    }

    void loadTimeline()
    return () => { cancelled = true }
    // timelineAt should seed a newly loaded timeline but not refetch it while scrubbing.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [showTimeline, timelineBounds.from, timelineBounds.to, timelineScope.assetId, timelineScope.deviceId, timelineScope.scoped])

  useEffect(() => {
    if (timelineLive || !timelinePlaying || timeline === null || timelineCursorMs === null) return
    let lastFrameMs = performance.now()
    let frameId = 0
    const playbackMsPerSecond = 5 * 60 * 1000

    const tick = (frameMs: number) => {
      const elapsedMs = Math.max(0, frameMs - lastFrameMs)
      lastFrameMs = frameMs
      setTimelineCursorMs((current) => {
        const max = new Date(timeline.to).getTime()
        const next = (current ?? max) + (elapsedMs / 1000) * playbackMsPerSecond
        return next >= max ? max : next
      })
      frameId = window.requestAnimationFrame(tick)
    }

    frameId = window.requestAnimationFrame(tick)
    return () => window.cancelAnimationFrame(frameId)
  }, [timeline, timelineLive, timelinePlaying])

  useEffect(() => {
    if (timeline === null || timelineCursorMs === null) return
    if (timelineCursorMs >= new Date(timeline.to).getTime()) setTimelinePlaying(false)
  }, [timeline, timelineCursorMs])

  const timelineSourcePositions = useMemo(() => {
    if (timelineLive || !showTimeline || timeline === null || timelineCursorMs === null) return positions

    const grouped = new Map<string, Observation[]>()
    for (const observation of timeline.observations) {
      const items = grouped.get(observation.deviceId)
      if (items) items.push(observation)
      else grouped.set(observation.deviceId, [observation])
    }

    return Array.from(grouped.values())
      .map((items) => observationAtCursor([...items].sort((a, b) => positionTimeMs(a) - positionTimeMs(b)), timelineCursorMs))
      .filter((item): item is Observation => item != null)
  }, [positions, showTimeline, timeline, timelineCursorMs, timelineLive])

  const visiblePositions = useMemo(() => timelineSourcePositions.filter((position) => {
    const device = deviceById.get(position.deviceId)
    if (providerFilter !== 'all' && (device?.provider ?? 'manual') !== providerFilter) return false
    if (feedFilter !== 'all' && (device?.integrationFeedId ?? '') !== feedFilter) return false
    if (timeFilterMinutes !== 'all' && observationAgeMs(position, nowMs) > Number(timeFilterMinutes) * 60 * 1000) return false
    return true
  }), [deviceById, feedFilter, nowMs, providerFilter, timeFilterMinutes, timelineSourcePositions])

  const displayPositions = useMemo<DisplayPosition[]>(() => {
    if (mapMode === 'trackers') return visiblePositions
    return groupPositionsByAsset(visiblePositions, deviceById)
  }, [deviceById, mapMode, visiblePositions])

  const separationAlerts = useMemo<SeparationAlert[]>(() => {
    const alerts: SeparationAlert[] = []
    const grouped = new Map<string, Observation[]>()
    for (const position of visiblePositions) {
      const device = deviceById.get(position.deviceId)
      const assetId = position.assetId ?? device?.assetId
      if (!assetId) continue
      grouped.set(assetId, [...(grouped.get(assetId) ?? []), position])
    }

    for (const [assetId, items] of grouped) {
      const active = items
        .filter((position) => observationAgeMs(position, nowMs) <= effectiveLiveMinutes * 60 * 1000)
        .sort((a, b) => positionTimeMs(b) - positionTimeMs(a))
      if (active.length < 2) continue

      let largest: SeparationAlert | null = null
      for (let i = 0; i < active.length; i += 1) {
        for (let j = i + 1; j < active.length; j += 1) {
          const first = active[i]
          const second = active[j]
          const firstAccuracy = displayAccuracyMeters(first, deviceById.get(first.deviceId)) ?? 0
          const secondAccuracy = displayAccuracyMeters(second, deviceById.get(second.deviceId)) ?? 0
          const threshold = Math.max(Number(separationRangeMeters), firstAccuracy + secondAccuracy)
          const distance = distanceMeters(first, second)
          if (distance <= threshold) continue
          const alert = {
            assetId,
            assetName: first.assetName ?? second.assetName ?? deviceById.get(first.deviceId)?.assetName ?? 'Tracked asset',
            distanceMeters: distance,
            thresholdMeters: threshold,
            primary: first,
            secondary: second,
          }
          if (!largest || alert.distanceMeters > largest.distanceMeters) largest = alert
        }
      }
      if (largest) alerts.push(largest)
    }

    return alerts.sort((a, b) => b.distanceMeters - a.distanceMeters)
  }, [deviceById, effectiveLiveMinutes, nowMs, separationRangeMeters, visiblePositions])

  const timelinePaths = useMemo(() => {
    if (timelineLive || !showTimeline || timeline === null || timelineCursorMs === null) return []
    const grouped = new Map<string, Observation[]>()
    for (const observation of timeline.observations) {
      const device = deviceById.get(observation.deviceId)
      if (providerFilter !== 'all' && (device?.provider ?? 'manual') !== providerFilter) continue
      if (feedFilter !== 'all' && (device?.integrationFeedId ?? '') !== feedFilter) continue
      const items = grouped.get(observation.deviceId)
      if (items) items.push(observation)
      else grouped.set(observation.deviceId, [observation])
    }

    return Array.from(grouped.entries()).map(([deviceId, items]) => {
      const ordered = [...items].sort((a, b) => positionTimeMs(a) - positionTimeMs(b))
      const cursorPosition = observationAtCursor(ordered, timelineCursorMs)
      const cursorPoint = cursorPosition ? [cursorPosition.latitude, cursorPosition.longitude] as [number, number] : null
      const past = ordered
        .filter((item) => positionTimeMs(item) <= timelineCursorMs)
        .map((item) => [item.latitude, item.longitude] as [number, number])
      const future = ordered
        .filter((item) => positionTimeMs(item) > timelineCursorMs)
        .map((item) => [item.latitude, item.longitude] as [number, number])

      if (cursorPoint) {
        const lastPast = past[past.length - 1]
        if (!lastPast || lastPast[0] !== cursorPoint[0] || lastPast[1] !== cursorPoint[1]) past.push(cursorPoint)
        const firstFuture = future[0]
        if (!firstFuture || firstFuture[0] !== cursorPoint[0] || firstFuture[1] !== cursorPoint[1]) future.unshift(cursorPoint)
      }

      return {
        deviceId,
        past,
        future,
      }
    }).filter((path) => path.past.length + path.future.length > 1)
  }, [deviceById, feedFilter, providerFilter, showTimeline, timeline, timelineCursorMs, timelineLive])

  const groupedTrackerHiddenCount = useMemo(
    () => displayPositions.reduce((total, position) => total + Math.max(0, (position.groupedTrackerCount ?? 1) - 1), 0),
    [displayPositions],
  )

  const staleFilteredCount = useMemo(() => {
    if (timeFilterMinutes === 'all') return 0
    const cutoffMs = Number(timeFilterMinutes) * 60 * 1000
    return positions.filter((position) => {
      const device = deviceById.get(position.deviceId)
      if (providerFilter !== 'all' && (device?.provider ?? 'manual') !== providerFilter) return false
      if (feedFilter !== 'all' && (device?.integrationFeedId ?? '') !== feedFilter) return false
      return observationAgeMs(position, nowMs) > cutoffMs
    }).length
  }, [deviceById, feedFilter, nowMs, positions, providerFilter, timeFilterMinutes])

  const providerOptions = useMemo(() => {
    const values = new Set(devices.map((device) => device.provider || 'manual'))
    return Array.from(values).sort((a, b) => a.localeCompare(b))
  }, [devices])

  const mapCenter = useMemo<[number, number]>(() => {
    if (displayPositions.length > 0) {
      const meanLat = displayPositions.reduce((sum, p) => sum + p.latitude, 0) / displayPositions.length
      const meanLng = displayPositions.reduce((sum, p) => sum + p.longitude, 0) / displayPositions.length
      return [meanLat, meanLng]
    }
    if (geofences.length > 0) return [geofences[0].centerLatitude, geofences[0].centerLongitude]
    return [0, 0]
  }, [displayPositions, geofences])

  const selectedCenter = useMemo<[number, number] | null>(() => {
    if (selectedNode?.type === 'asset') {
      const visibleSelected = displayPositions.find((position) =>
        position.assetId === selectedNode.assetId ||
        position.groupedTrackers?.some((tracker) => tracker.assetId === selectedNode.assetId))
      return visibleSelected ? [visibleSelected.latitude, visibleSelected.longitude] : null
    }
    if (selectedNode?.type === 'device') {
      const visibleSelected = displayPositions.find((position) =>
        position.deviceId === selectedNode.deviceId ||
        position.groupedTrackers?.some((tracker) => tracker.deviceId === selectedNode.deviceId))
      return visibleSelected ? [visibleSelected.latitude, visibleSelected.longitude] : null
    }
    if (deviceSummary?.lastLatitude != null && deviceSummary?.lastLongitude != null) {
      return [deviceSummary.lastLatitude, deviceSummary.lastLongitude]
    }
    return null
  }, [deviceSummary, displayPositions, selectedNode])
  const selectedFocusKey = useMemo(() => {
    if (selectedNode?.type === 'asset') return `asset:${selectedNode.assetId}`
    if (selectedNode?.type === 'device') return `device:${selectedNode.deviceId}`
    if (selectedNode?.type === 'geofence') return `geofence:${selectedNode.geofenceId}`
    if (selectedDeviceId !== null) return `device:${selectedDeviceId}`
    return null
  }, [selectedDeviceId, selectedNode])

  const mapTheme = useMemo(() => getBaseLayer(baseLayer, effectiveColorMode, themeStyle), [baseLayer, effectiveColorMode, themeStyle])
  const selectedGeofence = selectedNode?.type === 'geofence' ? geofences.find((item) => item.id === selectedNode.geofenceId) : null
  const selectedAssetPositions = useMemo(() => {
    if (!selectedAsset) return []
    return positions
      .filter((position) => position.assetId === selectedAsset.id || deviceById.get(position.deviceId)?.assetId === selectedAsset.id)
      .sort((a, b) => positionTimeMs(b) - positionTimeMs(a))
  }, [deviceById, positions, selectedAsset])
  const selectedAssetSeparationAlerts = useMemo(
    () => selectedAsset ? separationAlerts.filter((alert) => alert.assetId === selectedAsset.id) : [],
    [selectedAsset, separationAlerts],
  )
  const mapInitialCenter: [number, number] = mapViewport ? [mapViewport.lat, mapViewport.lng] : mapCenter
  const mapInitialZoom = mapViewport?.zoom ?? (displayPositions.length > 1 ? 3 : displayPositions.length === 1 ? 10 : 2)
  const handleViewportChange = useCallback((viewport: { lat: number; lng: number; zoom: number }) => {
    setMapViewport((current) => {
      if (
        current &&
        current.lat === viewport.lat &&
        current.lng === viewport.lng &&
        current.zoom === viewport.zoom
      ) {
        return current
      }
      return viewport
    })
  }, [])
  const handleTimelineDateChange = useCallback((value: string) => {
    timelineDateUserSetRef.current = true
    setTimelineLive(false)
    setTimelinePlaying(false)
    setTimelineDate(value)
  }, [])
  const handleTimelineCursorChange = useCallback((value: number) => {
    setTimelineLive(false)
    setTimelinePlaying(false)
    setTimelineCursorMs(value)
  }, [])
  const handleTimelinePlayToggle = useCallback(() => {
    if (timelineLive && timeline) {
      setTimelineCursorMs(new Date(timeline.from).getTime())
      setTimelineLive(false)
      setTimelinePlaying(true)
      return
    }
    setTimelineLive(false)
    setTimelinePlaying((value) => !value)
  }, [timeline, timelineLive])
  const handleTimelineLive = useCallback(() => {
    setTimelinePlaying(false)
    setTimelineLive(true)
    setTimelineCursorMs(timeline ? new Date(timeline.to).getTime() : Date.now())
  }, [timeline])

  if (loading) return <div className="map-workspace map-workspace-state">Loading map...</div>
  if (error) return <div className="map-workspace map-workspace-state">Error: {error}</div>

  return (
    <div className="map-workspace">
      <aside className="map-sidebar" aria-label="Map controls">
        <div className="map-sidebar-header">
          <div>
            <h1>Live Map</h1>
            <span className="muted">Last updated: {lastUpdated ?? '—'}</span>
          </div>
          {getSseStatus() === 'open'
            ? <span className="status-line status-live">Live</span>
            : <span className="status-line status-polling">Polling</span>}
        </div>

        <div className="map-panel" data-testid="map-layers-panel">
          <h2>Layers</h2>
          <div className="segmented-control" aria-label="Map mode">
            <button className={mapMode === 'assets' ? 'active' : ''} onClick={() => setMapMode('assets')} type="button">Assets</button>
            <button className={mapMode === 'trackers' ? 'active' : ''} onClick={() => setMapMode('trackers')} type="button">Trackers</button>
          </div>
          <label className="field">
            <span>Base map</span>
            <select value={baseLayer} onChange={(event) => setBaseLayer(event.target.value as MapBaseLayer)}>
              <option value="theme">Match site theme</option>
              <option value="street">Street</option>
              <option value="satellite">Satellite</option>
              <option value="terrain">Terrain</option>
            </select>
          </label>
          <label className="field">
            <span>Provider</span>
            <select value={providerFilter} onChange={(event) => setProviderFilter(event.target.value)}>
              <option value="all">All providers</option>
              {providerOptions.map((provider) => (
                <option key={provider} value={provider}>{provider}</option>
              ))}
            </select>
          </label>
          <label className="field">
            <span>Bridge feed</span>
            <select value={feedFilter} onChange={(event) => setFeedFilter(event.target.value)}>
              <option value="all">All bridge feeds</option>
              {feeds.map((feed) => (
                <option key={feed.id} value={feed.id}>{feed.name}</option>
              ))}
            </select>
          </label>
          <label className="field">
            <span>Signal age</span>
            <select value={timeFilterMinutes} onChange={(event) => setTimeFilterMinutes(event.target.value as TimeFilterMinutes)}>
              {timeFilterOptions.map((option) => (
                <option key={option.value} value={option.value}>{option.label}</option>
              ))}
            </select>
          </label>
          <details>
            <summary>Advanced map preferences</summary>
            <div className="field-grid compact-field-grid">
              <label className="field">
                <span>Live through minutes</span>
                <input min={1} onChange={(event) => setLiveMinutes(Math.max(1, Number(event.target.value) || 15))} type="number" value={effectiveLiveMinutes} />
              </label>
              <label className="field">
                <span>Idle through minutes</span>
                <input min={effectiveLiveMinutes} onChange={(event) => setIdleMinutes(Math.max(effectiveLiveMinutes, Number(event.target.value) || 60))} type="number" value={effectiveIdleMinutes} />
              </label>
              <label className="field">
                <span>Tracker separation</span>
                <select value={separationRangeMeters} onChange={(event) => setSeparationRangeMeters(event.target.value as SeparationRangeMeters)}>
                  {separationRangeOptions.map((option) => (
                    <option key={option.value} value={option.value}>{option.label}</option>
                  ))}
                </select>
              </label>
            </div>
          </details>
          <div className="layer-toggle-list">
            <label className="check-field">
              <input checked={showDevices} onChange={(event) => setShowDevices(event.target.checked)} type="checkbox" />
              <span>Devices</span>
            </label>
            <label className="check-field">
              <input checked={showTrail} onChange={(event) => setShowTrail(event.target.checked)} type="checkbox" />
              <span>Trail</span>
            </label>
            <label className="check-field">
              <input checked={showAccuracy} onChange={(event) => setShowAccuracy(event.target.checked)} type="checkbox" />
              <span>Accuracy range</span>
            </label>
            <label className="check-field">
              <input checked={showTimeline} onChange={(event) => setShowTimeline(event.target.checked)} type="checkbox" />
              <span>Timeline</span>
            </label>
            <label className="check-field">
              <input checked={showGeofences} onChange={(event) => setShowGeofences(event.target.checked)} type="checkbox" />
              <span>Geofences</span>
            </label>
          </div>
          <div className="asset-meta-row">
            <span>{mapMode === 'assets' ? 'Visible assets' : 'Visible trackers'}</span>
            <strong>{displayPositions.length}</strong>
          </div>
          {mapMode === 'assets' && (
            <div className="asset-meta-row">
              <span>Hidden trackers</span>
              <strong>{groupedTrackerHiddenCount}</strong>
            </div>
          )}
          {separationAlerts.length > 0 && (
            <div className="notice notice-warning map-alert-list">
              <strong>Tracker separation</strong>
              {separationAlerts.slice(0, 2).map((alert) => (
                <span key={`${alert.assetId}-${alert.primary.deviceId}-${alert.secondary.deviceId}`}>
                  {alert.assetName}: {Math.round(alert.distanceMeters)} m apart
                </span>
              ))}
            </div>
          )}
          {timeFilterMinutes !== 'all' && (
            <div className="asset-meta-row">
              <span>Hidden by age</span>
              <strong>{staleFilteredCount}</strong>
            </div>
          )}
        </div>

        <div className="map-panel">
          <h2>Trail</h2>
          <div className="field">
            <span>Select device</span>
            <select value={selectedDeviceId ?? ''} onChange={(e) => handleDeviceSelect(e.target.value)}>
              <option value="">None</option>
              {devices.map((d) => (
                <option key={d.id} value={d.id}>
                  {d.label ?? d.identifier}
                  {d.assetName ? ` (${d.assetName})` : ''}
                </option>
              ))}
            </select>
          </div>
          {selectedDeviceId !== null && (
            <div className="button-row">
              <span className="muted" style={{ alignSelf: 'center' }}>Points:</span>
              {([20, 50, 100] as const).map((n) => (
                <button
                  key={n}
                  className={`button button-secondary${trailLength === n ? ' active' : ''}`}
                  onClick={() => setTrailLength(n)}
                  type="button"
                >
                  {n}
                </button>
              ))}
            </div>
          )}
          {trailPoints.length > 0 && (
            <div className="trail-legend">
              <span className="trail-legend-dot" />
              <span>Trail ({trailPoints.length} pts)</span>
            </div>
          )}
        </div>

        {deviceSummary !== null && (
          <div className="map-panel health-panel">
            <div className="page-header">
              <h2>
                {deviceSummary.assetName ?? deviceSummary.label ?? deviceSummary.providerLongName ?? deviceSummary.providerLabel ?? deviceSummary.providerShortName ?? deviceSummary.identifier}
              </h2>
              <button
                className="button button-secondary"
                onClick={() => handleDeviceSelect('')}
                type="button"
              >
                Clear
              </button>
            </div>
            <div className="muted">
              {deviceSummary.identifier}
            </div>
            {(deviceSummary.providerLongName || deviceSummary.providerShortName || deviceSummary.providerHardwareModel || deviceSummary.providerRole) && (
              <div className="asset-meta">
                {deviceSummary.providerLongName && <div className="asset-meta-row"><span>Provider name</span><strong>{deviceSummary.providerLongName}</strong></div>}
                {deviceSummary.providerShortName && <div className="asset-meta-row"><span>Short name</span><strong>{deviceSummary.providerShortName}</strong></div>}
                {deviceSummary.providerHardwareModel && <div className="asset-meta-row"><span>Hardware</span><strong>{deviceSummary.providerHardwareModel}</strong></div>}
                {deviceSummary.providerRole && <div className="asset-meta-row"><span>Role</span><strong>{deviceSummary.providerRole}</strong></div>}
                {deviceSummary.providerProfileUpdatedAt && <div className="asset-meta-row"><span>Profile seen</span><strong>{formatRelativeTime(deviceSummary.providerProfileUpdatedAt)} · {new Date(deviceSummary.providerProfileUpdatedAt).toLocaleString()}</strong></div>}
              </div>
            )}
            <div className="health-row">
              <span>Last seen</span>
              <span>
                {deviceSummary.lastSeenAt
                  ? `${formatRelativeTime(deviceSummary.lastSeenAt)} · ${new Date(deviceSummary.lastSeenAt).toLocaleString()}`
                  : 'N/A'}
              </span>
            </div>
            <div className="health-row">
              <span>Speed</span>
              <span>
                {deviceSummary.latestSpeedKmh != null
                  ? `${deviceSummary.latestSpeedKmh.toFixed(1)} km/h`
                  : 'N/A'}
              </span>
            </div>
            <div className="health-row">
              <span>Heading</span>
              <span>
                {deviceSummary.latestHeadingDegrees != null
                  ? `${deviceSummary.latestHeadingDegrees.toFixed(0)}°`
                  : 'N/A'}
              </span>
            </div>
            <div className="health-row">
              <span>Speed alerts</span>
              <span
                className={`alert-pill${deviceSummary.unacknowledgedSpeedAlerts > 0 ? ' alert-pill-active' : ''}`}
              >
                {deviceSummary.unacknowledgedSpeedAlerts}
              </span>
            </div>
            <div className="health-row">
              <span>Geofence breaches</span>
              <span
                className={`alert-pill${deviceSummary.unacknowledgedGeofenceBreaches > 0 ? ' alert-pill-active' : ''}`}
              >
                {deviceSummary.unacknowledgedGeofenceBreaches}
              </span>
            </div>
          </div>
        )}
      </aside>

      <section className="map-canvas" aria-label="Asset map">
          <MapContainer center={mapInitialCenter} zoom={mapInitialZoom} style={{ height: '100%', width: '100%' }}>
            <MapViewportUpdater
              hasInitialViewport={initialViewport !== null}
              onViewportChange={handleViewportChange}
              selectedCenter={selectedCenter}
              selectedFocusKey={selectedFocusKey}
            />
            <TileLayer
              attribution={mapTheme.attribution}
              key={mapTheme.key}
              url={mapTheme.url}
            />
            {showGeofences && geofences.map((geofence) => (
              geofence.shapeType === 'polygon' && geofence.polygonCoordinates?.length
                ? (
                  <Polygon
                    eventHandlers={{ click: () => void selectGeofenceNode(geofence) }}
                    key={`geofence-${geofence.id}`}
                    pathOptions={{ color: mapTheme.geofenceColor, fillColor: mapTheme.geofenceFill, fillOpacity: effectiveColorMode === 'dark' ? 0.18 : 0.14, weight: themeStyle === 'contrast' ? 3 : 2 }}
                    positions={geofence.polygonCoordinates.map((point) => [point.latitude, point.longitude])}
                  >
                    <Popup>
                      <strong>{geofence.name}</strong>
                      <br />
                      Freeform boundary ({geofence.polygonCoordinates.length} points)
                    </Popup>
                  </Polygon>
                )
                : (
                  <Circle
                    center={[geofence.centerLatitude, geofence.centerLongitude]}
                    eventHandlers={{ click: () => void selectGeofenceNode(geofence) }}
                    key={`geofence-${geofence.id}`}
                    pathOptions={{ color: mapTheme.geofenceColor, fillColor: mapTheme.geofenceFill, fillOpacity: effectiveColorMode === 'dark' ? 0.18 : 0.14, weight: themeStyle === 'contrast' ? 3 : 2 }}
                    radius={geofence.radiusMeters}
                  >
                    <Popup>
                      <strong>{geofence.name}</strong>
                      <br />
                      Radius: {geofence.radiusMeters} m
                    </Popup>
                  </Circle>
                )
            ))}
            {showTrail && trailPoints.length > 0 && (
              <Polyline
                positions={trailPoints}
                pathOptions={{ color: mapTheme.trailColor, weight: themeStyle === 'condensed' ? 2 : 3, opacity: effectiveColorMode === 'dark' ? 0.92 : 0.82 }}
              />
            )}
            {timelinePaths.map((path) => (
              <Fragment key={`timeline-path-${path.deviceId}`}>
                {path.future.length > 1 && (
                  <Polyline
                    positions={path.future}
                    pathOptions={{ color: effectiveColorMode === 'dark' ? '#94a3b8' : '#64748b', dashArray: '5 8', opacity: 0.46, weight: timelineScope.scoped ? 4 : 2 }}
                  />
                )}
                {path.past.length > 1 && (
                  <Polyline
                    positions={path.past}
                    pathOptions={{ color: effectiveColorMode === 'dark' ? '#22c55e' : '#15803d', opacity: timelineScope.scoped ? 0.9 : 0.55, weight: timelineScope.scoped ? 5 : 2 }}
                  />
                )}
              </Fragment>
            ))}
            {showAccuracy && (
              <Pane className="accuracy-pane" name="accuracy-pane">
                {displayPositions.map((position) => {
                  const device = deviceById.get(position.deviceId)
                  const accuracyMeters = displayAccuracyMeters(position, device)
                  if (accuracyMeters == null || accuracyMeters <= 0) return null
                  const pathOptions = {
                    color: effectiveColorMode === 'dark' ? '#38bdf8' : '#0369a1',
                    fillColor: effectiveColorMode === 'dark' ? '#38bdf8' : '#0ea5e9',
                    fillOpacity: effectiveColorMode === 'dark' ? 0.16 : 0.12,
                    opacity: effectiveColorMode === 'dark' ? 0.95 : 0.82,
                    weight: themeStyle === 'contrast' ? 3 : 2,
                  }
                  return (
                    <Fragment key={`accuracy-${position.id}`}>
                      <Circle
                        center={[position.latitude, position.longitude]}
                        interactive={false}
                        pathOptions={pathOptions}
                        radius={Math.min(accuracyMeters, 50_000)}
                      />
                      {accuracyMeters < 10 && (
                        <CircleMarker
                          center={[position.latitude, position.longitude]}
                          interactive={false}
                          pathOptions={{ ...pathOptions, fillOpacity: effectiveColorMode === 'dark' ? 0.28 : 0.22 }}
                          radius={14}
                        />
                      )}
                    </Fragment>
                  )
                })}
              </Pane>
            )}
            {showDevices && (
              <DeviceMarkerLayer
                deviceById={deviceById}
                idleMinutes={effectiveIdleMinutes}
                liveMinutes={effectiveLiveMinutes}
                nowMs={nowMs}
                positions={displayPositions}
                recentPingByDeviceId={recentPingByDeviceId}
                selectAssetNode={selectAssetNode}
                selectDeviceNode={selectDeviceNode}
              />
            )}
          </MapContainer>
          {showTimeline && (
            <TimelineScrubber
              cursorMs={timelineCursorMs}
              error={timelineError}
              isPlaying={timelinePlaying}
              isLive={timelineLive}
              loading={timelineLoading}
              maxMs={timeline ? new Date(timeline.to).getTime() : timelineBounds.to.getTime()}
              minMs={timeline ? new Date(timeline.from).getTime() : timelineBounds.from.getTime()}
              onCursorChange={handleTimelineCursorChange}
              onDateChange={handleTimelineDateChange}
              onLive={handleTimelineLive}
              onPlayToggle={handleTimelinePlayToggle}
              onWindowChange={(value) => {
                setTimelineLive(false)
                setTimelinePlaying(false)
                setTimelineWindowHours(value)
              }}
              scoped={timelineScope.scoped}
              timeline={timeline}
              timelineDate={timelineDate}
              windowHours={timelineScope.scoped ? timelineWindowHours : '24'}
            />
          )}
      </section>

      <aside className="map-detail-panel" aria-label="Selected node details" data-testid="map-node-detail-panel">
        <div className="map-sidebar-header">
          <div>
            <h1>{selectedNode === null ? 'Details' : selectedNode.type === 'asset' ? 'Asset' : selectedNode.type === 'device' ? 'Tracker' : 'Geofence'}</h1>
            <span className="muted">{selectedNode === null ? 'Select a map marker or geofence' : nodeLoading ? 'Loading details...' : 'Inspection and actions'}</span>
          </div>
          {selectedNode !== null && (
            <button className="button button-secondary" onClick={() => setSelectedNode(null)} type="button">Close</button>
          )}
        </div>

        {selectedNode === null && (
          <div className="map-panel">
            <h2>Ready</h2>
            <p className="muted">Click an asset marker to see its trackers and recent signals. Switch to Trackers for individual device inspection and assignment.</p>
            <p className="muted">Click a geofence to review configuration and breach history.</p>
          </div>
        )}

        {nodeError && (
          <div className="notice notice-danger">
            <strong>Action failed</strong>
            <span>{nodeError}</span>
          </div>
        )}

        {selectedNode?.type === 'asset' && selectedAsset && (
          <>
            <div className="map-panel">
              <h2>{selectedAsset.name}</h2>
              <p className="muted">{selectedAsset.description ?? 'No description provided.'}</p>
              <div className="asset-meta">
                <div className="asset-meta-row"><span>Trackers</span><strong>{selectedAssetPositions.length}</strong></div>
                <div className="asset-meta-row"><span>Shown on map</span><strong>{selectedNode.observation.deviceIdentifier}</strong></div>
                <div className="asset-meta-row"><span>Last signal</span><strong>{formatRelativeTime(selectedNode.observation.observedAt, selectedNode.observation.receivedAt)}</strong></div>
                <div className="asset-meta-row"><span>Freshness</span><strong>{freshnessLabel(getFreshnessState(selectedNode.observation, nowMs, effectiveLiveMinutes, effectiveIdleMinutes))}</strong></div>
              </div>
            </div>

            {selectedAssetSeparationAlerts.length > 0 && (
              <div className="notice notice-warning">
                <strong>Tracker separation</strong>
                {selectedAssetSeparationAlerts.map((alert) => (
                  <span key={`${alert.primary.deviceId}-${alert.secondary.deviceId}`}>
                    {Math.round(alert.distanceMeters)} m apart, threshold {Math.round(alert.thresholdMeters)} m
                  </span>
                ))}
              </div>
            )}

            <div className="map-panel">
              <h2>Trackers</h2>
              <div className="node-log-list">
                {selectedAssetPositions.map((position) => {
                  const device = deviceById.get(position.deviceId)
                  const state = getFreshnessState(position, nowMs, effectiveLiveMinutes, effectiveIdleMinutes)
                  return (
                    <button className="node-log-row node-log-button" key={position.deviceId} onClick={() => void selectDeviceNode(position)} type="button">
                      <strong>{providerDisplayName(device, position.deviceIdentifier)}</strong>
                      <span>{freshnessLabel(state)} · {formatRelativeTime(position.observedAt, position.receivedAt)}</span>
                      <span className="muted">{position.latitude.toFixed(5)}, {position.longitude.toFixed(5)}</span>
                    </button>
                  )
                })}
                {selectedAssetPositions.length === 0 && <span className="muted">No tracker signals loaded.</span>}
              </div>
            </div>

            <div className="map-panel">
              <h2>Observation log</h2>
              <div className="node-log-list">
                {nodeHistory.map((item) => (
                  <div className="node-log-row" key={item.id}>
                    <strong>{item.deviceIdentifier}</strong>
                    <span>{formatRelativeTime(item.observedAt, item.receivedAt)} · {item.latitude.toFixed(5)}, {item.longitude.toFixed(5)}</span>
                    <span className="muted">{item.speedKmh != null ? `${item.speedKmh.toFixed(1)} km/h` : 'Speed N/A'}</span>
                  </div>
                ))}
                {nodeHistory.length === 0 && <span className="muted">No observations loaded.</span>}
              </div>
            </div>
          </>
        )}

        {selectedNode?.type === 'device' && selectedDevice && (
          <>
            <div className="map-panel">
              <h2>{selectedDevice.assetName ?? selectedDevice.label ?? providerDisplayName(selectedDevice)}</h2>
              <div className="asset-meta">
                <div className="asset-meta-row"><span>Identifier</span><strong>{selectedDevice.identifier}</strong></div>
                <div className="asset-meta-row"><span>Provider</span><strong>{selectedDevice.provider || 'manual'}</strong></div>
                <div className="asset-meta-row"><span>Bridge feed</span><strong>{selectedDevice.integrationFeedName ?? 'None'}</strong></div>
                <div className="asset-meta-row"><span>Asset</span><strong>{selectedDevice.assetName ?? 'Unassigned'}</strong></div>
                <div className="asset-meta-row"><span>Status</span><strong>{deviceSummary?.lastSeenAt ? formatRelativeTime(deviceSummary.lastSeenAt) : 'No signal'}</strong></div>
              </div>
            </div>

            {(selectedDevice.providerLongName || selectedDevice.providerShortName || selectedDevice.providerHardwareModel || selectedDevice.providerRole || selectedDevice.providerProfileJson) && (
              <div className="map-panel">
                <h2>Provider profile</h2>
                <div className="asset-meta">
                  {selectedDevice.providerLongName && <div className="asset-meta-row"><span>Long name</span><strong>{selectedDevice.providerLongName}</strong></div>}
                  {selectedDevice.providerShortName && <div className="asset-meta-row"><span>Short name</span><strong>{selectedDevice.providerShortName}</strong></div>}
                  {selectedDevice.providerHardwareModel && <div className="asset-meta-row"><span>Hardware</span><strong>{selectedDevice.providerHardwareModel}</strong></div>}
                  {selectedDevice.providerRole && <div className="asset-meta-row"><span>Role</span><strong>{selectedDevice.providerRole}</strong></div>}
                  {selectedDevice.providerProfileUpdatedAt && <div className="asset-meta-row"><span>Updated</span><strong>{formatRelativeTime(selectedDevice.providerProfileUpdatedAt)} · {new Date(selectedDevice.providerProfileUpdatedAt).toLocaleString()}</strong></div>}
                </div>
                {selectedDevice.providerProfileJson && (
                  <details>
                    <summary>Raw provider profile</summary>
                    <pre className="bridge-raw-payload">{selectedDevice.providerProfileJson}</pre>
                  </details>
                )}
              </div>
            )}

            {!selectedDevice.assetId && (
              <div className="map-panel">
                <h2>Convert to tracked asset</h2>
                <label className="field">
                  <span>Attach to existing asset</span>
                  <select value={attachAssetId} onChange={(event) => setAttachAssetId(event.target.value)}>
                    <option value="">Select asset</option>
                    {assets.map((asset) => (
                      <option key={asset.id} value={asset.id}>{asset.name}</option>
                    ))}
                  </select>
                </label>
                <button className="button button-secondary" disabled={nodeSubmitting || !attachAssetId} onClick={() => void assignSelectedDeviceToAsset(attachAssetId)} type="button">
                  Attach tracker
                </button>
                <label className="field">
                  <span>New asset name</span>
                  <input value={newAssetName} onChange={(event) => setNewAssetName(event.target.value)} />
                </label>
                <button className="button" disabled={nodeSubmitting} onClick={() => void createAssetFromSelectedDevice()} type="button">
                  Create tracked asset
                </button>
              </div>
            )}

            <div className="map-panel">
              <h2>Observation log</h2>
              <div className="node-log-list">
                {nodeHistory.map((item) => (
                  <div className="node-log-row" key={item.id}>
                    <strong>{formatRelativeTime(item.observedAt, item.receivedAt)}</strong>
                    <span>{item.latitude.toFixed(5)}, {item.longitude.toFixed(5)}</span>
                    <span className="muted">{item.speedKmh != null ? `${item.speedKmh.toFixed(1)} km/h` : 'Speed N/A'}</span>
                  </div>
                ))}
                {nodeHistory.length === 0 && <span className="muted">No observations loaded.</span>}
              </div>
            </div>

            <div className="map-panel">
              <h2>Geofence events</h2>
              <div className="node-log-list">
                {nodeBreaches.map((breach) => (
                  <div className="node-log-row" key={breach.id}>
                    <strong>{breach.eventType}</strong>
                    <span>{breach.geofenceName}</span>
                    <span className="muted">{new Date(breach.detectedAt).toLocaleString()}</span>
                  </div>
                ))}
                {nodeBreaches.length === 0 && <span className="muted">No geofence events for this node.</span>}
              </div>
            </div>
          </>
        )}

        {selectedNode?.type === 'geofence' && selectedGeofence && (
          <>
            <div className="map-panel">
              <h2>{selectedGeofence.name}</h2>
              <p className="muted">{selectedGeofence.description ?? 'No description provided.'}</p>
              <div className="asset-meta">
                <div className="asset-meta-row"><span>Shape</span><strong>{selectedGeofence.shapeType === 'polygon' ? 'Freeform polygon' : 'Circle'}</strong></div>
                <div className="asset-meta-row"><span>Center</span><strong className="coords">{selectedGeofence.centerLatitude.toFixed(5)}, {selectedGeofence.centerLongitude.toFixed(5)}</strong></div>
                <div className="asset-meta-row"><span>{selectedGeofence.shapeType === 'polygon' ? 'Vertices' : 'Radius'}</span><strong>{selectedGeofence.shapeType === 'polygon' ? `${selectedGeofence.polygonCoordinates?.length ?? 0} points` : `${selectedGeofence.radiusMeters} m`}</strong></div>
                <div className="asset-meta-row"><span>Status</span><strong>{selectedGeofence.isActive ? 'Active' : 'Inactive'}</strong></div>
              </div>
            </div>

            <div className="map-panel">
              <h2>Breach log</h2>
              <div className="node-log-list">
                {nodeBreaches.map((breach) => (
                  <div className="node-log-row" key={breach.id}>
                    <strong>{breach.eventType}</strong>
                    <span>{breach.assetName ?? breach.deviceIdentifier ?? breach.deviceId}</span>
                    <span className="muted">{new Date(breach.detectedAt).toLocaleString()}</span>
                  </div>
                ))}
                {nodeBreaches.length === 0 && <span className="muted">No breach events for this geofence.</span>}
              </div>
            </div>
          </>
        )}

        <div className={`live-update-stream${liveUpdateStreamCollapsed ? ' collapsed' : ''}`} aria-label="Live location updates">
          <button className="live-update-stream-header" onClick={() => setLiveUpdateStreamCollapsed((value) => !value)} type="button">
            <span>
              <strong>Location updates</strong>
              <small>{liveLocationUpdates.length === 0 ? 'Waiting for signals' : `${liveLocationUpdates.length} recent`}</small>
            </span>
            <span aria-hidden="true">{liveUpdateStreamCollapsed ? 'Expand' : 'Collapse'}</span>
          </button>
          {!liveUpdateStreamCollapsed && (
            <div className="live-update-stream-list">
              {liveLocationUpdates.map((update) => (
                <button
                  className="live-update-row"
                  key={update.id}
                  onClick={() => {
                    const position = positions.find((item) => item.deviceId === update.deviceId)
                    if (position) void selectDeviceNode(position)
                    else handleDeviceSelect(update.deviceId)
                  }}
                  type="button"
                >
                  <strong>{update.assetName ?? update.deviceIdentifier}</strong>
                  <span>{formatRelativeTime(update.observedAt, update.receivedAt)} · {update.latitude.toFixed(5)}, {update.longitude.toFixed(5)}</span>
                  <span className="muted">{update.speedKmh != null ? `${update.speedKmh.toFixed(1)} km/h` : 'Position ping'}</span>
                </button>
              ))}
              {liveLocationUpdates.length === 0 && <span className="muted">New bridge and API observations will appear here.</span>}
            </div>
          )}
        </div>
      </aside>
    </div>
  )
}
