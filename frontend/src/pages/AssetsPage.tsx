import { FormEvent, useEffect, useMemo, useState } from 'react'
import { Asset, createAsset, deleteAsset, getAssets } from '../api/assets'
import { getObservations, Observation } from '../api/observations'

function formatTimestamp(value: string) {
  return new Date(value).toLocaleString()
}

export function AssetsPage() {
  const [assets, setAssets] = useState<Asset[]>([])
  const [observations, setObservations] = useState<Observation[]>([])
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(true)
  const [showAddForm, setShowAddForm] = useState(false)
  const [name, setName] = useState('')
  const [description, setDescription] = useState('')
  const [speedThresholdKmh, setSpeedThresholdKmh] = useState<string>('')
  const [submitting, setSubmitting] = useState(false)

  async function load() {
    try {
      setError(null)
      const [assetItems, observationItems] = await Promise.all([getAssets(), getObservations()])
      setAssets(assetItems)
      setObservations(observationItems)
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
        speedThresholdKmh: speedValue,
      })
      setName('')
      setDescription('')
      setSpeedThresholdKmh('')
      setShowAddForm(false)
      await load()
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Unable to create asset.')
    } finally {
      setSubmitting(false)
    }
  }

  async function handleDeleteAsset(assetId: string) {
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

  const metrics = useMemo(() => {
    const deviceCount = assets.reduce((total, asset) => total + asset.devices.length, 0)
    const movingCount = observations.filter((observation) => (observation.speedKmh ?? 0) > 0).length

    return [
      { label: 'Assets', value: assets.length },
      { label: 'Devices', value: deviceCount },
      { label: 'Observations', value: observations.length },
      { label: 'Moving now', value: movingCount },
    ]
  }, [assets, observations])

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
            <button className="button button-secondary" onClick={() => setShowAddForm((value) => !value)} type="button">
              {showAddForm ? 'Cancel' : 'Add Asset'}
            </button>
          </div>
          {showAddForm && (
            <form className="card inline-form" onSubmit={handleCreateAsset}>
              <div className="field-grid">
                <label className="field">
                  <span>Name</span>
                  <input onChange={(event) => setName(event.target.value)} required value={name} />
                </label>
                <label className="field field-wide">
                  <span>Description</span>
                  <input onChange={(event) => setDescription(event.target.value)} value={description} />
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
          <div className="asset-grid">
            {assets.map((asset) => (
              <article className="list-card" key={asset.id}>
                <header>
                  <h3>{asset.name}</h3>
                  <span className="badge">{asset.category ?? 'Uncategorized'}</span>
                </header>
                <p className="muted">{asset.description ?? 'No description provided.'}</p>
                <p>Devices: {asset.devices.length}</p>
                <p>Speed threshold: {asset.speedThresholdKmh != null ? `${asset.speedThresholdKmh} km/h` : 'Default'}</p>
                <p>Updated: {formatTimestamp(asset.updatedAt)}</p>
                <div className="button-row">
                  <button
                    className="button button-danger"
                    disabled={submitting}
                    onClick={() => void handleDeleteAsset(asset.id)}
                    type="button"
                  >
                    Delete
                  </button>
                </div>
              </article>
            ))}
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
