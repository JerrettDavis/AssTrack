import { useEffect, useRef, useState } from 'react'
import { getWebhookStatus, fireWebhookTest, getWebhookDeliveries, type WebhookStatus, type WebhookDeliveryLog } from '../api/webhooks'
import { useIdentityContext } from '../context/IdentityContext'
import { useLiveDataRefresh } from '../hooks/useLiveDataRefresh'
import DisplayControls from '../components/DisplayControls'

export default function WebhooksPage() {
  const { isOperator, loading: identityLoading } = useIdentityContext()
  const [webhookViewMode, setWebhookViewMode] = useState<'cards' | 'table'>('table')
  const [status, setStatus] = useState<WebhookStatus | null>(null)
  const [deliveries, setDeliveries] = useState<WebhookDeliveryLog[]>([])
  const [deliveriesTotal, setDeliveriesTotal] = useState(0)
  const [deliveriesPage, setDeliveriesPage] = useState(1)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [testLoading, setTestLoading] = useState(false)
  const [testResult, setTestResult] = useState<string | null>(null)
  const [selectedEventType, setSelectedEventType] = useState<'speed_alert' | 'geofence_breach'>('speed_alert')
  const [lastUpdated, setLastUpdated] = useState<string | null>(null)
  const pollRef = useRef<number | null>(null)

  async function load() {
    try {
      setError(null)
      const [webhookStatus, deliveryLogs] = await Promise.all([
        getWebhookStatus(),
        getWebhookDeliveries(deliveriesPage, 20),
      ])
      setStatus(webhookStatus)
      setDeliveries(deliveryLogs.items)
      setDeliveriesTotal(deliveryLogs.totalCount)
      setLastUpdated(new Date().toLocaleTimeString())
    } catch (e: unknown) {
      setError(e instanceof Error ? e.message : String(e))
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    if (identityLoading || !isOperator) return
    void load()
    pollRef.current = window.setInterval(() => {
      void load()
    }, 30000)

    return () => {
      if (pollRef.current != null) {
        window.clearInterval(pollRef.current)
      }
    }
  }, [deliveriesPage, identityLoading, isOperator])

  useLiveDataRefresh(load, { eventTypes: ['data_changed', 'speed_alert', 'geofence_breach'], debounceMs: 1500, enabled: !identityLoading && isOperator })

  async function handleFireTest() {
    setTestLoading(true)
    setTestResult(null)
    try {
      const result = await fireWebhookTest(selectedEventType)
      setTestResult(result.message)
      await load()
    } catch (e: unknown) {
      setTestResult(`Error: ${e instanceof Error ? e.message : String(e)}`)
    } finally {
      setTestLoading(false)
    }
  }

  if (!isOperator && !identityLoading) {
    return (
      <div className="card">
        <h1>Webhooks</h1>
        <p className="muted">Webhook management is only accessible to operator keys.</p>
      </div>
    )
  }

  if (loading || identityLoading) return <div className="card">Loading webhook status…</div>
  if (error) return <div className="card">Error: {error}</div>

  const notConfigured = !status?.configured

  return (
    <div className="section">
      <div className="page-header">
        <h1>Webhooks</h1>
        <div className="ops-actions">
          <span className="muted">Last updated: {lastUpdated ?? '—'}</span>
          <DisplayControls mode={webhookViewMode} onModeChange={setWebhookViewMode} />
        </div>
      </div>

      <div className="card">
        <div className="page-header">
          <h2>Webhook Status</h2>
          <button className="button button-secondary" onClick={() => void load()} type="button">
            Refresh
          </button>
        </div>

        {notConfigured ? (
          <div className="notice notice-warning">
            <strong>No webhook URL configured</strong>
            <p className="muted">Alert delivery is paused until server webhook settings include a target URL.</p>
          </div>
        ) : (
          <div className="panel-grid">
            <div className="status-tile">
              <span className="muted">Status</span>
              <span className={`badge ${status?.configured ? 'badge-success' : 'badge-danger'}`}>
                {status?.configured ? 'Configured' : 'Not configured'}
              </span>
            </div>
            <div className="status-tile">
              <span className="muted">Last 24h Deliveries</span>
              <strong>{status?.last24hDeliveries ?? 0}</strong>
            </div>
            <div className="status-tile">
              <span className="muted">Last 24h Failures</span>
              <strong className={status?.last24hFailures && status.last24hFailures > 0 ? 'error-text' : undefined}>
                {status?.last24hFailures ?? 0}
              </strong>
            </div>
            <div className="status-tile">
              <span className="muted">Last Delivered</span>
              <strong>
                {status?.lastDeliveredAt ? new Date(status.lastDeliveredAt).toLocaleString() : '—'}
              </strong>
            </div>
            <div className="status-tile">
              <span className="muted">Avg Duration</span>
              <strong>
                {status?.avgDurationMs != null ? `${status.avgDurationMs.toFixed(0)}ms` : '—'}
              </strong>
            </div>
            <div className="status-tile">
              <span className="muted">Retry Queue</span>
              <strong>{status?.retryQueueDepth ?? 0}</strong>
            </div>
            <div className="status-tile">
              <span className="muted">Signing</span>
              <span className={`badge ${status?.signingEnabled ? 'badge-success' : 'badge-warning'}`}>
                {status?.signingEnabled ? 'Enabled' : 'Disabled'}
              </span>
            </div>
          </div>
        )}

        <div className="status-tile">
          <h3>Test Webhook</h3>
          <div className="compact-actions">
            <label className="field">
              <span>Event type</span>
              <select
                value={selectedEventType}
                onChange={(e) => setSelectedEventType(e.target.value as 'speed_alert' | 'geofence_breach')}
              >
                <option value="speed_alert">Speed Alert</option>
                <option value="geofence_breach">Geofence Breach</option>
              </select>
            </label>
            <button
              className="button button-primary"
              onClick={() => void handleFireTest()}
              disabled={testLoading || notConfigured}
              type="button"
            >
              {testLoading ? 'Sending…' : 'Send Test Event'}
            </button>
          </div>
          {testResult && (
            <div className={`notice ${testResult.startsWith('Error:') ? 'notice-danger' : 'notice-success'}`}>
              {testResult}
            </div>
          )}
        </div>
      </div>

      {!notConfigured && (
        <div className="card table-card">
          <h2>Recent Delivery Log</h2>
          {webhookViewMode === 'cards' ? (
          <div className="asset-grid">
            {deliveries.map((delivery) => (
              <article className="list-card" key={delivery.id}>
                <header>
                  <h3>{delivery.eventType}</h3>
                  <span className={delivery.success ? 'badge badge-success' : 'badge badge-danger'}>{delivery.success ? 'Delivered' : 'Failed'}</span>
                </header>
                <div className="asset-meta">
                  <div className="asset-meta-row"><span>Attempted</span><strong>{new Date(delivery.attemptedAt).toLocaleString()}</strong></div>
                  <div className="asset-meta-row"><span>Status</span><strong>{delivery.httpStatusCode ?? 'N/A'}</strong></div>
                  <div className="asset-meta-row"><span>Duration</span><strong>{delivery.durationMs}ms</strong></div>
                  <div className="asset-meta-row"><span>Attempt</span><strong>{delivery.attemptNumber}</strong></div>
                </div>
              </article>
            ))}
            {deliveries.length === 0 && <div className="card">No webhook delivery logs yet.</div>}
          </div>
          ) : (
          <table className="data-table">
            <thead>
              <tr>
                <th>Attempted At</th>
                <th>Event Type</th>
                <th>Target URL</th>
                <th>Success</th>
                <th>Status Code</th>
                <th>Attempt</th>
                <th>Correlation</th>
                <th>Duration (ms)</th>
                <th>Error Message</th>
              </tr>
            </thead>
            <tbody>
              {deliveries.map((delivery) => (
                <tr key={delivery.id}>
                  <td>{new Date(delivery.attemptedAt).toLocaleString()}</td>
                  <td>{delivery.eventType}</td>
                  <td className="truncate-cell" title={delivery.targetUrl}>
                    {delivery.targetUrl.length > 40 ? delivery.targetUrl.substring(0, 40) + '…' : delivery.targetUrl}
                  </td>
                  <td>
                    <span className={`badge ${delivery.success ? 'badge-success' : 'badge-danger'}`}>
                      {delivery.success ? 'Success' : 'Failed'}
                    </span>
                  </td>
                  <td>{delivery.httpStatusCode ?? '—'}</td>
                  <td>{delivery.attemptNumber ?? '—'}</td>
                  <td className="coords">{delivery.correlationId ?? '—'}</td>
                  <td>{delivery.durationMs}</td>
                  <td className={delivery.errorMessage ? 'error-text' : 'muted'}>
                    {delivery.errorMessage ? delivery.errorMessage.substring(0, 50) + (delivery.errorMessage.length > 50 ? '…' : '') : '—'}
                  </td>
                </tr>
              ))}
              {deliveries.length === 0 && (
                <tr>
                  <td className="muted" colSpan={9}>
                    No delivery logs available.
                  </td>
                </tr>
              )}
            </tbody>
          </table>
          )}
          <div className="table-actions">
            <span className="muted">
              Page {deliveriesPage} of {Math.ceil(deliveriesTotal / 20) || 1} (Total: {deliveriesTotal})
            </span>
            <div className="compact-actions">
              <button
                className="button button-secondary"
                onClick={() => setDeliveriesPage(Math.max(1, deliveriesPage - 1))}
                disabled={deliveriesPage === 1}
                type="button"
              >
                Previous
              </button>
              <button
                className="button button-secondary"
                onClick={() => setDeliveriesPage(deliveriesPage + 1)}
                disabled={deliveries.length < 20}
                type="button"
              >
                Next
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  )
}
