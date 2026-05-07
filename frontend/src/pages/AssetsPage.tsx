import { FormEvent, useEffect, useMemo, useState } from 'react'
import { createAsset, deleteAsset, getAssetClasses, getAssets, type Asset, type AssetClass, type SensorReading, updateAsset, type UpdateAssetRequest } from '../api/assets'
import { completeMaintenanceSchedule, createMaintenanceSchedule, deleteMaintenanceSchedule, getMaintenanceSchedules, getMaintenanceServiceRecords, type MaintenanceSchedule, type MaintenanceServiceRecord, type MaintenanceStatus } from '../api/maintenance'
import { getLatestPositions, getObservations, type Observation } from '../api/observations'
import { getSensorReadings } from '../api/sensors'
import { useIdentityContext } from '../context/IdentityContext'

function formatTimestamp(value: string) {
  return new Date(value).toLocaleString()
}

function ageTimestamp(observedAt: string, receivedAt?: string | null): string {
  const observedMs = new Date(observedAt).getTime()
  if (Number.isFinite(observedMs) && observedMs <= Date.now()) return observedAt
  return receivedAt ?? observedAt
}

function formatRelativeTime(observedAt: string, receivedAt?: string | null) {
  const diffMs = Math.max(0, Date.now() - new Date(ageTimestamp(observedAt, receivedAt)).getTime())
  const minutes = Math.floor(diffMs / 60000)
  if (minutes < 1) return 'Just now'
  if (minutes < 60) return `${minutes}m ago`
  const hours = Math.floor(minutes / 60)
  if (hours < 24) return `${hours}h ago`
  return `${Math.floor(hours / 24)}d ago`
}

function getObservationStatus(observation: Observation | undefined) {
  if (!observation) return { label: 'No signal', className: 'badge-danger' }
  const ageMs = Math.max(0, Date.now() - new Date(ageTimestamp(observation.observedAt, observation.receivedAt)).getTime())
  if (ageMs > 30 * 60 * 1000) return { label: 'Stale', className: 'badge-danger' }
  if (ageMs > 5 * 60 * 1000) return { label: 'Aging', className: 'badge-warning' }
  if ((observation.speedKmh ?? 0) > 0) return { label: 'Moving', className: 'badge-success' }
  return { label: 'Online', className: 'badge-success' }
}

type AssetStatusFilter = 'all' | 'moving' | 'stale' | 'unassigned'

const criticalityOptions = [
  { value: 'low', label: 'Low' },
  { value: 'normal', label: 'Normal' },
  { value: 'high', label: 'High' },
  { value: 'critical', label: 'Critical' },
]

function classLabel(value: string, classes: AssetClass[]) {
  return classes.find((item) => item.id === value)?.name ?? value
}

function sensorLabel(reading: SensorReading) {
  const name = reading.name || reading.sensorType.replaceAll('_', ' ')
  const value = reading.numericValue != null
    ? `${reading.numericValue}${reading.unit ? ` ${reading.unit}` : ''}`
    : reading.textValue ?? 'N/A'
  return `${name}: ${value}`
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

function TelemetryPanel({ latestReadings, recentReadings }: { latestReadings: SensorReading[], recentReadings: SensorReading[] }) {
  const latest = latestBySensorType([...latestReadings, ...recentReadings])
  const numericSeries = recentReadings
    .filter((reading) => reading.numericValue != null)
    .sort((a, b) => new Date(a.observedAt).getTime() - new Date(b.observedAt).getTime())
    .slice(-12)
  const maxValue = Math.max(1, ...numericSeries.map((reading) => Math.abs(reading.numericValue ?? 0)))

  if (latest.length === 0) {
    return (
      <div className="telemetry-panel telemetry-empty">
        <div className="muted">No telemetry readings yet.</div>
      </div>
    )
  }

  return (
    <div className="telemetry-panel">
      <div className="telemetry-header">
        <strong>Telemetry</strong>
        <span className="muted">{latest.length} sensors</span>
      </div>
      <div className="telemetry-grid">
        {latest.slice(0, 6).map((reading) => {
          const status = getSensorStatus(reading)
          return (
            <div className="telemetry-reading" key={`${reading.sensorType}-${reading.id}`}>
              <span>{sensorName(reading)}</span>
              <strong>{sensorValue(reading)}</strong>
              <span className={`badge ${status.className}`}>{status.label}</span>
            </div>
          )
        })}
      </div>
      {numericSeries.length > 1 && (
        <div className="telemetry-chart" aria-label="Recent numeric telemetry">
          {numericSeries.map((reading) => (
            <span
              key={reading.id}
              title={`${sensorName(reading)} ${sensorValue(reading)} at ${formatTimestamp(reading.observedAt)}`}
              style={{ height: `${Math.max(12, Math.round((Math.abs(reading.numericValue ?? 0) / maxValue) * 100))}%` }}
            />
          ))}
        </div>
      )}
    </div>
  )
}

function maintenanceStatusClass(status: MaintenanceStatus) {
  if (status === 'overdue') return 'badge-danger'
  if (status === 'due' || status === 'upcoming') return 'badge-warning'
  return 'badge-success'
}

function formatNumber(value?: number | null) {
  if (value == null) return null
  return Number.isInteger(value) ? value.toString() : value.toFixed(1)
}

function maintenanceDueText(schedule: MaintenanceSchedule) {
  const parts = [
    schedule.nextDueAt ? `date ${formatTimestamp(schedule.nextDueAt)}` : null,
    schedule.nextOdometerKm != null ? `odometer ${formatNumber(schedule.latestOdometerKm) ?? 'N/A'} / ${formatNumber(schedule.nextOdometerKm)} km` : null,
    schedule.nextRuntimeHours != null ? `runtime ${formatNumber(schedule.latestRuntimeHours) ?? 'N/A'} / ${formatNumber(schedule.nextRuntimeHours)} h` : null,
  ].filter(Boolean)
  return parts.length > 0 ? parts.join(' | ') : 'No due target'
}

function MaintenancePanel({
  records,
  schedules,
  onComplete,
  onDelete,
  submitting,
  isOperator,
}: {
  records: MaintenanceServiceRecord[]
  schedules: MaintenanceSchedule[]
  onComplete: (schedule: MaintenanceSchedule) => void
  onDelete: (schedule: MaintenanceSchedule) => void
  submitting: boolean
  isOperator: boolean
}) {
  if (schedules.length === 0) {
    return (
      <div className="maintenance-panel maintenance-empty">
        <span className="muted">No maintenance schedules.</span>
        {records.length > 0 && (
          <span className="muted">Last service: {records[0].scheduleTitle} on {formatTimestamp(records[0].completedAt)}</span>
        )}
      </div>
    )
  }

  return (
    <div className="maintenance-panel">
      <div className="maintenance-header">
        <strong>Maintenance</strong>
        <span className="muted">{schedules.length} schedules</span>
      </div>
      <div className="maintenance-list">
        {schedules.map((schedule) => (
          <div className="maintenance-row" key={schedule.id}>
            <div>
              <strong>{schedule.title}</strong>
              <span className="muted">{maintenanceDueText(schedule)}</span>
            </div>
            <span className={`badge ${maintenanceStatusClass(schedule.status)}`}>{schedule.status}</span>
            {isOperator && (
              <div className="compact-actions">
                <button className="button button-secondary button-compact" disabled={submitting} onClick={() => onComplete(schedule)} type="button">
                  Complete
                </button>
                <button className="button button-secondary button-compact" disabled={submitting} onClick={() => onDelete(schedule)} type="button">
                  Remove
                </button>
              </div>
            )}
          </div>
        ))}
      </div>
      {records.length > 0 && (
        <div className="maintenance-records">
          <strong>Recent service</strong>
          {records.slice(0, 3).map((record) => (
            <div className="maintenance-record-row" key={record.id}>
              <span>{record.notes ? `${record.scheduleTitle}: ${record.notes}` : record.scheduleTitle}</span>
              <span className="muted">{formatTimestamp(record.completedAt)}</span>
            </div>
          ))}
        </div>
      )}
    </div>
  )
}

export function AssetsPage() {
  const [assets, setAssets] = useState<Asset[]>([])
  const [assetClasses, setAssetClasses] = useState<AssetClass[]>([])
  const [observations, setObservations] = useState<Observation[]>([])
  const [latestPositions, setLatestPositions] = useState<Observation[]>([])
  const [sensorReadings, setSensorReadings] = useState<SensorReading[]>([])
  const [maintenanceSchedules, setMaintenanceSchedules] = useState<MaintenanceSchedule[]>([])
  const [maintenanceRecords, setMaintenanceRecords] = useState<MaintenanceServiceRecord[]>([])
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(true)
  const [showAddForm, setShowAddForm] = useState(false)
  const [name, setName] = useState('')
  const [description, setDescription] = useState('')
  const [category, setCategory] = useState('')
  const [assetClass, setAssetClass] = useState('property')
  const [criticality, setCriticality] = useState('normal')
  const [speedThresholdKmh, setSpeedThresholdKmh] = useState<string>('')
  const [searchTerm, setSearchTerm] = useState('')
  const [statusFilter, setStatusFilter] = useState<AssetStatusFilter>('all')
  const [submitting, setSubmitting] = useState(false)
  const [editingId, setEditingId] = useState<string | null>(null)
  const [editForm, setEditForm] = useState<UpdateAssetRequest>({ name: '', description: null, assetClass: 'property', category: null, criticality: 'normal', speedThresholdKmh: null })
  const [maintenanceForm, setMaintenanceForm] = useState({ assetId: '', title: '', serviceType: 'general', intervalDays: '', intervalOdometerKm: '', intervalRuntimeHours: '', lastServiceAt: '', lastOdometerKm: '', lastRuntimeHours: '' })
  const { isOperator } = useIdentityContext()

  async function load() {
    try {
      setError(null)
      const [assetItems, classItems, observationItems, latestPositionItems, sensorItems, maintenanceItems, maintenanceRecordItems] = await Promise.all([
        getAssets(),
        getAssetClasses(),
        getObservations(),
        getLatestPositions(),
        getSensorReadings({ limit: 500 }),
        getMaintenanceSchedules(),
        getMaintenanceServiceRecords({ limit: 200 }),
      ])
      setAssets(assetItems)
      setAssetClasses(classItems)
      setObservations(observationItems)
      setLatestPositions(latestPositionItems)
      setSensorReadings(sensorItems)
      setMaintenanceSchedules(maintenanceItems)
      setMaintenanceRecords(maintenanceRecordItems)
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
        assetClass,
        category: category.trim() || undefined,
        criticality,
        speedThresholdKmh: speedValue,
      })
      setName('')
      setDescription('')
      setCategory('')
      setAssetClass('property')
      setCriticality('normal')
      setSpeedThresholdKmh('')
      setShowAddForm(false)
      await load()
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Unable to create asset.')
    } finally {
      setSubmitting(false)
    }
  }

  async function handleDeleteAsset(assetId: string, assetName: string) {
    if (!window.confirm(`Delete asset "${assetName}"? This cannot be undone.`)) return
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

  function startEdit(asset: Asset) {
    setEditingId(asset.id)
    setEditForm({
      name: asset.name,
      description: asset.description ?? null,
      assetClass: asset.assetClass,
      category: asset.category ?? null,
      criticality: asset.criticality,
      speedThresholdKmh: asset.speedThresholdKmh ?? null,
    })
  }

  async function saveEdit() {
    if (!editingId) return
    setSubmitting(true)
    try {
      await updateAsset(editingId, editForm)
      setEditingId(null)
      await load()
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Unable to update asset.')
    } finally {
      setSubmitting(false)
    }
  }

  async function handleCreateMaintenance(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setSubmitting(true)
    try {
      await createMaintenanceSchedule({
        assetId: maintenanceForm.assetId,
        title: maintenanceForm.title.trim(),
        serviceType: maintenanceForm.serviceType,
        intervalDays: maintenanceForm.intervalDays ? parseInt(maintenanceForm.intervalDays, 10) : null,
        intervalOdometerKm: maintenanceForm.intervalOdometerKm ? parseFloat(maintenanceForm.intervalOdometerKm) : null,
        intervalRuntimeHours: maintenanceForm.intervalRuntimeHours ? parseFloat(maintenanceForm.intervalRuntimeHours) : null,
        lastServiceAt: maintenanceForm.lastServiceAt ? new Date(maintenanceForm.lastServiceAt).toISOString() : null,
        lastOdometerKm: maintenanceForm.lastOdometerKm ? parseFloat(maintenanceForm.lastOdometerKm) : null,
        lastRuntimeHours: maintenanceForm.lastRuntimeHours ? parseFloat(maintenanceForm.lastRuntimeHours) : null,
      })
      setMaintenanceForm({ assetId: '', title: '', serviceType: 'general', intervalDays: '', intervalOdometerKm: '', intervalRuntimeHours: '', lastServiceAt: '', lastOdometerKm: '', lastRuntimeHours: '' })
      await load()
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Unable to create maintenance schedule.')
    } finally {
      setSubmitting(false)
    }
  }

  async function handleDeleteMaintenance(schedule: MaintenanceSchedule) {
    if (!window.confirm(`Remove maintenance schedule "${schedule.title}"?`)) return
    setSubmitting(true)
    try {
      await deleteMaintenanceSchedule(schedule.id)
      await load()
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Unable to remove maintenance schedule.')
    } finally {
      setSubmitting(false)
    }
  }

  async function handleCompleteMaintenance(schedule: MaintenanceSchedule) {
    const notes = window.prompt(`Complete "${schedule.title}"? Optional service notes:`)
    if (notes === null) return
    setSubmitting(true)
    try {
      await completeMaintenanceSchedule(schedule.id, {
        completedAt: new Date().toISOString(),
        odometerKm: schedule.latestOdometerKm ?? null,
        runtimeHours: schedule.latestRuntimeHours ?? null,
        notes: notes.trim() || null,
      })
      await load()
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Unable to complete maintenance schedule.')
    } finally {
      setSubmitting(false)
    }
  }

  const metrics = useMemo(() => {
    const deviceCount = assets.reduce((total, asset) => total + asset.devices.length, 0)
    const movingCount = latestPositions.filter((p) => (p.speedKmh ?? 0) > 0).length
    const staleCount = assets.filter((asset) => {
      const latest = latestPositions.find((position) => asset.devices.some((device) => device.id === position.deviceId))
      return getObservationStatus(latest).label === 'Stale' || getObservationStatus(latest).label === 'No signal'
    }).length
    const staleSensorCount = sensorReadings.filter((reading) => getSensorStatus(reading).label === 'Stale').length
    const maintenanceAttentionCount = maintenanceSchedules.filter((schedule) => schedule.status === 'due' || schedule.status === 'overdue').length

    return [
      { label: 'Assets', value: assets.length },
      { label: 'Devices', value: deviceCount },
      { label: 'Observations', value: observations.length },
      { label: 'Sensor readings', value: sensorReadings.length },
      { label: 'Vehicles', value: assets.filter((asset) => asset.assetClass === 'vehicle').length },
      { label: 'Pets', value: assets.filter((asset) => asset.assetClass === 'pet').length },
      { label: 'Moving now', value: movingCount },
      { label: 'Needs attention', value: staleCount },
      { label: 'Stale sensors', value: staleSensorCount },
      { label: 'Maintenance due', value: maintenanceAttentionCount },
    ]
  }, [assets, observations, latestPositions, sensorReadings, maintenanceSchedules])

  const readingsByAssetId = useMemo(() => {
    const grouped = new Map<string, SensorReading[]>()
    sensorReadings.forEach((reading) => {
      if (!reading.assetId) return
      grouped.set(reading.assetId, [...(grouped.get(reading.assetId) ?? []), reading])
    })
    return grouped
  }, [sensorReadings])

  const maintenanceByAssetId = useMemo(() => {
    const grouped = new Map<string, MaintenanceSchedule[]>()
    maintenanceSchedules.forEach((schedule) => {
      grouped.set(schedule.assetId, [...(grouped.get(schedule.assetId) ?? []), schedule])
    })
    return grouped
  }, [maintenanceSchedules])

  const maintenanceRecordsByAssetId = useMemo(() => {
    const grouped = new Map<string, MaintenanceServiceRecord[]>()
    maintenanceRecords.forEach((record) => {
      grouped.set(record.assetId, [...(grouped.get(record.assetId) ?? []), record])
    })
    return grouped
  }, [maintenanceRecords])

  const filteredAssets = useMemo(() => {
    const normalizedSearch = searchTerm.trim().toLowerCase()

    return assets.filter((asset) => {
      const latest = latestPositions.find((position) => asset.devices.some((device) => device.id === position.deviceId))
      const status = getObservationStatus(latest).label
      const searchMatches =
        normalizedSearch.length === 0 ||
        asset.name.toLowerCase().includes(normalizedSearch) ||
        asset.assetClass.toLowerCase().includes(normalizedSearch) ||
        asset.criticality.toLowerCase().includes(normalizedSearch) ||
        (asset.description ?? '').toLowerCase().includes(normalizedSearch) ||
        (asset.category ?? '').toLowerCase().includes(normalizedSearch) ||
        asset.devices.some((device) =>
          device.identifier.toLowerCase().includes(normalizedSearch) ||
          (device.label ?? '').toLowerCase().includes(normalizedSearch),
        )

      const statusMatches =
        statusFilter === 'all' ||
        (statusFilter === 'moving' && status === 'Moving') ||
        (statusFilter === 'stale' && (status === 'Stale' || status === 'No signal' || status === 'Aging')) ||
        (statusFilter === 'unassigned' && asset.devices.length === 0)

      return searchMatches && statusMatches
    })
  }, [assets, latestPositions, searchTerm, statusFilter])

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
            {isOperator && (
              <button className="button button-secondary" onClick={() => setShowAddForm((value) => !value)} type="button">
                {showAddForm ? 'Cancel' : 'Add Asset'}
              </button>
            )}
          </div>
          <div className="card toolbar">
            <label className="field">
              <span>Search inventory</span>
              <input
                onChange={(event) => setSearchTerm(event.target.value)}
                placeholder="Asset, category, device"
                type="search"
                value={searchTerm}
              />
            </label>
            <label className="field">
              <span>Status</span>
              <select onChange={(event) => setStatusFilter(event.target.value as AssetStatusFilter)} value={statusFilter}>
                <option value="all">All assets</option>
                <option value="moving">Moving now</option>
                <option value="stale">Needs attention</option>
                <option value="unassigned">Unassigned</option>
              </select>
            </label>
            <div className="compact-actions">
              <span className="muted">{filteredAssets.length} shown</span>
            </div>
          </div>
          {showAddForm && isOperator && (
            <form className="card inline-form" onSubmit={handleCreateAsset}>
              <div className="field-grid">
                <label className="field">
                  <span>Name</span>
                  <input onChange={(event) => setName(event.target.value)} required value={name} />
                </label>
                <label className="field">
                  <span>Asset class</span>
                  <select onChange={(event) => setAssetClass(event.target.value)} value={assetClass}>
                    {assetClasses.map((item) => (
                      <option key={item.id} value={item.id}>{item.name}</option>
                    ))}
                  </select>
                </label>
                <label className="field">
                  <span>Criticality</span>
                  <select onChange={(event) => setCriticality(event.target.value)} value={criticality}>
                    {criticalityOptions.map((option) => (
                      <option key={option.value} value={option.value}>{option.label}</option>
                    ))}
                  </select>
                </label>
                <label className="field field-wide">
                  <span>Description</span>
                  <input onChange={(event) => setDescription(event.target.value)} value={description} />
                </label>
                <label className="field">
                  <span>Category</span>
                  <input onChange={(event) => setCategory(event.target.value)} placeholder="Trailer, service dog, generator" value={category} />
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
          {isOperator && assets.length > 0 && (
            <form className="card inline-form" onSubmit={handleCreateMaintenance}>
              <div className="page-header">
                <h2>Maintenance schedule</h2>
                <span className="muted">Date, odometer, and runtime intervals</span>
              </div>
              <div className="field-grid">
                <label className="field">
                  <span>Asset</span>
                  <select required onChange={(event) => setMaintenanceForm((f) => ({ ...f, assetId: event.target.value }))} value={maintenanceForm.assetId}>
                    <option value="">Select asset</option>
                    {assets.map((asset) => (
                      <option key={asset.id} value={asset.id}>{asset.name}</option>
                    ))}
                  </select>
                </label>
                <label className="field">
                  <span>Title</span>
                  <input onChange={(event) => setMaintenanceForm((f) => ({ ...f, title: event.target.value }))} placeholder="Oil service" required value={maintenanceForm.title} />
                </label>
                <label className="field">
                  <span>Type</span>
                  <select onChange={(event) => setMaintenanceForm((f) => ({ ...f, serviceType: event.target.value }))} value={maintenanceForm.serviceType}>
                    <option value="general">General</option>
                    <option value="oil">Oil</option>
                    <option value="inspection">Inspection</option>
                    <option value="tire">Tire</option>
                    <option value="battery">Battery</option>
                    <option value="calibration">Calibration</option>
                  </select>
                </label>
                <label className="field">
                  <span>Every days</span>
                  <input min={1} onChange={(event) => setMaintenanceForm((f) => ({ ...f, intervalDays: event.target.value }))} type="number" value={maintenanceForm.intervalDays} />
                </label>
                <label className="field">
                  <span>Every km</span>
                  <input min={0.001} onChange={(event) => setMaintenanceForm((f) => ({ ...f, intervalOdometerKm: event.target.value }))} type="number" value={maintenanceForm.intervalOdometerKm} />
                </label>
                <label className="field">
                  <span>Every runtime h</span>
                  <input min={0.001} onChange={(event) => setMaintenanceForm((f) => ({ ...f, intervalRuntimeHours: event.target.value }))} type="number" value={maintenanceForm.intervalRuntimeHours} />
                </label>
                <label className="field">
                  <span>Last service</span>
                  <input onChange={(event) => setMaintenanceForm((f) => ({ ...f, lastServiceAt: event.target.value }))} type="datetime-local" value={maintenanceForm.lastServiceAt} />
                </label>
                <label className="field">
                  <span>Last odometer km</span>
                  <input min={0} onChange={(event) => setMaintenanceForm((f) => ({ ...f, lastOdometerKm: event.target.value }))} type="number" value={maintenanceForm.lastOdometerKm} />
                </label>
                <label className="field">
                  <span>Last runtime h</span>
                  <input min={0} onChange={(event) => setMaintenanceForm((f) => ({ ...f, lastRuntimeHours: event.target.value }))} type="number" value={maintenanceForm.lastRuntimeHours} />
                </label>
              </div>
              <div className="button-row">
                <button className="button" disabled={submitting} type="submit">
                  {submitting ? 'Saving…' : 'Create maintenance schedule'}
                </button>
              </div>
            </form>
          )}
          {assets.length === 0 && (
            <div className="notice notice-info">
              <strong>No assets yet</strong>
              <span className="muted">Head to Settings → Demo Data to seed example fleet data and explore the UI.</span>
            </div>
          )}
          <div className="asset-grid">
            {filteredAssets.map((asset) => {
              const latest = latestPositions.find((position) => asset.devices.some((device) => device.id === position.deviceId))
              const status = getObservationStatus(latest)

              return (
              <article className="list-card" key={asset.id}>
                {editingId === asset.id ? (
                  <div className="inline-form">
                    <div className="field-grid">
                      <label className="field">
                        <span>Name</span>
                        <input onChange={(e) => setEditForm(f => ({ ...f, name: e.target.value }))} required value={editForm.name} />
                      </label>
                      <label className="field field-wide">
                        <span>Description</span>
                        <input onChange={(e) => setEditForm(f => ({ ...f, description: e.target.value || null }))} value={editForm.description ?? ''} />
                      </label>
                      <label className="field">
                        <span>Asset class</span>
                        <select onChange={(e) => setEditForm(f => ({ ...f, assetClass: e.target.value }))} value={editForm.assetClass ?? 'property'}>
                          {assetClasses.map((item) => (
                            <option key={item.id} value={item.id}>{item.name}</option>
                          ))}
                        </select>
                      </label>
                      <label className="field">
                        <span>Criticality</span>
                        <select onChange={(e) => setEditForm(f => ({ ...f, criticality: e.target.value }))} value={editForm.criticality ?? 'normal'}>
                          {criticalityOptions.map((option) => (
                            <option key={option.value} value={option.value}>{option.label}</option>
                          ))}
                        </select>
                      </label>
                      <label className="field">
                        <span>Category</span>
                        <input onChange={(e) => setEditForm(f => ({ ...f, category: e.target.value || null }))} value={editForm.category ?? ''} />
                      </label>
                      <label className="field">
                        <span>Speed Threshold (km/h)</span>
                        <input
                          min={0.001}
                          onChange={(e) => setEditForm(f => ({ ...f, speedThresholdKmh: e.target.value ? parseFloat(e.target.value) : null }))}
                          placeholder="Default 120 km/h"
                          type="number"
                          value={editForm.speedThresholdKmh ?? ''}
                        />
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
                ) : (
                  <>
                    <header>
                      <h3>{asset.name}</h3>
                      {asset.isSeeded && <span className="badge badge-demo">Demo</span>}
                      <span className="badge">{classLabel(asset.assetClass, assetClasses)}</span>
                      <span className={`badge ${asset.criticality === 'critical' || asset.criticality === 'high' ? 'badge-warning' : ''}`}>{asset.criticality}</span>
                      <span className="badge">{asset.category ?? 'Uncategorized'}</span>
                      <span className={`badge ${status.className}`}>{status.label}</span>
                    </header>
                    <p className="muted">{asset.description ?? 'No description provided.'}</p>
                    <div className="asset-meta">
                      <div className="asset-meta-row">
                        <span>Devices</span>
                        <strong>{asset.devices.length}</strong>
                      </div>
                      <div className="asset-meta-row">
                        <span>Speed threshold</span>
                        <strong>{asset.speedThresholdKmh != null ? `${asset.speedThresholdKmh} km/h` : 'Default'}</strong>
                      </div>
                      {asset.latestSensorReadings.slice(0, 4).map((reading) => (
                        <div className="asset-meta-row" key={reading.id}>
                          <span>{reading.sensorType.replaceAll('_', ' ')}</span>
                          <strong>{sensorLabel(reading).split(': ').slice(1).join(': ')}</strong>
                        </div>
                      ))}
                      <div className="asset-meta-row">
                        <span>Last signal</span>
                        <strong>{latest ? formatRelativeTime(latest.observedAt, latest.receivedAt) : 'Never'}</strong>
                      </div>
                      {latest && (
                        <div className="asset-meta-row">
                          <span>Position</span>
                          <strong className="coords">{latest.latitude.toFixed(4)}, {latest.longitude.toFixed(4)}</strong>
                        </div>
                      )}
                      <div className="asset-meta-row">
                        <span>Updated</span>
                        <strong>{formatTimestamp(asset.updatedAt)}</strong>
                      </div>
                    </div>
                    <TelemetryPanel latestReadings={asset.latestSensorReadings} recentReadings={readingsByAssetId.get(asset.id) ?? []} />
                    <MaintenancePanel
                      isOperator={isOperator}
                      onComplete={(schedule) => void handleCompleteMaintenance(schedule)}
                      onDelete={(schedule) => void handleDeleteMaintenance(schedule)}
                      records={maintenanceRecordsByAssetId.get(asset.id) ?? []}
                      schedules={maintenanceByAssetId.get(asset.id) ?? []}
                      submitting={submitting}
                    />
                    <div className="button-row">
                      {isOperator && (
                        <button className="button button-secondary" disabled={submitting} onClick={() => startEdit(asset)} type="button">
                          Edit
                        </button>
                      )}
                      {isOperator && (
                        <button
                          className="button button-danger"
                          disabled={submitting}
                          onClick={() => void handleDeleteAsset(asset.id, asset.name)}
                          type="button"
                        >
                          Delete
                        </button>
                      )}
                    </div>
                  </>
                )}
              </article>
            )})}
            {assets.length > 0 && filteredAssets.length === 0 && (
              <div className="card">No assets match the current filters.</div>
            )}
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
