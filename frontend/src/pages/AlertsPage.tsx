import { useEffect, useState } from 'react'
import { getSpeedAlerts, type SpeedAlert } from '../api/alerts'
import { getGeofenceBreaches, type GeofenceBreach } from '../api/geofenceBreaches'

export default function AlertsPage() {
  const [alerts, setAlerts] = useState<SpeedAlert[]>([])
  const [breaches, setBreaches] = useState<GeofenceBreach[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    Promise.all([getSpeedAlerts(), getGeofenceBreaches()])
      .then(([speedAlerts, geofenceBreaches]) => {
        setAlerts(speedAlerts)
        setBreaches(geofenceBreaches)
      })
      .catch((e: unknown) => setError(e instanceof Error ? e.message : String(e)))
      .finally(() => setLoading(false))
  }, [])

  if (loading) return <div className="card">Loading alerts…</div>
  if (error) return <div className="card">Error: {error}</div>

  return (
    <div>
      <h1>Speed Alerts</h1>
      <table>
        <thead>
          <tr>
            <th>Device</th>
            <th>Speed (km/h)</th>
            <th>Threshold (km/h)</th>
            <th>Triggered At</th>
          </tr>
        </thead>
        <tbody>
          {alerts.map(a => (
            <tr key={a.id}>
              <td>{a.deviceId}</td>
              <td>{a.observedSpeedKmh.toFixed(1)}</td>
              <td>{a.thresholdKmh.toFixed(1)}</td>
              <td>{new Date(a.triggeredAt).toLocaleString()}</td>
            </tr>
          ))}
        </tbody>
      </table>

      <h2>Geofence Breaches</h2>
      <table>
        <thead>
          <tr>
            <th>Device</th>
            <th>Geofence</th>
            <th>Detected At</th>
          </tr>
        </thead>
        <tbody>
          {breaches.map(b => (
            <tr key={b.id}>
              <td>{b.deviceId}</td>
              <td>{b.geofenceName}</td>
              <td>{new Date(b.detectedAt).toLocaleString()}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}

