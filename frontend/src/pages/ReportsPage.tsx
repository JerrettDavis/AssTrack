import { useEffect, useMemo, useState } from 'react'
import { getAssets, type Asset } from '../api/assets'
import { getUtilizationReport, type UtilizationReport, type UtilizationReportParams } from '../api/reports'
import { useLiveDataRefresh } from '../hooks/useLiveDataRefresh'

function toDateTimeLocal(value: Date) {
  const offsetMs = value.getTimezoneOffset() * 60 * 1000
  return new Date(value.getTime() - offsetMs).toISOString().slice(0, 16)
}

function formatDate(value?: string | null) {
  return value ? new Date(value).toLocaleString() : '-'
}

function formatHours(minutes: number) {
  return `${(minutes / 60).toFixed(1)} h`
}

export default function ReportsPage() {
  const defaultTo = useMemo(() => new Date(), [])
  const defaultFrom = useMemo(() => new Date(defaultTo.getTime() - 7 * 24 * 60 * 60 * 1000), [defaultTo])
  const [assets, setAssets] = useState<Asset[]>([])
  const [report, setReport] = useState<UtilizationReport | null>(null)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [filters, setFilters] = useState<UtilizationReportParams>({
    fromDate: toDateTimeLocal(defaultFrom),
    toDate: toDateTimeLocal(defaultTo),
  })

  const devices = useMemo(() => {
    const map = new Map<string, { id: string; label: string; assetId?: string | null }>()
    assets.forEach((asset) => {
      asset.devices.forEach((device) => {
        map.set(device.id, {
          id: device.id,
          label: device.label || device.identifier,
          assetId: device.assetId,
        })
      })
    })
    return Array.from(map.values()).sort((a, b) => a.label.localeCompare(b.label))
  }, [assets])

  async function loadAssets() {
    const assetList = await getAssets()
    setAssets(assetList)
  }

  async function loadReport(nextFilters = filters) {
    try {
      setError(null)
      setLoading(true)
      const toUtc = (value?: string) => value ? new Date(value).toISOString() : undefined
      const data = await getUtilizationReport({
        ...nextFilters,
        fromDate: toUtc(nextFilters.fromDate),
        toDate: toUtc(nextFilters.toDate),
      })
      setReport(data)
    } catch (e: unknown) {
      setError(e instanceof Error ? e.message : String(e))
      setReport(null)
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    void loadAssets()
    void loadReport()
  }, [])

  useLiveDataRefresh(async () => {
    await loadAssets()
    await loadReport()
  }, { eventTypes: ['data_changed', 'observation'], debounceMs: 2000 })

  return (
    <div className="section ops-page">
      <div className="ops-header">
        <div className="ops-title">
          <h1>Reports</h1>
          <p>Review utilization, movement, and stop activity across tracked assets</p>
        </div>
      </div>

      <div className="card control-bar reports-control-bar">
        <div className="field">
          <span>Asset</span>
          <select
            value={filters.assetId || ''}
            onChange={(e) => setFilters((prev) => ({ ...prev, assetId: e.target.value || undefined }))}
          >
            <option value="">All Assets</option>
            {assets.map((asset) => (
              <option key={asset.id} value={asset.id}>{asset.name}</option>
            ))}
          </select>
        </div>
        <div className="field">
          <span>Device</span>
          <select
            value={filters.deviceId || ''}
            onChange={(e) => setFilters((prev) => ({ ...prev, deviceId: e.target.value || undefined }))}
          >
            <option value="">All Devices</option>
            {devices.map((device) => (
              <option key={device.id} value={device.id}>{device.label}</option>
            ))}
          </select>
        </div>
        <div className="field">
          <span>From</span>
          <input
            type="datetime-local"
            value={filters.fromDate || ''}
            onChange={(e) => setFilters((prev) => ({ ...prev, fromDate: e.target.value || undefined }))}
          />
        </div>
        <div className="field">
          <span>To</span>
          <input
            type="datetime-local"
            value={filters.toDate || ''}
            onChange={(e) => setFilters((prev) => ({ ...prev, toDate: e.target.value || undefined }))}
          />
        </div>
        <div className="compact-actions">
          <button className="button button-primary" type="button" disabled={loading} onClick={() => void loadReport()}>
            {loading ? 'Running...' : 'Run Report'}
          </button>
        </div>
      </div>

      {error && <div className="notice notice-danger">{error}</div>}

      {report && (
        <>
          <div className="metrics kpi-strip">
            <div className="metric"><span>Distance</span><strong>{report.totalDistanceKm.toFixed(1)} km</strong></div>
            <div className="metric"><span>Moving time</span><strong>{formatHours(report.totalMovingMinutes)}</strong></div>
            <div className="metric"><span>Idle time</span><strong>{formatHours(report.totalIdleMinutes)}</strong></div>
            <div className="metric"><span>Assets</span><strong>{report.assetCount}</strong></div>
            <div className="metric"><span>Devices</span><strong>{report.deviceCount}</strong></div>
            <div className="metric"><span>Observations</span><strong>{report.observationCount}</strong></div>
          </div>

          <div className="card table-card">
            <div className="asset-list-header">
              <p className="muted">
                {formatDate(report.from)} to {formatDate(report.to)}
              </p>
              <span className="badge">Generated {formatDate(report.generatedAt)}</span>
            </div>
            <table className="data-table">
              <thead>
                <tr>
                  <th>Asset</th>
                  <th>Device</th>
                  <th>Distance</th>
                  <th>Moving</th>
                  <th>Idle</th>
                  <th>Stops</th>
                  <th>Max Speed</th>
                  <th>Last Seen</th>
                </tr>
              </thead>
              <tbody>
                {report.items.map((item) => (
                  <tr key={item.deviceId}>
                    <td>{item.assetName ?? '-'}</td>
                    <td>{item.deviceIdentifier}</td>
                    <td>{item.distanceKm.toFixed(1)} km</td>
                    <td>{formatHours(item.movingMinutes)}</td>
                    <td>{formatHours(item.idleMinutes)}</td>
                    <td>{item.stopCount}</td>
                    <td>{item.maxSpeedKmh != null ? `${item.maxSpeedKmh.toFixed(1)} km/h` : '-'}</td>
                    <td>{formatDate(item.lastObservedAt)}</td>
                  </tr>
                ))}
                {report.items.length === 0 && (
                  <tr>
                    <td className="muted" colSpan={8}>No movement data found for this report range.</td>
                  </tr>
                )}
              </tbody>
            </table>
          </div>
        </>
      )}
    </div>
  )
}
