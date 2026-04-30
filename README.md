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
| GET | `/api/devices/{id}` | Get a single device by ID |
| GET | `/api/devices/{id}/summary` | Get device summary with latest observation and alert counts |
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
| PUT | `/api/geofences/{id}` | Update a geofence (returns 404 if not found) |
| DELETE | `/api/geofences/{id}` | Delete a geofence (returns 204 or 404) |

Speed alerts are created automatically when an observation is ingested with `SpeedKmh` exceeding the asset's `SpeedThresholdKmh` (if set) or the default of 120 km/h.
Geofence breaches are recorded automatically when an ingested observation falls within any active geofence.

## Observation History & Export

### GET /api/observations/history

Retrieve paginated observation history with optional filtering by device, asset, or date range. Supports both JSON and CSV export formats.

#### Query Parameters

| Parameter | Type | Optional | Default | Description |
|---|---|---|---|---|
| `deviceId` | Guid | Yes | — | Filter observations by a specific device |
| `assetId` | Guid | Yes | — | Filter observations by a specific asset |
| `from` | ISO 8601 | Yes | — | Start date/time for filtering (e.g., `2025-06-01T00:00:00Z`) |
| `to` | ISO 8601 | Yes | — | End date/time for filtering (e.g., `2025-06-30T23:59:59Z`) |
| `page` | int | Yes | 1 | Page number for pagination |
| `pageSize` | int | Yes | 50 | Number of items per page |
| `format` | string | Yes | json | Response format: `json` or `csv` |

#### Response

**JSON (default):**
```json
{
  "items": [
    {
      "id": "550e8400-e29b-41d4-a716-446655440000",
      "deviceId": "c3a12345-b9c2-4d85-a78e-9f1d2e3c4b5a",
      "assetId": "b2f91234-a1b2-3c4d-5e6f-7a8b9c0d1e2f",
      "observedAt": "2025-06-15T14:30:00Z",
      "latitude": 51.5074,
      "longitude": -0.1278,
      "altitude": 45.2,
      "speedKmh": 85.5,
      "heading": 270,
      "metadata": {}
    }
  ],
  "totalCount": 1250,
  "page": 1,
  "pageSize": 50
}
```

**CSV (`format=csv`):**
```
ObservationId,DeviceId,AssetId,ObservedAt,Latitude,Longitude,Altitude,SpeedKmh,Heading
550e8400-e29b-41d4-a716-446655440000,c3a12345-b9c2-4d85-a78e-9f1d2e3c4b5a,b2f91234-a1b2-3c4d-5e6f-7a8b9c0d1e2f,2025-06-15T14:30:00Z,51.5074,-0.1278,45.2,85.5,270
```

#### Examples

Get all observations for a device (page 1, 50 items):
```
GET /api/observations/history?deviceId=c3a12345-b9c2-4d85-a78e-9f1d2e3c4b5a
```

Get observations for an asset in June 2025:
```
GET /api/observations/history?assetId=b2f91234-a1b2-3c4d-5e6f-7a8b9c0d1e2f&from=2025-06-01T00:00:00Z&to=2025-06-30T23:59:59Z
```

Export observations to CSV with custom page size:
```
GET /api/observations/history?deviceId=c3a12345-b9c2-4d85-a78e-9f1d2e3c4b5a&pageSize=100&format=csv
```

### CSV Export Behavior

CSV export is available on the following endpoints:
- `GET /api/observations/history`
- `GET /api/speed-alerts`
- `GET /api/geofences/breaches`

#### Requirements

- **Filter required**: At least one filter parameter must be provided (e.g., `deviceId`, `assetId`, `from`, `to`). Requests without any filters return HTTP 422 Unprocessable Entity.
- **Response header**: `Content-Type: text/csv`

#### Export Limits

| Endpoint | Max rows |
|---|---|
| `/api/observations/history` | 5000 |
| `/api/speed-alerts` | No limit |
| `/api/geofences/breaches` | No limit |

Requests exceeding the observation limit are automatically capped at 5000 rows.

#### Error Handling

If no filter parameters are provided when requesting CSV format:

```
HTTP 422 Unprocessable Entity

{
  "message": "At least one filter parameter must be provided for CSV export."
}
```

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

## Map Features

The live map provides real-time asset tracking and situational awareness:

- **Live Device Positions**: View the latest position of all devices with color-coded staleness indicators (blue for fresh, orange for stale, gray for very stale)
- **Device Selection**: Choose a device from the trail selector to focus on its recent movement
- **Trail Visualization**: Render 20, 50, or 100 recent points as an on-map trail polyline
- **Device Health Panel**: When a device is selected, display last seen time, latest speed, heading, and counts of unacknowledged alerts
- **Geofence Overlay**: Active geofence zones are rendered as blue circles on the map
- **Real-time Updates**: Map positions, selected trails, and device health refresh every 30 seconds

## Webhook Alert Delivery

AssTrack can deliver alert events to an external HTTP endpoint the moment they are created, giving operators immediate integration value with their own tooling (Slack, PagerDuty, IFTTT, custom dashboards, etc.) without polling the API.

### Configuration

Set `Webhooks:Url` to your target endpoint. Leave it empty (the default) to disable webhook delivery entirely — there is no impact on ingest when unconfigured.

**appsettings.json / environment variables:**

```json
{
  "Webhooks": {
    "Url": "https://hooks.yourcompany.example/asstrack",
    "TimeoutSeconds": 10
  }
}
```

Or via environment variables:
```
Webhooks__Url=https://hooks.yourcompany.example/asstrack
Webhooks__TimeoutSeconds=10
```

### Events delivered

| Event | `eventType` field | Trigger |
|---|---|---|
| Speed alert created | `speed_alert` | Observation ingested with speed exceeding asset/default threshold |
| Geofence breach created | `geofence_breach` | Observation lands inside or exits an active geofence |

### Payload – `speed_alert`

```json
{
  "eventType": "speed_alert",
  "alertId": "3fa85f64-...",
  "deviceId": "c3a12...",
  "deviceIdentifier": "VAN-001",
  "assetId": "b2f91...",
  "assetName": "Fleet Van 1",
  "observedSpeedKmh": 148.3,
  "thresholdKmh": 120.0,
  "triggeredAt": "2025-06-01T14:22:00Z",
  "deliveredAt": "2025-06-01T14:22:00.123Z"
}
```

### Payload – `geofence_breach`

```json
{
  "eventType": "geofence_breach",
  "breachId": "7ab34...",
  "deviceId": "c3a12...",
  "deviceIdentifier": "VAN-001",
  "assetId": "b2f91...",
  "assetName": "Fleet Van 1",
  "geofenceId": "88cd1...",
  "geofenceName": "Depot Zone",
  "breachEventType": "Enter",
  "detectedAt": "2025-06-01T14:22:05Z",
  "deliveredAt": "2025-06-01T14:22:05.210Z"
}
```

`breachEventType` is either `"Enter"` or `"Exit"`.

### Delivery semantics

- All payloads are sent as `HTTP POST` with `Content-Type: application/json`.
- Failures (non-2xx responses, timeouts, network errors) are **logged and silently dropped** — they never fail the ingest request.
- No retries or queuing. If reliable delivery is required, point the URL at a durable intermediary (e.g. a message queue webhook adapter).
- Every delivery attempt (success or failure) is persisted as a `WebhookDeliveryLog` row for observability.

## Webhook Delivery Logs

Every outbound webhook attempt — whether successful or not — is recorded in a standalone delivery log table.  Logs are never FK-constrained to alert or breach records, so they are safe to query even after data has been deleted.

### GET /api/webhooks/deliveries

Retrieve paginated delivery logs with optional filtering.

#### Query Parameters

| Parameter | Type | Optional | Default | Description |
|---|---|---|---|---|
| `success` | bool | Yes | — | Filter by delivery outcome (`true` = succeeded, `false` = failed) |
| `eventType` | string | Yes | — | Filter by event type, e.g. `speed_alert` or `geofence_breach` |
| `since` | ISO 8601 | Yes | — | Only return attempts on or after this timestamp |
| `page` | int | Yes | 1 | Page number |
| `pageSize` | int | Yes | 50 | Page size (max 200) |

#### Example

```
GET /api/webhooks/deliveries?success=false&eventType=speed_alert&page=1&pageSize=50
```

#### Response

```json
{
  "items": [
    {
      "id": 1,
      "attemptedAt": "2025-06-15T14:30:00Z",
      "eventType": "speed_alert",
      "targetUrl": "https://hooks.yourcompany.example/asstrack",
      "success": false,
      "httpStatusCode": 503,
      "durationMs": 4820,
      "errorMessage": "HTTP 503",
      "requestPayloadSummary": "{\"eventType\":\"speed_alert\", ...}"
    }
  ],
  "totalCount": 1,
  "page": 1,
  "pageSize": 50
}
```

### GET /api/webhooks/status

Returns webhook configuration state and 24-hour delivery statistics.

#### Response

```json
{
  "configured": true,
  "last24hDeliveries": 42,
  "last24hFailures": 3,
  "lastDeliveredAt": "2025-06-15T14:30:00Z",
  "avgDurationMs": 312.5
}
```

| Field | Description |
|---|---|
| `configured` | `true` when `Webhooks:Url` is set |
| `last24hDeliveries` | Total attempts in the last 24 hours |
| `last24hFailures` | Failed attempts (non-2xx or exception) in the last 24 hours |
| `lastDeliveredAt` | Timestamp of the most recent attempt (any outcome) |
| `avgDurationMs` | Average response time (ms) over the last 24 hours |



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
- **Out-of-band webhook delivery** on speed alert and geofence breach creation (opt-in via `Webhooks:Url`)
- Alert acknowledgement for both speed alerts and geofence breaches (`POST .../acknowledge`)
- Latest-position feed per device for live-map consumption
- API key authentication (`X-Api-Key` header); empty key = open access for dev environments
- Browse all contracts via Swagger/OpenAPI at `/swagger`

## Ingest Hardening

- Idempotency prevents duplicate observation rows by returning the existing observation when the same device and `ObservedAt` timestamp are ingested again.
- Out-of-order protection ignores older observations when a newer geofence state has already been recorded for the same device/geofence pair.
- Rate limiting applies a fixed-window `ingest` policy to both observation POST routes to return HTTP 429 when the configured ingest threshold is exceeded.

### Edit & Reassign

- Edit asset details (name, description, category, speed threshold) inline on the Assets page
- Edit device details inline and reassign a device to any asset from the Devices page
- Edit geofence properties inline including name, coordinates, radius, and active state on the Geofences page

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
