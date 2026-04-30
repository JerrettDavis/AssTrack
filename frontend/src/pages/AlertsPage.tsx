import { useEffect, useRef, useState } from 'react'
import { acknowledgeSpeedAlert, bulkAcknowledgeSpeedAlerts, getSpeedAlerts, type SpeedAlert } from '../api/alerts'
import { acknowledgeBreach, bulkAcknowledgeBreaches, getGeofenceBreaches, type GeofenceBreach } from '../api/geofenceBreaches'

type FilterTab = 'all' | 'unacknowledged'

export default function AlertsPage() {
  const [alerts, setAlerts] = useState<SpeedAlert[]>([])
  const [breaches, setBreaches] = useState<GeofenceBreach[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [lastUpdated, setLastUpdated] = useState<string | null>(null)
  const [speedFilter, setSpeedFilter] = useState<FilterTab>('all')
  const [breachFilter, setBreachFilter] = useState<FilterTab>('all')
  const [selectedSpeedAlerts, setSelectedSpeedAlerts] = useState<Set<string>>(new Set())
  const [selectedBreaches, setSelectedBreaches] = useState<Set<string>>(new Set())
  const pollRef = useRef<number | null>(null)

  async function load() {
    try {
      setError(null)
      const [speedAlerts, geofenceBreaches] = await Promise.all([
        getSpeedAlerts({ unacknowledged: speedFilter === 'unacknowledged' || undefined }),
        getGeofenceBreaches({ unacknowledged: breachFilter === 'unacknowledged' || undefined }),
      ])
      setAlerts(speedAlerts)
      setBreaches(geofenceBreaches)
      setLastUpdated(new Date().toLocaleTimeString())
    } catch (e: unknown) {
      setError(e instanceof Error ? e.message : String(e))
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    void load()
    pollRef.current = window.setInterval(() => {
      void load()
    }, 30000)

    return () => {
      if (pollRef.current != null) {
        window.clearInterval(pollRef.current)
      }
    }
  }, [speedFilter, breachFilter])

  async function handleAcknowledgeAlert(id: string) {
    const acknowledgedBy = window.prompt('Your name (optional):') ?? undefined
    try {
      await acknowledgeSpeedAlert(id, acknowledgedBy || undefined)
      await load()
    } catch (e: unknown) {
      setError(e instanceof Error ? e.message : String(e))
    }
  }

  async function handleAcknowledgeBreach(id: string) {
    const acknowledgedBy = window.prompt('Your name (optional):') ?? undefined
    try {
      await acknowledgeBreach(id, acknowledgedBy || undefined)
      await load()
    } catch (e: unknown) {
      setError(e instanceof Error ? e.message : String(e))
    }
  }

  async function handleBulkAcknowledgeSpeedAlerts() {
    if (selectedSpeedAlerts.size === 0) return
    const acknowledgedBy = window.prompt('Your name (optional):') ?? undefined
    try {
      await bulkAcknowledgeSpeedAlerts(Array.from(selectedSpeedAlerts), acknowledgedBy || undefined)
      setSelectedSpeedAlerts(new Set())
      await load()
    } catch (e: unknown) {
      setError(e instanceof Error ? e.message : String(e))
    }
  }

  async function handleBulkAcknowledgeBreaches() {
    if (selectedBreaches.size === 0) return
    const acknowledgedBy = window.prompt('Your name (optional):') ?? undefined
    try {
      await bulkAcknowledgeBreaches(Array.from(selectedBreaches), acknowledgedBy || undefined)
      setSelectedBreaches(new Set())
      await load()
    } catch (e: unknown) {
      setError(e instanceof Error ? e.message : String(e))
    }
  }

  function toggleSpeedAlert(id: string) {
    const newSet = new Set(selectedSpeedAlerts)
    if (newSet.has(id)) newSet.delete(id)
    else newSet.add(id)
    setSelectedSpeedAlerts(newSet)
  }

  function toggleBreach(id: string) {
    const newSet = new Set(selectedBreaches)
    if (newSet.has(id)) newSet.delete(id)
    else newSet.add(id)
    setSelectedBreaches(newSet)
  }

  if (loading) return <div className="card">Loading alerts…</div>
  if (error) return <div className="card">Error: {error}</div>

  const unacknowledgedAlerts = alerts.filter(a => !a.acknowledgedAtUtc)
  const unacknowledgedBreaches = breaches.filter(b => !b.acknowledgedAtUtc)

  return (
    <div className="section">
      <div className="page-header">
        <h1>Alerts</h1>
        <span className="muted">Last updated: {lastUpdated ?? '—'}</span>
      </div>

      <div className="card table-card">
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '1rem' }}>
          <h2>Speed Alerts</h2>
          {selectedSpeedAlerts.size > 0 && (
            <button className="button button-primary" onClick={() => void handleBulkAcknowledgeSpeedAlerts()} type="button">
              Acknowledge {selectedSpeedAlerts.size} selected
            </button>
          )}
        </div>
        <div className="alert-tabs">
          <button
            className={speedFilter === 'all' ? 'active' : ''}
            onClick={() => setSpeedFilter('all')}
            type="button"
          >
            All ({alerts.length})
          </button>
          <button
            className={speedFilter === 'unacknowledged' ? 'active' : ''}
            onClick={() => setSpeedFilter('unacknowledged')}
            type="button"
          >
            Unacknowledged ({unacknowledgedAlerts.length})
          </button>
        </div>
        <table className="data-table">
          <thead>
            <tr>
              <th style={{ width: '40px' }}>
                {speedFilter === 'unacknowledged' && unacknowledgedAlerts.length > 0 && (
                  <input
                    type="checkbox"
                    checked={unacknowledgedAlerts.every(a => selectedSpeedAlerts.has(a.id))}
                    onChange={(e) => {
                      if (e.target.checked) {
                        setSelectedSpeedAlerts(new Set(unacknowledgedAlerts.map(a => a.id)))
                      } else {
                        setSelectedSpeedAlerts(new Set())
                      }
                    }}
                  />
                )}
              </th>
              <th>Device</th>
              <th>Speed (km/h)</th>
              <th>Threshold (km/h)</th>
              <th>Triggered At</th>
              <th>Status</th>
            </tr>
          </thead>
          <tbody>
            {alerts.map((alert) => (
              <tr key={alert.id}>
                <td>
                  {!alert.acknowledgedAtUtc && (
                    <input
                      type="checkbox"
                      checked={selectedSpeedAlerts.has(alert.id)}
                      onChange={() => toggleSpeedAlert(alert.id)}
                    />
                  )}
                </td>
                <td>{alert.assetName ?? alert.deviceIdentifier ?? alert.deviceId}</td>
                <td>{alert.observedSpeedKmh.toFixed(1)}</td>
                <td>{alert.thresholdKmh.toFixed(1)}</td>
                <td>{new Date(alert.triggeredAt).toLocaleString()}</td>
                <td>
                  {alert.acknowledgedAtUtc ? (
                    <span>
                      Acknowledged {new Date(alert.acknowledgedAtUtc).toLocaleString()}
                      {alert.acknowledgedBy ? ` by ${alert.acknowledgedBy}` : ''}
                    </span>
                  ) : (
                    <button className="button button-secondary" onClick={() => void handleAcknowledgeAlert(alert.id)} type="button">
                      Acknowledge
                    </button>
                  )}
                </td>
              </tr>
            ))}
            {alerts.length === 0 && (
              <tr>
                <td className="muted" colSpan={6}>
                  No speed alerts available.
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>

      <div className="card table-card">
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '1rem' }}>
          <h2>Geofence Breaches</h2>
          {selectedBreaches.size > 0 && (
            <button className="button button-primary" onClick={() => void handleBulkAcknowledgeBreaches()} type="button">
              Acknowledge {selectedBreaches.size} selected
            </button>
          )}
        </div>
        <div className="alert-tabs">
          <button
            className={breachFilter === 'all' ? 'active' : ''}
            onClick={() => setBreachFilter('all')}
            type="button"
          >
            All ({breaches.length})
          </button>
          <button
            className={breachFilter === 'unacknowledged' ? 'active' : ''}
            onClick={() => setBreachFilter('unacknowledged')}
            type="button"
          >
            Unacknowledged ({unacknowledgedBreaches.length})
          </button>
        </div>
        <table className="data-table">
          <thead>
            <tr>
              <th style={{ width: '40px' }}>
                {breachFilter === 'unacknowledged' && unacknowledgedBreaches.length > 0 && (
                  <input
                    type="checkbox"
                    checked={unacknowledgedBreaches.every(b => selectedBreaches.has(b.id))}
                    onChange={(e) => {
                      if (e.target.checked) {
                        setSelectedBreaches(new Set(unacknowledgedBreaches.map(b => b.id)))
                      } else {
                        setSelectedBreaches(new Set())
                      }
                    }}
                  />
                )}
              </th>
              <th>Device</th>
              <th>Event</th>
              <th>Geofence</th>
              <th>Detected At</th>
              <th>Status</th>
            </tr>
          </thead>
          <tbody>
            {breaches.map((breach) => (
              <tr key={breach.id}>
                <td>
                  {!breach.acknowledgedAtUtc && (
                    <input
                      type="checkbox"
                      checked={selectedBreaches.has(breach.id)}
                      onChange={() => toggleBreach(breach.id)}
                    />
                  )}
                </td>
                <td>{breach.assetName ?? breach.deviceIdentifier ?? breach.deviceId}</td>
                <td>{breach.eventType}</td>
                <td>{breach.geofenceName}</td>
                <td>{new Date(breach.detectedAt).toLocaleString()}</td>
                <td>
                  {breach.acknowledgedAtUtc ? (
                    <span>
                      Acknowledged {new Date(breach.acknowledgedAtUtc).toLocaleString()}
                      {breach.acknowledgedBy ? ` by ${breach.acknowledgedBy}` : ''}
                    </span>
                  ) : (
                    <button className="button button-secondary" onClick={() => void handleAcknowledgeBreach(breach.id)} type="button">
                      Acknowledge
                    </button>
                  )}
                </td>
              </tr>
            ))}
            {breaches.length === 0 && (
              <tr>
                <td className="muted" colSpan={6}>
                  No geofence breaches available.
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>
    </div>
  )
}
