import { useEffect, useState, type CSSProperties } from 'react'
import { apiPost } from '../api/client'
import { getSystemStatus, type SystemStatus } from '../api/system'

interface SimulateResult {
  observationsCreated: number
  speedAlertsTriggered: number
  geofenceBreaches: number
  deviceId: string
  deviceIdentifier: string
  assetId: string | null
  eventLog: string[]
}

type SimulationPreset = 'NormalRoute' | 'SpeedViolation' | 'GeofenceEntryExit'

const badgeStyle = (enabled: boolean): CSSProperties => ({
  display: 'inline-flex',
  alignItems: 'center',
  justifyContent: 'center',
  padding: '0.2rem 0.6rem',
  borderRadius: '999px',
  fontSize: '0.85rem',
  fontWeight: 700,
  color: enabled ? '#dcfce7' : '#fee2e2',
  backgroundColor: enabled ? 'rgba(22, 163, 74, 0.2)' : 'rgba(220, 38, 38, 0.2)',
  border: `1px solid ${enabled ? 'rgba(34, 197, 94, 0.35)' : 'rgba(248, 113, 113, 0.35)'}`,
})

const tileStyle: CSSProperties = {
  padding: '1rem',
  backgroundColor: 'rgba(30, 41, 59, 0.7)',
  borderRadius: '12px',
  border: '1px solid rgba(148, 163, 184, 0.15)',
  display: 'grid',
  gap: '0.6rem',
}

export default function SettingsPage() {
  const [status, setStatus] = useState<SystemStatus | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [lastUpdated, setLastUpdated] = useState<string | null>(null)
  const [preset, setPreset] = useState<SimulationPreset>('NormalRoute')
  const [deviceIdentifier, setDeviceIdentifier] = useState('')
  const [runError, setRunError] = useState<string | null>(null)
  const [runLoading, setRunLoading] = useState(false)
  const [result, setResult] = useState<SimulateResult | null>(null)

  async function loadStatus() {
    try {
      setError(null)
      const nextStatus = await getSystemStatus()
      setStatus(nextStatus)
      setLastUpdated(new Date().toLocaleTimeString())
    } catch (e: unknown) {
      setError(e instanceof Error ? e.message : String(e))
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    void loadStatus()
  }, [])

  async function handleRunSimulation() {
    try {
      setRunLoading(true)
      setRunError(null)
      const simulationResult = await apiPost<SimulateResult>('/api/observations/simulate', {
        preset,
        deviceIdentifier: deviceIdentifier || undefined,
      })
      setResult(simulationResult)
      await loadStatus()
    } catch (e: unknown) {
      setRunError(e instanceof Error ? e.message : String(e))
    } finally {
      setRunLoading(false)
    }
  }

  if (loading) {
    return <div className="card">Loading settings…</div>
  }

  if (error || !status) {
    return (
      <div className="section">
        <div className="card">
          <h1 style={{ marginTop: 0 }}>Settings</h1>
          <p style={{ color: '#fca5a5' }}>Error: {error ?? 'Unable to load system status.'}</p>
          <button className="button button-secondary" type="button" onClick={() => void loadStatus()}>
            Retry
          </button>
        </div>
      </div>
    )
  }

  return (
    <div className="section">
      <div className="page-header">
        <div>
          <h1 style={{ margin: 0 }}>Settings</h1>
          <p className="muted" style={{ margin: '0.35rem 0 0' }}>
            System status, safe configuration visibility, and simulation tooling.
          </p>
        </div>
        <div style={{ display: 'grid', gap: '0.35rem', justifyItems: 'end' }}>
          <span className="muted">Last updated: {lastUpdated ?? '—'}</span>
          <button className="button button-secondary" type="button" onClick={() => void loadStatus()}>
            Refresh
          </button>
        </div>
      </div>

      {!status.webhookConfigured && (
        <div
          className="card"
          style={{ borderColor: 'rgba(250, 204, 21, 0.35)', backgroundColor: 'rgba(120, 53, 15, 0.35)' }}
        >
          <strong style={{ display: 'block', marginBottom: '0.35rem', color: '#fde68a' }}>Webhook not configured</strong>
          <span className="muted">Alert events will stay local until Webhooks:Url is configured.</span>
        </div>
      )}

      {!status.apiKeyConfigured && (
        <div
          className="card"
          style={{ borderColor: 'rgba(248, 113, 113, 0.35)', backgroundColor: 'rgba(127, 29, 29, 0.35)' }}
        >
          <strong style={{ display: 'block', marginBottom: '0.35rem', color: '#fecaca' }}>API key missing</strong>
          <span className="muted">Clients will be unable to authenticate until Auth:ApiKey is configured.</span>
        </div>
      )}

      <div className="card">
        <div className="page-header" style={{ marginBottom: '1rem' }}>
          <h2>System Status</h2>
        </div>
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(180px, 1fr))', gap: '1rem' }}>
          <div style={tileStyle}>
            <span className="muted">Environment</span>
            <strong>{status.environment}</strong>
          </div>
          <div style={tileStyle}>
            <span className="muted">Simulation</span>
            <span style={badgeStyle(status.simulationEnabled)}>{status.simulationEnabled ? 'Enabled' : 'Disabled'}</span>
          </div>
          <div style={tileStyle}>
            <span className="muted">Webhooks</span>
            <span style={badgeStyle(status.webhookConfigured)}>{status.webhookConfigured ? 'Configured' : 'Not configured'}</span>
          </div>
          <div style={tileStyle}>
            <span className="muted">API Key</span>
            <span style={badgeStyle(status.apiKeyConfigured)}>{status.apiKeyConfigured ? 'Configured' : 'Missing'}</span>
          </div>
          <div style={tileStyle}>
            <span className="muted">Swagger</span>
            <span style={badgeStyle(status.swaggerEnabled)}>{status.swaggerEnabled ? 'Enabled' : 'Disabled'}</span>
          </div>
          <div style={tileStyle}>
            <span className="muted">Rate Limit</span>
            <strong>{status.rateLimitPermitLimit} requests</strong>
            <span className="muted">per {status.rateLimitWindowSeconds}s window</span>
          </div>
          <div style={tileStyle}>
            <span className="muted">Database</span>
            <strong>{status.databaseProvider}</strong>
          </div>
        </div>
      </div>

      {status.simulationEnabled && (
        <div className="card">
          <div className="page-header" style={{ marginBottom: '1rem' }}>
            <div>
              <h2>Simulation Runner</h2>
              <p className="muted" style={{ margin: '0.35rem 0 0' }}>
                Generate realistic observations and alerts without external devices.
              </p>
            </div>
          </div>

          <div className="inline-form">
            <div className="field-grid">
              <label className="field">
                <span>Preset</span>
                <select value={preset} onChange={(e) => setPreset(e.target.value as SimulationPreset)}>
                  <option value="NormalRoute">NormalRoute</option>
                  <option value="SpeedViolation">SpeedViolation</option>
                  <option value="GeofenceEntryExit">GeofenceEntryExit</option>
                </select>
              </label>

              <label className="field">
                <span>Device identifier (optional)</span>
                <input
                  value={deviceIdentifier}
                  onChange={(e) => setDeviceIdentifier(e.target.value)}
                  placeholder="Leave blank to auto-generate"
                />
              </label>
            </div>

            <div className="button-row">
              <button className="button" type="button" onClick={() => void handleRunSimulation()} disabled={runLoading}>
                {runLoading ? 'Running…' : 'Run Simulation'}
              </button>
            </div>

            {runError && (
              <div
                style={{
                  padding: '0.9rem 1rem',
                  borderRadius: '10px',
                  backgroundColor: 'rgba(127, 29, 29, 0.35)',
                  border: '1px solid rgba(248, 113, 113, 0.35)',
                  color: '#fecaca',
                }}
              >
                {runError}
              </div>
            )}

            {result && (
              <div
                style={{
                  display: 'grid',
                  gap: '1rem',
                  marginTop: '0.5rem',
                  padding: '1rem',
                  borderRadius: '12px',
                  backgroundColor: 'rgba(15, 118, 110, 0.2)',
                  border: '1px solid rgba(45, 212, 191, 0.25)',
                }}
              >
                <div className="page-header">
                  <div>
                    <h3 style={{ margin: 0 }}>Simulation completed</h3>
                    <p className="muted" style={{ margin: '0.35rem 0 0' }}>
                      Device {result.deviceIdentifier} • Device ID {result.deviceId}
                    </p>
                  </div>
                </div>

                <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(160px, 1fr))', gap: '0.75rem' }}>
                  <div style={tileStyle}>
                    <span className="muted">Observations</span>
                    <strong>{result.observationsCreated}</strong>
                  </div>
                  <div style={tileStyle}>
                    <span className="muted">Speed alerts</span>
                    <strong>{result.speedAlertsTriggered}</strong>
                  </div>
                  <div style={tileStyle}>
                    <span className="muted">Geofence breaches</span>
                    <strong>{result.geofenceBreaches}</strong>
                  </div>
                  <div style={tileStyle}>
                    <span className="muted">Asset ID</span>
                    <strong style={{ fontSize: '0.95rem' }}>{result.assetId ?? 'Unassigned'}</strong>
                  </div>
                </div>

                <details>
                  <summary style={{ cursor: 'pointer', fontWeight: 600 }}>Event log ({result.eventLog.length})</summary>
                  <div
                    style={{
                      marginTop: '0.75rem',
                      padding: '0.9rem',
                      borderRadius: '10px',
                      backgroundColor: 'rgba(15, 23, 42, 0.55)',
                      border: '1px solid rgba(148, 163, 184, 0.15)',
                      maxHeight: '260px',
                      overflowY: 'auto',
                    }}
                  >
                    {result.eventLog.length > 0 ? (
                      <ul style={{ margin: 0, paddingLeft: '1.25rem', display: 'grid', gap: '0.5rem' }}>
                        {result.eventLog.map((entry, index) => (
                          <li key={`${entry}-${index}`}>{entry}</li>
                        ))}
                      </ul>
                    ) : (
                      <span className="muted">No event log entries returned.</span>
                    )}
                  </div>
                </details>
              </div>
            )}
          </div>
        </div>
      )}
    </div>
  )
}
