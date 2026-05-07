import { BrowserRouter, Routes, Route, NavLink, useLocation } from 'react-router-dom'
import { useEffect, useRef, useState } from 'react'
import { AssetsPage } from './pages/AssetsPage'
import DevicesPage from './pages/DevicesPage'
import MapPage from './pages/MapPage'
import AlertsPage from './pages/AlertsPage'
import GeofencesPage from './pages/GeofencesPage'
import HistoryPage from './pages/HistoryPage'
import WebhooksPage from './pages/WebhooksPage'
import SettingsPage from './pages/SettingsPage'
import IntegrationsPage from './pages/IntegrationsPage'
import MessagesPage from './pages/MessagesPage'
import { getAlertSummary } from './api/alerts'
import { useLiveEvents } from './hooks/useLiveEvents'
import { IdentityProvider, useIdentityContext } from './context/IdentityContext'
import { AppearanceProvider } from './context/AppearanceContext'
import './styles.css'

function AppContent() {
  const [unacknowledgedCount, setUnacknowledgedCount] = useState(0)
  const pollRef = useRef<number | null>(null)
  const { isOperator } = useIdentityContext()
  const location = useLocation()
  const isMapRoute = location.pathname.startsWith('/map')

  async function loadSummary() {
    try {
      const summary = await getAlertSummary()
      setUnacknowledgedCount(summary.unacknowledgedSpeedAlerts + summary.unacknowledgedBreaches)
    } catch {
      // ignore errors
    }
  }

  useEffect(() => {
    void loadSummary()
    pollRef.current = window.setInterval(() => {
      void loadSummary()
    }, 30000)

    return () => {
      if (pollRef.current != null) {
        window.clearInterval(pollRef.current)
      }
    }
  }, [])

  useLiveEvents((type, _data) => {
    if (type === 'speed_alert' || type === 'geofence_breach') {
      void loadSummary()
    }
  })

  return (
    <>
      <a className="sr-only" href="#main-content">Skip to main content</a>
      <nav aria-label="Primary" className="app-nav">
        <div className="nav-inner">
          <NavLink className="brand-link" to="/" end>
            <span className="brand-mark">AT</span>
            <span>
              <strong>AssTrack</strong>
              <small>Asset operations</small>
            </span>
          </NavLink>
          <div className="nav-links">
            <NavLink to="/" end>Assets</NavLink>
            <NavLink to="/devices">Devices</NavLink>
            <NavLink to="/map">Map</NavLink>
            <NavLink to="/geofences">Geofences</NavLink>
            <NavLink to="/alerts">
              Alerts
              {unacknowledgedCount > 0 && <span className="nav-badge">{unacknowledgedCount}</span>}
            </NavLink>
            <NavLink to="/history">History</NavLink>
            <NavLink to="/messages">Messages</NavLink>
            {isOperator && <NavLink to="/integrations">Bridge</NavLink>}
            {isOperator && <NavLink to="/webhooks">Webhooks</NavLink>}
            <NavLink to="/settings">Settings</NavLink>
          </div>
        </div>
      </nav>
      <main className={`app-main${isMapRoute ? ' app-main-map' : ''}`} id="main-content">
        <Routes>
          <Route path="/" element={<AssetsPage />} />
          <Route path="/devices" element={<DevicesPage />} />
          <Route path="/map" element={<MapPage />} />
          <Route path="/geofences" element={<GeofencesPage />} />
          <Route path="/alerts" element={<AlertsPage />} />
          <Route path="/history" element={<HistoryPage />} />
          <Route path="/messages" element={<MessagesPage />} />
          <Route path="/integrations" element={<IntegrationsPage />} />
          <Route path="/webhooks" element={<WebhooksPage />} />
          <Route path="/settings" element={<SettingsPage />} />
        </Routes>
      </main>
    </>
  )
}

export default function App() {
  return (
    <BrowserRouter>
      <AppearanceProvider>
        <IdentityProvider>
          <AppContent />
        </IdentityProvider>
      </AppearanceProvider>
    </BrowserRouter>
  )
}
