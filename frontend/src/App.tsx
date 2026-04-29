import { AssetsPage } from './pages/AssetsPage'

export default function App() {
  return (
    <main className="app-shell">
      <section className="hero">
        <p className="badge">Initial PoC</p>
        <h1>AssTrack asset telemetry dashboard</h1>
        <p>
          Track assets, inspect device telemetry, and validate the proof-of-concept API and data
          model foundation.
        </p>
      </section>

      <AssetsPage />
    </main>
  )
}
