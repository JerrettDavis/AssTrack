import { useEffect, useMemo, useRef, useState } from 'react'
import { Circle, MapContainer, Marker, Popup, TileLayer, useMap } from 'react-leaflet'
import L from 'leaflet'
import 'leaflet/dist/leaflet.css'
import markerIcon2x from 'leaflet/dist/images/marker-icon-2x.png'
import markerIcon from 'leaflet/dist/images/marker-icon.png'
import markerShadow from 'leaflet/dist/images/marker-shadow.png'
import { getGeofences, type Geofence } from '../api/geofences'
import { type Observation, getLatestPositions } from '../api/observations'

// Fix leaflet default marker icons broken by Vite bundler
delete (L.Icon.Default.prototype as unknown as Record<string, unknown>)._getIconUrl
L.Icon.Default.mergeOptions({
  iconRetinaUrl: markerIcon2x,
  iconUrl: markerIcon,
  shadowUrl: markerShadow,
})

function MapViewportUpdater({ center }: { center: [number, number] }) {
  const map = useMap()

  useEffect(() => {
    map.setView(center)
  }, [center, map])

  return null
}

export default function MapPage() {
  const [positions, setPositions] = useState<Observation[]>([])
  const [geofences, setGeofences] = useState<Geofence[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [lastUpdated, setLastUpdated] = useState<string | null>(null)
  const pollRef = useRef<number | null>(null)

  useEffect(() => {
    async function load() {
      try {
        setError(null)
        const [latestPositions, geofenceItems] = await Promise.all([getLatestPositions(), getGeofences()])
        setPositions(latestPositions)
        setGeofences(geofenceItems)
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
      if (pollRef.current != null) {
        window.clearInterval(pollRef.current)
      }
    }
  }, [])

  const mapCenter = useMemo<[number, number]>(() => {
    if (positions.length > 0) {
      const meanLat = positions.reduce((sum, position) => sum + position.latitude, 0) / positions.length
      const meanLng = positions.reduce((sum, position) => sum + position.longitude, 0) / positions.length
      return [meanLat, meanLng]
    }

    if (geofences.length > 0) {
      return [geofences[0].centerLatitude, geofences[0].centerLongitude]
    }

    return [0, 0]
  }, [geofences, positions])

  if (loading) return <div className="card">Loading map…</div>
  if (error) return <div className="card">Error: {error}</div>
  if (positions.length === 0 && geofences.length === 0) return <div className="card">No map data available yet.</div>

  return (
    <div className="section">
      <div className="page-header">
        <h2>Live Map</h2>
        <span className="muted">Last updated: {lastUpdated ?? '—'}</span>
      </div>
      <div className="card map-card">
        <MapContainer center={mapCenter} zoom={positions.length > 0 ? 10 : 2} style={{ height: '100%', width: '100%' }}>
          <MapViewportUpdater center={mapCenter} />
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
          {positions.map((position) => (
            <Marker key={position.id} position={[position.latitude, position.longitude]}>
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
          ))}
        </MapContainer>
      </div>
    </div>
  )
}
