import { Fragment, FormEvent, useEffect, useState } from 'react'
import { getAssets, type Asset } from '../api/assets'
import { createDevice, deleteDevice, getDevices, updateDevice, type DeviceListItem, type UpdateDeviceRequest } from '../api/devices'

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
  const [editingId, setEditingId] = useState<string | null>(null)
  const [editForm, setEditForm] = useState<UpdateDeviceRequest>({ identifier: '', label: null, protocol: null, assetId: null })

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

  async function handleDeleteDevice(deviceId: string, deviceIdentifier: string) {
    if (!window.confirm(`Delete device "${deviceIdentifier}"? This cannot be undone.`)) return
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

  function startEdit(device: DeviceListItem) {
    setEditingId(device.id)
    setEditForm({ identifier: device.identifier, label: device.label ?? null, protocol: device.protocol ?? null, assetId: device.assetId ?? null })
  }

  async function saveEdit() {
    if (!editingId) return
    setSubmitting(true)
    try {
      await updateDevice(editingId, editForm)
      setEditingId(null)
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
              <Fragment key={device.id}>
                <tr>
                  <td>{device.identifier}</td>
                  <td>{device.label ?? '—'}</td>
                  <td>{device.protocol}</td>
                  <td>{device.assetName ?? '—'}</td>
                  <td>{new Date(device.createdAt).toLocaleString()}</td>
                  <td>
                    <div className="button-row">
                      <button className="button button-secondary" disabled={submitting} onClick={() => startEdit(device)} type="button">
                        Edit
                      </button>
                      <button
                        className="button button-danger"
                        disabled={submitting}
                        onClick={() => void handleDeleteDevice(device.id, device.identifier)}
                        type="button"
                      >
                        Delete
                      </button>
                    </div>
                  </td>
                </tr>
                {editingId === device.id && (
                  <tr key={`${device.id}-edit`}>
                    <td colSpan={6}>
                      <div className="inline-form">
                        <div className="field-grid">
                          <label className="field">
                            <span>Identifier</span>
                            <input onChange={(e) => setEditForm(f => ({ ...f, identifier: e.target.value }))} required value={editForm.identifier} />
                          </label>
                          <label className="field">
                            <span>Label</span>
                            <input onChange={(e) => setEditForm(f => ({ ...f, label: e.target.value || null }))} value={editForm.label ?? ''} />
                          </label>
                          <label className="field">
                            <span>Protocol</span>
                            <input onChange={(e) => setEditForm(f => ({ ...f, protocol: e.target.value || null }))} placeholder="e.g. https, mqtt, tcp" value={editForm.protocol ?? ''} />
                          </label>
                          <label className="field">
                            <span>Asset</span>
                            <select onChange={(e) => setEditForm(f => ({ ...f, assetId: e.target.value || null }))} value={editForm.assetId ?? ''}>
                              <option value="">— Unassigned —</option>
                              {assets.map((a) => (
                                <option key={a.id} value={a.id}>{a.name}</option>
                              ))}
                            </select>
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
