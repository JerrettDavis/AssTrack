import { Fragment, FormEvent, useEffect, useMemo, useState } from 'react'
import { createAsset, getAssets, type Asset } from '../api/assets'
import { createDevice, deleteDevice, getDevices, updateDevice, type DeviceListItem, type UpdateDeviceRequest } from '../api/devices'
import { getIntegrationFeeds, type IntegrationFeed } from '../api/integrations'
import { getLatestPositions, type Observation } from '../api/observations'
import { useIdentityContext } from '../context/IdentityContext'

export default function DevicesPage() {
  const [devices, setDevices] = useState<DeviceListItem[]>([])
  const [assets, setAssets] = useState<Asset[]>([])
  const [feeds, setFeeds] = useState<IntegrationFeed[]>([])
  const [latestPositions, setLatestPositions] = useState<Observation[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [showAddForm, setShowAddForm] = useState(false)
  const [identifier, setIdentifier] = useState('')
  const [label, setLabel] = useState('')
  const [protocol, setProtocol] = useState('')
  const [provider, setProvider] = useState('manual')
  const [externalId, setExternalId] = useState('')
  const [tags, setTags] = useState('')
  const [integrationFeedId, setIntegrationFeedId] = useState('')
  const [assetId, setAssetId] = useState('')
  const [searchTerm, setSearchTerm] = useState('')
  const [assignmentFilter, setAssignmentFilter] = useState<'all' | 'assigned' | 'unassigned'>('all')
  const [submitting, setSubmitting] = useState(false)
  const [editingId, setEditingId] = useState<string | null>(null)
  const [editForm, setEditForm] = useState<UpdateDeviceRequest>({ identifier: '', label: null, protocol: null, assetId: null, provider: null, externalId: null, tags: null, integrationFeedId: null })
  const [quickAssetByDevice, setQuickAssetByDevice] = useState<Record<string, string>>({})
  const [newAssetByDevice, setNewAssetByDevice] = useState<Record<string, string>>({})
  const { isOperator } = useIdentityContext()

  async function load() {
    try {
      setError(null)
      const [deviceItems, assetItems, feedItems, latestItems] = await Promise.all([getDevices(), getAssets(), getIntegrationFeeds(), getLatestPositions()])
      setDevices(deviceItems)
      setAssets(assetItems)
      setFeeds(feedItems)
      setLatestPositions(latestItems)
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
        provider: provider.trim() || undefined,
        externalId: externalId.trim() || undefined,
        tags: tags.trim() || undefined,
        integrationFeedId: integrationFeedId || undefined,
        assetId: assetId || undefined,
      })
      setIdentifier('')
      setLabel('')
      setProtocol('')
      setProvider('manual')
      setExternalId('')
      setTags('')
      setIntegrationFeedId('')
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
    setEditForm({
      identifier: device.identifier,
      label: device.label ?? null,
      protocol: device.protocol ?? null,
      assetId: device.assetId ?? null,
      provider: device.provider ?? 'manual',
      externalId: device.externalId ?? null,
      tags: device.tags ?? null,
      integrationFeedId: device.integrationFeedId ?? null,
    })
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

  async function assignDeviceToAsset(device: DeviceListItem, targetAssetId: string) {
    if (!targetAssetId) return
    setSubmitting(true)
    try {
      await updateDevice(device.id, {
        identifier: device.identifier,
        label: device.label ?? null,
        protocol: device.protocol ?? null,
        assetId: targetAssetId,
        provider: device.provider ?? null,
        externalId: device.externalId ?? null,
        tags: device.tags ?? null,
        integrationFeedId: device.integrationFeedId ?? null,
      })
      setQuickAssetByDevice((current) => ({ ...current, [device.id]: '' }))
      await load()
    } catch (e: unknown) {
      setError(e instanceof Error ? e.message : String(e))
    } finally {
      setSubmitting(false)
    }
  }

  async function createAssetFromDevice(device: DeviceListItem) {
    const proposedName = newAssetByDevice[device.id]?.trim() || device.label || device.identifier
    setSubmitting(true)
    try {
      const asset = await createAsset({
        name: proposedName,
        description: `Created from ${device.provider || 'manual'} tracking signal ${device.identifier}.`,
        category: device.provider === 'meshtastic' ? 'Mesh tracker' : 'Tracked asset',
      })
      await assignDeviceToAsset(device, asset.id)
      setNewAssetByDevice((current) => ({ ...current, [device.id]: '' }))
    } catch (e: unknown) {
      setError(e instanceof Error ? e.message : String(e))
      setSubmitting(false)
    }
  }

  const filteredDevices = useMemo(() => {
    const normalizedSearch = searchTerm.trim().toLowerCase()

    return devices.filter((device) => {
      const searchMatches =
        normalizedSearch.length === 0 ||
        device.identifier.toLowerCase().includes(normalizedSearch) ||
        (device.label ?? '').toLowerCase().includes(normalizedSearch) ||
        (device.protocol ?? '').toLowerCase().includes(normalizedSearch) ||
        (device.assetName ?? '').toLowerCase().includes(normalizedSearch) ||
        (device.provider ?? '').toLowerCase().includes(normalizedSearch) ||
        (device.externalId ?? '').toLowerCase().includes(normalizedSearch) ||
        (device.tags ?? '').toLowerCase().includes(normalizedSearch)

      const assignmentMatches =
        assignmentFilter === 'all' ||
        (assignmentFilter === 'assigned' && Boolean(device.assetId)) ||
        (assignmentFilter === 'unassigned' && !device.assetId)

      return searchMatches && assignmentMatches
    })
  }, [assignmentFilter, devices, searchTerm])

  const protocols = useMemo(() => new Set(devices.map((device) => device.protocol || 'Unspecified')).size, [devices])
  const providers = useMemo(() => new Set(devices.map((device) => device.provider || 'manual')).size, [devices])
  const latestByDeviceId = useMemo(() => new Map(latestPositions.map((position) => [position.deviceId, position])), [latestPositions])
  const signalInbox = useMemo(() => devices
    .filter((device) => !device.assetId && (device.integrationFeedId || (device.provider && device.provider !== 'manual')))
    .sort((a, b) => {
      const aTime = latestByDeviceId.get(a.id)?.observedAt ?? a.createdAt
      const bTime = latestByDeviceId.get(b.id)?.observedAt ?? b.createdAt
      return new Date(bTime).getTime() - new Date(aTime).getTime()
    }), [devices, latestByDeviceId])

  if (loading) return <div className="card">Loading devices…</div>
  if (error) return <div className="card">Error: {error}</div>

  return (
    <div className="section">
      <div className="page-header">
        <h1>Devices</h1>
        {isOperator && (
          <button className="button button-secondary" onClick={() => setShowAddForm((value) => !value)} type="button">
            {showAddForm ? 'Cancel' : 'Add Device'}
          </button>
        )}
      </div>

      <div className="metrics">
        <div className="metric">
          <span>Devices</span>
          <strong>{devices.length}</strong>
        </div>
        <div className="metric">
          <span>Assigned</span>
          <strong>{devices.filter((device) => device.assetId).length}</strong>
        </div>
        <div className="metric">
          <span>Unassigned</span>
          <strong>{devices.filter((device) => !device.assetId).length}</strong>
        </div>
        <div className="metric">
          <span>Providers</span>
          <strong>{providers}</strong>
        </div>
        <div className="metric">
          <span>Protocols</span>
          <strong>{protocols}</strong>
        </div>
      </div>

      <div className="card tracking-inbox" data-testid="tracking-signal-inbox">
        <div className="page-header">
          <div>
            <h2>Tracking signal inbox</h2>
            <p className="muted">Bridge-created devices that are receiving location signals but are not attached to an asset yet.</p>
          </div>
          <span className="badge badge-warning">{signalInbox.length} unassigned</span>
        </div>
        {signalInbox.length === 0 ? (
          <div className="notice notice-info">
            <strong>No unassigned bridge signals</strong>
            <span className="muted">New tracker IDs from Meshtastic, Home Assistant, GPS webhooks, or other bridges will appear here when auto-create is enabled.</span>
          </div>
        ) : (
          <div className="signal-grid">
            {signalInbox.slice(0, 8).map((device) => {
              const latest = latestByDeviceId.get(device.id)
              return (
                <article className="list-card signal-card" key={device.id}>
                  <header>
                    <h3>{device.label || device.identifier}</h3>
                    <span className="badge">{device.provider || 'manual'}</span>
                    {device.integrationFeedName && <span className="badge badge-inline">{device.integrationFeedName}</span>}
                  </header>
                  <div className="asset-meta">
                    <div className="asset-meta-row">
                      <span>Tracker ID</span>
                      <strong>{device.externalId || device.identifier}</strong>
                    </div>
                    <div className="asset-meta-row">
                      <span>Last signal</span>
                      <strong>{latest ? new Date(latest.observedAt).toLocaleString() : 'No observation yet'}</strong>
                    </div>
                    {latest && (
                      <div className="asset-meta-row">
                        <span>Position</span>
                        <strong className="coords">{latest.latitude.toFixed(4)}, {latest.longitude.toFixed(4)}</strong>
                      </div>
                    )}
                  </div>
                  {isOperator && (
                    <div className="signal-actions">
                      <label className="field">
                        <span>Add to asset</span>
                        <select
                          onChange={(event) => setQuickAssetByDevice((current) => ({ ...current, [device.id]: event.target.value }))}
                          value={quickAssetByDevice[device.id] ?? ''}
                        >
                          <option value="">Select asset</option>
                          {assets.map((asset) => (
                            <option key={asset.id} value={asset.id}>{asset.name}</option>
                          ))}
                        </select>
                      </label>
                      <button
                        className="button button-secondary"
                        disabled={submitting || !quickAssetByDevice[device.id]}
                        onClick={() => void assignDeviceToAsset(device, quickAssetByDevice[device.id])}
                        type="button"
                      >
                        Attach
                      </button>
                      <label className="field">
                        <span>New asset name</span>
                        <input
                          onChange={(event) => setNewAssetByDevice((current) => ({ ...current, [device.id]: event.target.value }))}
                          placeholder={device.label || device.identifier}
                          value={newAssetByDevice[device.id] ?? ''}
                        />
                      </label>
                      <button className="button" disabled={submitting} onClick={() => void createAssetFromDevice(device)} type="button">
                        Create asset
                      </button>
                    </div>
                  )}
                </article>
              )
            })}
          </div>
        )}
      </div>

      <div className="card toolbar">
        <label className="field">
          <span>Search devices</span>
          <input
            onChange={(event) => setSearchTerm(event.target.value)}
            placeholder="Identifier, label, asset, protocol"
            type="search"
            value={searchTerm}
          />
        </label>
        <label className="field">
          <span>Assignment</span>
          <select onChange={(event) => setAssignmentFilter(event.target.value as 'all' | 'assigned' | 'unassigned')} value={assignmentFilter}>
            <option value="all">All devices</option>
            <option value="assigned">Assigned</option>
            <option value="unassigned">Unassigned</option>
          </select>
        </label>
        <span className="muted">{filteredDevices.length} shown</span>
      </div>

      {showAddForm && isOperator && (
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
              <span>Provider</span>
              <input onChange={(event) => setProvider(event.target.value)} placeholder="manual, gps-http, meshtastic" value={provider} />
            </label>
            <label className="field">
              <span>External tracker ID</span>
              <input onChange={(event) => setExternalId(event.target.value)} value={externalId} />
            </label>
            <label className="field">
              <span>Tags</span>
              <input onChange={(event) => setTags(event.target.value)} placeholder="truck, airtag, primary" value={tags} />
            </label>
            <label className="field">
              <span>Integration feed</span>
              <select onChange={(event) => setIntegrationFeedId(event.target.value)} value={integrationFeedId}>
                <option value="">None</option>
                {feeds.map((feed) => (
                  <option key={feed.id} value={feed.id}>{feed.name}</option>
                ))}
              </select>
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
              <th>Provider</th>
              <th>Tags</th>
              <th>Asset</th>
              <th>Created</th>
              <th>Actions</th>
            </tr>
          </thead>
          <tbody>
            {filteredDevices.map((device) => (
              <Fragment key={device.id}>
                <tr>
                  <td>{device.identifier}{device.isSeeded && <span className="badge badge-demo badge-inline">Demo</span>}</td>
                  <td>{device.label ?? '—'}</td>
                  <td><span className="badge">{device.protocol || 'Unspecified'}</span></td>
                  <td>
                    <span className="badge">{device.provider || 'manual'}</span>
                    {device.integrationFeedName && <span className="badge badge-inline">{device.integrationFeedName}</span>}
                    {device.externalId && <div className="muted">{device.externalId}</div>}
                  </td>
                  <td>{device.tags ?? '—'}</td>
                  <td>{device.assetName ?? <span className="badge badge-warning">Unassigned</span>}</td>
                  <td>{new Date(device.createdAt).toLocaleString()}</td>
                  <td>
                    <div className="button-row">
                      {isOperator && (
                        <button className="button button-secondary" disabled={submitting} onClick={() => startEdit(device)} type="button">
                          Edit
                        </button>
                      )}
                      {isOperator && (
                        <button
                          className="button button-danger"
                          disabled={submitting}
                          onClick={() => void handleDeleteDevice(device.id, device.identifier)}
                          type="button"
                        >
                          Delete
                        </button>
                      )}
                    </div>
                  </td>
                </tr>
                {editingId === device.id && (
                  <tr key={`${device.id}-edit`}>
                    <td colSpan={8}>
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
                            <span>Provider</span>
                            <input onChange={(e) => setEditForm(f => ({ ...f, provider: e.target.value || null }))} value={editForm.provider ?? ''} />
                          </label>
                          <label className="field">
                            <span>External tracker ID</span>
                            <input onChange={(e) => setEditForm(f => ({ ...f, externalId: e.target.value || null }))} value={editForm.externalId ?? ''} />
                          </label>
                          <label className="field">
                            <span>Tags</span>
                            <input onChange={(e) => setEditForm(f => ({ ...f, tags: e.target.value || null }))} value={editForm.tags ?? ''} />
                          </label>
                          <label className="field">
                            <span>Integration feed</span>
                            <select onChange={(e) => setEditForm(f => ({ ...f, integrationFeedId: e.target.value || null }))} value={editForm.integrationFeedId ?? ''}>
                              <option value="">None</option>
                              {feeds.map((feed) => (
                                <option key={feed.id} value={feed.id}>{feed.name}</option>
                              ))}
                            </select>
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
                <td className="muted" colSpan={8}>
                  No devices available yet.
                </td>
              </tr>
            )}
            {devices.length > 0 && filteredDevices.length === 0 && (
              <tr>
                <td className="muted" colSpan={8}>
                  No devices match the current filters.
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>
    </div>
  )
}
