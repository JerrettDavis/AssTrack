import { useEffect, useMemo, useRef, useState } from 'react'
import { Circle, MapContainer, Marker, Polyline, Popup, TileLayer, useMap } from 'react-leaflet'
import L from 'leaflet'
import 'leaflet/dist/leaflet.css'
import markerIcon2x from 'leaflet/dist/images/marker-icon-2x.png'
import markerIcon from 'leaflet/dist/images/marker-icon.png'
import markerShadow from 'leaflet/dist/images/marker-shadow.png'
import { getGeofences, type Geofence } from '../api/geofences'
import { type Observation, getLatestPositions, getDeviceTrail } from '../api/observations'
import { getDevices, getDeviceSummary, type DeviceSummary } from '../api/devices'
import type { Device } from '../api/assets'
import { useLiveEvents } from '../hooks/useLiveEvents'
import { getSseStatus, type ObservationEvent } from '../api/sseClient'

delete (L.Icon.Default.prototype as unknown as Record<string, unknown>)._getIconUrl
L.Icon.Default.mergeOptions({
  iconRetinaUrl: markerIcon2x,
  iconUrl: markerIcon,
  shadowUrl: markerShadow,
})

function getStaleClass(observedAt: string): '' | 'stale' | 'very-stale' {
  const ageMs = Date.now() - new Date(observedAt).getTime()
  if (ageMs > 30 * 60 * 1000) return 'very-stale'
  if (ageMs > 5 * 60 * 1000) return 'stale'
  return ''
}

function makeMarkerIcon(staleClass: string) {
  return L.divIcon({
    className: '',
    html: `<div class="device-marker ${staleClass}"></div>`,
    iconSize: [20, 20],
    iconAnchor: [10, 10],
    popupAnchor: [0, -10],
  })
}

function formatRelativeTime(dateStr: string): string {
  const diffMs = Date.now() - new Date(dateStr).getTime()
  const diffSec = Math.floor(diffMs / 1000)
  if (diffSec < 60) return `${diffSec}s ago`
  const diffMin = Math.floor(diffSec / 60)
  if (diffMin < 60) return `${diffMin}m ago`
  const diffHr = Math.floor(diffMin / 60)
  if (diffHr < 24) return `${diffHr}h ago`
  return `${Math.floor(diffHr / 24)}d ago`
}

function MapViewportUpdater({ center, selectedCenter }: { center: [number, number]; selectedCenter: [number, number] | null }) {
  const map = useMap()
  const hasUserMoved = useRef(false)
  const prevSelectedCenter = useRef<[number, number] | null>(null)

  useEffect(() => {
    const handler = () => { hasUserMoved.current = true }
    map.on('dragstart', handler)
    return () => { map.off('dragstart', handler) }
  }, [map])

  useEffect(() => {
    if (selectedCenter !== null) {
      const [lat, lng] = selectedCenter
      const prev = prevSelectedCenter.current
      if (prev === null || prev[0] !== lat || prev[1] !== lng) {
        prevSelectedCenter.current = selectedCenter
        hasUserMoved.current = false
        map.setView(selectedCenter, 13)
      }
    }
  }, [selectedCenter, map])

  useEffect(() => {
    if (selectedCenter === null && !hasUserMoved.current) {
      map.setView(center)
    }
  }, [center, selectedCenter, map])

  return null
}

export default function MapPage() {
  const [positions, setPositions] = useState<Observation[]>([])
  const [geofences, setGeofences] = useState<Geofence[]>([])
  const [devices, setDevices] = useState<Device[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [lastUpdated, setLastUpdated] = useState<string | null>(null)
  const [selectedDeviceId, setSelectedDeviceId] = useState<string | null>(null)
  const [trailPoints, setTrailPoints] = useState<[number, number][]>([])
  const [trailLength, setTrailLength] = useState<number>(50)
  const [deviceSummary, setDeviceSummary] = useState<DeviceSummary | null>(null)
  const pollRef = useRef<number | null>(null)

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
        const [latestPositions, geofenceItems, deviceList] = await Promise.all([
          getLatestPositions(),
          getGeofences(),
          getDevices(),
        ])
        setPositions(latestPositions)
        setGeofences(geofenceItems)
        setDevices(deviceList)
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
    if (selectedDeviceId === null) return
    void loadTrailAndSummary(selectedDeviceId, trailLength)
    const refreshInterval = window.setInterval(() => {
      void loadTrailAndSummary(selectedDeviceId, trailLength)
    }, 30000)
    return () => window.clearInterval(refreshInterval)
  }, [selectedDeviceId, trailLength])

  useLiveEvents((type, data) => {
    if (type === 'observation') {
      const obs = data as ObservationEvent
      setPositions(prev => {
        const idx = prev.findIndex(p => p.deviceId === obs.deviceId)
        const newPos = {
          id: obs.id,
          deviceId: obs.deviceId,
          assetId: obs.assetId ?? undefined,
          latitude: obs.latitude,
          longitude: obs.longitude,
          speedKmh: obs.speedKmh ?? undefined,
          observedAt: obs.observedAt,
          receivedAt: obs.observedAt,
          assetName: idx >= 0 ? prev[idx].assetName : undefined,
          deviceIdentifier: idx >= 0 ? prev[idx].deviceIdentifier : (obs.deviceId),
        }
        if (idx >= 0) {
          const existing = prev[idx]
          if (new Date(obs.observedAt) <= new Date(existing.observedAt)) return prev
          const updated = [...prev]
          updated[idx] = { ...existing, ...newPos }
          return updated
        }
        return [...prev, newPos]
      })
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
      setTrailPoints([])
      setDeviceSummary(null)
    } else {
      setSelectedDeviceId(deviceId)
    }
  }

  const mapCenter = useMemo<[number, number]>(() => {
    if (positions.length > 0) {
      const meanLat = positions.reduce((sum, p) => sum + p.latitude, 0) / positions.length
      const meanLng = positions.reduce((sum, p) => sum + p.longitude, 0) / positions.length
      return [meanLat, meanLng]
    }
    if (geofences.length > 0) return [geofences[0].centerLatitude, geofences[0].centerLongitude]
    return [0, 0]
  }, [geofences, positions])

  const selectedCenter = useMemo<[number, number] | null>(() => {
    if (deviceSummary?.lastLatitude != null && deviceSummary?.lastLongitude != null) {
      return [deviceSummary.lastLatitude, deviceSummary.lastLongitude]
    }
    return null
  }, [deviceSummary])

  if (loading) return <div className="card">Loading map…</div>
  if (error) return <div className="card">Error: {error}</div>
  if (positions.length === 0 && geofences.length === 0) return <div className="card">No map data available yet.</div>

  return (
    <div className="section">
      <div className="page-header">
        <h2>Live Map</h2>
        <div style={{ display: 'flex', alignItems: 'center', gap: '0.75rem' }}>
          <span className="muted">Last updated: {lastUpdated ?? '—'}</span>
          {getSseStatus() === 'open'
            ? <span style={{ color: '#22c55e', fontSize: '0.8rem' }}>● Live</span>
            : <span style={{ color: '#94a3b8', fontSize: '0.8rem' }}>○ Polling</span>}
        </div>
      </div>
      <div className="map-layout">
        <div className="card map-card">
          <MapContainer center={mapCenter} zoom={positions.length > 0 ? 10 : 2} style={{ height: '100%', width: '100%' }}>
            <MapViewportUpdater center={mapCenter} selectedCenter={selectedCenter} />
            <TileLayer
              attribution='&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a>'
              url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png"
            />
            {geofences.map((geofence) => (
              <Circle
                center={[geofence.centerLatitude, geofence.centerLongitude]}
                key={`geofence-${geofence.id}`}
                pathOptions={{ color: '#60a5fa', fillColor: '#2563eb', fillOpacity: 0.2 }}
                radius={geofence.radiusMeters}
              >
                <Popup>
                  <strong>{geofence.name}</strong>
                  <br />
                  Radius: {geofence.radiusMeters} m
                </Popup>
              </Circle>
            ))}
            {trailPoints.length > 0 && (
              <Polyline
                positions={trailPoints}
                pathOptions={{ color: '#f59e0b', weight: 3, opacity: 0.8 }}
              />
            )}
            {positions.map((position) => {
              const staleClass = getStaleClass(position.observedAt)
              return (
                <Marker key={position.id} position={[position.latitude, position.longitude]} icon={makeMarkerIcon(staleClass)}>
                  <Popup>
                    <strong>{position.assetName ?? position.deviceIdentifier}</strong>
                    <br />
                    {position.latitude.toFixed(4)}, {position.longitude.toFixed(4)}
                    <br />
                    {position.speedKmh != null ? `${position.speedKmh.toFixed(1)} km/h` : 'Speed N/A'}
                    <br />
                    {new Date(position.observedAt).toLocaleString()}
                  </Popup>
                </Marker>
              )
            })}
          </MapContainer>
        </div>
        <div className="map-sidebar">
          <div className="card trail-controls">
            <h3>Trail</h3>
            <div className="field">
              <span>Select device</span>
              <select value={selectedDeviceId ?? ''} onChange={(e) => handleDeviceSelect(e.target.value)}>
                <option value="">— None —</option>
                {devices.map((d) => (
                  <option key={d.id} value={d.id}>
                    {d.label ?? d.identifier}
                    {d.assetName ? ` (${d.assetName})` : ''}
                  </option>
                ))}
              </select>
            </div>
            {selectedDeviceId !== null && (
              <div className="button-row" style={{ marginTop: '0.75rem' }}>
                <span style={{ alignSelf: 'center', fontSize: '0.85rem', color: '#cbd5e1' }}>Points:</span>
                {([20, 50, 100] as const).map((n) => (
                  <button
                    key={n}
                    className={`button button-secondary${trailLength === n ? ' active' : ''}`}
                    style={{ padding: '0.4rem 0.75rem', fontSize: '0.85rem' }}
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
            <div className="card health-panel">
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start' }}>
                <h3 style={{ margin: 0 }}>
                  {deviceSummary.assetName ?? deviceSummary.label ?? deviceSummary.identifier}
                </h3>
                <button
                  className="button button-secondary"
                  style={{ padding: '0.3rem 0.75rem', fontSize: '0.8rem' }}
                  onClick={() => handleDeviceSelect('')}
                  type="button"
                >
                  Clear trail
                </button>
              </div>
              <div style={{ color: '#94a3b8', fontSize: '0.8rem', marginBottom: '0.75rem' }}>
                {deviceSummary.identifier}
              </div>
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
        </div>
      </div>
    </div>
  )
}
