import { BrowserRouter, Routes, Route, NavLink } from 'react-router-dom'
import AssetsPage from './pages/AssetsPage'
import DevicesPage from './pages/DevicesPage'
import MapPage from './pages/MapPage'
import AlertsPage from './pages/AlertsPage'
import './styles.css'

export default function App() {
  return (
    <BrowserRouter>
      <nav className="app-nav">
        <NavLink to="/" end>Assets</NavLink>
        <NavLink to="/devices">Devices</NavLink>
        <NavLink to="/map">Map</NavLink>
        <NavLink to="/alerts">Alerts</NavLink>
      </nav>
      <main className="app-main">
        <Routes>
          <Route path="/" element={<AssetsPage />} />
          <Route path="/devices" element={<DevicesPage />} />
          <Route path="/map" element={<MapPage />} />
          <Route path="/alerts" element={<AlertsPage />} />
        </Routes>
      </main>
    </BrowserRouter>
  )
}
