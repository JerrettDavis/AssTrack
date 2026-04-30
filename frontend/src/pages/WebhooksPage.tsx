import { useEffect, useRef, useState } from 'react'
import { getWebhookStatus, fireWebhookTest, getWebhookDeliveries, type WebhookStatus, type WebhookDeliveryLog } from '../api/webhooks'
import { useIdentityContext } from '../context/IdentityContext'

export default function WebhooksPage() {
  const { isOperator, loading: identityLoading } = useIdentityContext()

  if (!isOperator && !identityLoading) {
    return (
      <div className="card">
        <h1>Webhooks</h1>
        <p className="muted">Webhook management is only accessible to operator keys.</p>
      </div>
    )
  }

  const [status, setStatus] = useState<WebhookStatus | null>(null)
  const [deliveries, setDeliveries] = useState<WebhookDeliveryLog[]>([])
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
        getWebhookDeliveries(1, 20),
      ])
      setStatus(webhookStatus)
      setDeliveries(deliveryLogs.items)
      setLastUpdated(new Date().toLocaleTimeString())
    } catch (e: unknown) {
      setError(e instanceof Error ? e.message : String(e))
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    void load()
    pollRef.current = window.setInterval(() => {
      void load()
    }, 30000)

    return () => {
      if (pollRef.current != null) {
        window.clearInterval(pollRef.current)
      }
    }
  }, [])

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

  if (loading) return <div className="card">Loading webhook status…</div>
  if (error) return <div className="card">Error: {error}</div>

  const notConfigured = !status?.configured

  return (
    <div className="section">
      <div className="page-header">
        <h1>Webhooks</h1>
        <span className="muted">Last updated: {lastUpdated ?? '—'}</span>
      </div>

      <div className="card">
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '1rem' }}>
          <h2>Webhook Status</h2>
          <button className="button button-secondary" onClick={() => void load()} type="button">
            Refresh
          </button>
        </div>

        {notConfigured ? (
          <div style={{ padding: '1rem', backgroundColor: '#f5f5f5', borderRadius: '4px', color: '#666' }}>
            <p>No webhook URL configured in server settings</p>
          </div>
        ) : (
          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(200px, 1fr))', gap: '1rem', marginBottom: '1.5rem' }}>
            <div style={{ padding: '1rem', backgroundColor: '#f9f9f9', borderRadius: '4px' }}>
              <div style={{ fontSize: '0.9rem', color: '#666', marginBottom: '0.5rem' }}>Status</div>
              <div style={{ fontSize: '1.2rem', fontWeight: 'bold' }}>
                {status?.configured ? '✓ Configured' : '✗ Not Configured'}
              </div>
            </div>
            <div style={{ padding: '1rem', backgroundColor: '#f9f9f9', borderRadius: '4px' }}>
              <div style={{ fontSize: '0.9rem', color: '#666', marginBottom: '0.5rem' }}>Last 24h Deliveries</div>
              <div style={{ fontSize: '1.2rem', fontWeight: 'bold' }}>{status?.last24hDeliveries ?? 0}</div>
            </div>
            <div style={{ padding: '1rem', backgroundColor: '#f9f9f9', borderRadius: '4px' }}>
              <div style={{ fontSize: '0.9rem', color: '#666', marginBottom: '0.5rem' }}>Last 24h Failures</div>
              <div style={{ fontSize: '1.2rem', fontWeight: 'bold', color: status?.last24hFailures && status.last24hFailures > 0 ? '#d9534f' : '#5cb85c' }}>
                {status?.last24hFailures ?? 0}
              </div>
            </div>
            <div style={{ padding: '1rem', backgroundColor: '#f9f9f9', borderRadius: '4px' }}>
              <div style={{ fontSize: '0.9rem', color: '#666', marginBottom: '0.5rem' }}>Last Delivered</div>
              <div style={{ fontSize: '1rem', fontWeight: 'bold' }}>
                {status?.lastDeliveredAt ? new Date(status.lastDeliveredAt).toLocaleString() : '—'}
              </div>
            </div>
            <div style={{ padding: '1rem', backgroundColor: '#f9f9f9', borderRadius: '4px' }}>
              <div style={{ fontSize: '0.9rem', color: '#666', marginBottom: '0.5rem' }}>Avg Duration</div>
              <div style={{ fontSize: '1rem', fontWeight: 'bold' }}>
                {status?.avgDurationMs != null ? `${status.avgDurationMs.toFixed(0)}ms` : '—'}
              </div>
            </div>
          </div>
        )}

        <div style={{ marginTop: '1.5rem', paddingTop: '1.5rem', borderTop: '1px solid #eee' }}>
          <h3>Test Webhook</h3>
          <div style={{ display: 'flex', gap: '0.5rem', alignItems: 'center', marginTop: '1rem' }}>
            <select
              value={selectedEventType}
              onChange={(e) => setSelectedEventType(e.target.value as 'speed_alert' | 'geofence_breach')}
              style={{ padding: '0.5rem', borderRadius: '4px', border: '1px solid #ddd' }}
            >
              <option value="speed_alert">Speed Alert</option>
              <option value="geofence_breach">Geofence Breach</option>
            </select>
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
            <div style={{ marginTop: '1rem', padding: '0.75rem', backgroundColor: '#f5f5f5', borderRadius: '4px', fontSize: '0.9rem' }}>
              {testResult}
            </div>
          )}
        </div>
      </div>

      {!notConfigured && (
        <div className="card table-card">
          <h2>Recent Delivery Log</h2>
          <table className="data-table">
            <thead>
              <tr>
                <th>Attempted At</th>
                <th>Event Type</th>
                <th>Target URL</th>
                <th>Success</th>
                <th>Status Code</th>
                <th>Duration (ms)</th>
                <th>Error Message</th>
              </tr>
            </thead>
            <tbody>
              {deliveries.map((delivery) => (
                <tr key={delivery.id}>
                  <td>{new Date(delivery.attemptedAt).toLocaleString()}</td>
                  <td>{delivery.eventType}</td>
                  <td style={{ maxWidth: '200px', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }} title={delivery.targetUrl}>
                    {delivery.targetUrl.length > 40 ? delivery.targetUrl.substring(0, 40) + '…' : delivery.targetUrl}
                  </td>
                  <td>{delivery.success ? '✓' : '✗'}</td>
                  <td>{delivery.httpStatusCode ?? '—'}</td>
                  <td>{delivery.durationMs}</td>
                  <td style={{ color: delivery.errorMessage ? '#d9534f' : '#ccc' }}>
                    {delivery.errorMessage ? delivery.errorMessage.substring(0, 50) + (delivery.errorMessage.length > 50 ? '…' : '') : '—'}
                  </td>
                </tr>
              ))}
              {deliveries.length === 0 && (
                <tr>
                  <td className="muted" colSpan={7}>
                    No delivery logs available.
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      )}
    </div>
  )
}
