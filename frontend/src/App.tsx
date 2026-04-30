import { BrowserRouter, Routes, Route, NavLink } from 'react-router-dom'
import { useEffect, useRef, useState } from 'react'
import { AssetsPage } from './pages/AssetsPage'
import DevicesPage from './pages/DevicesPage'
import MapPage from './pages/MapPage'
import AlertsPage from './pages/AlertsPage'
import GeofencesPage from './pages/GeofencesPage'
import HistoryPage from './pages/HistoryPage'
import WebhooksPage from './pages/WebhooksPage'
import SettingsPage from './pages/SettingsPage'
import { getAlertSummary } from './api/alerts'
import { useLiveEvents } from './hooks/useLiveEvents'
import './styles.css'

export default function App() {
  const [unacknowledgedCount, setUnacknowledgedCount] = useState(0)
  const pollRef = useRef<number | null>(null)

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
    <BrowserRouter>
      <nav className="app-nav">
        <NavLink to="/" end>Assets</NavLink>
        <NavLink to="/devices">Devices</NavLink>
        <NavLink to="/map">Map</NavLink>
        <NavLink to="/geofences">Geofences</NavLink>
        <NavLink to="/alerts">
          Alerts
          {unacknowledgedCount > 0 && <span className="nav-badge">{unacknowledgedCount}</span>}
        </NavLink>
        <NavLink to="/history">History</NavLink>
        <NavLink to="/webhooks">Webhooks</NavLink>
        <NavLink to="/settings">⚙ Settings</NavLink>
      </nav>
      <main className="app-main">
        <Routes>
          <Route path="/" element={<AssetsPage />} />
          <Route path="/devices" element={<DevicesPage />} />
          <Route path="/map" element={<MapPage />} />
          <Route path="/geofences" element={<GeofencesPage />} />
          <Route path="/alerts" element={<AlertsPage />} />
          <Route path="/history" element={<HistoryPage />} />
          <Route path="/webhooks" element={<WebhooksPage />} />
          <Route path="/settings" element={<SettingsPage />} />
        </Routes>
      </main>
    </BrowserRouter>
  )
}
