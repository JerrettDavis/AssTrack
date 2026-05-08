import { Fragment, FormEvent, useEffect, useMemo, useState } from 'react'
import { createAsset, getAssets, type Asset } from '../api/assets'
import type { SensorReading } from '../api/assets'
import { createDevice, deleteDevice, getDevices, updateDevice, type DeviceListItem, type UpdateDeviceRequest } from '../api/devices'
import { getIntegrationFeeds, type IntegrationFeed } from '../api/integrations'
import { getLatestPositions, type Observation } from '../api/observations'
import { getSensorReadings } from '../api/sensors'
import { useIdentityContext } from '../context/IdentityContext'
import { useLiveDataRefresh } from '../hooks/useLiveDataRefresh'
import DisplayControls from '../components/DisplayControls'

function providerDisplayName(device: DeviceListItem): string {
  return device.providerLongName || device.providerLabel || device.providerShortName || device.label || device.identifier
}

function sensorName(reading: SensorReading) {
  return reading.name || reading.sensorType.replaceAll('_', ' ')
}

function sensorValue(reading: SensorReading) {
  if (reading.numericValue != null) return `${reading.numericValue}${reading.unit ? ` ${reading.unit}` : ''}`
  return reading.textValue ?? 'N/A'
}

function getSensorStatus(reading: SensorReading) {
  const ageMs = Math.max(0, Date.now() - new Date(reading.observedAt).getTime())
  if (ageMs > 24 * 60 * 60 * 1000) return { label: 'Stale', className: 'badge-danger' }
  if (ageMs > 6 * 60 * 60 * 1000) return { label: 'Aging', className: 'badge-warning' }
  return { label: 'Fresh', className: 'badge-success' }
}

function latestBySensorType(readings: SensorReading[]) {
  const latest = new Map<string, SensorReading>()
  readings.forEach((reading) => {
    const current = latest.get(reading.sensorType)
    if (!current || new Date(reading.observedAt).getTime() > new Date(current.observedAt).getTime()) {
      latest.set(reading.sensorType, reading)
    }
  })
  return Array.from(latest.values()).sort((a, b) => a.sensorType.localeCompare(b.sensorType))
}

function DeviceTelemetrySummary({ readings }: { readings: SensorReading[] }) {
  const latest = latestBySensorType(readings).slice(0, 3)
  if (latest.length === 0) return <span className="muted">No telemetry</span>

  return (
    <div className="device-telemetry">
      {latest.map((reading) => {
        const status = getSensorStatus(reading)
        return (
          <span className="device-telemetry-item" key={`${reading.sensorType}-${reading.id}`}>
            <span>{sensorName(reading)}</span>
            <strong>{sensorValue(reading)}</strong>
            <span className={`badge ${status.className}`}>{status.label}</span>
          </span>
        )
      })}
    </div>
  )
}

export default function DevicesPage() {
  const [devices, setDevices] = useState<DeviceListItem[]>([])
  const [assets, setAssets] = useState<Asset[]>([])
  const [feeds, setFeeds] = useState<IntegrationFeed[]>([])
  const [latestPositions, setLatestPositions] = useState<Observation[]>([])
  const [sensorReadings, setSensorReadings] = useState<SensorReading[]>([])
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
  const [deviceViewMode, setDeviceViewMode] = useState<'cards' | 'table'>('table')
  const [devicePage, setDevicePage] = useState(1)
  const [devicePageSize, setDevicePageSize] = useState(24)
  const [submitting, setSubmitting] = useState(false)
  const [editingId, setEditingId] = useState<string | null>(null)
  const [editForm, setEditForm] = useState<UpdateDeviceRequest>({ identifier: '', label: null, protocol: null, assetId: null, provider: null, externalId: null, tags: null, integrationFeedId: null })
  const [quickAssetByDevice, setQuickAssetByDevice] = useState<Record<string, string>>({})
  const [newAssetByDevice, setNewAssetByDevice] = useState<Record<string, string>>({})
  const { isOperator } = useIdentityContext()

  async function load() {
    try {
      setError(null)
      const [deviceItems, assetItems, feedItems, latestItems, sensorItems] = await Promise.all([
        getDevices(),
        getAssets(),
        getIntegrationFeeds(),
        getLatestPositions(),
        getSensorReadings({ limit: 500 }),
      ])
      setDevices(deviceItems)
      setAssets(assetItems)
      setFeeds(feedItems)
      setLatestPositions(latestItems)
      setSensorReadings(sensorItems)
    } catch (e: unknown) {
      setError(e instanceof Error ? e.message : String(e))
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    void load()
  }, [])

  useLiveDataRefresh(load, { eventTypes: ['data_changed', 'observation'], debounceMs: 1200 })

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
    const proposedName = newAssetByDevice[device.id]?.trim() || providerDisplayName(device)
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
        (device.tags ?? '').toLowerCase().includes(normalizedSearch) ||
        (device.providerLongName ?? '').toLowerCase().includes(normalizedSearch) ||
        (device.providerShortName ?? '').toLowerCase().includes(normalizedSearch) ||
        (device.providerHardwareModel ?? '').toLowerCase().includes(normalizedSearch) ||
        (device.providerRole ?? '').toLowerCase().includes(normalizedSearch)

      const assignmentMatches =
        assignmentFilter === 'all' ||
        (assignmentFilter === 'assigned' && Boolean(device.assetId)) ||
        (assignmentFilter === 'unassigned' && !device.assetId)

      return searchMatches && assignmentMatches
    })
  }, [assignmentFilter, devices, searchTerm])

  const deviceTotalPages = Math.max(1, Math.ceil(filteredDevices.length / devicePageSize))
  const visibleDevicePage = Math.min(devicePage, deviceTotalPages)
  const pagedDevices = useMemo(() => {
    const start = (visibleDevicePage - 1) * devicePageSize
    return filteredDevices.slice(start, start + devicePageSize)
  }, [devicePageSize, filteredDevices, visibleDevicePage])
  const deviceStart = filteredDevices.length === 0 ? 0 : (visibleDevicePage - 1) * devicePageSize + 1
  const deviceEnd = Math.min(filteredDevices.length, visibleDevicePage * devicePageSize)

  useEffect(() => {
    setDevicePage(1)
  }, [assignmentFilter, devicePageSize, searchTerm])

  useEffect(() => {
    setDevicePage((current) => Math.min(current, deviceTotalPages))
  }, [deviceTotalPages])

  const protocols = useMemo(() => new Set(devices.map((device) => device.protocol || 'Unspecified')).size, [devices])
  const providers = useMemo(() => new Set(devices.map((device) => device.provider || 'manual')).size, [devices])
  const latestByDeviceId = useMemo(() => new Map(latestPositions.map((position) => [position.deviceId, position])), [latestPositions])
  const readingsByDeviceId = useMemo(() => {
    const grouped = new Map<string, SensorReading[]>()
    sensorReadings.forEach((reading) => {
      if (!reading.deviceId) return
      grouped.set(reading.deviceId, [...(grouped.get(reading.deviceId) ?? []), reading])
    })
    return grouped
  }, [sensorReadings])
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
    <div className="section ops-page">
      <div className="ops-header">
        <div className="ops-title">
          <h1>Devices</h1>
          <p>{devices.length} trackers, sensors, and bridge-created nodes</p>
        </div>
        <div className="ops-actions">
          {isOperator && (
            <button className="button button-secondary" onClick={() => setShowAddForm((value) => !value)} type="button">
              {showAddForm ? 'Cancel' : 'Add Device'}
            </button>
          )}
        </div>
      </div>

      <div className="metrics kpi-strip">
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

      <details className="quiet-disclosure tracking-inbox" data-testid="tracking-signal-inbox" open={signalInbox.length > 0}>
        <summary>
          Tracking signal inbox
          <span className={signalInbox.length > 0 ? 'badge badge-warning' : 'badge'}>{signalInbox.length} unassigned</span>
        </summary>
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
                    <h3>{providerDisplayName(device)}</h3>
                    <span className="badge">{device.provider || 'manual'}</span>
                    {device.integrationFeedName && <span className="badge badge-inline">{device.integrationFeedName}</span>}
                  </header>
                  <div className="asset-meta">
                    <div className="asset-meta-row">
                      <span>Tracker ID</span>
                      <strong>{device.externalId || device.identifier}</strong>
                    </div>
                    {(device.providerShortName || device.providerHardwareModel || device.providerRole) && (
                      <div className="asset-meta-row">
                        <span>Provider profile</span>
                        <strong>{[device.providerShortName, device.providerHardwareModel, device.providerRole].filter(Boolean).join(' / ')}</strong>
                      </div>
                    )}
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
                          placeholder={providerDisplayName(device)}
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
      </details>

      <div className="card control-bar">
        <label className="field">
          <span>Search</span>
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
        <label className="field compact-field">
          <span>Page size</span>
          <select onChange={(event) => setDevicePageSize(Number(event.target.value))} value={devicePageSize}>
            <option value={12}>12</option>
            <option value={24}>24</option>
            <option value={48}>48</option>
            <option value={96}>96</option>
          </select>
        </label>
        <div className="compact-actions">
          <span className="control-bar-result"><strong>{filteredDevices.length}</strong> shown</span>
          <DisplayControls mode={deviceViewMode} onModeChange={setDeviceViewMode} />
          {(searchTerm || assignmentFilter !== 'all') && (
            <button
              className="button button-secondary button-compact"
              onClick={() => {
                setSearchTerm('')
                setAssignmentFilter('all')
              }}
              type="button"
            >
              Clear
            </button>
          )}
        </div>
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

      {deviceViewMode === 'cards' ? (
      <div className="asset-grid">
        {pagedDevices.map((device) => {
          const latest = latestByDeviceId.get(device.id)
          return (
            <article className="list-card" key={device.id}>
              <header>
                <h3>{providerDisplayName(device)}</h3>
                <span className={device.assetId ? 'badge badge-success' : 'badge badge-warning'}>{device.assetId ? 'Assigned' : 'Unassigned'}</span>
              </header>
              <div className="asset-meta">
                <div className="asset-meta-row"><span>Identifier</span><strong>{device.identifier}</strong></div>
                <div className="asset-meta-row"><span>Provider</span><strong>{device.integrationFeedName ?? device.provider ?? 'manual'}</strong></div>
                <div className="asset-meta-row"><span>Asset</span><strong>{device.assetName ?? 'Unassigned'}</strong></div>
                <div className="asset-meta-row"><span>Last signal</span><strong>{latest ? new Date(latest.observedAt).toLocaleString() : 'No observation yet'}</strong></div>
              </div>
              <DeviceTelemetrySummary readings={readingsByDeviceId.get(device.id) ?? []} />
            </article>
          )
        })}
        {devices.length > 0 && filteredDevices.length === 0 && <div className="card">No devices match the current filters.</div>}
      </div>
      ) : (
      <div className="card table-card">
        <div className="asset-list-header">
          <span className="muted">
            Showing {deviceStart}-{deviceEnd} of {filteredDevices.length}
          </span>
          <div className="pagination-controls" aria-label="Device pagination">
            <button
              className="button button-secondary button-compact"
              disabled={visibleDevicePage <= 1}
              onClick={() => setDevicePage((page) => Math.max(1, page - 1))}
              type="button"
            >
              Previous
            </button>
            <span className="muted">Page {visibleDevicePage} of {deviceTotalPages}</span>
            <button
              className="button button-secondary button-compact"
              disabled={visibleDevicePage >= deviceTotalPages}
              onClick={() => setDevicePage((page) => Math.min(deviceTotalPages, page + 1))}
              type="button"
            >
              Next
            </button>
          </div>
        </div>
        <table className="data-table">
          <thead>
            <tr>
              <th>Identifier</th>
              <th>Label</th>
              <th>Protocol</th>
              <th>Provider</th>
              <th>Tags</th>
              <th>Asset</th>
              <th>Telemetry</th>
              <th>Created</th>
            </tr>
          </thead>
          <tbody>
            {pagedDevices.map((device) => (
              <Fragment key={device.id}>
                <tr>
                  <td>{device.identifier}{device.isSeeded && <span className="badge badge-demo badge-inline">Demo</span>}</td>
                  <td>
                    {device.label ?? '—'}
                    {(device.providerLongName || device.providerShortName) && (
                      <div className="muted">Provider: {device.providerLongName ?? device.providerShortName}</div>
                    )}
                  </td>
                  <td><span className="badge">{device.protocol || 'Unspecified'}</span></td>
                  <td>
                    <span className="badge">{device.provider || 'manual'}</span>
                    {device.integrationFeedName && <span className="badge badge-inline">{device.integrationFeedName}</span>}
                    {device.externalId && <div className="muted">{device.externalId}</div>}
                    {(device.providerHardwareModel || device.providerRole) && (
                      <div className="muted">{[device.providerHardwareModel, device.providerRole].filter(Boolean).join(' / ')}</div>
                    )}
                  </td>
                  <td>{device.tags ?? '—'}</td>
                  <td>{device.assetName ?? <span className="badge badge-warning">Unassigned</span>}</td>
                  <td><DeviceTelemetrySummary readings={readingsByDeviceId.get(device.id) ?? []} /></td>
                  <td>{new Date(device.createdAt).toLocaleString()}</td>
                </tr>
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
        {filteredDevices.length > devicePageSize && (
          <div className="asset-list-header asset-list-footer">
            <span className="muted">
              Showing {deviceStart}-{deviceEnd} of {filteredDevices.length}
            </span>
            <div className="pagination-controls" aria-label="Device pagination bottom">
              <button
                className="button button-secondary button-compact"
                disabled={visibleDevicePage <= 1}
                onClick={() => setDevicePage((page) => Math.max(1, page - 1))}
                type="button"
              >
                Previous
              </button>
              <span className="muted">Page {visibleDevicePage} of {deviceTotalPages}</span>
              <button
                className="button button-secondary button-compact"
                disabled={visibleDevicePage >= deviceTotalPages}
                onClick={() => setDevicePage((page) => Math.min(deviceTotalPages, page + 1))}
                type="button"
              >
                Next
              </button>
            </div>
          </div>
        )}
      </div>
      )}

      {isOperator && (
        <details className="quiet-disclosure">
          <summary>
            Admin device management
            <span className="badge">{deviceStart}-{deviceEnd} of {filteredDevices.length}</span>
          </summary>
          <div className="table-scroll">
            <table className="data-table admin-table">
              <thead>
                <tr>
                  <th>Device</th>
                  <th>Provider</th>
                  <th>Asset</th>
                  <th>Created</th>
                  <th>Manage</th>
                </tr>
              </thead>
              <tbody>
                {pagedDevices.map((device) => (
                  <Fragment key={`admin-${device.id}`}>
                    <tr>
                      <td>
                        <strong>{device.identifier}</strong>
                        <div className="muted">{device.label ?? providerDisplayName(device)}</div>
                      </td>
                      <td>
                        <span className="badge">{device.provider || 'manual'}</span>
                        {device.integrationFeedName && <span className="badge badge-inline">{device.integrationFeedName}</span>}
                      </td>
                      <td>{device.assetName ?? <span className="badge badge-warning">Unassigned</span>}</td>
                      <td>{new Date(device.createdAt).toLocaleString()}</td>
                      <td>
                        <div className="compact-actions">
                          <button className="button button-secondary button-compact" disabled={submitting} onClick={() => startEdit(device)} type="button">
                            Edit
                          </button>
                          <button
                            className="button button-danger button-compact"
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
                      <tr key={`admin-${device.id}-edit`}>
                        <td colSpan={5}>
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
                                  <option value="">Unassigned</option>
                                  {assets.map((a) => (
                                    <option key={a.id} value={a.id}>{a.name}</option>
                                  ))}
                                </select>
                              </label>
                            </div>
                            <div className="button-row">
                              <button className="button" disabled={submitting} onClick={() => void saveEdit()} type="button">
                                {submitting ? 'Saving…' : 'Save changes'}
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
              </tbody>
            </table>
          </div>
        </details>
      )}
    </div>
  )
}
