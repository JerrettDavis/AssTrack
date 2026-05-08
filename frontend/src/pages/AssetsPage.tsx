import { FormEvent, useEffect, useMemo, useState } from 'react'
import { createAsset, deleteAsset, getAssetClasses, getAssets, type Asset, type AssetClass, type SensorReading, updateAsset, type UpdateAssetRequest } from '../api/assets'
import { createCustodyEvent, getCustodyEvents, type CustodyEvent } from '../api/custody'
import { completeMaintenanceSchedule, createMaintenanceSchedule, deleteMaintenanceSchedule, getMaintenanceReminders, getMaintenanceSchedules, getMaintenanceServiceRecords, type MaintenanceReminder, type MaintenanceSchedule, type MaintenanceServiceRecord, type MaintenanceStatus } from '../api/maintenance'
import { getLatestPositions, getObservations, type Observation } from '../api/observations'
import { getSensorReadings } from '../api/sensors'
import { useIdentityContext } from '../context/IdentityContext'
import { useLiveDataRefresh } from '../hooks/useLiveDataRefresh'

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
type AssetCardTab = 'overview' | 'telemetry' | 'custody' | 'maintenance'
type AssetSortMode = 'attention' | 'recent' | 'name' | 'devices'

const assetCardTabs: Array<{ value: AssetCardTab; label: string }> = [
  { value: 'overview', label: 'Overview' },
  { value: 'telemetry', label: 'Telemetry' },
  { value: 'custody', label: 'Custody' },
  { value: 'maintenance', label: 'Maintenance' },
]

const criticalityOptions = [
  { value: 'low', label: 'Low' },
  { value: 'normal', label: 'Normal' },
  { value: 'high', label: 'High' },
  { value: 'critical', label: 'Critical' },
]

const custodyStatusOptions = [
  { value: 'available', label: 'Available' },
  { value: 'checked_out', label: 'Checked out' },
  { value: 'in_transit', label: 'In transit' },
  { value: 'maintenance', label: 'Maintenance' },
  { value: 'missing', label: 'Missing' },
]

function classLabel(value: string, classes: AssetClass[]) {
  return classes.find((item) => item.id === value)?.name ?? value
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
    schedule.latestDiagnosticAt ? `diagnostic ${schedule.latestDiagnosticValue ?? schedule.diagnosticSensorType} at ${formatTimestamp(schedule.latestDiagnosticAt)}` : null,
  ].filter(Boolean)
  return parts.length > 0 ? parts.join(' | ') : 'No due target'
}

function reminderText(reminder: MaintenanceReminder) {
  if (reminder.diagnosticAt) return `${reminder.reason}: ${reminder.diagnosticValue ?? formatTimestamp(reminder.diagnosticAt)}`
  if (reminder.dueAt) return `${reminder.reason}: ${formatTimestamp(reminder.dueAt)}`
  return reminder.reason
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

function custodyLabel(value: string) {
  return custodyStatusOptions.find((option) => option.value === value)?.label ?? value.replaceAll('_', ' ')
}

function custodyStatusClass(status: string) {
  if (status === 'missing') return 'badge-danger'
  if (status === 'checked_out' || status === 'in_transit' || status === 'maintenance') return 'badge-warning'
  return 'badge-success'
}

function CustodyPanel({ asset, events }: { asset: Asset, events: CustodyEvent[] }) {
  return (
    <div className="custody-panel">
      <div className="custody-header">
        <strong>Custody</strong>
        <span className={`badge ${custodyStatusClass(asset.custodyStatus)}`}>{custodyLabel(asset.custodyStatus)}</span>
      </div>
      <div className="asset-meta">
        <div className="asset-meta-row">
          <span>Custodian</span>
          <strong>{asset.custodianName ?? 'Unassigned'}</strong>
        </div>
        {asset.custodianContact && (
          <div className="asset-meta-row">
            <span>Contact</span>
            <strong>{asset.custodianContact}</strong>
          </div>
        )}
        {asset.custodySince && (
          <div className="asset-meta-row">
            <span>Since</span>
            <strong>{formatTimestamp(asset.custodySince)}</strong>
          </div>
        )}
      </div>
      {events.length > 0 && (
        <div className="custody-events">
          <strong>Recent custody</strong>
          {events.slice(0, 3).map((event) => (
            <div className="custody-event-row" key={event.id}>
              <span>{event.toCustodianName ? `${event.eventType.replaceAll('_', ' ')}: ${event.toCustodianName}` : event.eventType.replaceAll('_', ' ')}</span>
              <span className="muted">{formatTimestamp(event.occurredAt)}</span>
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
  const [maintenanceReminders, setMaintenanceReminders] = useState<MaintenanceReminder[]>([])
  const [custodyEvents, setCustodyEvents] = useState<CustodyEvent[]>([])
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
  const [assetClassFilter, setAssetClassFilter] = useState('all')
  const [criticalityFilter, setCriticalityFilter] = useState('all')
  const [sortMode, setSortMode] = useState<AssetSortMode>('attention')
  const [assetPage, setAssetPage] = useState(1)
  const [assetPageSize, setAssetPageSize] = useState(24)
  const [activeAssetTabs, setActiveAssetTabs] = useState<Record<string, AssetCardTab>>({})
  const [submitting, setSubmitting] = useState(false)
  const [editingId, setEditingId] = useState<string | null>(null)
  const [editForm, setEditForm] = useState<UpdateAssetRequest>({ name: '', description: null, assetClass: 'property', category: null, criticality: 'normal', speedThresholdKmh: null })
  const [maintenanceForm, setMaintenanceForm] = useState({ assetId: '', title: '', serviceType: 'general', intervalDays: '', intervalOdometerKm: '', intervalRuntimeHours: '', diagnosticSensorType: '', diagnosticTextContains: '', lastServiceAt: '', lastOdometerKm: '', lastRuntimeHours: '' })
  const [custodyForm, setCustodyForm] = useState({ assetId: '', eventType: 'check_out', custodianName: '', custodianContact: '', custodyStatus: '', location: '', notes: '' })
  const { isOperator } = useIdentityContext()

  async function load() {
    try {
      setError(null)
      const [assetItems, classItems, observationItems, latestPositionItems, sensorItems, maintenanceItems, maintenanceRecordItems, maintenanceReminderItems, custodyEventItems] = await Promise.all([
        getAssets(),
        getAssetClasses(),
        getObservations(),
        getLatestPositions(),
        getSensorReadings({ limit: 500 }),
        getMaintenanceSchedules(),
        getMaintenanceServiceRecords({ limit: 200 }),
        getMaintenanceReminders(),
        getCustodyEvents({ limit: 200 }),
      ])
      setAssets(assetItems)
      setAssetClasses(classItems)
      setObservations(observationItems)
      setLatestPositions(latestPositionItems)
      setSensorReadings(sensorItems)
      setMaintenanceSchedules(maintenanceItems)
      setMaintenanceRecords(maintenanceRecordItems)
      setMaintenanceReminders(maintenanceReminderItems)
      setCustodyEvents(custodyEventItems)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Unable to load API data.')
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    void load()
  }, [])

  useLiveDataRefresh(load, { eventTypes: ['data_changed', 'observation', 'speed_alert', 'geofence_breach'], debounceMs: 1200 })

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
      custodyStatus: asset.custodyStatus,
      custodianName: asset.custodianName ?? null,
      custodianContact: asset.custodianContact ?? null,
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
        diagnosticSensorType: maintenanceForm.diagnosticSensorType.trim() || null,
        diagnosticTextContains: maintenanceForm.diagnosticTextContains.trim() || null,
        lastServiceAt: maintenanceForm.lastServiceAt ? new Date(maintenanceForm.lastServiceAt).toISOString() : null,
        lastOdometerKm: maintenanceForm.lastOdometerKm ? parseFloat(maintenanceForm.lastOdometerKm) : null,
        lastRuntimeHours: maintenanceForm.lastRuntimeHours ? parseFloat(maintenanceForm.lastRuntimeHours) : null,
      })
      setMaintenanceForm({ assetId: '', title: '', serviceType: 'general', intervalDays: '', intervalOdometerKm: '', intervalRuntimeHours: '', diagnosticSensorType: '', diagnosticTextContains: '', lastServiceAt: '', lastOdometerKm: '', lastRuntimeHours: '' })
      await load()
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Unable to create maintenance schedule.')
    } finally {
      setSubmitting(false)
    }
  }

  async function handleCreateCustodyEvent(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setSubmitting(true)
    try {
      await createCustodyEvent({
        assetId: custodyForm.assetId,
        eventType: custodyForm.eventType,
        toCustodianName: custodyForm.custodianName.trim() || null,
        toCustodianContact: custodyForm.custodianContact.trim() || null,
        custodyStatus: custodyForm.custodyStatus || null,
        location: custodyForm.location.trim() || null,
        notes: custodyForm.notes.trim() || null,
        occurredAt: new Date().toISOString(),
      })
      setCustodyForm({ assetId: '', eventType: 'check_out', custodianName: '', custodianContact: '', custodyStatus: '', location: '', notes: '' })
      await load()
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Unable to record custody event.')
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
      setActiveAssetTabs((current) => ({ ...current, [schedule.assetId]: 'maintenance' }))
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
    const custodyAttentionCount = assets.filter((asset) => asset.custodyStatus === 'missing' || asset.custodyStatus === 'in_transit').length

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
      { label: 'Custody attention', value: custodyAttentionCount },
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

  const custodyEventsByAssetId = useMemo(() => {
    const grouped = new Map<string, CustodyEvent[]>()
    custodyEvents.forEach((event) => {
      grouped.set(event.assetId, [...(grouped.get(event.assetId) ?? []), event])
    })
    return grouped
  }, [custodyEvents])

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

      const classMatches = assetClassFilter === 'all' || asset.assetClass === assetClassFilter
      const criticalityMatches = criticalityFilter === 'all' || asset.criticality === criticalityFilter

      return searchMatches && statusMatches && classMatches && criticalityMatches
    }).sort((a, b) => {
      const latestA = latestPositions.find((position) => a.devices.some((device) => device.id === position.deviceId))
      const latestB = latestPositions.find((position) => b.devices.some((device) => device.id === position.deviceId))
      if (sortMode === 'name') return a.name.localeCompare(b.name)
      if (sortMode === 'devices') return b.devices.length - a.devices.length || a.name.localeCompare(b.name)
      if (sortMode === 'recent') {
        const aTime = latestA ? new Date(ageTimestamp(latestA.observedAt, latestA.receivedAt)).getTime() : 0
        const bTime = latestB ? new Date(ageTimestamp(latestB.observedAt, latestB.receivedAt)).getTime() : 0
        return bTime - aTime || a.name.localeCompare(b.name)
      }

      const attentionScore = (asset: Asset, latest: Observation | undefined) => {
        const status = getObservationStatus(latest).label
        const maintenanceCount = (maintenanceByAssetId.get(asset.id) ?? []).filter((schedule) => schedule.status === 'due' || schedule.status === 'overdue').length
        const recentServiceCount = (maintenanceRecordsByAssetId.get(asset.id) ?? []).filter((record) => Date.now() - new Date(record.completedAt).getTime() < 60 * 60 * 1000).length
        const custodyScore = asset.custodyStatus === 'missing' ? 4 : asset.custodyStatus === 'in_transit' ? 2 : 0
        const signalScore = status === 'No signal' || status === 'Stale' ? 3 : status === 'Aging' ? 1 : 0
        const criticalityScore = asset.criticality === 'critical' ? 2 : asset.criticality === 'high' ? 1 : 0
        return maintenanceCount * 5 + recentServiceCount * 4 + custodyScore + signalScore + criticalityScore
      }

      return attentionScore(b, latestB) - attentionScore(a, latestA) || a.name.localeCompare(b.name)
    })
  }, [assets, latestPositions, searchTerm, statusFilter, assetClassFilter, criticalityFilter, sortMode, maintenanceByAssetId, maintenanceRecordsByAssetId])

  const assetTotalPages = Math.max(1, Math.ceil(filteredAssets.length / assetPageSize))
  const visibleAssetPage = Math.min(assetPage, assetTotalPages)
  const pagedAssets = useMemo(() => {
    const start = (visibleAssetPage - 1) * assetPageSize
    return filteredAssets.slice(start, start + assetPageSize)
  }, [assetPageSize, filteredAssets, visibleAssetPage])
  const assetStart = filteredAssets.length === 0 ? 0 : (visibleAssetPage - 1) * assetPageSize + 1
  const assetEnd = Math.min(filteredAssets.length, visibleAssetPage * assetPageSize)

  useEffect(() => {
    setAssetPage(1)
  }, [searchTerm, statusFilter, assetClassFilter, criticalityFilter, sortMode, assetPageSize])

  useEffect(() => {
    setAssetPage((current) => Math.min(current, assetTotalPages))
  }, [assetTotalPages])

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
          {maintenanceReminders.length > 0 && (
            <div className="reminder-strip">
              {maintenanceReminders.slice(0, 4).map((reminder) => (
                <div className="reminder-item" key={reminder.scheduleId}>
                  <span className={`badge ${maintenanceStatusClass(reminder.status)}`}>{reminder.status}</span>
                  <strong>{reminder.assetName ?? 'Asset'}: {reminder.title}</strong>
                  <span className="muted">{reminderText(reminder)}</span>
                </div>
              ))}
            </div>
          )}
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
            <label className="field">
              <span>Sort</span>
              <select onChange={(event) => setSortMode(event.target.value as AssetSortMode)} value={sortMode}>
                <option value="attention">Attention first</option>
                <option value="recent">Most recent signal</option>
                <option value="name">Name</option>
                <option value="devices">Most devices</option>
              </select>
            </label>
            <label className="field compact-field">
              <span>Page size</span>
              <select onChange={(event) => setAssetPageSize(Number(event.target.value))} value={assetPageSize}>
                <option value={12}>12</option>
                <option value={24}>24</option>
                <option value={48}>48</option>
                <option value={96}>96</option>
              </select>
            </label>
            <div className="compact-actions">
              <span className="muted">{filteredAssets.length} matched</span>
              {(searchTerm || statusFilter !== 'all' || assetClassFilter !== 'all' || criticalityFilter !== 'all') && (
                <button
                  className="button button-secondary button-compact"
                  onClick={() => {
                    setSearchTerm('')
                    setStatusFilter('all')
                    setAssetClassFilter('all')
                    setCriticalityFilter('all')
                  }}
                  type="button"
                >
                  Clear
                </button>
              )}
            </div>
          </div>
          <details className="filter-disclosure">
            <summary>
              More filters
              {(assetClassFilter !== 'all' || criticalityFilter !== 'all') && <span className="badge">Active</span>}
            </summary>
            <div className="filter-grid">
              <label className="field">
                <span>Class</span>
                <select onChange={(event) => setAssetClassFilter(event.target.value)} value={assetClassFilter}>
                  <option value="all">All classes</option>
                  {assetClasses.map((item) => (
                    <option key={item.id} value={item.id}>{item.name}</option>
                  ))}
                </select>
              </label>
              <label className="field">
                <span>Criticality</span>
                <select onChange={(event) => setCriticalityFilter(event.target.value)} value={criticalityFilter}>
                  <option value="all">All levels</option>
                  {criticalityOptions.map((option) => (
                    <option key={option.value} value={option.value}>{option.label}</option>
                  ))}
                </select>
              </label>
            </div>
          </details>
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
            <details className="card disclosure-panel">
              <summary>
                <span>
                  <strong>Maintenance schedule</strong>
                  <small>Date, odometer, runtime, and diagnostic intervals</small>
                </span>
                <span className="badge">Advanced</span>
              </summary>
              <form className="inline-form" onSubmit={handleCreateMaintenance}>
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
                  <span>Diagnostic sensor</span>
                  <input onChange={(event) => setMaintenanceForm((f) => ({ ...f, diagnosticSensorType: event.target.value }))} placeholder="diagnostic_code" value={maintenanceForm.diagnosticSensorType} />
                </label>
                <label className="field">
                  <span>Diagnostic contains</span>
                  <input onChange={(event) => setMaintenanceForm((f) => ({ ...f, diagnosticTextContains: event.target.value }))} placeholder="P0420, fault, low pressure" value={maintenanceForm.diagnosticTextContains} />
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
            </details>
          )}
          {isOperator && assets.length > 0 && (
            <details className="card disclosure-panel">
              <summary>
                <span>
                  <strong>Custody event</strong>
                  <small>Checkout, check-in, transfer, and status history</small>
                </span>
                <span className="badge">Advanced</span>
              </summary>
              <form className="inline-form" onSubmit={handleCreateCustodyEvent}>
              <div className="field-grid">
                <label className="field">
                  <span>Asset</span>
                  <select required onChange={(event) => setCustodyForm((f) => ({ ...f, assetId: event.target.value }))} value={custodyForm.assetId}>
                    <option value="">Select asset</option>
                    {assets.map((asset) => (
                      <option key={asset.id} value={asset.id}>{asset.name}</option>
                    ))}
                  </select>
                </label>
                <label className="field">
                  <span>Event</span>
                  <select onChange={(event) => setCustodyForm((f) => ({ ...f, eventType: event.target.value }))} value={custodyForm.eventType}>
                    <option value="check_out">Check out</option>
                    <option value="check_in">Check in</option>
                    <option value="transfer">Transfer</option>
                    <option value="status_change">Status change</option>
                  </select>
                </label>
                <label className="field">
                  <span>Status override</span>
                  <select onChange={(event) => setCustodyForm((f) => ({ ...f, custodyStatus: event.target.value }))} value={custodyForm.custodyStatus}>
                    <option value="">Derived from event</option>
                    {custodyStatusOptions.map((option) => (
                      <option key={option.value} value={option.value}>{option.label}</option>
                    ))}
                  </select>
                </label>
                <label className="field">
                  <span>Custodian</span>
                  <input disabled={custodyForm.eventType === 'check_in'} onChange={(event) => setCustodyForm((f) => ({ ...f, custodianName: event.target.value }))} required={custodyForm.eventType === 'check_out' || custodyForm.eventType === 'transfer'} value={custodyForm.custodianName} />
                </label>
                <label className="field">
                  <span>Contact</span>
                  <input disabled={custodyForm.eventType === 'check_in'} onChange={(event) => setCustodyForm((f) => ({ ...f, custodianContact: event.target.value }))} value={custodyForm.custodianContact} />
                </label>
                <label className="field">
                  <span>Location</span>
                  <input onChange={(event) => setCustodyForm((f) => ({ ...f, location: event.target.value }))} value={custodyForm.location} />
                </label>
                <label className="field field-wide">
                  <span>Notes</span>
                  <input onChange={(event) => setCustodyForm((f) => ({ ...f, notes: event.target.value }))} value={custodyForm.notes} />
                </label>
              </div>
              <div className="button-row">
                <button className="button" disabled={submitting} type="submit">
                  {submitting ? 'Saving…' : 'Record custody event'}
                </button>
              </div>
              </form>
            </details>
          )}
          {assets.length === 0 && (
            <div className="notice notice-info">
              <strong>No assets yet</strong>
              <span className="muted">Head to Settings → Demo Data to seed example fleet data and explore the UI.</span>
            </div>
          )}
          <div className="asset-list-header">
            <span className="muted">
              Showing {assetStart}-{assetEnd} of {filteredAssets.length}
            </span>
            <div className="pagination-controls" aria-label="Asset pagination">
              <button className="button button-secondary button-compact" disabled={visibleAssetPage <= 1} onClick={() => setAssetPage(1)} type="button">First</button>
              <button className="button button-secondary button-compact" disabled={visibleAssetPage <= 1} onClick={() => setAssetPage((page) => Math.max(1, page - 1))} type="button">Previous</button>
              <span className="muted">Page {visibleAssetPage} of {assetTotalPages}</span>
              <button className="button button-secondary button-compact" disabled={visibleAssetPage >= assetTotalPages} onClick={() => setAssetPage((page) => Math.min(assetTotalPages, page + 1))} type="button">Next</button>
              <button className="button button-secondary button-compact" disabled={visibleAssetPage >= assetTotalPages} onClick={() => setAssetPage(assetTotalPages)} type="button">Last</button>
            </div>
          </div>
          <div className="asset-grid">
            {pagedAssets.map((asset) => {
              const latest = latestPositions.find((position) => asset.devices.some((device) => device.id === position.deviceId))
              const status = getObservationStatus(latest)
              const assetReadings = readingsByAssetId.get(asset.id) ?? []
              const assetMaintenance = maintenanceByAssetId.get(asset.id) ?? []
              const assetMaintenanceRecords = maintenanceRecordsByAssetId.get(asset.id) ?? []
              const assetCustodyEvents = custodyEventsByAssetId.get(asset.id) ?? []
              const dueMaintenance = assetMaintenance.filter((schedule) => schedule.status === 'due' || schedule.status === 'overdue').length
              const hasRecentMaintenanceRecord = assetMaintenanceRecords.some((record) => Date.now() - new Date(record.completedAt).getTime() < 60 * 60 * 1000)
              const activeTab = activeAssetTabs[asset.id] ?? (dueMaintenance > 0 || hasRecentMaintenanceRecord ? 'maintenance' : 'overview')

              return (
              <article className="list-card asset-card" key={asset.id}>
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
                        <span>Custody status</span>
                        <select onChange={(e) => setEditForm(f => ({ ...f, custodyStatus: e.target.value }))} value={editForm.custodyStatus ?? 'available'}>
                          {custodyStatusOptions.map((option) => (
                            <option key={option.value} value={option.value}>{option.label}</option>
                          ))}
                        </select>
                      </label>
                      <label className="field">
                        <span>Custodian</span>
                        <input onChange={(e) => setEditForm(f => ({ ...f, custodianName: e.target.value || null }))} value={editForm.custodianName ?? ''} />
                      </label>
                      <label className="field">
                        <span>Custodian contact</span>
                        <input onChange={(e) => setEditForm(f => ({ ...f, custodianContact: e.target.value || null }))} value={editForm.custodianContact ?? ''} />
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
                    <header className="asset-card-header">
                      <div className="asset-title-block">
                        <div className="asset-title-row">
                          <h3>{asset.name}</h3>
                          {asset.isSeeded && <span className="badge badge-demo">Demo</span>}
                          <span className={`badge ${status.className}`}>{status.label}</span>
                        </div>
                        <p className="muted">{asset.description ?? 'No description provided.'}</p>
                      </div>
                      <div className="asset-card-badges" aria-label="Asset attributes">
                        <span className="badge">{classLabel(asset.assetClass, assetClasses)}</span>
                        <span className={`badge ${asset.criticality === 'critical' || asset.criticality === 'high' ? 'badge-warning' : ''}`}>{asset.criticality}</span>
                        <span className="badge">{asset.category ?? 'Uncategorized'}</span>
                        <span className={`badge ${custodyStatusClass(asset.custodyStatus)}`}>{custodyLabel(asset.custodyStatus)}</span>
                      </div>
                    </header>
                    <div className="asset-card-tabs" role="tablist" aria-label={`${asset.name} sections`}>
                      {assetCardTabs.map((tab) => (
                        <button
                          aria-selected={activeTab === tab.value}
                          className={activeTab === tab.value ? 'active' : ''}
                          key={tab.value}
                          onClick={() => setActiveAssetTabs((current) => ({ ...current, [asset.id]: tab.value }))}
                          role="tab"
                          type="button"
                        >
                          {tab.label}
                        </button>
                      ))}
                    </div>
                    {activeTab === 'overview' && (
                      <div className="asset-tab-panel" role="tabpanel">
                        <div className="asset-summary-grid">
                          <div className="status-tile">
                            <span className="muted">Devices</span>
                            <strong>{asset.devices.length}</strong>
                          </div>
                          <div className="status-tile">
                            <span className="muted">Last signal</span>
                            <strong>{latest ? formatRelativeTime(latest.observedAt, latest.receivedAt) : 'Never'}</strong>
                          </div>
                          <div className="status-tile">
                            <span className="muted">Sensors</span>
                            <strong>{latestBySensorType([...asset.latestSensorReadings, ...assetReadings]).length}</strong>
                          </div>
                          <div className="status-tile">
                            <span className="muted">Maintenance due</span>
                            <strong>{dueMaintenance}</strong>
                          </div>
                        </div>
                        <div className="asset-meta">
                          <div className="asset-meta-row">
                            <span>Speed threshold</span>
                            <strong>{asset.speedThresholdKmh != null ? `${asset.speedThresholdKmh} km/h` : 'Default'}</strong>
                          </div>
                          <div className="asset-meta-row">
                            <span>Custody</span>
                            <strong>{custodyLabel(asset.custodyStatus)}</strong>
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
                      </div>
                    )}
                    {activeTab === 'telemetry' && (
                      <div className="asset-tab-panel" role="tabpanel">
                        <TelemetryPanel latestReadings={asset.latestSensorReadings} recentReadings={assetReadings} />
                      </div>
                    )}
                    {activeTab === 'custody' && (
                      <div className="asset-tab-panel" role="tabpanel">
                        <CustodyPanel asset={asset} events={assetCustodyEvents} />
                      </div>
                    )}
                    {activeTab === 'maintenance' && (
                      <div className="asset-tab-panel" role="tabpanel">
                        <MaintenancePanel
                          isOperator={isOperator}
                          onComplete={(schedule) => void handleCompleteMaintenance(schedule)}
                          onDelete={(schedule) => void handleDeleteMaintenance(schedule)}
                          records={assetMaintenanceRecords}
                          schedules={assetMaintenance}
                          submitting={submitting}
                        />
                      </div>
                    )}
                    <div className="asset-card-footer">
                      {latest && (
                        <div className="asset-meta-row">
                          <span>Current position</span>
                          <strong className="coords">{latest.latitude.toFixed(4)}, {latest.longitude.toFixed(4)}</strong>
                        </div>
                      )}
                      {isOperator && (
                        <div className="button-row">
                          <button className="button button-secondary" disabled={submitting} onClick={() => startEdit(asset)} type="button">
                            Edit
                          </button>
                          <button
                            className="button button-danger"
                            disabled={submitting}
                            onClick={() => void handleDeleteAsset(asset.id, asset.name)}
                            type="button"
                          >
                            Delete
                          </button>
                        </div>
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
          {filteredAssets.length > assetPageSize && (
            <div className="asset-list-header asset-list-footer">
              <span className="muted">
                Showing {assetStart}-{assetEnd} of {filteredAssets.length}
              </span>
              <div className="pagination-controls" aria-label="Asset pagination">
                <button className="button button-secondary button-compact" disabled={visibleAssetPage <= 1} onClick={() => setAssetPage((page) => Math.max(1, page - 1))} type="button">Previous</button>
                <span className="muted">Page {visibleAssetPage} of {assetTotalPages}</span>
                <button className="button button-secondary button-compact" disabled={visibleAssetPage >= assetTotalPages} onClick={() => setAssetPage((page) => Math.min(assetTotalPages, page + 1))} type="button">Next</button>
              </div>
            </div>
          )}
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
