import { useEffect, useState } from 'react'
import { getDevices, type DeviceListItem } from '../api/devices'

export default function DevicesPage() {
  const [devices, setDevices] = useState<DeviceListItem[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    getDevices()
      .then(setDevices)
      .catch((e: unknown) => setError(e instanceof Error ? e.message : String(e)))
      .finally(() => setLoading(false))
  }, [])

  if (loading) return <div className="card">Loading devices…</div>
  if (error) return <div className="card">Error: {error}</div>

  return (
    <div>
      <h1>Devices</h1>
      <table>
        <thead>
          <tr>
            <th>Identifier</th>
            <th>Label</th>
            <th>Protocol</th>
            <th>Asset</th>
            <th>Created</th>
          </tr>
        </thead>
        <tbody>
          {devices.map(d => (
            <tr key={d.id}>
              <td>{d.identifier}</td>
              <td>{d.label ?? '—'}</td>
              <td>{d.protocol}</td>
              <td>{d.assetName ?? '—'}</td>
              <td>{new Date(d.createdAt).toLocaleString()}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}
