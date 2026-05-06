import { useEffect, useMemo, useRef, useState } from 'react'
import { Circle, MapContainer, Marker, Polygon, Polyline, Popup, TileLayer, useMap } from 'react-leaflet'
import L from 'leaflet'
import 'leaflet/dist/leaflet.css'
import markerIcon2x from 'leaflet/dist/images/marker-icon-2x.png'
import markerIcon from 'leaflet/dist/images/marker-icon.png'
import markerShadow from 'leaflet/dist/images/marker-shadow.png'
import { getGeofenceBreaches, getGeofences, type Geofence, type GeofenceBreach } from '../api/geofences'
import { type Observation, getLatestPositions, getDeviceTrail, getObservationHistory } from '../api/observations'
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

function getStaleClass(observedAt: string): '' | 'stale' | 'very-stale' {
  const ageMs = Date.now() - new Date(observedAt).getTime()
  if (ageMs > 30 * 60 * 1000) return 'very-stale'
  if (ageMs > 5 * 60 * 1000) return 'stale'
  return ''
}

type MapBaseLayer = 'theme' | 'street' | 'satellite' | 'terrain'
type SelectedMapNode =
  | { type: 'device'; deviceId: string; observation: Observation }
  | { type: 'geofence'; geofenceId: string }

function makeMarkerIcon(staleClass: string, provider?: string | null) {
  const providerClass = provider ? ` provider-${provider.replace(/[^a-z0-9-]/gi, '-').toLowerCase()}` : ''
  return L.divIcon({
    className: '',
    html: `<div class="device-marker ${staleClass}${providerClass}"></div>`,
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
  const { effectiveColorMode, themeStyle } = useAppearance()
  const [positions, setPositions] = useState<Observation[]>([])
  const [geofences, setGeofences] = useState<Geofence[]>([])
  const [devices, setDevices] = useState<Device[]>([])
  const [assets, setAssets] = useState<Asset[]>([])
  const [feeds, setFeeds] = useState<IntegrationFeed[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [lastUpdated, setLastUpdated] = useState<string | null>(null)
  const [selectedDeviceId, setSelectedDeviceId] = useState<string | null>(null)
  const [trailPoints, setTrailPoints] = useState<[number, number][]>([])
  const [trailLength, setTrailLength] = useState<number>(50)
  const [deviceSummary, setDeviceSummary] = useState<DeviceSummary | null>(null)
  const [baseLayer, setBaseLayer] = useState<MapBaseLayer>('theme')
  const [showDevices, setShowDevices] = useState(true)
  const [showGeofences, setShowGeofences] = useState(true)
  const [showTrail, setShowTrail] = useState(true)
  const [providerFilter, setProviderFilter] = useState('all')
  const [feedFilter, setFeedFilter] = useState('all')
  const [selectedNode, setSelectedNode] = useState<SelectedMapNode | null>(null)
  const [nodeHistory, setNodeHistory] = useState<Observation[]>([])
  const [nodeBreaches, setNodeBreaches] = useState<GeofenceBreach[]>([])
  const [nodeLoading, setNodeLoading] = useState(false)
  const [nodeError, setNodeError] = useState<string | null>(null)
  const [attachAssetId, setAttachAssetId] = useState('')
  const [newAssetName, setNewAssetName] = useState('')
  const [nodeSubmitting, setNodeSubmitting] = useState(false)
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

  async function selectDeviceNode(position: Observation) {
    setSelectedNode({ type: 'device', deviceId: position.deviceId, observation: position })
    setSelectedDeviceId(position.deviceId)
    setAttachAssetId('')
    setNewAssetName(position.assetName ?? position.deviceIdentifier)
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
        name: newAssetName.trim() || device.label || device.identifier,
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

  const visiblePositions = useMemo(() => positions.filter((position) => {
    const device = deviceById.get(position.deviceId)
    if (providerFilter !== 'all' && (device?.provider ?? 'manual') !== providerFilter) return false
    if (feedFilter !== 'all' && (device?.integrationFeedId ?? '') !== feedFilter) return false
    return true
  }), [deviceById, feedFilter, positions, providerFilter])

  const providerOptions = useMemo(() => {
    const values = new Set(devices.map((device) => device.provider || 'manual'))
    return Array.from(values).sort((a, b) => a.localeCompare(b))
  }, [devices])

  const mapCenter = useMemo<[number, number]>(() => {
    if (visiblePositions.length > 0) {
      const meanLat = visiblePositions.reduce((sum, p) => sum + p.latitude, 0) / visiblePositions.length
      const meanLng = visiblePositions.reduce((sum, p) => sum + p.longitude, 0) / visiblePositions.length
      return [meanLat, meanLng]
    }
    if (geofences.length > 0) return [geofences[0].centerLatitude, geofences[0].centerLongitude]
    return [0, 0]
  }, [geofences, visiblePositions])

  const selectedCenter = useMemo<[number, number] | null>(() => {
    if (deviceSummary?.lastLatitude != null && deviceSummary?.lastLongitude != null) {
      return [deviceSummary.lastLatitude, deviceSummary.lastLongitude]
    }
    return null
  }, [deviceSummary])

  const mapTheme = useMemo(() => getBaseLayer(baseLayer, effectiveColorMode, themeStyle), [baseLayer, effectiveColorMode, themeStyle])
  const selectedDevice = selectedNode?.type === 'device' ? deviceById.get(selectedNode.deviceId) : null
  const selectedGeofence = selectedNode?.type === 'geofence' ? geofences.find((item) => item.id === selectedNode.geofenceId) : null

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
              <input checked={showGeofences} onChange={(event) => setShowGeofences(event.target.checked)} type="checkbox" />
              <span>Geofences</span>
            </label>
          </div>
          <div className="asset-meta-row">
            <span>Visible signals</span>
            <strong>{visiblePositions.length}</strong>
          </div>
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
                {deviceSummary.assetName ?? deviceSummary.label ?? deviceSummary.identifier}
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
          <MapContainer center={mapCenter} zoom={visiblePositions.length > 0 ? 10 : 2} style={{ height: '100%', width: '100%' }}>
            <MapViewportUpdater center={mapCenter} selectedCenter={selectedCenter} />
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
            {showDevices && visiblePositions.map((position) => {
              const device = deviceById.get(position.deviceId)
              const staleClass = getStaleClass(position.observedAt)
              return (
                <Marker key={position.id} position={[position.latitude, position.longitude]} icon={makeMarkerIcon(staleClass, device?.provider)} eventHandlers={{ click: () => void selectDeviceNode(position) }}>
                  <Popup>
                    <strong>{position.assetName ?? position.deviceIdentifier}</strong>
                    <br />
                    <span>{device?.provider ?? 'manual'}{device?.integrationFeedName ? ` / ${device.integrationFeedName}` : ''}</span>
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
      </section>

      <aside className="map-detail-panel" aria-label="Selected node details" data-testid="map-node-detail-panel">
        <div className="map-sidebar-header">
          <div>
            <h1>{selectedNode === null ? 'Node details' : selectedNode.type === 'device' ? 'Tracker node' : 'Geofence node'}</h1>
            <span className="muted">{selectedNode === null ? 'Select a map marker or geofence' : nodeLoading ? 'Loading details...' : 'Inspection and actions'}</span>
          </div>
          {selectedNode !== null && (
            <button className="button button-secondary" onClick={() => setSelectedNode(null)} type="button">Close</button>
          )}
        </div>

        {selectedNode === null && (
          <div className="map-panel">
            <h2>Ready</h2>
            <p className="muted">Click a tracker marker to inspect its signal, assign it to an asset, create a tracked asset, and review recent observation and geofence logs.</p>
            <p className="muted">Click a geofence to review configuration and breach history.</p>
          </div>
        )}

        {nodeError && (
          <div className="notice notice-danger">
            <strong>Action failed</strong>
            <span>{nodeError}</span>
          </div>
        )}

        {selectedNode?.type === 'device' && selectedDevice && (
          <>
            <div className="map-panel">
              <h2>{selectedDevice.assetName ?? selectedDevice.label ?? selectedDevice.identifier}</h2>
              <div className="asset-meta">
                <div className="asset-meta-row"><span>Identifier</span><strong>{selectedDevice.identifier}</strong></div>
                <div className="asset-meta-row"><span>Provider</span><strong>{selectedDevice.provider || 'manual'}</strong></div>
                <div className="asset-meta-row"><span>Bridge feed</span><strong>{selectedDevice.integrationFeedName ?? 'None'}</strong></div>
                <div className="asset-meta-row"><span>Asset</span><strong>{selectedDevice.assetName ?? 'Unassigned'}</strong></div>
                <div className="asset-meta-row"><span>Status</span><strong>{deviceSummary?.lastSeenAt ? formatRelativeTime(deviceSummary.lastSeenAt) : 'No signal'}</strong></div>
              </div>
            </div>

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
                    <strong>{formatRelativeTime(item.observedAt)}</strong>
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
      </aside>
    </div>
  )
}
