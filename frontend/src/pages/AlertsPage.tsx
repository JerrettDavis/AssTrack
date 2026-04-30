import { useEffect, useRef, useState } from 'react'
import { acknowledgeSpeedAlert, getSpeedAlerts, type SpeedAlert } from '../api/alerts'
import { acknowledgeBreach, getGeofenceBreaches, type GeofenceBreach } from '../api/geofenceBreaches'

export default function AlertsPage() {
  const [alerts, setAlerts] = useState<SpeedAlert[]>([])
  const [breaches, setBreaches] = useState<GeofenceBreach[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [lastUpdated, setLastUpdated] = useState<string | null>(null)
  const pollRef = useRef<number | null>(null)

  async function load() {
    try {
      setError(null)
      const [speedAlerts, geofenceBreaches] = await Promise.all([getSpeedAlerts(), getGeofenceBreaches()])
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
  }, [])

  async function handleAcknowledgeAlert(id: string) {
    try {
      await acknowledgeSpeedAlert(id)
      await load()
    } catch (e: unknown) {
      setError(e instanceof Error ? e.message : String(e))
    }
  }

  async function handleAcknowledgeBreach(id: string) {
    try {
      await acknowledgeBreach(id)
      await load()
    } catch (e: unknown) {
      setError(e instanceof Error ? e.message : String(e))
    }
  }

  if (loading) return <div className="card">Loading alerts…</div>
  if (error) return <div className="card">Error: {error}</div>

  return (
    <div className="section">
      <div className="page-header">
        <h1>Alerts</h1>
        <span className="muted">Last updated: {lastUpdated ?? '—'}</span>
      </div>

      <div className="card table-card">
        <h2>Speed Alerts</h2>
        <table className="data-table">
          <thead>
            <tr>
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
                <td className="muted" colSpan={5}>
                  No speed alerts available.
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>

      <div className="card table-card">
        <h2>Geofence Breaches</h2>
        <table className="data-table">
          <thead>
            <tr>
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
                <td className="muted" colSpan={5}>
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
