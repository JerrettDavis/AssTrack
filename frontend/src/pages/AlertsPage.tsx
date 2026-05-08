import { useEffect, useRef, useState } from 'react'
import {
  acknowledgeSpeedAlert,
  bulkAcknowledgeSpeedAlerts,
  createAlertRoute,
  deleteAlertRoute,
  getAlertRoutes,
  getSpeedAlerts,
  updateAlertRoute,
  type AlertRoutingRule,
  type AlertRoutingRuleRequest,
  type SpeedAlert,
} from '../api/alerts'
import { acknowledgeBreach, bulkAcknowledgeBreaches, getGeofenceBreaches, type GeofenceBreach } from '../api/geofenceBreaches'
import { useLiveEvents } from '../hooks/useLiveEvents'
import { useLiveDataRefresh } from '../hooks/useLiveDataRefresh'
import DisplayControls from '../components/DisplayControls'
import AcknowledgeModal from '../components/AcknowledgeModal'
import { getIntegrationFeeds, type IntegrationFeed } from '../api/integrations'

type FilterTab = 'all' | 'unacknowledged'

export default function AlertsPage() {
  const [alertViewMode, setAlertViewMode] = useState<'cards' | 'table'>('table')
  const [alerts, setAlerts] = useState<SpeedAlert[]>([])
  const [breaches, setBreaches] = useState<GeofenceBreach[]>([])
  const [routes, setRoutes] = useState<AlertRoutingRule[]>([])
  const [feeds, setFeeds] = useState<IntegrationFeed[]>([])
  const [alertsTotal, setAlertsTotal] = useState(0)
  const [breachesTotal, setBreachesTotal] = useState(0)
  const [alertsPage, setAlertsPage] = useState(1)
  const [breachesPage, setBreachesPage] = useState(1)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [lastUpdated, setLastUpdated] = useState<string | null>(null)
  const [speedFilter, setSpeedFilter] = useState<FilterTab>('all')
  const [breachFilter, setBreachFilter] = useState<FilterTab>('all')
  const [selectedSpeedAlerts, setSelectedSpeedAlerts] = useState<Set<string>>(new Set())
  const [selectedBreaches, setSelectedBreaches] = useState<Set<string>>(new Set())
  const [acknowledgeModal, setAcknowledgeModal] = useState<{ open: boolean; title: string; onConfirm: (name: string | undefined) => void } | null>(null)
  const [routeForm, setRouteForm] = useState<AlertRoutingRuleRequest>({
    name: '',
    isEnabled: true,
    eventType: 'all',
    channel: 'direct',
    provider: 'meshtastic',
    integrationFeedId: null,
    externalPeerId: '',
    displayName: '',
    recipient: '',
    messageTemplate: '',
  })
  const [editingRouteId, setEditingRouteId] = useState<string | null>(null)
  const pollRef = useRef<number | null>(null)

  async function load() {
    try {
      setError(null)
      const [speedAlerts, geofenceBreaches, routeItems, feedItems] = await Promise.all([
        getSpeedAlerts({ unacknowledged: speedFilter === 'unacknowledged' || undefined, page: alertsPage, pageSize: 50 }),
        getGeofenceBreaches({ unacknowledged: breachFilter === 'unacknowledged' || undefined, page: breachesPage, pageSize: 50 }),
        getAlertRoutes(),
        getIntegrationFeeds(),
      ])
      setAlerts(speedAlerts.items)
      setAlertsTotal(speedAlerts.totalCount)
      setBreaches(geofenceBreaches.items)
      setBreachesTotal(geofenceBreaches.totalCount)
      setRoutes(routeItems)
      setFeeds(feedItems)
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
  }, [speedFilter, breachFilter, alertsPage, breachesPage])

  useLiveEvents((type, _data) => {
    if (type === 'speed_alert' || type === 'geofence_breach') {
      void load()
    }
  })

  useLiveDataRefresh(load, { eventTypes: ['data_changed'], debounceMs: 750 })

  async function handleAcknowledgeAlert(id: string) {
    setAcknowledgeModal({
      open: true,
      title: 'Acknowledge Speed Alert',
      onConfirm: async (acknowledgedBy) => {
        setAcknowledgeModal(null)
        try {
          await acknowledgeSpeedAlert(id, acknowledgedBy)
          await load()
        } catch (e: unknown) {
          setError(e instanceof Error ? e.message : String(e))
        }
      },
    })
  }

  async function handleAcknowledgeBreach(id: string) {
    setAcknowledgeModal({
      open: true,
      title: 'Acknowledge Geofence Breach',
      onConfirm: async (acknowledgedBy) => {
        setAcknowledgeModal(null)
        try {
          await acknowledgeBreach(id, acknowledgedBy)
          await load()
        } catch (e: unknown) {
          setError(e instanceof Error ? e.message : String(e))
        }
      },
    })
  }

  async function handleBulkAcknowledgeSpeedAlerts() {
    if (selectedSpeedAlerts.size === 0) return
    setAcknowledgeModal({
      open: true,
      title: `Acknowledge ${selectedSpeedAlerts.size} Speed Alerts`,
      onConfirm: async (acknowledgedBy) => {
        setAcknowledgeModal(null)
        try {
          await bulkAcknowledgeSpeedAlerts(Array.from(selectedSpeedAlerts), acknowledgedBy)
          setSelectedSpeedAlerts(new Set())
          await load()
        } catch (e: unknown) {
          setError(e instanceof Error ? e.message : String(e))
        }
      },
    })
  }

  async function handleBulkAcknowledgeBreaches() {
    if (selectedBreaches.size === 0) return
    setAcknowledgeModal({
      open: true,
      title: `Acknowledge ${selectedBreaches.size} Geofence Breaches`,
      onConfirm: async (acknowledgedBy) => {
        setAcknowledgeModal(null)
        try {
          await bulkAcknowledgeBreaches(Array.from(selectedBreaches), acknowledgedBy)
          setSelectedBreaches(new Set())
          await load()
        } catch (e: unknown) {
          setError(e instanceof Error ? e.message : String(e))
        }
      },
    })
  }

  function toggleSpeedAlert(id: string) {
    const newSet = new Set(selectedSpeedAlerts)
    if (newSet.has(id)) newSet.delete(id)
    else newSet.add(id)
    setSelectedSpeedAlerts(newSet)
  }

  function toggleBreach(id: string) {
    const newSet = new Set(selectedBreaches)
    if (newSet.has(id)) newSet.delete(id)
    else newSet.add(id)
    setSelectedBreaches(newSet)
  }

  async function saveRoute() {
    try {
      setError(null)
      const payload = {
        ...routeForm,
        integrationFeedId: routeForm.integrationFeedId || null,
        externalPeerId: routeForm.externalPeerId || null,
        displayName: routeForm.displayName || null,
        recipient: routeForm.recipient || null,
        messageTemplate: routeForm.messageTemplate || null,
      }
      if (editingRouteId) {
        await updateAlertRoute(editingRouteId, payload)
      } else {
        await createAlertRoute(payload)
      }
      setEditingRouteId(null)
      setRouteForm({
        name: '',
        isEnabled: true,
        eventType: 'all',
        channel: 'direct',
        provider: 'meshtastic',
        integrationFeedId: null,
        externalPeerId: '',
        displayName: '',
        recipient: '',
        messageTemplate: '',
      })
      await load()
    } catch (e: unknown) {
      setError(e instanceof Error ? e.message : String(e))
    }
  }

  function editRoute(route: AlertRoutingRule) {
    setEditingRouteId(route.id)
    setRouteForm({
      name: route.name,
      isEnabled: route.isEnabled,
      eventType: route.eventType,
      channel: route.channel,
      provider: route.provider,
      integrationFeedId: route.integrationFeedId ?? null,
      externalPeerId: route.externalPeerId ?? '',
      displayName: route.displayName ?? '',
      recipient: route.recipient ?? '',
      messageTemplate: route.messageTemplate ?? '',
    })
  }

  if (loading) return <div className="card">Loading alerts…</div>
  if (error) return <div className="card">Error: {error}</div>

  return (
    <div className="section ops-page">
      <div className="ops-header">
        <div className="ops-title">
          <h1>Alerts</h1>
          <p>Exception review, acknowledgements, and routing</p>
        </div>
        <div className="ops-actions">
          <span className="muted">Last updated: {lastUpdated ?? '—'}</span>
          <DisplayControls mode={alertViewMode} onModeChange={setAlertViewMode} />
        </div>
      </div>

      <AcknowledgeModal
        open={acknowledgeModal?.open ?? false}
        title={acknowledgeModal?.title ?? ''}
        onConfirm={(name) => acknowledgeModal?.onConfirm(name)}
        onCancel={() => setAcknowledgeModal(null)}
      />

      <div className="metrics kpi-strip">
        <div className="metric">
          <span>Speed alerts</span>
          <strong>{alertsTotal}</strong>
        </div>
        <div className="metric">
          <span>Geofence breaches</span>
          <strong>{breachesTotal}</strong>
        </div>
        <div className="metric">
          <span>Unacknowledged</span>
          <strong>{alerts.filter(a => !a.acknowledgedAtUtc).length + breaches.filter(b => !b.acknowledgedAtUtc).length}</strong>
        </div>
        <div className="metric">
          <span>Routes</span>
          <strong>{routes.length}</strong>
        </div>
      </div>

      <details className="quiet-disclosure">
        <summary>
          Alert routing
          <span className="badge">{routes.length} routes</span>
        </summary>
        <div className="compact-field-grid">
          <label className="field">
            <span>Name</span>
            <input value={routeForm.name} onChange={(event) => setRouteForm({ ...routeForm, name: event.target.value })} placeholder="Dispatch channel" />
          </label>
          <label className="field">
            <span>Event</span>
            <select value={routeForm.eventType} onChange={(event) => setRouteForm({ ...routeForm, eventType: event.target.value as AlertRoutingRuleRequest['eventType'] })}>
              <option value="all">All alerts</option>
              <option value="speed_alert">Speed alerts</option>
              <option value="geofence_breach">Geofence breaches</option>
            </select>
          </label>
          <label className="field">
            <span>Provider</span>
            <input value={routeForm.provider} onChange={(event) => setRouteForm({ ...routeForm, provider: event.target.value })} />
          </label>
          <label className="field">
            <span>Feed</span>
            <select value={routeForm.integrationFeedId ?? ''} onChange={(event) => setRouteForm({ ...routeForm, integrationFeedId: event.target.value || null })}>
              <option value="">No bridge feed</option>
              {feeds.map((feed) => (
                <option key={feed.id} value={feed.id}>{feed.name}</option>
              ))}
            </select>
          </label>
          <label className="field">
            <span>Peer</span>
            <input value={routeForm.externalPeerId ?? ''} onChange={(event) => setRouteForm({ ...routeForm, externalPeerId: event.target.value })} placeholder="Node, chat, phone, or email" />
          </label>
          <label className="field">
            <span>Label</span>
            <input value={routeForm.displayName ?? ''} onChange={(event) => setRouteForm({ ...routeForm, displayName: event.target.value })} placeholder="Dispatch" />
          </label>
        </div>
        <label className="field">
          <span>Message template</span>
          <input value={routeForm.messageTemplate ?? ''} onChange={(event) => setRouteForm({ ...routeForm, messageTemplate: event.target.value })} placeholder="Optional, use {message} for the generated alert text" />
        </label>
        <div className="table-actions">
          <label className="check-row">
            <input type="checkbox" checked={routeForm.isEnabled} onChange={(event) => setRouteForm({ ...routeForm, isEnabled: event.target.checked })} />
            Enabled
          </label>
          <div className="compact-actions">
            {editingRouteId && (
              <button className="button button-secondary" onClick={() => setEditingRouteId(null)} type="button">Cancel</button>
            )}
            <button className="button button-primary" onClick={() => void saveRoute()} type="button">{editingRouteId ? 'Update route' : 'Add route'}</button>
          </div>
        </div>
        <div className="table-scroll">
          <table className="data-table">
            <thead>
              <tr>
                <th>Name</th>
                <th>Event</th>
                <th>Target</th>
                <th>Status</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              {routes.map((route) => (
                <tr key={route.id}>
                  <td>{route.name}</td>
                  <td>{route.eventType}</td>
                  <td>{route.integrationFeedName ?? route.provider} {route.externalPeerId ? `· ${route.externalPeerId}` : ''}</td>
                  <td>{route.isEnabled ? 'Enabled' : 'Paused'}</td>
                  <td>
                    <div className="compact-actions">
                      <button className="button button-secondary" onClick={() => editRoute(route)} type="button">Edit</button>
                      <button className="button button-secondary" onClick={() => void deleteAlertRoute(route.id).then(load)} type="button">Delete</button>
                    </div>
                  </td>
                </tr>
              ))}
              {routes.length === 0 && (
                <tr><td className="muted" colSpan={5}>No alert routes configured.</td></tr>
              )}
            </tbody>
          </table>
        </div>
      </details>

      <div className="card table-card">
        <div className="page-header">
          <h2>Speed Alerts</h2>
          {selectedSpeedAlerts.size > 0 && (
            <button className="button button-primary" onClick={() => void handleBulkAcknowledgeSpeedAlerts()} type="button">
              Acknowledge {selectedSpeedAlerts.size} selected
            </button>
          )}
        </div>
        <div className="alert-tabs">
          <button
            className={speedFilter === 'all' ? 'active' : ''}
            onClick={() => {
              setSpeedFilter('all')
              setAlertsPage(1)
            }}
            type="button"
          >
            All ({alertsTotal})
          </button>
          <button
            className={speedFilter === 'unacknowledged' ? 'active' : ''}
            onClick={() => {
              setSpeedFilter('unacknowledged')
              setAlertsPage(1)
            }}
            type="button"
          >
            Unacknowledged ({alerts.filter(a => !a.acknowledgedAtUtc).length})
          </button>
        </div>
        {alertViewMode === 'cards' ? (
        <div className="asset-grid">
          {alerts.map((alert) => (
            <article className="list-card" key={alert.id}>
              <header>
                <h3>{alert.assetName ?? alert.deviceIdentifier ?? alert.deviceId}</h3>
                <span className={alert.acknowledgedAtUtc ? 'badge badge-success' : 'badge badge-warning'}>{alert.acknowledgedAtUtc ? 'Acknowledged' : 'Open'}</span>
              </header>
              <div className="asset-meta">
                <div className="asset-meta-row"><span>Speed</span><strong>{alert.observedSpeedKmh.toFixed(1)} km/h</strong></div>
                <div className="asset-meta-row"><span>Threshold</span><strong>{alert.thresholdKmh.toFixed(1)} km/h</strong></div>
                <div className="asset-meta-row"><span>Triggered</span><strong>{new Date(alert.triggeredAt).toLocaleString()}</strong></div>
              </div>
              {!alert.acknowledgedAtUtc && (
                <button className="button button-secondary" onClick={() => void handleAcknowledgeAlert(alert.id)} type="button">Acknowledge</button>
              )}
            </article>
          ))}
          {alerts.length === 0 && <div className="card">No speed alerts available.</div>}
        </div>
        ) : (
        <table className="data-table">
          <thead>
            <tr>
              <th style={{ width: '40px' }}>
                {speedFilter === 'unacknowledged' && alerts.filter(a => !a.acknowledgedAtUtc).length > 0 && (
                  <input
                    type="checkbox"
                    checked={alerts.filter(a => !a.acknowledgedAtUtc).every(a => selectedSpeedAlerts.has(a.id))}
                    onChange={(e) => {
                      if (e.target.checked) {
                        setSelectedSpeedAlerts(new Set(alerts.filter(a => !a.acknowledgedAtUtc).map(a => a.id)))
                      } else {
                        setSelectedSpeedAlerts(new Set())
                      }
                    }}
                  />
                )}
              </th>
              <th>Device</th>
              <th>Speed (km/h)</th>
              <th>Threshold (km/h)</th>
              <th>Triggered At</th>
              <th>Status</th>
            </tr>
          </thead>
          <tbody>
            {alerts.map((alert) => (
              <tr key={alert.id}>
                <td>
                  {!alert.acknowledgedAtUtc && (
                    <input
                      type="checkbox"
                      checked={selectedSpeedAlerts.has(alert.id)}
                      onChange={() => toggleSpeedAlert(alert.id)}
                    />
                  )}
                </td>
                <td>{alert.assetName ?? alert.deviceIdentifier ?? alert.deviceId}</td>
                <td>{alert.observedSpeedKmh.toFixed(1)}</td>
                <td>{alert.thresholdKmh.toFixed(1)}</td>
                <td>{new Date(alert.triggeredAt).toLocaleString()}</td>
                <td>
                  {alert.acknowledgedAtUtc ? (
                    <span>
                      Acknowledged {new Date(alert.acknowledgedAtUtc).toLocaleString()}
                      {alert.acknowledgedBy ? ` by ${alert.acknowledgedBy}` : ''}
                    </span>
                  ) : (
                    <button className="button button-secondary" onClick={() => void handleAcknowledgeAlert(alert.id)} type="button">
                      Acknowledge
                    </button>
                  )}
                </td>
              </tr>
            ))}
            {alerts.length === 0 && (
              <tr>
                <td className="muted" colSpan={6}>
                  No speed alerts available.
                </td>
              </tr>
            )}
          </tbody>
        </table>
        )}
        <div className="table-actions">
          <span className="muted">
            Page {alertsPage} of {Math.ceil(alertsTotal / 50) || 1} (Total: {alertsTotal})
          </span>
          <div className="compact-actions">
            <button
              className="button button-secondary"
              onClick={() => setAlertsPage(Math.max(1, alertsPage - 1))}
              disabled={alertsPage === 1}
              type="button"
            >
              Previous
            </button>
            <button
              className="button button-secondary"
              onClick={() => setAlertsPage(alertsPage + 1)}
              disabled={alerts.length < 50}
              type="button"
            >
              Next
            </button>
          </div>
        </div>
      </div>

      <div className="card table-card">
        <div className="page-header">
          <h2>Geofence Breaches</h2>
          {selectedBreaches.size > 0 && (
            <button className="button button-primary" onClick={() => void handleBulkAcknowledgeBreaches()} type="button">
              Acknowledge {selectedBreaches.size} selected
            </button>
          )}
        </div>
        <div className="alert-tabs">
          <button
            className={breachFilter === 'all' ? 'active' : ''}
            onClick={() => {
              setBreachFilter('all')
              setBreachesPage(1)
            }}
            type="button"
          >
            All ({breachesTotal})
          </button>
          <button
            className={breachFilter === 'unacknowledged' ? 'active' : ''}
            onClick={() => {
              setBreachFilter('unacknowledged')
              setBreachesPage(1)
            }}
            type="button"
          >
            Unacknowledged ({breaches.filter(b => !b.acknowledgedAtUtc).length})
          </button>
        </div>
        {alertViewMode === 'cards' ? (
        <div className="asset-grid">
          {breaches.map((breach) => (
            <article className="list-card" key={breach.id}>
              <header>
                <h3>{breach.assetName ?? breach.deviceIdentifier ?? breach.deviceId}</h3>
                <span className={breach.acknowledgedAtUtc ? 'badge badge-success' : 'badge badge-warning'}>{breach.acknowledgedAtUtc ? 'Acknowledged' : 'Open'}</span>
              </header>
              <div className="asset-meta">
                <div className="asset-meta-row"><span>Geofence</span><strong>{breach.geofenceName}</strong></div>
                <div className="asset-meta-row"><span>Event</span><strong>{breach.eventType}</strong></div>
                <div className="asset-meta-row"><span>Detected</span><strong>{new Date(breach.detectedAt).toLocaleString()}</strong></div>
              </div>
              {!breach.acknowledgedAtUtc && (
                <button className="button button-secondary" onClick={() => void handleAcknowledgeBreach(breach.id)} type="button">Acknowledge</button>
              )}
            </article>
          ))}
          {breaches.length === 0 && <div className="card">No geofence breaches available.</div>}
        </div>
        ) : (
        <table className="data-table">
          <thead>
            <tr>
              <th style={{ width: '40px' }}>
                {breachFilter === 'unacknowledged' && breaches.filter(b => !b.acknowledgedAtUtc).length > 0 && (
                  <input
                    type="checkbox"
                    checked={breaches.filter(b => !b.acknowledgedAtUtc).every(b => selectedBreaches.has(b.id))}
                    onChange={(e) => {
                      if (e.target.checked) {
                        setSelectedBreaches(new Set(breaches.filter(b => !b.acknowledgedAtUtc).map(b => b.id)))
                      } else {
                        setSelectedBreaches(new Set())
                      }
                    }}
                  />
                )}
              </th>
              <th>Device</th>
              <th>Event</th>
              <th>Geofence</th>
              <th>Detected At</th>
              <th>Status</th>
            </tr>
          </thead>
          <tbody>
            {breaches.map((breach) => (
              <tr key={breach.id}>
                <td>
                  {!breach.acknowledgedAtUtc && (
                    <input
                      type="checkbox"
                      checked={selectedBreaches.has(breach.id)}
                      onChange={() => toggleBreach(breach.id)}
                    />
                  )}
                </td>
                <td>{breach.assetName ?? breach.deviceIdentifier ?? breach.deviceId}</td>
                <td>{breach.eventType}</td>
                <td>{breach.geofenceName}</td>
                <td>{new Date(breach.detectedAt).toLocaleString()}</td>
                <td>
                  {breach.acknowledgedAtUtc ? (
                    <span>
                      Acknowledged {new Date(breach.acknowledgedAtUtc).toLocaleString()}
                      {breach.acknowledgedBy ? ` by ${breach.acknowledgedBy}` : ''}
                    </span>
                  ) : (
                    <button className="button button-secondary" onClick={() => void handleAcknowledgeBreach(breach.id)} type="button">
                      Acknowledge
                    </button>
                  )}
                </td>
              </tr>
            ))}
            {breaches.length === 0 && (
              <tr>
                <td className="muted" colSpan={6}>
                  No geofence breaches available.
                </td>
              </tr>
            )}
          </tbody>
        </table>
        )}
        <div className="table-actions">
          <span className="muted">
            Page {breachesPage} of {Math.ceil(breachesTotal / 50) || 1} (Total: {breachesTotal})
          </span>
          <div className="compact-actions">
            <button
              className="button button-secondary"
              onClick={() => setBreachesPage(Math.max(1, breachesPage - 1))}
              disabled={breachesPage === 1}
              type="button"
            >
              Previous
            </button>
            <button
              className="button button-secondary"
              onClick={() => setBreachesPage(breachesPage + 1)}
              disabled={breaches.length < 50}
              type="button"
            >
              Next
            </button>
          </div>
        </div>
      </div>
    </div>
  )
}
