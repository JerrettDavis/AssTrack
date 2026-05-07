import { FormEvent, useEffect, useMemo, useState } from 'react'
import { createAsset, deleteAsset, getAssetClasses, getAssets, type Asset, type AssetClass, type SensorReading, updateAsset, type UpdateAssetRequest } from '../api/assets'
import { getLatestPositions, getObservations, type Observation } from '../api/observations'
import { useIdentityContext } from '../context/IdentityContext'

function formatTimestamp(value: string) {
  return new Date(value).toLocaleString()
}

function ageTimestamp(observedAt: string, receivedAt?: string | null): string {
  const observedMs = new Date(observedAt).getTime()
  if (Number.isFinite(observedMs) && observedMs <= Date.now()) return observedAt
  return receivedAt ?? observedAt
}

function formatRelativeTime(observedAt: string, receivedAt?: string | null) {
  const diffMs = Math.max(0, Date.now() - new Date(ageTimestamp(observedAt, receivedAt)).getTime())
  const minutes = Math.floor(diffMs / 60000)
  if (minutes < 1) return 'Just now'
  if (minutes < 60) return `${minutes}m ago`
  const hours = Math.floor(minutes / 60)
  if (hours < 24) return `${hours}h ago`
  return `${Math.floor(hours / 24)}d ago`
}

function getObservationStatus(observation: Observation | undefined) {
  if (!observation) return { label: 'No signal', className: 'badge-danger' }
  const ageMs = Math.max(0, Date.now() - new Date(ageTimestamp(observation.observedAt, observation.receivedAt)).getTime())
  if (ageMs > 30 * 60 * 1000) return { label: 'Stale', className: 'badge-danger' }
  if (ageMs > 5 * 60 * 1000) return { label: 'Aging', className: 'badge-warning' }
  if ((observation.speedKmh ?? 0) > 0) return { label: 'Moving', className: 'badge-success' }
  return { label: 'Online', className: 'badge-success' }
}

type AssetStatusFilter = 'all' | 'moving' | 'stale' | 'unassigned'

const criticalityOptions = [
  { value: 'low', label: 'Low' },
  { value: 'normal', label: 'Normal' },
  { value: 'high', label: 'High' },
  { value: 'critical', label: 'Critical' },
]

function classLabel(value: string, classes: AssetClass[]) {
  return classes.find((item) => item.id === value)?.name ?? value
}

function sensorLabel(reading: SensorReading) {
  const name = reading.name || reading.sensorType.replaceAll('_', ' ')
  const value = reading.numericValue != null
    ? `${reading.numericValue}${reading.unit ? ` ${reading.unit}` : ''}`
    : reading.textValue ?? 'N/A'
  return `${name}: ${value}`
}

export function AssetsPage() {
  const [assets, setAssets] = useState<Asset[]>([])
  const [assetClasses, setAssetClasses] = useState<AssetClass[]>([])
  const [observations, setObservations] = useState<Observation[]>([])
  const [latestPositions, setLatestPositions] = useState<Observation[]>([])
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(true)
  const [showAddForm, setShowAddForm] = useState(false)
  const [name, setName] = useState('')
  const [description, setDescription] = useState('')
  const [category, setCategory] = useState('')
  const [assetClass, setAssetClass] = useState('property')
  const [criticality, setCriticality] = useState('normal')
  const [speedThresholdKmh, setSpeedThresholdKmh] = useState<string>('')
  const [searchTerm, setSearchTerm] = useState('')
  const [statusFilter, setStatusFilter] = useState<AssetStatusFilter>('all')
  const [submitting, setSubmitting] = useState(false)
  const [editingId, setEditingId] = useState<string | null>(null)
  const [editForm, setEditForm] = useState<UpdateAssetRequest>({ name: '', description: null, assetClass: 'property', category: null, criticality: 'normal', speedThresholdKmh: null })
  const { isOperator } = useIdentityContext()

  async function load() {
    try {
      setError(null)
      const [assetItems, classItems, observationItems, latestPositionItems] = await Promise.all([getAssets(), getAssetClasses(), getObservations(), getLatestPositions()])
      setAssets(assetItems)
      setAssetClasses(classItems)
      setObservations(observationItems)
      setLatestPositions(latestPositionItems)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Unable to load API data.')
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    void load()
  }, [])

  async function handleCreateAsset(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setSubmitting(true)
    try {
      const speedValue = speedThresholdKmh ? parseFloat(speedThresholdKmh) : null
      if (speedValue !== null && !isFinite(speedValue)) {
        setError('Speed threshold must be a valid number.')
        setSubmitting(false)
        return
      }
      await createAsset({
        name: name.trim(),
        description: description.trim() || undefined,
        assetClass,
        category: category.trim() || undefined,
        criticality,
        speedThresholdKmh: speedValue,
      })
      setName('')
      setDescription('')
      setCategory('')
      setAssetClass('property')
      setCriticality('normal')
      setSpeedThresholdKmh('')
      setShowAddForm(false)
      await load()
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Unable to create asset.')
    } finally {
      setSubmitting(false)
    }
  }

  async function handleDeleteAsset(assetId: string, assetName: string) {
    if (!window.confirm(`Delete asset "${assetName}"? This cannot be undone.`)) return
    setSubmitting(true)
    try {
      await deleteAsset(assetId)
      await load()
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Unable to delete asset.')
    } finally {
      setSubmitting(false)
    }
  }

  function startEdit(asset: Asset) {
    setEditingId(asset.id)
    setEditForm({
      name: asset.name,
      description: asset.description ?? null,
      assetClass: asset.assetClass,
      category: asset.category ?? null,
      criticality: asset.criticality,
      speedThresholdKmh: asset.speedThresholdKmh ?? null,
    })
  }

  async function saveEdit() {
    if (!editingId) return
    setSubmitting(true)
    try {
      await updateAsset(editingId, editForm)
      setEditingId(null)
      await load()
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Unable to update asset.')
    } finally {
      setSubmitting(false)
    }
  }

  const metrics = useMemo(() => {
    const deviceCount = assets.reduce((total, asset) => total + asset.devices.length, 0)
    const movingCount = latestPositions.filter((p) => (p.speedKmh ?? 0) > 0).length
    const staleCount = assets.filter((asset) => {
      const latest = latestPositions.find((position) => asset.devices.some((device) => device.id === position.deviceId))
      return getObservationStatus(latest).label === 'Stale' || getObservationStatus(latest).label === 'No signal'
    }).length

    return [
      { label: 'Assets', value: assets.length },
      { label: 'Devices', value: deviceCount },
      { label: 'Observations', value: observations.length },
      { label: 'Vehicles', value: assets.filter((asset) => asset.assetClass === 'vehicle').length },
      { label: 'Pets', value: assets.filter((asset) => asset.assetClass === 'pet').length },
      { label: 'Moving now', value: movingCount },
      { label: 'Needs attention', value: staleCount },
    ]
  }, [assets, observations, latestPositions])

  const filteredAssets = useMemo(() => {
    const normalizedSearch = searchTerm.trim().toLowerCase()

    return assets.filter((asset) => {
      const latest = latestPositions.find((position) => asset.devices.some((device) => device.id === position.deviceId))
      const status = getObservationStatus(latest).label
      const searchMatches =
        normalizedSearch.length === 0 ||
        asset.name.toLowerCase().includes(normalizedSearch) ||
        asset.assetClass.toLowerCase().includes(normalizedSearch) ||
        asset.criticality.toLowerCase().includes(normalizedSearch) ||
        (asset.description ?? '').toLowerCase().includes(normalizedSearch) ||
        (asset.category ?? '').toLowerCase().includes(normalizedSearch) ||
        asset.devices.some((device) =>
          device.identifier.toLowerCase().includes(normalizedSearch) ||
          (device.label ?? '').toLowerCase().includes(normalizedSearch),
        )

      const statusMatches =
        statusFilter === 'all' ||
        (statusFilter === 'moving' && status === 'Moving') ||
        (statusFilter === 'stale' && (status === 'Stale' || status === 'No signal' || status === 'Aging')) ||
        (statusFilter === 'unassigned' && asset.devices.length === 0)

      return searchMatches && statusMatches
    })
  }, [assets, latestPositions, searchTerm, statusFilter])

  if (loading) {
    return <div className="card">Loading AssTrack data…</div>
  }

  if (error) {
    return <div className="card">API unavailable: {error}</div>
  }

  return (
    <div className="layout">
      <section className="section">
        <div className="card">
          <h2>Fleet overview</h2>
          <div className="metrics">
            {metrics.map((metric) => (
              <div className="metric" key={metric.label}>
                <span className="muted">{metric.label}</span>
                <strong>{metric.value}</strong>
              </div>
            ))}
          </div>
        </div>

        <div className="section">
          <div className="page-header">
            <h2>Assets</h2>
            {isOperator && (
              <button className="button button-secondary" onClick={() => setShowAddForm((value) => !value)} type="button">
                {showAddForm ? 'Cancel' : 'Add Asset'}
              </button>
            )}
          </div>
          <div className="card toolbar">
            <label className="field">
              <span>Search inventory</span>
              <input
                onChange={(event) => setSearchTerm(event.target.value)}
                placeholder="Asset, category, device"
                type="search"
                value={searchTerm}
              />
            </label>
            <label className="field">
              <span>Status</span>
              <select onChange={(event) => setStatusFilter(event.target.value as AssetStatusFilter)} value={statusFilter}>
                <option value="all">All assets</option>
                <option value="moving">Moving now</option>
                <option value="stale">Needs attention</option>
                <option value="unassigned">Unassigned</option>
              </select>
            </label>
            <div className="compact-actions">
              <span className="muted">{filteredAssets.length} shown</span>
            </div>
          </div>
          {showAddForm && isOperator && (
            <form className="card inline-form" onSubmit={handleCreateAsset}>
              <div className="field-grid">
                <label className="field">
                  <span>Name</span>
                  <input onChange={(event) => setName(event.target.value)} required value={name} />
                </label>
                <label className="field">
                  <span>Asset class</span>
                  <select onChange={(event) => setAssetClass(event.target.value)} value={assetClass}>
                    {assetClasses.map((item) => (
                      <option key={item.id} value={item.id}>{item.name}</option>
                    ))}
                  </select>
                </label>
                <label className="field">
                  <span>Criticality</span>
                  <select onChange={(event) => setCriticality(event.target.value)} value={criticality}>
                    {criticalityOptions.map((option) => (
                      <option key={option.value} value={option.value}>{option.label}</option>
                    ))}
                  </select>
                </label>
                <label className="field field-wide">
                  <span>Description</span>
                  <input onChange={(event) => setDescription(event.target.value)} value={description} />
                </label>
                <label className="field">
                  <span>Category</span>
                  <input onChange={(event) => setCategory(event.target.value)} placeholder="Trailer, service dog, generator" value={category} />
                </label>
                <label className="field">
                  <span>Speed Threshold (km/h)</span>
                  <input
                    min={0.001}
                    onChange={(event) => setSpeedThresholdKmh(event.target.value)}
                    placeholder="Default 120 km/h"
                    type="number"
                    value={speedThresholdKmh}
                  />
                </label>
              </div>
              <div className="button-row">
                <button className="button" disabled={submitting} type="submit">
                  {submitting ? 'Saving…' : 'Create asset'}
                </button>
              </div>
            </form>
          )}
          {assets.length === 0 && (
            <div className="notice notice-info">
              <strong>No assets yet</strong>
              <span className="muted">Head to Settings → Demo Data to seed example fleet data and explore the UI.</span>
            </div>
          )}
          <div className="asset-grid">
            {filteredAssets.map((asset) => {
              const latest = latestPositions.find((position) => asset.devices.some((device) => device.id === position.deviceId))
              const status = getObservationStatus(latest)

              return (
              <article className="list-card" key={asset.id}>
                {editingId === asset.id ? (
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
                        <span>Asset class</span>
                        <select onChange={(e) => setEditForm(f => ({ ...f, assetClass: e.target.value }))} value={editForm.assetClass ?? 'property'}>
                          {assetClasses.map((item) => (
                            <option key={item.id} value={item.id}>{item.name}</option>
                          ))}
                        </select>
                      </label>
                      <label className="field">
                        <span>Criticality</span>
                        <select onChange={(e) => setEditForm(f => ({ ...f, criticality: e.target.value }))} value={editForm.criticality ?? 'normal'}>
                          {criticalityOptions.map((option) => (
                            <option key={option.value} value={option.value}>{option.label}</option>
                          ))}
                        </select>
                      </label>
                      <label className="field">
                        <span>Category</span>
                        <input onChange={(e) => setEditForm(f => ({ ...f, category: e.target.value || null }))} value={editForm.category ?? ''} />
                      </label>
                      <label className="field">
                        <span>Speed Threshold (km/h)</span>
                        <input
                          min={0.001}
                          onChange={(e) => setEditForm(f => ({ ...f, speedThresholdKmh: e.target.value ? parseFloat(e.target.value) : null }))}
                          placeholder="Default 120 km/h"
                          type="number"
                          value={editForm.speedThresholdKmh ?? ''}
                        />
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
                ) : (
                  <>
                    <header>
                      <h3>{asset.name}</h3>
                      {asset.isSeeded && <span className="badge badge-demo">Demo</span>}
                      <span className="badge">{classLabel(asset.assetClass, assetClasses)}</span>
                      <span className={`badge ${asset.criticality === 'critical' || asset.criticality === 'high' ? 'badge-warning' : ''}`}>{asset.criticality}</span>
                      <span className="badge">{asset.category ?? 'Uncategorized'}</span>
                      <span className={`badge ${status.className}`}>{status.label}</span>
                    </header>
                    <p className="muted">{asset.description ?? 'No description provided.'}</p>
                    <div className="asset-meta">
                      <div className="asset-meta-row">
                        <span>Devices</span>
                        <strong>{asset.devices.length}</strong>
                      </div>
                      <div className="asset-meta-row">
                        <span>Speed threshold</span>
                        <strong>{asset.speedThresholdKmh != null ? `${asset.speedThresholdKmh} km/h` : 'Default'}</strong>
                      </div>
                      {asset.latestSensorReadings.slice(0, 4).map((reading) => (
                        <div className="asset-meta-row" key={reading.id}>
                          <span>{reading.sensorType.replaceAll('_', ' ')}</span>
                          <strong>{sensorLabel(reading).split(': ').slice(1).join(': ')}</strong>
                        </div>
                      ))}
                      <div className="asset-meta-row">
                        <span>Last signal</span>
                        <strong>{latest ? formatRelativeTime(latest.observedAt, latest.receivedAt) : 'Never'}</strong>
                      </div>
                      {latest && (
                        <div className="asset-meta-row">
                          <span>Position</span>
                          <strong className="coords">{latest.latitude.toFixed(4)}, {latest.longitude.toFixed(4)}</strong>
                        </div>
                      )}
                      <div className="asset-meta-row">
                        <span>Updated</span>
                        <strong>{formatTimestamp(asset.updatedAt)}</strong>
                      </div>
                    </div>
                    <div className="button-row">
                      {isOperator && (
                        <button className="button button-secondary" disabled={submitting} onClick={() => startEdit(asset)} type="button">
                          Edit
                        </button>
                      )}
                      {isOperator && (
                        <button
                          className="button button-danger"
                          disabled={submitting}
                          onClick={() => void handleDeleteAsset(asset.id, asset.name)}
                          type="button"
                        >
                          Delete
                        </button>
                      )}
                    </div>
                  </>
                )}
              </article>
            )})}
            {assets.length > 0 && filteredAssets.length === 0 && (
              <div className="card">No assets match the current filters.</div>
            )}
          </div>
        </div>
      </section>

      <aside className="section">
        <h2>Recent observations</h2>
        <div className="list">
          {observations.slice(0, 6).map((observation) => (
            <article className="list-card" key={observation.id}>
              <header>
                <h3>{observation.assetName ?? observation.deviceIdentifier}</h3>
                <span className="badge">{observation.speedKmh ?? 0} km/h</span>
              </header>
              <div className="coords">
                {observation.latitude.toFixed(4)}, {observation.longitude.toFixed(4)}
              </div>
              <p className="muted">Observed {formatTimestamp(observation.observedAt)}</p>
            </article>
          ))}
          {observations.length === 0 && <div className="card">No observations available yet.</div>}
        </div>
      </aside>
    </div>
  )
}

export default AssetsPage
