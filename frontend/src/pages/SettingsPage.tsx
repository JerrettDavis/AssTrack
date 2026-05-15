import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { apiPost } from '../api/client'
import { applyEnterpriseRetention, getSystemStatus, seedDemoData, type EnterpriseRetentionCleanupResult, type SeedResult, type SystemStatus } from '../api/system'
import { useIdentityContext } from '../context/IdentityContext'
import { useAppearance, type ColorMode, type ThemeStyle } from '../context/AppearanceContext'
import DisplayControls from '../components/DisplayControls'
import { useLiveDataRefresh } from '../hooks/useLiveDataRefresh'

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

const themeOptions: Array<{ value: ThemeStyle; label: string; description: string }> = [
  { value: 'modern', label: 'Modern', description: 'Balanced spacing, soft surfaces, and operational color cues.' },
  { value: 'classic', label: 'Classic', description: 'Warmer surfaces, stronger borders, and a more traditional console feel.' },
  { value: 'condensed', label: 'Condensed', description: 'Higher-density tables and controls for monitoring large fleets.' },
  { value: 'minimal', label: 'Minimal', description: 'Reduced shadows and quieter chrome for low-distraction work.' },
  { value: 'contrast', label: 'Contrast', description: 'Stronger color separation and borders for fast scanning.' },
]

const colorModeOptions: Array<{ value: ColorMode; label: string }> = [
  { value: 'system', label: 'System' },
  { value: 'light', label: 'Light' },
  { value: 'dark', label: 'Dark' },
]

export default function SettingsPage() {
  const { isOperator, isAdmin, loading: identityLoading } = useIdentityContext()
  const { colorMode, effectiveColorMode, themeStyle, setColorMode, setThemeStyle } = useAppearance()
  const [status, setStatus] = useState<SystemStatus | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [lastUpdated, setLastUpdated] = useState<string | null>(null)
  const [preset, setPreset] = useState<SimulationPreset>('NormalRoute')
  const [deviceIdentifier, setDeviceIdentifier] = useState('')
  const [runError, setRunError] = useState<string | null>(null)
  const [runLoading, setRunLoading] = useState(false)
  const [result, setResult] = useState<SimulateResult | null>(null)
  const [seedResult, setSeedResult] = useState<SeedResult | null>(null)
  const [seedError, setSeedError] = useState<string | null>(null)
  const [seedLoading, setSeedLoading] = useState(false)
  const [retentionDays, setRetentionDays] = useState({ audit: 365, signal: 180, webhook: 90 })
  const [retentionLoading, setRetentionLoading] = useState(false)
  const [retentionError, setRetentionError] = useState<string | null>(null)
  const [retentionResult, setRetentionResult] = useState<EnterpriseRetentionCleanupResult | null>(null)

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
    if (identityLoading || !isOperator) return
    void loadStatus()
  }, [identityLoading, isOperator])

  useLiveDataRefresh(loadStatus, { eventTypes: ['data_changed'], debounceMs: 1500, enabled: !identityLoading && isOperator })

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

  async function handleSeed(reset: boolean) {
    try {
      setSeedLoading(true)
      setSeedError(null)
      const r = await seedDemoData(reset)
      setSeedResult(r)
      await loadStatus()
    } catch (e: unknown) {
      setSeedError(e instanceof Error ? e.message : String(e))
    } finally {
      setSeedLoading(false)
    }
  }

  async function handleRetention(dryRun: boolean) {
    try {
      setRetentionLoading(true)
      setRetentionError(null)
      const r = await applyEnterpriseRetention({
        auditDays: retentionDays.audit,
        signalDays: retentionDays.signal,
        webhookDays: retentionDays.webhook,
        dryRun,
      })
      setRetentionResult(r)
      await loadStatus()
    } catch (e: unknown) {
      setRetentionError(e instanceof Error ? e.message : String(e))
    } finally {
      setRetentionLoading(false)
    }
  }

  const showOperatorSettings = isOperator && !identityLoading && !loading && status !== null && error === null

  return (
    <div className="section">
      <div className="page-header">
        <div>
          <h1>Settings</h1>
          <p className="muted">
            Appearance, system status, safe configuration visibility, and simulation tooling.
          </p>
        </div>
        {isOperator && (
          <div className="compact-actions">
            <span className="muted">Last updated: {lastUpdated ?? '—'}</span>
            <button className="button button-secondary" type="button" onClick={() => void loadStatus()}>
              Refresh
            </button>
          </div>
        )}
      </div>

      <div className="card">
        <div className="page-header">
          <div>
            <h2>Appearance</h2>
            <p className="muted">
              Current mode: {effectiveColorMode}. Your choices are saved on this device.
            </p>
          </div>
        </div>

        <div className="field-grid">
          <label className="field">
            <span>Color mode</span>
            <select onChange={(event) => setColorMode(event.target.value as ColorMode)} value={colorMode}>
              {colorModeOptions.map((option) => (
                <option key={option.value} value={option.value}>{option.label}</option>
              ))}
            </select>
          </label>
          <label className="field">
            <span>Theme</span>
            <select onChange={(event) => setThemeStyle(event.target.value as ThemeStyle)} value={themeStyle}>
              {themeOptions.map((option) => (
                <option key={option.value} value={option.value}>{option.label}</option>
              ))}
            </select>
          </label>
        </div>

        <div className="theme-grid">
          {themeOptions.map((option) => (
            <button
              className={`theme-option${themeStyle === option.value ? ' active' : ''}`}
              key={option.value}
              onClick={() => setThemeStyle(option.value)}
              type="button"
            >
              <span className={`theme-preview theme-preview-${option.value}`}>
                <span />
                <span />
                <span />
              </span>
              <strong>{option.label}</strong>
              <span className="muted">{option.description}</span>
            </button>
          ))}
        </div>

        <div className="segmented-control" aria-label="Color mode">
          {colorModeOptions.map((option) => (
            <button
              className={colorMode === option.value ? 'active' : ''}
              key={option.value}
              onClick={() => setColorMode(option.value)}
              type="button"
            >
              {option.label}
            </button>
          ))}
        </div>

        <div className="page-header">
          <div>
            <h3>List display</h3>
            <p className="muted">Choose how list-heavy pages render by default.</p>
          </div>
          <DisplayControls />
        </div>
      </div>

      {!isOperator && !identityLoading && (
        <div className="notice notice-info">
          <strong>Viewer settings</strong>
          <span className="muted">System operations, simulation, and demo data tools require an operator key.</span>
        </div>
      )}

      {isOperator && !identityLoading && (
        <div className="card">
          <div className="page-header">
            <div>
              <h2>Bridge Gateway</h2>
              <p className="muted">
                Configure Meshtastic, Home Assistant, Google Find Hub handoffs, Samsung SmartThings handoffs, and webhook feeds.
              </p>
            </div>
            <Link className="button" to="/integrations">Open Bridge Setup</Link>
          </div>
          <div className="panel-grid">
            <div className="status-tile">
              <span className="muted">Gateway</span>
              <strong>http://127.0.0.1:5056</strong>
            </div>
            <div className="status-tile">
              <span className="muted">Config source</span>
              <strong>In-app bridge feeds</strong>
            </div>
            <div className="status-tile">
              <span className="muted">Setup path</span>
              <strong>Bridge → Add bridge feed</strong>
            </div>
          </div>
        </div>
      )}

      {isOperator && (loading || identityLoading) && <div className="card">Loading operator settings…</div>}

      {isOperator && !identityLoading && (error || !status) && (
        <div className="card">
          <h2>Operator Settings</h2>
          <p className="error-text">Error: {error ?? 'Unable to load system status.'}</p>
          <button className="button button-secondary" type="button" onClick={() => void loadStatus()}>
            Retry
          </button>
        </div>
      )}

      {showOperatorSettings && status && (
        <>
      {!status.webhookConfigured && (
        <div className="notice notice-warning">
          <strong>Webhook not configured</strong>
          <span className="muted">Alert events will stay local until Webhooks:Url is configured.</span>
        </div>
      )}

      {!status.apiKeyConfigured && (
        <div className="notice notice-danger">
          <strong>API key missing</strong>
          <span className="muted">Clients will be unable to authenticate until Auth:ApiKey is configured.</span>
        </div>
      )}

      <div className="card">
        <div className="page-header">
          <h2>System Status</h2>
        </div>
        <div className="panel-grid">
          <div className="status-tile">
            <span className="muted">Environment</span>
            <strong>{status.environment}</strong>
          </div>
          <div className="status-tile">
            <span className="muted">Simulation</span>
            <span className={`badge ${status.simulationEnabled ? 'badge-success' : 'badge-danger'}`}>{status.simulationEnabled ? 'Enabled' : 'Disabled'}</span>
          </div>
          <div className="status-tile">
            <span className="muted">Webhooks</span>
            <span className={`badge ${status.webhookConfigured ? 'badge-success' : 'badge-warning'}`}>{status.webhookConfigured ? 'Configured' : 'Not configured'}</span>
          </div>
          <div className="status-tile">
            <span className="muted">API Key</span>
            <span className={`badge ${status.apiKeyConfigured ? 'badge-success' : 'badge-danger'}`}>{status.apiKeyConfigured ? 'Configured' : 'Missing'}</span>
          </div>
          <div className="status-tile">
            <span className="muted">Admin Key</span>
            <span className={`badge ${status.adminApiKeyConfigured ? 'badge-success' : 'badge-warning'}`}>{status.adminApiKeyConfigured ? 'Configured' : 'Operator fallback'}</span>
          </div>
          <div className="status-tile">
            <span className="muted">Ingest Key</span>
            <span className={`badge ${status.ingestApiKeyConfigured ? 'badge-success' : 'badge-danger'}`}>{status.ingestApiKeyConfigured ? 'Configured' : 'Missing'}</span>
          </div>
          <div className="status-tile">
            <span className="muted">Access Tier</span>
            <strong>{status.accessTier}</strong>
          </div>
          <div className="status-tile">
            <span className="muted">Swagger</span>
            <span className={`badge ${status.swaggerEnabled ? 'badge-success' : 'badge-warning'}`}>{status.swaggerEnabled ? 'Enabled' : 'Disabled'}</span>
          </div>
          <div className="status-tile">
            <span className="muted">Rate Limit</span>
            <strong>{status.rateLimitPermitLimit} requests</strong>
            <span className="muted">per {status.rateLimitWindowSeconds}s window</span>
          </div>
          <div className="status-tile">
            <span className="muted">Database</span>
            <strong>{status.databaseProvider}</strong>
          </div>
          <div className="status-tile">
            <span className="muted">Data</span>
            <span className={`badge ${status.hasData ? 'badge-success' : 'badge-warning'}`}>{status.hasData ? 'Present' : 'Empty'}</span>
          </div>
        </div>
      </div>

      {status.simulationEnabled && (
        <>
          <div className="card">
            <div className="page-header">
              <div>
                <h2>Simulation Runner</h2>
                <p className="muted">
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
                <div className="notice notice-danger">{runError}</div>
              )}

              {result && (
                <div className="notice notice-success">
                  <div className="page-header">
                    <div>
                      <h3>Simulation completed</h3>
                      <p className="muted">
                        Device {result.deviceIdentifier} • Device ID {result.deviceId}
                      </p>
                    </div>
                  </div>

                  <div className="panel-grid">
                    <div className="status-tile">
                      <span className="muted">Observations</span>
                      <strong>{result.observationsCreated}</strong>
                    </div>
                    <div className="status-tile">
                      <span className="muted">Speed alerts</span>
                      <strong>{result.speedAlertsTriggered}</strong>
                    </div>
                    <div className="status-tile">
                      <span className="muted">Geofence breaches</span>
                      <strong>{result.geofenceBreaches}</strong>
                    </div>
                    <div className="status-tile">
                      <span className="muted">Asset ID</span>
                      <strong>{result.assetId ?? 'Unassigned'}</strong>
                    </div>
                  </div>

                  <details>
                    <summary>Event log ({result.eventLog.length})</summary>
                    <div className="status-tile scroll-panel">
                      {result.eventLog.length > 0 ? (
                        <ul className="inline-list">
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

          <div className="card">
            <div className="page-header">
              <div>
                <h2>Demo Data</h2>
                <p className="muted">
                  Seed realistic demo assets, devices, and geofences to explore the UI.
                </p>
              </div>
            </div>
            <div className="button-row">
              <button className="button" type="button" onClick={() => void handleSeed(false)} disabled={seedLoading}>
                {seedLoading ? 'Seeding…' : 'Seed Demo Data'}
              </button>
              <button className="button button-secondary" type="button" onClick={() => void handleSeed(true)} disabled={seedLoading}>
                {seedLoading ? 'Resetting…' : 'Reset & Re-seed'}
              </button>
            </div>
            {seedError && (
              <div className="notice notice-danger">{seedError}</div>
            )}
            {seedResult && (
              <div className="notice notice-success">
                {seedResult.alreadySeeded ? (
                  <p className="muted">Demo data already present. Use Reset &amp; Re-seed to refresh.</p>
                ) : (
                  <div className="panel-grid">
                    <div className="status-tile"><span className="muted">Assets</span><strong>{seedResult.assetsCreated}</strong></div>
                    <div className="status-tile"><span className="muted">Devices</span><strong>{seedResult.devicesCreated}</strong></div>
                    <div className="status-tile"><span className="muted">Geofences</span><strong>{seedResult.geofencesCreated}</strong></div>
                    <div className="status-tile"><span className="muted">Reset</span><strong>{seedResult.resetPerformed ? 'Yes' : 'No'}</strong></div>
                  </div>
                )}
              </div>
            )}
          </div>
        </>
      )}
        </>
      )}

      {showOperatorSettings && status && isAdmin && (
        <div className="card">
          <div className="page-header">
            <div>
              <h2>Enterprise Retention</h2>
              <p className="muted">Purge old audit events, resolved signals, and webhook delivery logs after export windows close.</p>
            </div>
          </div>
          <div className="field-grid">
            <label className="field">
              <span>Audit days</span>
              <input min={1} onChange={(event) => setRetentionDays((v) => ({ ...v, audit: parseInt(event.target.value || '365', 10) }))} type="number" value={retentionDays.audit} />
            </label>
            <label className="field">
              <span>Resolved signal days</span>
              <input min={1} onChange={(event) => setRetentionDays((v) => ({ ...v, signal: parseInt(event.target.value || '180', 10) }))} type="number" value={retentionDays.signal} />
            </label>
            <label className="field">
              <span>Webhook log days</span>
              <input min={1} onChange={(event) => setRetentionDays((v) => ({ ...v, webhook: parseInt(event.target.value || '90', 10) }))} type="number" value={retentionDays.webhook} />
            </label>
          </div>
          <div className="button-row">
            <button className="button button-secondary" disabled={retentionLoading} onClick={() => void handleRetention(true)} type="button">
              {retentionLoading ? 'Checking...' : 'Dry Run'}
            </button>
            <button className="button button-danger" disabled={retentionLoading} onClick={() => void handleRetention(false)} type="button">
              {retentionLoading ? 'Applying...' : 'Apply Retention'}
            </button>
          </div>
          {retentionError && <div className="notice notice-danger">{retentionError}</div>}
          {retentionResult && (
            <div className={`notice ${retentionResult.dryRun ? 'notice-info' : 'notice-success'}`}>
              <div className="panel-grid">
                <div className="status-tile"><span className="muted">Audit events</span><strong>{retentionResult.deletedAuditEvents} / {retentionResult.matchingAuditEvents}</strong></div>
                <div className="status-tile"><span className="muted">Resolved signals</span><strong>{retentionResult.deletedResolvedIntegrationEvents} / {retentionResult.matchingResolvedIntegrationEvents}</strong></div>
                <div className="status-tile"><span className="muted">Webhook logs</span><strong>{retentionResult.deletedWebhookDeliveries} / {retentionResult.matchingWebhookDeliveries}</strong></div>
                <div className="status-tile"><span className="muted">Mode</span><strong>{retentionResult.dryRun ? 'Dry run' : 'Applied'}</strong></div>
              </div>
            </div>
          )}
        </div>
      )}
    </div>
  )
}
