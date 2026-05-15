import { useEffect, useState } from 'react'
import { exportAuditEventsCsv, getAuditEvents, type AuditEvent, type AuditEventQuery, type AuditEventResult } from '../api/audit'

function formatDate(value: string) {
  return new Date(value).toLocaleString()
}

function formatJson(value?: string | null) {
  if (!value) return null
  try {
    return JSON.stringify(JSON.parse(value), null, 2)
  } catch {
    return value
  }
}

export default function AuditPage() {
  const [result, setResult] = useState<AuditEventResult | null>(null)
  const [filters, setFilters] = useState<AuditEventQuery>({ page: 1, pageSize: 50 })
  const [loading, setLoading] = useState(false)
  const [exporting, setExporting] = useState(false)
  const [error, setError] = useState<string | null>(null)

  async function load(nextFilters = filters) {
    try {
      setLoading(true)
      setError(null)
      setResult(await getAuditEvents(nextFilters))
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err))
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    void load()
  }, [])

  function updateFilters(next: AuditEventQuery) {
    const merged = { ...filters, ...next, page: next.page ?? 1 }
    setFilters(merged)
    void load(merged)
  }

  async function handleExport() {
    setExporting(true)
    setError(null)
    try {
      const blob = await exportAuditEventsCsv(filters)
      const url = URL.createObjectURL(blob)
      const link = document.createElement('a')
      link.href = url
      link.download = `audit-events-${new Date().toISOString().split('T')[0]}.csv`
      link.click()
      URL.revokeObjectURL(url)
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err))
    } finally {
      setExporting(false)
    }
  }

  const totalPages = result ? Math.max(1, Math.ceil(result.totalCount / result.pageSize)) : 1

  return (
    <div className="section ops-page">
      <div className="ops-header">
        <div className="ops-title">
          <h1>Audit</h1>
          <p>Review administrative and operational actions across the deployment</p>
        </div>
        <div className="ops-actions">
          <button className="button button-secondary" disabled={exporting} onClick={() => void handleExport()} type="button">
            {exporting ? 'Exporting...' : 'Export CSV'}
          </button>
        </div>
      </div>

      <div className="card control-bar reports-control-bar">
        <label className="field">
          <span>Action</span>
          <select value={filters.action ?? ''} onChange={(event) => updateFilters({ action: event.target.value || undefined })}>
            <option value="">All actions</option>
            <option value="alert_route.created">Alert route created</option>
            <option value="alert_route.updated">Alert route updated</option>
            <option value="alert_route.deleted">Alert route deleted</option>
            <option value="system.seed">System seed</option>
            <option value="maintenance.clean_null_island">Null island cleanup</option>
            <option value="maintenance.clean_auto_provider_assets">Provider asset cleanup</option>
            <option value="maintenance.clean_e2e_data">E2E cleanup</option>
          </select>
        </label>
        <label className="field">
          <span>Entity</span>
          <select value={filters.entityType ?? ''} onChange={(event) => updateFilters({ entityType: event.target.value || undefined })}>
            <option value="">All entities</option>
            <option value="alert_route">Alert routes</option>
            <option value="system">System</option>
            <option value="system_maintenance">Maintenance</option>
          </select>
        </label>
        <label className="field">
          <span>Actor</span>
          <input value={filters.actor ?? ''} onChange={(event) => setFilters((current) => ({ ...current, actor: event.target.value || undefined }))} />
        </label>
        <div className="compact-actions">
          <button className="button button-primary" disabled={loading} onClick={() => void load({ ...filters, page: 1 })} type="button">
            {loading ? 'Loading...' : 'Search'}
          </button>
        </div>
      </div>

      {error && <div className="notice notice-danger">{error}</div>}

      <div className="card table-card">
        <div className="asset-list-header">
          <p className="muted">{result ? `${result.totalCount} audit events` : 'Loading audit events'}</p>
          <span className="badge">Admin</span>
        </div>
        <table className="data-table">
          <thead>
            <tr>
              <th>Time</th>
              <th>Actor</th>
              <th>Action</th>
              <th>Entity</th>
              <th>Summary</th>
            </tr>
          </thead>
          <tbody>
            {(result?.items ?? []).map((event: AuditEvent) => (
              <tr key={event.id}>
                <td>{formatDate(event.occurredAt)}</td>
                <td>{event.actorName}<br /><span className="muted">{event.actorRole}</span></td>
                <td><span className="badge">{event.action}</span></td>
                <td>{event.entityName ?? event.entityId ?? event.entityType}<br /><span className="muted">{event.entityType}</span></td>
                <td>
                  {event.summary ?? '-'}
                  {event.metadataJson && (
                    <details className="quiet-disclosure">
                      <summary>Metadata</summary>
                      <pre className="bridge-raw-payload">{formatJson(event.metadataJson)}</pre>
                    </details>
                  )}
                </td>
              </tr>
            ))}
            {result?.items.length === 0 && (
              <tr>
                <td className="muted" colSpan={5}>No audit events match the current filters.</td>
              </tr>
            )}
          </tbody>
        </table>
        <div className="pagination-controls">
          <button className="button button-secondary" disabled={!result || result.page <= 1 || loading} onClick={() => updateFilters({ page: Math.max(1, (result?.page ?? 1) - 1) })} type="button">Previous</button>
          <span className="muted">Page {result?.page ?? 1} of {totalPages}</span>
          <button className="button button-secondary" disabled={!result || result.page >= totalPages || loading} onClick={() => updateFilters({ page: (result?.page ?? 1) + 1 })} type="button">Next</button>
        </div>
      </div>
    </div>
  )
}
