import { useEffect, useMemo, useState } from 'react'
import { Asset, getAssets } from '../api/assets'
import { getObservations, Observation } from '../api/observations'

function formatTimestamp(value: string) {
  return new Date(value).toLocaleString()
}

export function AssetsPage() {
  const [assets, setAssets] = useState<Asset[]>([])
  const [observations, setObservations] = useState<Observation[]>([])
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    async function load() {
      try {
        const [assetItems, observationItems] = await Promise.all([getAssets(), getObservations()])
        setAssets(assetItems)
        setObservations(observationItems)
      } catch (err) {
        setError(err instanceof Error ? err.message : 'Unable to load API data.')
      } finally {
        setLoading(false)
      }
    }

    void load()
  }, [])

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
          <h2>Assets</h2>
          <div className="asset-grid">
            {assets.map((asset) => (
              <article className="list-card" key={asset.id}>
                <header>
                  <h3>{asset.name}</h3>
                  <span className="badge">{asset.category ?? 'Uncategorized'}</span>
                </header>
                <p className="muted">{asset.description ?? 'No description provided.'}</p>
                <p>Devices: {asset.devices.length}</p>
                <p>Updated: {formatTimestamp(asset.updatedAt)}</p>
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
