import { FormEvent, useEffect, useState } from 'react'
import { getAssets, type Asset } from '../api/assets'
import { createDevice, deleteDevice, getDevices, type DeviceListItem } from '../api/devices'

export default function DevicesPage() {
  const [devices, setDevices] = useState<DeviceListItem[]>([])
  const [assets, setAssets] = useState<Asset[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [showAddForm, setShowAddForm] = useState(false)
  const [identifier, setIdentifier] = useState('')
  const [label, setLabel] = useState('')
  const [protocol, setProtocol] = useState('')
  const [assetId, setAssetId] = useState('')
  const [submitting, setSubmitting] = useState(false)

  async function load() {
    try {
      setError(null)
      const [deviceItems, assetItems] = await Promise.all([getDevices(), getAssets()])
      setDevices(deviceItems)
      setAssets(assetItems)
    } catch (e: unknown) {
      setError(e instanceof Error ? e.message : String(e))
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    void load()
  }, [])

  async function handleCreateDevice(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setSubmitting(true)
    try {
      await createDevice({
        identifier: identifier.trim(),
        label: label.trim() || undefined,
        protocol: protocol.trim() || undefined,
        assetId: assetId || undefined,
      })
      setIdentifier('')
      setLabel('')
      setProtocol('')
      setAssetId('')
      setShowAddForm(false)
      await load()
    } catch (e: unknown) {
      setError(e instanceof Error ? e.message : String(e))
    } finally {
      setSubmitting(false)
    }
  }

  async function handleDeleteDevice(deviceId: string) {
    setSubmitting(true)
    try {
      await deleteDevice(deviceId)
      await load()
    } catch (e: unknown) {
      setError(e instanceof Error ? e.message : String(e))
    } finally {
      setSubmitting(false)
    }
  }

  if (loading) return <div className="card">Loading devices…</div>
  if (error) return <div className="card">Error: {error}</div>

  return (
    <div className="section">
      <div className="page-header">
        <h1>Devices</h1>
        <button className="button button-secondary" onClick={() => setShowAddForm((value) => !value)} type="button">
          {showAddForm ? 'Cancel' : 'Add Device'}
        </button>
      </div>

      {showAddForm && (
        <form className="card inline-form" onSubmit={handleCreateDevice}>
          <div className="field-grid">
            <label className="field">
              <span>Identifier</span>
              <input onChange={(event) => setIdentifier(event.target.value)} required value={identifier} />
            </label>
            <label className="field">
              <span>Label</span>
              <input onChange={(event) => setLabel(event.target.value)} value={label} />
            </label>
            <label className="field">
              <span>Protocol</span>
              <input onChange={(event) => setProtocol(event.target.value)} value={protocol} />
            </label>
            <label className="field">
              <span>Asset</span>
              <select onChange={(event) => setAssetId(event.target.value)} value={assetId}>
                <option value="">Unassigned</option>
                {assets.map((asset) => (
                  <option key={asset.id} value={asset.id}>
                    {asset.name}
                  </option>
                ))}
              </select>
            </label>
          </div>
          <div className="button-row">
            <button className="button" disabled={submitting} type="submit">
              {submitting ? 'Saving…' : 'Create device'}
            </button>
          </div>
        </form>
      )}

      <div className="card table-card">
        <table className="data-table">
          <thead>
            <tr>
              <th>Identifier</th>
              <th>Label</th>
              <th>Protocol</th>
              <th>Asset</th>
              <th>Created</th>
              <th>Actions</th>
            </tr>
          </thead>
          <tbody>
            {devices.map((device) => (
              <tr key={device.id}>
                <td>{device.identifier}</td>
                <td>{device.label ?? '—'}</td>
                <td>{device.protocol}</td>
                <td>{device.assetName ?? '—'}</td>
                <td>{new Date(device.createdAt).toLocaleString()}</td>
                <td>
                  <button
                    className="button button-danger"
                    disabled={submitting}
                    onClick={() => void handleDeleteDevice(device.id)}
                    type="button"
                  >
                    Delete
                  </button>
                </td>
              </tr>
            ))}
            {devices.length === 0 && (
              <tr>
                <td className="muted" colSpan={6}>
                  No devices available yet.
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>
    </div>
  )
}
