import { Fragment, FormEvent, useEffect, useMemo, useState } from 'react'
import { Circle, MapContainer, Polygon, Popup, TileLayer, useMap, useMapEvents } from 'react-leaflet'
import 'leaflet/dist/leaflet.css'
import { createGeofence, deleteGeofence, getGeofences, updateGeofence, type Geofence, type GeofencePoint, type UpdateGeofenceRequest } from '../api/geofences'
import { useIdentityContext } from '../context/IdentityContext'
import { useAppearance, type ThemeStyle } from '../context/AppearanceContext'

function MapViewportUpdater({ center }: { center: [number, number] }) {
  const map = useMap()

  useEffect(() => {
    map.setView(center)
  }, [center, map])

  return null
}

function GeofenceCreateClickHandler({ enabled, onPick }: { enabled: boolean; onPick: (lat: number, lng: number) => void }) {
  useMapEvents({
    click(event) {
      if (enabled) onPick(event.latlng.lat, event.latlng.lng)
    },
  })

  return null
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
  }
}

export default function GeofencesPage() {
  const { effectiveColorMode, themeStyle } = useAppearance()
  const [geofences, setGeofences] = useState<Geofence[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [submitting, setSubmitting] = useState(false)
  const [name, setName] = useState('')
  const [description, setDescription] = useState('')
  const [shapeType, setShapeType] = useState<'circle' | 'polygon'>('circle')
  const [centerLatitude, setCenterLatitude] = useState('')
  const [centerLongitude, setCenterLongitude] = useState('')
  const [radiusMeters, setRadiusMeters] = useState('')
  const [polygonPoints, setPolygonPoints] = useState<GeofencePoint[]>([])
  const [isActive, setIsActive] = useState(true)
  const [editingId, setEditingId] = useState<string | null>(null)
  const [editForm, setEditForm] = useState<UpdateGeofenceRequest>({ name: '', centerLatitude: 0, centerLongitude: 0, radiusMeters: 0, isActive: true })
  const { isOperator } = useIdentityContext()

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
        description: description.trim() || undefined,
        shapeType,
        centerLatitude: Number(centerLatitude),
        centerLongitude: Number(centerLongitude),
        radiusMeters: shapeType === 'polygon' ? 0 : Number(radiusMeters),
        polygonCoordinates: shapeType === 'polygon' ? polygonPoints : undefined,
        isActive,
      })
      setName('')
      setDescription('')
      setShapeType('circle')
      setCenterLatitude('')
      setCenterLongitude('')
      setRadiusMeters('')
      setPolygonPoints([])
      setIsActive(true)
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

  function startEdit(geofence: Geofence) {
    setEditingId(geofence.id)
    setEditForm({
      name: geofence.name,
      description: geofence.description ?? null,
      shapeType: geofence.shapeType === 'polygon' ? 'polygon' : 'circle',
      centerLatitude: geofence.centerLatitude,
      centerLongitude: geofence.centerLongitude,
      radiusMeters: geofence.radiusMeters,
      polygonCoordinates: geofence.polygonCoordinates ?? null,
      isActive: geofence.isActive,
    })
  }

  async function saveEdit() {
    if (!editingId) return
    setSubmitting(true)
    try {
      await updateGeofence(editingId, editForm)
      setEditingId(null)
      await load()
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Unable to update geofence.')
    } finally {
      setSubmitting(false)
    }
  }

  const mapCenter = useMemo<[number, number]>(
    () => (geofences.length > 0 ? [geofences[0].centerLatitude, geofences[0].centerLongitude] : [0, 0]),
    [geofences],
  )

  const mapTheme = useMemo(() => getMapTheme(effectiveColorMode, themeStyle), [effectiveColorMode, themeStyle])
  const previewCenter = useMemo<[number, number] | null>(() => {
    const lat = Number(centerLatitude)
    const lng = Number(centerLongitude)
    return Number.isFinite(lat) && Number.isFinite(lng) && lat >= -90 && lat <= 90 && lng >= -180 && lng <= 180
      ? [lat, lng]
      : null
  }, [centerLatitude, centerLongitude])
  const previewRadius = Number(radiusMeters)

  function pickCreateCenter(lat: number, lng: number) {
    if (shapeType === 'polygon') {
      const nextPoints = [...polygonPoints, { latitude: Number(lat.toFixed(6)), longitude: Number(lng.toFixed(6)) }]
      setPolygonPoints(nextPoints)
      setCenterLatitude((nextPoints.reduce((sum, point) => sum + point.latitude, 0) / nextPoints.length).toFixed(6))
      setCenterLongitude((nextPoints.reduce((sum, point) => sum + point.longitude, 0) / nextPoints.length).toFixed(6))
      return
    }

    setCenterLatitude(lat.toFixed(6))
    setCenterLongitude(lng.toFixed(6))
    if (!radiusMeters) setRadiusMeters('250')
  }

  function undoPolygonPoint() {
    const nextPoints = polygonPoints.slice(0, -1)
    setPolygonPoints(nextPoints)
    if (nextPoints.length > 0) {
      setCenterLatitude((nextPoints.reduce((sum, point) => sum + point.latitude, 0) / nextPoints.length).toFixed(6))
      setCenterLongitude((nextPoints.reduce((sum, point) => sum + point.longitude, 0) / nextPoints.length).toFixed(6))
    }
  }

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

          {isOperator ? (
            <form className="inline-form" onSubmit={handleCreateGeofence}>
              <div className="field-grid">
                <label className="field">
                  <span>Name</span>
                  <input onChange={(event) => setName(event.target.value)} required value={name} />
                </label>
                <label className="field field-wide">
                  <span>Description</span>
                  <input onChange={(event) => setDescription(event.target.value)} placeholder="Depot yard, customer site, restricted zone" value={description} />
                </label>
                <label className="field">
                  <span>Shape</span>
                  <select onChange={(event) => setShapeType(event.target.value as 'circle' | 'polygon')} value={shapeType}>
                    <option value="circle">Circle</option>
                    <option value="polygon">Freeform polygon</option>
                  </select>
                </label>
                <label className="field">
                  <span>Latitude</span>
                  <input onChange={(event) => setCenterLatitude(event.target.value)} readOnly={shapeType === 'polygon'} required step={0.000001} type="number" value={centerLatitude} />
                </label>
                <label className="field">
                  <span>Longitude</span>
                  <input onChange={(event) => setCenterLongitude(event.target.value)} readOnly={shapeType === 'polygon'} required step={0.000001} type="number" value={centerLongitude} />
                </label>
                <label className="field">
                  <span>Radius (m)</span>
                  <input disabled={shapeType === 'polygon'} onChange={(event) => setRadiusMeters(event.target.value)} min="1" required={shapeType === 'circle'} type="number" value={radiusMeters} />
                </label>
                <label className="field check-field">
                  <input checked={isActive} onChange={(event) => setIsActive(event.target.checked)} type="checkbox" />
                  <span>Active</span>
                </label>
              </div>
              {shapeType === 'circle' && (
              <div>
                <span className="muted">Radius presets</span>
                <div className="segmented-control geofence-radius-presets" aria-label="Radius presets">
                  {[100, 250, 500, 1000].map((radius) => (
                    <button className={radiusMeters === String(radius) ? 'active' : ''} key={radius} onClick={() => setRadiusMeters(String(radius))} type="button">
                      {radius >= 1000 ? `${radius / 1000} km` : `${radius} m`}
                    </button>
                  ))}
                </div>
              </div>
              )}
              {shapeType === 'polygon' && (
                <div className="notice notice-info">
                  <strong>{polygonPoints.length} point{polygonPoints.length === 1 ? '' : 's'} placed</strong>
                  <span className="muted">Click the map to add vertices. A freeform geofence requires at least three points.</span>
                </div>
              )}
              <div className="button-row">
                <button className="button" disabled={submitting} type="submit">
                  {submitting ? 'Saving…' : 'Create geofence'}
                </button>
                {shapeType === 'polygon' && (
                  <>
                    <button className="button button-secondary" disabled={polygonPoints.length === 0} onClick={undoPolygonPoint} type="button">Undo point</button>
                    <button className="button button-secondary" disabled={polygonPoints.length === 0} onClick={() => setPolygonPoints([])} type="button">Clear points</button>
                  </>
                )}
                <span className="muted" style={{ alignSelf: 'center' }}>{shapeType === 'polygon' ? 'Click the map to draw the freeform boundary.' : 'Click the map to set the center.'}</span>
              </div>
            </form>
          ) : (
            <div className="notice notice-info">
              <strong>Viewer mode</strong>
              <span className="muted">Geofence creation and editing require an operator key.</span>
            </div>
          )}
        </div>

        <div className="card table-card">
          <table className="data-table">
            <thead>
              <tr>
                <th>Name</th>
                <th>Shape</th>
                <th>Center</th>
                <th>Radius</th>
                <th>Created</th>
                <th>Actions</th>
              </tr>
            </thead>
            <tbody>
              {geofences.map((geofence) => (
                <Fragment key={geofence.id}>
                  <tr>
                    <td>{geofence.name}{geofence.isSeeded && <span className="badge badge-demo badge-inline">Demo</span>}</td>
                    <td><span className="badge">{geofence.shapeType === 'polygon' ? 'Freeform' : 'Circle'}</span></td>
                    <td className="coords">
                      {geofence.centerLatitude.toFixed(4)}, {geofence.centerLongitude.toFixed(4)}
                    </td>
                    <td>{geofence.shapeType === 'polygon' ? `${geofence.polygonCoordinates?.length ?? 0} points` : `${geofence.radiusMeters} m`}</td>
                    <td>{new Date(geofence.createdAt).toLocaleString()}</td>
                    <td>
                      <div className="button-row">
                        {isOperator && (
                          <button className="button button-secondary" disabled={submitting} onClick={() => startEdit(geofence)} type="button">
                            Edit
                          </button>
                        )}
                        {isOperator && (
                          <button
                            className="button button-danger"
                            disabled={submitting}
                            onClick={() => void handleDeleteGeofence(geofence.id, geofence.name)}
                            type="button"
                          >
                            Delete
                          </button>
                        )}
                      </div>
                    </td>
                  </tr>
                  {editingId === geofence.id && (
                    <tr key={`${geofence.id}-edit`}>
                      <td colSpan={6}>
                        <div className="inline-form">
                          <div className="field-grid">
                            <label className="field">
                              <span>Name</span>
                              <input onChange={(e) => setEditForm(f => ({ ...f, name: e.target.value }))} required value={editForm.name} />
                            </label>
                            <label className="field field-wide">
                              <span>Description</span>
                              <input onChange={(e) => setEditForm(f => ({ ...f, description: e.target.value || null }))} value={editForm.description ?? ''} />
                            </label>
                            <label className="field">
                              <span>Shape</span>
                              <select onChange={(e) => setEditForm(f => ({ ...f, shapeType: e.target.value as 'circle' | 'polygon' }))} value={editForm.shapeType ?? 'circle'}>
                                <option value="circle">Circle</option>
                                <option value="polygon">Freeform polygon</option>
                              </select>
                            </label>
                            <label className="field">
                              <span>Latitude</span>
                              <input onChange={(e) => setEditForm(f => ({ ...f, centerLatitude: Number(e.target.value) }))} required step={0.0001} type="number" value={editForm.centerLatitude} />
                            </label>
                            <label className="field">
                              <span>Longitude</span>
                              <input onChange={(e) => setEditForm(f => ({ ...f, centerLongitude: Number(e.target.value) }))} required step={0.0001} type="number" value={editForm.centerLongitude} />
                            </label>
                            <label className="field">
                              <span>Radius (m)</span>
                              <input disabled={editForm.shapeType === 'polygon'} min={1} onChange={(e) => setEditForm(f => ({ ...f, radiusMeters: Number(e.target.value) }))} required={editForm.shapeType !== 'polygon'} type="number" value={editForm.radiusMeters} />
                            </label>
                            <label className="field" style={{ flexDirection: 'row', alignItems: 'center', gap: '0.5rem' }}>
                              <input checked={editForm.isActive ?? true} onChange={(e) => setEditForm(f => ({ ...f, isActive: e.target.checked }))} type="checkbox" />
                              <span>Active</span>
                            </label>
                          </div>
                          <div className="button-row">
                            <button className="button" disabled={submitting} onClick={() => void saveEdit()} type="button">
                              {submitting ? 'Saving…' : 'Save'}
                            </button>
                            <button className="button button-secondary" onClick={() => setEditingId(null)} type="button">
                              Cancel
                            </button>
                          </div>
                        </div>
                      </td>
                    </tr>
                  )}
                </Fragment>
              ))}
              {geofences.length === 0 && (
                <tr>
                  <td className="muted" colSpan={6}>
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
            <GeofenceCreateClickHandler enabled={isOperator} onPick={pickCreateCenter} />
            <TileLayer
              attribution={mapTheme.attribution}
              key={mapTheme.key}
              url={mapTheme.url}
            />
            {shapeType === 'circle' && previewCenter && Number.isFinite(previewRadius) && previewRadius > 0 && (
              <Circle
                center={previewCenter}
                pathOptions={{ color: mapTheme.geofenceColor, dashArray: '8 6', fillColor: mapTheme.geofenceFill, fillOpacity: 0.1, weight: 2 }}
                radius={previewRadius}
              >
                <Popup>
                  <strong>{name || 'New geofence preview'}</strong>
                  <br />
                  {previewRadius} m radius
                </Popup>
              </Circle>
            )}
            {shapeType === 'polygon' && polygonPoints.length > 0 && (
              <Polygon
                pathOptions={{ color: mapTheme.geofenceColor, dashArray: '8 6', fillColor: mapTheme.geofenceFill, fillOpacity: 0.12, weight: 2 }}
                positions={polygonPoints.map((point) => [point.latitude, point.longitude])}
              >
                <Popup>
                  <strong>{name || 'New freeform geofence'}</strong>
                  <br />
                  {polygonPoints.length} point{polygonPoints.length === 1 ? '' : 's'}
                </Popup>
              </Polygon>
            )}
            {geofences.map((geofence) => (
              geofence.shapeType === 'polygon' && geofence.polygonCoordinates?.length
                ? (
                  <Polygon
                    key={geofence.id}
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
                    key={geofence.id}
                    pathOptions={{ color: mapTheme.geofenceColor, fillColor: mapTheme.geofenceFill, fillOpacity: effectiveColorMode === 'dark' ? 0.18 : 0.14, weight: themeStyle === 'contrast' ? 3 : 2 }}
                    radius={geofence.radiusMeters}
                  >
                    <Popup>
                      <strong>{geofence.name}</strong>
                      <br />
                      {geofence.radiusMeters} m radius
                    </Popup>
                  </Circle>
                )
            ))}
          </MapContainer>
        </div>
      </aside>
    </div>
  )
}
