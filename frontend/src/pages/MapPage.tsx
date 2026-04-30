import { useEffect, useState } from 'react'
import { MapContainer, TileLayer, Marker, Popup } from 'react-leaflet'
import L from 'leaflet'
import 'leaflet/dist/leaflet.css'
import markerIcon2x from 'leaflet/dist/images/marker-icon-2x.png'
import markerIcon from 'leaflet/dist/images/marker-icon.png'
import markerShadow from 'leaflet/dist/images/marker-shadow.png'
import { type Observation, getLatestPositions } from '../api/observations'

// Fix leaflet default marker icons broken by Vite bundler
delete (L.Icon.Default.prototype as unknown as Record<string, unknown>)._getIconUrl
L.Icon.Default.mergeOptions({
  iconRetinaUrl: markerIcon2x,
  iconUrl: markerIcon,
  shadowUrl: markerShadow,
})

export default function MapPage() {
  const [positions, setPositions] = useState<Observation[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    getLatestPositions()
      .then(setPositions)
      .catch((e: unknown) => setError(e instanceof Error ? e.message : String(e)))
      .finally(() => setLoading(false))
  }, [])

  if (loading) return <div className="card">Loading map…</div>
  if (error) return <div className="card">Error: {error}</div>
  if (positions.length === 0) return <div className="card">No device positions available yet.</div>

  const meanLat = positions.reduce((s, p) => s + p.latitude, 0) / positions.length
  const meanLng = positions.reduce((s, p) => s + p.longitude, 0) / positions.length

  return (
    <div className="section">
      <h2>Live Map</h2>
      <div style={{ height: '500px', borderRadius: '12px', overflow: 'hidden' }}>
        <MapContainer center={[meanLat, meanLng]} zoom={10} style={{ height: '100%', width: '100%' }}>
          <TileLayer
            attribution='&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a>'
            url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png"
          />
          {positions.map(p => (
            <Marker key={p.id} position={[p.latitude, p.longitude]}>
              <Popup>
                <strong>{p.assetName ?? p.deviceIdentifier}</strong><br />
                {p.latitude.toFixed(4)}, {p.longitude.toFixed(4)}<br />
                {p.speedKmh != null ? `${p.speedKmh.toFixed(1)} km/h` : 'Speed N/A'}<br />
                {new Date(p.observedAt).toLocaleString()}
              </Popup>
            </Marker>
          ))}
        </MapContainer>
      </div>
    </div>
  )
}
