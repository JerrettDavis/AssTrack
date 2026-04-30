# AssTrack

AssTrack is an asset tracking platform with a .NET minimal API backend, EF Core persistence, xUnit integration tests, and a React + TypeScript frontend with routing, a live map, and speed-alert views.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Node.js 18+](https://nodejs.org/) and npm

## Solution structure

| Project | Description |
|---|---|
| `src\AssTrack.Domain` | Domain models, DTOs, contracts, and the `SpeedAlertEvaluator` service |
| `src\AssTrack.Infrastructure` | EF Core SQLite context (`AssTrackDbContext`) and repository classes |
| `src\AssTrack.Api` | ASP.NET Core minimal-API endpoints, Swagger/OpenAPI, and DI composition |
| `tests\AssTrack.Tests` | xUnit + `WebApplicationFactory` integration tests |
| `frontend` | Vite + React 19 + TypeScript SPA with react-router-dom and Leaflet map |
| `docs\openapi` | Exported OpenAPI artefacts |

## Backend overview

The API is built with ASP.NET Core minimal APIs on .NET 10. The route prefix for all endpoints is `/api`.

### API endpoints

| Method | Route | Description |
|---|---|---|
| GET | `/api/assets` | List all assets (with linked devices) |
| POST | `/api/assets` | Create an asset |
| GET | `/api/devices` | List all devices |
| POST | `/api/devices` | Create a device (returns 409 if identifier already exists) |
| GET | `/api/observations` | Recent observations (last 100) |
| GET | `/api/observations/latest-positions` | Latest observation per device (live map feed) |
| GET | `/api/observations/latest/{deviceId}` | Latest observation for a specific device |
| POST | `/api/observations` | Ingest an observation (triggers speed alert if `speedKmh > 120`) |
| POST | `/api/observations/ingest` | Alias for the POST above |
| GET | `/api/speed-alerts` | Recent speed alerts |
| POST | `/api/speed-alerts/{id}/acknowledge` | Acknowledge a speed alert |
| GET | `/api/geofences` | List geofences |
| POST | `/api/geofences` | Create a geofence |
| GET | `/api/geofences/breaches` | Recent geofence breaches (last 100) |
| POST | `/api/geofences/breaches/{id}/acknowledge` | Acknowledge a geofence breach |
| PUT | `/api/assets/{id}` | Update an asset (returns 404 if not found) |
| DELETE | `/api/assets/{id}` | Delete an asset (returns 204 or 404) |
| GET | `/api/assets/{id}` | Get a single asset by ID |
| PUT | `/api/devices/{id}` | Update a device (returns 404 if not found) |
| DELETE | `/api/devices/{id}` | Delete a device (returns 204 or 404) |
| GET | `/api/devices/{id}` | Get a single device by ID |
| PUT | `/api/geofences/{id}` | Update a geofence (returns 404 if not found) |
| DELETE | `/api/geofences/{id}` | Delete a geofence (returns 204 or 404) |

Speed alerts are created automatically when an observation is ingested with `SpeedKmh` exceeding the asset's `SpeedThresholdKmh` (if set) or the default of 120 km/h.
Geofence breaches are recorded automatically when an ingested observation falls within any active geofence.

## Alert Management

The alert system provides comprehensive management of speed alerts and geofence breaches:

- **Filter Tabs**: Switch between "All" and "Unacknowledged" views for both speed alerts and geofence breaches
- **Bulk Acknowledge**: Select multiple alerts and acknowledge them in one action
- **Navigation Badge**: The Alerts nav link displays a red badge showing total unacknowledged alerts (speed + breach)
- **Map Staleness Indicators**: Device markers on the map show staleness:
  - Blue: Fresh (< 5 minutes)
  - Orange: Stale (5-30 minutes)
  - Gray: Very stale (> 30 minutes)

### API Endpoints

| Method | Route | Description |
|---|---|---|
| GET | `/api/speed-alerts?unacknowledged=true&limit=100&since=<ISO>` | Get speed alerts with filters |
| POST | `/api/speed-alerts/{id}/acknowledge` | Acknowledge a single speed alert |
| POST | `/api/speed-alerts/bulk-acknowledge` | Acknowledge multiple speed alerts |
| GET | `/api/geofences/breaches?unacknowledged=true&limit=100&since=<ISO>` | Get geofence breaches with filters |
| POST | `/api/geofences/breaches/bulk-acknowledge` | Acknowledge multiple breaches |
| GET | `/api/alerts/summary` | Get count of unacknowledged speed alerts and breaches |

## Alert Acknowledgement

Both speed alerts and geofence breaches support acknowledgement. Use the acknowledge endpoints to mark an alert as reviewed:

- `POST /api/speed-alerts/{id}/acknowledge` — body: `{ "acknowledgedBy": "operator-name" }`
- `POST /api/geofences/breaches/{id}/acknowledge` — body: `{ "acknowledgedBy": "operator-name" }`

The `acknowledgedAtUtc` and `acknowledgedBy` fields are returned in list responses so the UI can show acknowledgement status.

## API Authentication

The API supports optional key-based authentication via the `X-Api-Key` header.

Configure the key in `appsettings.json` or via environment variable:

```json
{
  "Auth": {
    "ApiKey": "your-secret-key"
  }
}
```

Or via environment variable: `Auth__ApiKey=your-secret-key`

When `Auth:ApiKey` is empty (default), all requests are allowed. When a key is configured, all `/api/*` endpoints require the header `X-Api-Key: <your-key>`. Health check endpoints (`/healthz/*` and `/api/health`) are always public.

## Frontend overview

The frontend is a single-page app built with Vite + React 19 + TypeScript. Dependencies include:

- **react-router-dom** – client-side routing
- **leaflet** + **react-leaflet** – interactive map on the Map page

### Pages / Routes

| Path | Component | Description |
|---|---|---|
| `/` | `AssetsPage` | Fleet overview – assets, metrics, and recent observations |
| `/devices` | `DevicesPage` | Table of all registered devices |
| `/map` | `MapPage` | Live Leaflet map showing the latest position of every device |
| `/alerts` | `AlertsPage` | Speed alerts and geofence breach alerts |

The Vite dev server runs on `http://localhost:5174` and proxies `/api` requests to `VITE_E2E_PROXY_TARGET` when set, otherwise `http://localhost:5019`.

Set `VITE_API_KEY` in a `.env.local` file (or environment) to send `X-Api-Key` with every request:

```
VITE_API_KEY=your-secret-key
```

## Local startup

### Backend

```powershell
dotnet restore
cd src\AssTrack.Api
dotnet run
```

Swagger UI is available at `http://localhost:5019/swagger`.

### Frontend

```powershell
cd frontend
npm install
npm run dev
```

The dev server starts on `http://localhost:5174` and proxies `/api` to the backend at `http://localhost:5019` by default.

## Docker / containerised startup

> Prerequisites: Docker Desktop (or Docker Engine + Compose plugin).

```bash
# Copy and optionally customise the env file
cp .env.example .env

# Build and start the full stack
docker compose up --build

# API: http://localhost:5019
# Frontend: http://localhost:5174
# Swagger: http://localhost:5019/swagger
```

To set an API key:
```bash
echo "ASSTRACK_API_KEY=mysecretkey" > .env
docker compose up --build
```

The SQLite database is persisted in the `asstrack-data` Docker volume. To reset:
```bash
docker compose down -v
```

## Sample observation ingest flow

```powershell
# 1. Create an asset
$asset = Invoke-RestMethod -Method Post http://localhost:5019/api/assets `
  -ContentType application/json `
  -Body '{"name":"Fleet Van 1","description":"Primary vehicle","category":"Vehicle"}'

# 2. Create a device linked to the asset
$device = Invoke-RestMethod -Method Post http://localhost:5019/api/devices `
  -ContentType application/json `
  -Body "{`"identifier`":`"VAN-001`",`"label`":`"Van 1 GPS`",`"protocol`":`"https`",`"assetId`":`"$($asset.id)`"}"

# 3. Post a telemetry observation
Invoke-RestMethod -Method Post http://localhost:5019/api/observations `
  -ContentType application/json `
  -Body "{`"deviceId`":`"$($device.id)`",`"observedAt`":`"$(Get-Date -Format o)`",`"latitude`":51.5074,`"longitude`":-0.1278,`"speedKmh`":85}"

# 4. Fetch the latest position for all devices
Invoke-RestMethod http://localhost:5019/api/observations/latest-positions
```

## Validation commands

```powershell
# Backend build
dotnet build C:\git\AssTrack

# Backend tests (all should pass)
dotnet test C:\git\AssTrack

# Frontend build
cd frontend
npm run build
```

## End-to-end tests (Reqnroll + Playwright)

Full-stack E2E tests that start the real backend and Vite dev server, seed data via the API, and drive a headless Chromium browser.

| | |
|---|---|
| **Test runner** | xUnit via Reqnroll (BDD scenarios in `tests\AssTrack.E2ETests\Features\CoreFlows.feature`) |
| **Browser automation** | Playwright .NET — headless Chromium |
| **Backend** | Starts on `http://localhost:5099` with a temporary isolated SQLite DB |
| **Frontend** | Vite dev server on `http://localhost:5174`, proxying `/api` to port 5099 |
| **Data setup** | Seeded via direct HTTP calls in step definitions (no UI forms required) |

### Scenarios covered

1. **App loads** — navigate to `/`, verify "Fleet overview" heading
2. **Asset appears in UI** — seed asset via API, navigate to `/`, verify asset name visible
3. **Device appears in UI** — seed device via API, navigate to `/devices`, verify row
4. **Live map renders** — seed observation, navigate to `/map`, verify "Live Map" heading
5. **Speed alert in UI** — seed observation at 150 km/h, navigate to `/alerts`, verify "150.0" in table

### Run command

```powershell
cd C:\git\AssTrack
# Ensure frontend deps are installed
npm install --prefix frontend
# Build the API (needed by --no-build in test startup)
dotnet build src\AssTrack.Api
# Run E2E tests (auto-installs Chromium on first run)
dotnet test tests\AssTrack.E2ETests
```

Or use the helper script (Windows PowerShell):

```powershell
cd C:\git\AssTrack
.\tests\run-e2e.ps1
```

## CI

GitHub Actions runs on every push and pull request:

| Job | What it does |
|---|---|
| `backend` | `dotnet restore` → `dotnet build` → `dotnet test` (unit + integration) |
| `frontend` | `npm ci` → `npm run build` |
| `docker-build` | Builds both Docker images to validate Dockerfiles |
| `e2e` | Starts real backend + Vite dev server, runs Reqnroll + Playwright scenarios headlessly |

The E2E job runs on `ubuntu-latest` after `backend` and `frontend` succeed. Playwright Chromium browsers are cached by browser version. You can also run E2E tests locally on Windows with `.\tests\run-e2e.ps1`.

## Capabilities

- Full CRUD (create, read, update, delete) for assets, devices, and geofences
- Ingest telemetry observations (lat/lon, altitude, speed, heading, metadata)
- Automatic speed alerts triggered above the per-asset `SpeedThresholdKmh` (nullable; defaults to 120 km/h if not set)
- Automatic geofence breach records when an observation lands inside an active circular geofence
- Alert acknowledgement for both speed alerts and geofence breaches (`POST .../acknowledge`)
- Latest-position feed per device for live-map consumption
- API key authentication (`X-Api-Key` header); empty key = open access for dev environments
- Browse all contracts via Swagger/OpenAPI at `/swagger`

## Health & readiness endpoints

| Endpoint | Purpose |
|---|---|
| `GET /healthz` | Overall health (ASP.NET HealthChecks JSON) |
| `GET /healthz/live` | Liveness probe – always 200 |
| `GET /healthz/ready` | Readiness probe – checks DB |
| `GET /api/health` | Legacy health + DB connectivity probe |

## Production configuration

Copy `appsettings.Production.json` and set the following environment variables or override in your deployment:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=data/asstrack.db"
  },
  "Cors": {
    "AllowedOrigins": ["https://your-frontend.example.com"]
  },
  "Auth": {
    "ApiKey": "your-production-api-key"
  }
}
```

Or via environment variables:
```
Auth__ApiKey=your-production-api-key
Cors__AllowedOrigins__0=https://your-frontend.example.com
```

### CORS (required in production)

**CORS is required in production.** If `Cors:AllowedOrigins` is empty or unset when the environment is `Production`, startup will fail with:

> `InvalidOperationException: Cors:AllowedOrigins must be configured in Production.`

Set at least one allowed origin via:
- `appsettings.Production.json` → `"Cors": { "AllowedOrigins": ["https://your-frontend.example.com"] }`
- Environment variable: `Cors__AllowedOrigins__0=https://your-frontend.example.com`

### Swagger

Swagger/OpenAPI is **disabled by default in production**. To enable it (e.g. for internal tooling environments):

```
Swagger__Enabled=true
```

Or in `appsettings.Production.json`:
```json
{ "Swagger": { "Enabled": true } }
```

In Development, Swagger is always enabled regardless of the config flag.

Run migrations on startup automatically (already configured via `Migrate()`).
