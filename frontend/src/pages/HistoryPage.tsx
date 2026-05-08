import { useEffect, useState } from 'react'
import { getAssets, type Asset } from '../api/assets'
import {
  getObservationHistory,
  exportObservationsCsv,
  type Observation,
  type ObservationHistoryParams,
  type PagedResult,
} from '../api/observations'
import { useLiveDataRefresh } from '../hooks/useLiveDataRefresh'

export default function HistoryPage() {
  const [assets, setAssets] = useState<Asset[]>([])
  const [devices, setDevices] = useState<{ id: string; identifier: string; name?: string }[]>([])
  const [result, setResult] = useState<PagedResult<Observation> | null>(null)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [exporting, setExporting] = useState(false)

  const [filters, setFilters] = useState<ObservationHistoryParams>({
    page: 1,
    pageSize: 50,
  })

  async function loadAssets() {
    try {
      const assetList = await getAssets()
      setAssets(assetList)

      // Extract unique devices from assets
      const deviceMap = new Map<string, { id: string; identifier: string; name?: string }>()
      assetList.forEach((asset) => {
        asset.devices.forEach((device) => {
          if (!deviceMap.has(device.id)) {
            deviceMap.set(device.id, {
              id: device.id,
              identifier: device.identifier,
              name: device.label || device.identifier,
            })
          }
        })
      })
      setDevices(Array.from(deviceMap.values()).sort((a, b) => a.identifier.localeCompare(b.identifier)))
    } catch (e: unknown) {
      setError(e instanceof Error ? e.message : String(e))
    }
  }

  async function handleSearch(searchFilters = filters) {
    try {
      setError(null)
      setLoading(true)
      
      // datetime-local values represent the operator's local time.
      const convertToUTC = (dateTimeLocal: string | undefined): string | undefined => {
        if (!dateTimeLocal) return undefined
        return new Date(dateTimeLocal).toISOString()
      }

      const data = await getObservationHistory({
        ...searchFilters,
        fromDate: convertToUTC(searchFilters.fromDate),
        toDate: convertToUTC(searchFilters.toDate),
      })
      setResult(data)
    } catch (e: unknown) {
      setError(e instanceof Error ? e.message : String(e))
      setResult(null)
    } finally {
      setLoading(false)
    }
  }

  async function handleExport() {
    if (filters.deviceId === undefined && filters.assetId === undefined && !filters.fromDate && !filters.toDate) {
      setError('Please set at least one filter before exporting')
      return
    }

    try {
      setError(null)
      setExporting(true)
      
      const convertToUTC = (dateTimeLocal: string | undefined): string | undefined => {
        if (!dateTimeLocal) return undefined
        return new Date(dateTimeLocal).toISOString()
      }
      
      const blob = await exportObservationsCsv({
        deviceId: filters.deviceId,
        assetId: filters.assetId,
        fromDate: convertToUTC(filters.fromDate),
        toDate: convertToUTC(filters.toDate),
        pageSize: 5000,
      })

      const url = URL.createObjectURL(blob)
      const link = document.createElement('a')
      link.href = url
      link.download = `observations-export-${new Date().toISOString().split('T')[0]}.csv`
      document.body.appendChild(link)
      link.click()
      document.body.removeChild(link)
      URL.revokeObjectURL(url)
    } catch (e: unknown) {
      setError(e instanceof Error ? e.message : String(e))
    } finally {
      setExporting(false)
    }
  }

  function handlePageChange(newPage: number) {
    const updatedFilters = { ...filters, page: newPage }
    setFilters(updatedFilters)
    handleSearch(updatedFilters)
  }

  useEffect(() => {
    void loadAssets()
  }, [])

  useLiveDataRefresh(async () => {
    await loadAssets()
    if (result !== null) {
      await handleSearch()
    }
  }, { eventTypes: ['data_changed', 'observation'], debounceMs: 1500 })

  const hasFilters =
    filters.deviceId || filters.assetId || filters.fromDate || filters.toDate

  return (
    <div className="section">
      <div className="page-header">
        <h1>Observation History</h1>
      </div>

      <div className="card">
        <h2>Filters</h2>
        <div className="field-grid">
          <div className="field">
            <span>Device</span>
            <select
              value={filters.deviceId || ''}
              onChange={(e) =>
                setFilters((prev) => ({
                  ...prev,
                  deviceId: e.target.value || undefined,
                }))
              }
            >
              <option value="">All Devices</option>
              {devices.map((device) => (
                <option key={device.id} value={device.id}>
                  {device.name}
                </option>
              ))}
            </select>
          </div>

          <div className="field">
            <span>Asset</span>
            <select
              value={filters.assetId || ''}
              onChange={(e) =>
                setFilters((prev) => ({
                  ...prev,
                  assetId: e.target.value || undefined,
                }))
              }
            >
              <option value="">All Assets</option>
              {assets.map((asset) => (
                <option key={asset.id} value={asset.id}>
                  {asset.name}
                </option>
              ))}
            </select>
          </div>

          <div className="field">
            <span>From Date</span>
            <input
              type="datetime-local"
              value={filters.fromDate || ''}
              onChange={(e) =>
                setFilters((prev) => ({
                  ...prev,
                  fromDate: e.target.value || undefined,
                }))
              }
            />
          </div>

          <div className="field">
            <span>To Date</span>
            <input
              type="datetime-local"
              value={filters.toDate || ''}
              onChange={(e) =>
                setFilters((prev) => ({
                  ...prev,
                  toDate: e.target.value || undefined,
                }))
              }
            />
          </div>
        </div>

        <div className="button-row">
          <button className="button button-primary" onClick={() => void handleSearch()} type="button" disabled={loading}>
            {loading ? 'Searching…' : 'Search'}
          </button>
          <button
            className="button"
            onClick={() => void handleExport()}
            type="button"
            disabled={!hasFilters || exporting}
          >
            {exporting ? 'Exporting…' : 'Export CSV'}
          </button>
        </div>

        {error && <div className="notice notice-danger">{error}</div>}
      </div>

      {result && (
        <div className="card table-card">
          <div>
            <p className="muted">
              Showing {result.items.length} of {result.totalCount} observations
            </p>
          </div>

          <table className="data-table">
            <thead>
              <tr>
                <th>Device</th>
                <th>Asset</th>
                <th>Observed At</th>
                <th>Latitude</th>
                <th>Longitude</th>
                <th>Speed (km/h)</th>
                <th>Heading (°)</th>
              </tr>
            </thead>
            <tbody>
              {result.items.map((obs) => (
                <tr key={obs.id}>
                  <td>{obs.deviceIdentifier}</td>
                  <td>{obs.assetName ?? '—'}</td>
                  <td>{new Date(obs.observedAt).toLocaleString()}</td>
                  <td className="coords">{obs.latitude.toFixed(6)}</td>
                  <td className="coords">{obs.longitude.toFixed(6)}</td>
                  <td>{obs.speedKmh?.toFixed(1) ?? '—'}</td>
                  <td>{obs.headingDegrees?.toFixed(1) ?? '—'}</td>
                </tr>
              ))}
              {result.items.length === 0 && (
                <tr>
                  <td className="muted" colSpan={7}>
                    No observations found.
                  </td>
                </tr>
              )}
            </tbody>
          </table>

          {result && result.pageSize > 0 && Math.ceil(result.totalCount / result.pageSize) > 1 && (
            <div className="table-actions">
              <button
                className="button button-secondary"
                onClick={() => handlePageChange(result.page - 1)}
                disabled={result.page <= 1}
                type="button"
              >
                Previous
              </button>
              <span className="muted">
                Page {result.page} of {Math.ceil(result.totalCount / result.pageSize)}
              </span>
              <button
                className="button button-secondary"
                onClick={() => handlePageChange(result.page + 1)}
                disabled={result.page >= Math.ceil(result.totalCount / result.pageSize)}
                type="button"
              >
                Next
              </button>
            </div>
          )}
        </div>
      )}
    </div>
  )
}
