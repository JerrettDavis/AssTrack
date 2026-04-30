import { FormEvent, useEffect, useMemo, useState } from 'react'
import { Circle, MapContainer, Popup, TileLayer, useMap } from 'react-leaflet'
import 'leaflet/dist/leaflet.css'
import { createGeofence, deleteGeofence, getGeofences, type Geofence } from '../api/geofences'

function MapViewportUpdater({ center }: { center: [number, number] }) {
  const map = useMap()

  useEffect(() => {
    map.setView(center)
  }, [center, map])

  return null
}

export default function GeofencesPage() {
  const [geofences, setGeofences] = useState<Geofence[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [submitting, setSubmitting] = useState(false)
  const [name, setName] = useState('')
  const [centerLatitude, setCenterLatitude] = useState('')
  const [centerLongitude, setCenterLongitude] = useState('')
  const [radiusMeters, setRadiusMeters] = useState('')

  async function load() {
    try {
      setError(null)
      setGeofences(await getGeofences())
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Unable to load geofences.')
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    void load()
  }, [])

  async function handleCreateGeofence(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setSubmitting(true)
    try {
      await createGeofence({
        name: name.trim(),
        centerLatitude: Number(centerLatitude),
        centerLongitude: Number(centerLongitude),
        radiusMeters: Number(radiusMeters),
      })
      setName('')
      setCenterLatitude('')
      setCenterLongitude('')
      setRadiusMeters('')
      await load()
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Unable to create geofence.')
    } finally {
      setSubmitting(false)
    }
  }

  async function handleDeleteGeofence(id: string, geofenceName: string) {
    if (!window.confirm(`Delete geofence "${geofenceName}"? This cannot be undone.`)) return
    setSubmitting(true)
    try {
      await deleteGeofence(id)
      await load()
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Unable to delete geofence.')
    } finally {
      setSubmitting(false)
    }
  }

  const mapCenter = useMemo<[number, number]>(
    () => (geofences.length > 0 ? [geofences[0].centerLatitude, geofences[0].centerLongitude] : [0, 0]),
    [geofences],
  )

  if (loading) return <div className="card">Loading geofences…</div>
  if (error) return <div className="card">Error: {error}</div>

  return (
    <div className="layout">
      <section className="section">
        <div className="card">
          <div className="page-header">
            <h1>Geofences</h1>
            <span className="muted">{geofences.length} configured</span>
          </div>

          <form className="inline-form" onSubmit={handleCreateGeofence}>
            <div className="field-grid">
              <label className="field">
                <span>Name</span>
                <input onChange={(event) => setName(event.target.value)} required value={name} />
              </label>
              <label className="field">
                <span>Latitude</span>
                <input onChange={(event) => setCenterLatitude(event.target.value)} required type="number" value={centerLatitude} />
              </label>
              <label className="field">
                <span>Longitude</span>
                <input onChange={(event) => setCenterLongitude(event.target.value)} required type="number" value={centerLongitude} />
              </label>
              <label className="field">
                <span>Radius (m)</span>
                <input onChange={(event) => setRadiusMeters(event.target.value)} min="1" required type="number" value={radiusMeters} />
              </label>
            </div>
            <div className="button-row">
              <button className="button" disabled={submitting} type="submit">
                {submitting ? 'Saving…' : 'Create geofence'}
              </button>
            </div>
          </form>
        </div>

        <div className="card table-card">
          <table className="data-table">
            <thead>
              <tr>
                <th>Name</th>
                <th>Center</th>
                <th>Radius</th>
                <th>Created</th>
                <th>Actions</th>
              </tr>
            </thead>
            <tbody>
              {geofences.map((geofence) => (
                <tr key={geofence.id}>
                  <td>{geofence.name}</td>
                  <td className="coords">
                    {geofence.centerLatitude.toFixed(4)}, {geofence.centerLongitude.toFixed(4)}
                  </td>
                  <td>{geofence.radiusMeters} m</td>
                  <td>{new Date(geofence.createdAt).toLocaleString()}</td>
                  <td>
                    <button
                      className="button button-danger"
                      disabled={submitting}
                      onClick={() => void handleDeleteGeofence(geofence.id, geofence.name)}
                      type="button"
                    >
                      Delete
                    </button>
                  </td>
                </tr>
              ))}
              {geofences.length === 0 && (
                <tr>
                  <td className="muted" colSpan={5}>
                    No geofences configured yet.
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      </section>

      <aside className="section">
        <div className="card map-card">
          <MapContainer center={mapCenter} zoom={geofences.length > 0 ? 10 : 2} style={{ height: '100%', width: '100%' }}>
            <MapViewportUpdater center={mapCenter} />
            <TileLayer
              attribution='&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a>'
              url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png"
            />
            {geofences.map((geofence) => (
              <Circle
                center={[geofence.centerLatitude, geofence.centerLongitude]}
                key={geofence.id}
                pathOptions={{ color: '#60a5fa', fillColor: '#2563eb', fillOpacity: 0.2 }}
                radius={geofence.radiusMeters}
              >
                <Popup>
                  <strong>{geofence.name}</strong>
                  <br />
                  {geofence.radiusMeters} m radius
                </Popup>
              </Circle>
            ))}
          </MapContainer>
        </div>
      </aside>
    </div>
  )
}
