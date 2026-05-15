import { FormEvent, useEffect, useState } from 'react'
import {
  acknowledgeIntegrationEvent,
  exportIntegrationEventsCsv,
  getIntegrationEvents,
  publishIntegrationEvent,
  resolveIntegrationEvent,
  type CreateIntegrationEventRequest,
  type IntegrationEvent,
  type IntegrationEventQuery,
  type IntegrationEventResult,
} from '../api/integrationEvents'
import { useIdentityContext } from '../context/IdentityContext'
import { useLiveDataRefresh } from '../hooks/useLiveDataRefresh'

type SignalForm = {
  source: string
  externalEventId: string
  eventType: string
  severity: string
  subjectType: string
  subjectId: string
  subjectName: string
  message: string
  payload: string
}

const initialForm: SignalForm = {
  source: 'enterprise-console',
  externalEventId: '',
  eventType: 'operator.test_signal',
  severity: 'info',
  subjectType: 'system',
  subjectId: 'manual-test',
  subjectName: 'Manual signal test',
  message: 'Enterprise integration signal test.',
  payload: '{\n  "source": "manual",\n  "test": true\n}',
}

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

function parsePayload(value: string): unknown {
  if (!value.trim()) return null
  return JSON.parse(value)
}

function buildRequest(form: SignalForm): CreateIntegrationEventRequest {
  return {
    source: form.source,
    externalEventId: form.externalEventId || null,
    eventType: form.eventType,
    severity: form.severity,
    subjectType: form.subjectType || null,
    subjectId: form.subjectId || null,
    subjectName: form.subjectName || null,
    message: form.message,
    payload: parsePayload(form.payload),
  }
}

export default function SignalsPage() {
  const { isOperator, loading: identityLoading } = useIdentityContext()
  const [result, setResult] = useState<IntegrationEventResult | null>(null)
  const [filters, setFilters] = useState<IntegrationEventQuery>({ page: 1, pageSize: 50 })
  const [form, setForm] = useState<SignalForm>(initialForm)
  const [loading, setLoading] = useState(false)
  const [publishing, setPublishing] = useState(false)
  const [exporting, setExporting] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [publishResult, setPublishResult] = useState<string | null>(null)
  const [resolutionNotes, setResolutionNotes] = useState<Record<string, string>>({})

  async function load(nextFilters = filters) {
    if (!isOperator) return
    try {
      setLoading(true)
      setError(null)
      setResult(await getIntegrationEvents(nextFilters))
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err))
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    if (!identityLoading && isOperator) void load()
  }, [identityLoading, isOperator])

  useLiveDataRefresh(
    async () => {
      await load()
    },
    { eventTypes: ['integration_event', 'data_changed'], debounceMs: 750, enabled: !identityLoading && isOperator },
  )

  function updateFilters(next: IntegrationEventQuery) {
    const merged = { ...filters, ...next, page: next.page ?? 1 }
    setFilters(merged)
    void load(merged)
  }

  async function handlePublish(event: FormEvent) {
    event.preventDefault()
    setPublishing(true)
    setPublishResult(null)
    setError(null)
    try {
      const created = await publishIntegrationEvent(buildRequest(form))
      setPublishResult(`Published ${created.eventType}`)
      setFilters((current) => ({ ...current, page: 1 }))
      await load({ ...filters, page: 1 })
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err))
    } finally {
      setPublishing(false)
    }
  }

  async function handleAcknowledge(signal: IntegrationEvent) {
    setError(null)
    try {
      await acknowledgeIntegrationEvent(signal.id)
      await load()
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err))
    }
  }

  async function handleResolve(signal: IntegrationEvent) {
    setError(null)
    try {
      await resolveIntegrationEvent(signal.id, resolutionNotes[signal.id])
      setResolutionNotes((current) => {
        const next = { ...current }
        delete next[signal.id]
        return next
      })
      await load()
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err))
    }
  }

  async function handleExport() {
    setExporting(true)
    setError(null)
    try {
      const blob = await exportIntegrationEventsCsv(filters)
      const url = URL.createObjectURL(blob)
      const link = document.createElement('a')
      link.href = url
      link.download = `integration-signals-${new Date().toISOString().split('T')[0]}.csv`
      link.click()
      URL.revokeObjectURL(url)
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err))
    } finally {
      setExporting(false)
    }
  }

  if (!isOperator && !identityLoading) {
    return (
      <div className="card">
        <h1>Signals</h1>
        <p className="muted">Enterprise signal management is only accessible to operator keys.</p>
      </div>
    )
  }

  if (identityLoading) return <div className="card">Loading signals...</div>

  const totalPages = result ? Math.max(1, Math.ceil(result.totalCount / result.pageSize)) : 1
  const criticalCount = result?.items.filter((item) => item.severity === 'critical').length ?? 0
  const warningCount = result?.items.filter((item) => item.severity === 'warning').length ?? 0
  const openCount = result?.items.filter((item) => item.status === 'open').length ?? 0

  return (
    <div className="section ops-page">
      <div className="ops-header">
        <div className="ops-title">
          <h1>Signals</h1>
          <p>Publish and inspect enterprise integration events across hooks, live streams, messaging routes, and audit</p>
        </div>
        <div className="ops-actions">
          <button className="button button-secondary" disabled={exporting} onClick={() => void handleExport()} type="button">
            {exporting ? 'Exporting...' : 'Export CSV'}
          </button>
          <button className="button button-secondary" disabled={loading} onClick={() => void load()} type="button">
            Refresh
          </button>
        </div>
      </div>

      <div className="metrics kpi-strip">
        <div className="metric"><span>Signals</span><strong>{result?.totalCount ?? 0}</strong></div>
        <div className="metric"><span>Critical</span><strong>{criticalCount}</strong></div>
        <div className="metric"><span>Warnings</span><strong>{warningCount}</strong></div>
        <div className="metric"><span>Open</span><strong>{openCount}</strong></div>
      </div>

      <form className="card inline-form" data-testid="signal-publish-form" onSubmit={(event) => void handlePublish(event)}>
        <div className="asset-list-header">
          <h2>Publish Signal</h2>
          <span className="badge">Operator</span>
        </div>
        <div className="field-grid">
          <label className="field">
            <span>Source</span>
            <input onChange={(event) => setForm((value) => ({ ...value, source: event.target.value }))} required value={form.source} />
          </label>
          <label className="field">
            <span>External event ID</span>
            <input onChange={(event) => setForm((value) => ({ ...value, externalEventId: event.target.value }))} value={form.externalEventId} />
          </label>
          <label className="field">
            <span>Event type</span>
            <input onChange={(event) => setForm((value) => ({ ...value, eventType: event.target.value }))} required value={form.eventType} />
          </label>
          <label className="field">
            <span>Severity</span>
            <select onChange={(event) => setForm((value) => ({ ...value, severity: event.target.value }))} value={form.severity}>
              <option value="info">Info</option>
              <option value="warning">Warning</option>
              <option value="critical">Critical</option>
            </select>
          </label>
          <label className="field">
            <span>Subject type</span>
            <input onChange={(event) => setForm((value) => ({ ...value, subjectType: event.target.value }))} value={form.subjectType} />
          </label>
          <label className="field">
            <span>Subject ID</span>
            <input onChange={(event) => setForm((value) => ({ ...value, subjectId: event.target.value }))} value={form.subjectId} />
          </label>
          <label className="field">
            <span>Subject name</span>
            <input onChange={(event) => setForm((value) => ({ ...value, subjectName: event.target.value }))} value={form.subjectName} />
          </label>
          <label className="field field-wide">
            <span>Message</span>
            <input onChange={(event) => setForm((value) => ({ ...value, message: event.target.value }))} required value={form.message} />
          </label>
          <label className="field field-wide">
            <span>Payload JSON</span>
            <textarea onChange={(event) => setForm((value) => ({ ...value, payload: event.target.value }))} rows={5} value={form.payload} />
          </label>
        </div>
        <div className="button-row">
          <button className="button button-primary" disabled={publishing} type="submit">
            {publishing ? 'Publishing...' : 'Publish Signal'}
          </button>
        </div>
        {publishResult && <div className="notice notice-success">{publishResult}</div>}
      </form>

      <div className="card control-bar reports-control-bar">
        <label className="field">
          <span>Source</span>
          <input value={filters.source ?? ''} onChange={(event) => setFilters((current) => ({ ...current, source: event.target.value || undefined }))} />
        </label>
        <label className="field">
          <span>External event ID</span>
          <input value={filters.externalEventId ?? ''} onChange={(event) => setFilters((current) => ({ ...current, externalEventId: event.target.value || undefined }))} />
        </label>
        <label className="field">
          <span>Event type</span>
          <input value={filters.eventType ?? ''} onChange={(event) => setFilters((current) => ({ ...current, eventType: event.target.value || undefined }))} />
        </label>
        <label className="field">
          <span>Severity</span>
          <select value={filters.severity ?? ''} onChange={(event) => updateFilters({ severity: event.target.value || undefined })}>
            <option value="">All severities</option>
            <option value="info">Info</option>
            <option value="warning">Warning</option>
            <option value="critical">Critical</option>
          </select>
        </label>
        <label className="field">
          <span>Status</span>
          <select value={filters.status ?? ''} onChange={(event) => updateFilters({ status: event.target.value || undefined })}>
            <option value="">All statuses</option>
            <option value="open">Open</option>
            <option value="acknowledged">Acknowledged</option>
            <option value="resolved">Resolved</option>
          </select>
        </label>
        <div className="compact-actions">
          <button className="button button-primary" disabled={loading} onClick={() => void load({ ...filters, page: 1 })} type="button">
            {loading ? 'Loading...' : 'Search'}
          </button>
          <button className="button button-secondary" onClick={() => updateFilters({ source: undefined, externalEventId: undefined, eventType: undefined, severity: undefined, status: undefined, page: 1 })} type="button">
            Clear
          </button>
        </div>
      </div>

      {error && <div className="notice notice-danger">{error}</div>}

      <div className="card table-card">
        <div className="asset-list-header">
          <p className="muted">{result ? `${result.totalCount} integration signals` : 'Loading integration signals'}</p>
          <span className="badge">Realtime</span>
        </div>
        <table className="data-table">
          <thead>
            <tr>
              <th>Time</th>
              <th>Source</th>
              <th>Event</th>
              <th>Status</th>
              <th>Subject</th>
              <th>Message</th>
              <th>Actions</th>
            </tr>
          </thead>
          <tbody>
            {(result?.items ?? []).map((signal: IntegrationEvent) => (
              <tr key={signal.id}>
                <td>{formatDate(signal.occurredAt)}</td>
                <td>{signal.source}<br /><span className="muted">{signal.externalEventId ?? signal.correlationId ?? 'No external ID'}</span></td>
                <td><span className={`badge ${signal.severity === 'critical' ? 'badge-danger' : signal.severity === 'warning' ? 'badge-warning' : ''}`}>{signal.eventType}</span></td>
                <td>
                  <span className={`badge ${signal.status === 'resolved' ? 'badge-success' : signal.status === 'acknowledged' ? 'badge-warning' : 'badge-danger'}`}>{signal.status}</span>
                  {signal.acknowledgedAt && <div className="muted">Ack {formatDate(signal.acknowledgedAt)}</div>}
                  {signal.resolvedAt && <div className="muted">Resolved {formatDate(signal.resolvedAt)}</div>}
                </td>
                <td>{signal.subjectName ?? signal.subjectId ?? '-'}<br /><span className="muted">{signal.subjectType ?? 'No subject'}</span></td>
                <td>
                  {signal.message}
                  {signal.payloadJson && (
                    <details className="quiet-disclosure">
                      <summary>Payload</summary>
                      <pre className="bridge-raw-payload">{formatJson(signal.payloadJson)}</pre>
                    </details>
                  )}
                  {signal.resolutionNote && <div className="muted">Resolution: {signal.resolutionNote}</div>}
                </td>
                <td>
                  {signal.status !== 'resolved' ? (
                    <div className="signal-actions">
                      {signal.status === 'open' && (
                        <button className="button button-secondary button-compact" onClick={() => void handleAcknowledge(signal)} type="button">Acknowledge</button>
                      )}
                      <input
                        aria-label={`Resolution note for ${signal.eventType}`}
                        className="compact-input"
                        onChange={(event) => setResolutionNotes((current) => ({ ...current, [signal.id]: event.target.value }))}
                        placeholder="Resolution note"
                        value={resolutionNotes[signal.id] ?? ''}
                      />
                      <button className="button button-primary button-compact" onClick={() => void handleResolve(signal)} type="button">Resolve</button>
                    </div>
                  ) : (
                    <span className="muted">{signal.resolvedBy ?? 'Resolved'}</span>
                  )}
                </td>
              </tr>
            ))}
            {result?.items.length === 0 && (
              <tr>
                <td className="muted" colSpan={7}>No integration signals match the current filters.</td>
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
