# AssTrack

[![CI](https://github.com/JerrettDavis/AssTrack/actions/workflows/ci.yml/badge.svg)](https://github.com/JerrettDavis/AssTrack/actions/workflows/ci.yml)
[![CodeQL](https://github.com/JerrettDavis/AssTrack/actions/workflows/codeql.yml/badge.svg)](https://github.com/JerrettDavis/AssTrack/actions/workflows/codeql.yml)
[![Docs](https://github.com/JerrettDavis/AssTrack/actions/workflows/docs.yml/badge.svg)](https://github.com/JerrettDavis/AssTrack/actions/workflows/docs.yml)
[![codecov](https://codecov.io/gh/JerrettDavis/AssTrack/branch/master/graph/badge.svg)](https://codecov.io/gh/JerrettDavis/AssTrack)

AssTrack is an asset tracking platform with a .NET minimal API backend, EF Core persistence, xUnit integration tests, and a React + TypeScript frontend with routing, a live map, and speed-alert views.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Node.js 18+](https://nodejs.org/) and npm

## Releases

AssTrack ships through `.github\workflows\release.yml`. The release pipeline derives the next SemVer version from conventional commits on `master`, bootstraps the first public release as `0.1.0`, validates release artifacts, and only then publishes a GitHub release.

### Release outputs

The release workflow publishes:

- Portable self-contained ZIP packages for the integrated `AssTrack.Api` app on `win-x64`, `linux-x64`, `linux-arm64`, `osx-x64`, and `osx-arm64`
- Portable self-contained ZIP packages for `AssTrack.BridgeGateway`, `AssTrack.SignalWorker`, and `AssTrack.TelegramWorker` on `win-x64` and `linux-x64`
- A Windows x64 MSI installer for the integrated app
- GHCR container images for `asstrack-api`, `asstrack-frontend`, and `asstrack-bridge-gateway`
- A release bundle containing `docker-compose.release.yml`, `docker-compose.release.env.example`, `.env.example`, the README, and the exported OpenAPI document
- `SHA256SUMS.txt` for all uploaded release assets

### Release validation

Before publishing, the workflow runs:

1. Backend tests
2. Frontend production build
3. Managed E2E, AppHost E2E, and production-publish E2E suites
4. Docker Compose smoke validation against release-tagged images
5. Windows MSI install validation that launches the installed app and runs the E2E suite against it

Use conventional commits (`feat:`, `fix:`, `perf:`, and `BREAKING CHANGE:` footers) to control release bumps. Non-releasable commits keep the workflow idle after planning. You can trigger the workflow manually with GitHub Actions `workflow_dispatch`, but pushes to `master` also evaluate whether a release should be cut.

## Solution structure

| Project | Description |
|---|---|
| `src\AssTrack.Domain` | Domain models, DTOs, contracts, and the `SpeedAlertEvaluator` service |
| `src\AssTrack.Infrastructure` | EF Core SQLite context (`AssTrackDbContext`) and repository classes |
| `src\AssTrack.Api` | ASP.NET Core minimal-API endpoints, Swagger/OpenAPI, and DI composition |
| `src\AssTrack.BridgeGateway` | Pluggable provider bridge gateway for native webhook/import payloads |
| `src\AssTrack.AppHost` | .NET Aspire 13.2 AppHost for running the API, bridge gateway, and frontend together |
| `tests\AssTrack.Tests` | xUnit + `WebApplicationFactory` integration tests |
| `frontend` | Vite + React 19 + TypeScript SPA with react-router-dom and Leaflet map |
| `docs\openapi` | Exported OpenAPI artefacts |
| `docs\integrations.md` | Provider integration playbook and bridge implementation checklist |

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
| GET | `/api/reports/utilization` | Date-bounded utilization report with distance, moving time, idle time, stops, and max speed by device |
| POST | `/api/observations` | Ingest an observation (triggers speed alert if `speedKmh > 120`) |
| POST | `/api/observations/ingest` | Alias for the POST above |
| GET | `/api/integrations/providers` | List supported integration provider profiles |
| GET | `/api/integrations` | List configured integration feeds |
| POST | `/api/integrations` | Create a configurable integration feed |
| PUT | `/api/integrations/{id}` | Update an integration feed |
| DELETE | `/api/integrations/{id}` | Delete an integration feed |
| POST | `/api/integrations/{id}/observations` | Ingest a normalized provider observation for a feed |
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

## Live Events Authentication

The `/api/events` Server-Sent Events (SSE) endpoint delivers real-time observations, speed alerts, and geofence breaches. To establish a connection, clients must follow a two-step token-based authentication flow:

1. **Issue a token** via `POST /api/events/token` with `X-Api-Key` header
   - Returns `{ "token": "...", "expiresAt": "2025-06-15T14:40:00Z" }`
   - Token TTL: 10 minutes (configurable via `SseToken:TtlMinutes` setting)

2. **Connect to SSE** via `GET /api/events?token=<your-token>`
   - Token must be passed as a query parameter
   - Connection persists until token expires or connection closes

This approach is more secure than passing API keys in the URL query string, as tokens are:
- Short-lived (10 minute default TTL)
- Opaque (cannot be reverse-engineered)
- Independent of long-lived API keys

The frontend handles token management automatically: it fetches a fresh token on startup and reconnects gracefully if the token expires.

## Key Separation (Migration)

AssTrack supports API-key based RBAC with explicit roles and an access tier claim:

| Key | Environment Variable | Role(s) | Purpose |
|-----|---------------------|---------|---------|
| Admin key | `ASSTRACK_ADMIN_API_KEY` | `admin`, `operator`, `viewer`, `ingest` | Enterprise administration and destructive maintenance |
| Operator key | `ASSTRACK_API_KEY` | `operator`, `viewer`, `ingest` | Day-to-day control-plane access |
| Ingest key | `ASSTRACK_INGEST_API_KEY` | `ingest` | Device POST-only access |

### Backward Compatibility

If `ASSTRACK_ADMIN_API_KEY` is not set, the operator key (`ASSTRACK_API_KEY`) also receives the `admin` role so existing deployments keep working. If `ASSTRACK_INGEST_API_KEY` is not set, the operator key continues to work for ingest endpoints.

Set `ASSTRACK_ACCESS_TIER` to `community`, `professional`, or `enterprise` to expose the deployment tier through `/api/auth/me` and `/api/system/status`. The default is `enterprise`.

### Upgrading to Key Separation

1. Generate a new ingest key:
   ```sh
   openssl rand -hex 32
   ```
2. Set `ASSTRACK_INGEST_API_KEY` in your `.env` or environment:
   ```
   ASSTRACK_INGEST_API_KEY=<your-new-ingest-key>
   ```
3. Update device firmware, ingestion clients, and integration bridge workers to use the new ingest key in the `X-Api-Key` header.
4. Generate and set `ASSTRACK_ADMIN_API_KEY` before enforcing separate enterprise administration.
5. The ingest key is restricted to direct observation ingest and integration-feed observation ingest.

### Policy Summary

| Endpoint | Required Policy | Allowed Keys |
|----------|----------------|--------------|
| `POST /api/observations` | `Ingest` | operator key, ingest key |
| `POST /api/observations/ingest` | `Ingest` | operator key, ingest key |
| `POST /api/integrations/{id}/observations` | `Ingest` | operator key, ingest key |
| `POST /api/system/maintenance/*`, `DELETE /api/system/maintenance/e2e-data` | `Admin` | admin key, or operator key when no admin key is configured |
| `GET /api/integrations/providers` | `Operator` | operator key only |
| `GET\|POST\|PUT\|DELETE /api/integrations*` | `Operator` | operator key only |
| `GET /api/system/status` | `Operator` | operator key only |
| `POST /api/events/token` | `Operator` | operator key only |
| All other `/api/*` endpoints | `Operator` | operator key only |

## Location Integrations

AssTrack supports configurable location integration feeds for providers such as generic webhooks, GPS/cellular HTTP trackers, Meshtastic, Apple Find My/AirTag bridges, Google Find Hub bridges, Samsung SmartThings Find bridges, OwnTracks, and Traccar.

The AssTrack-side contract is intentionally normalized: each bridge posts a location observation with an external tracker ID, timestamp, coordinates, optional telemetry, and optional tags. Devices can be auto-created from feeds and multiple devices can be linked to one asset.

The repo also includes `src\AssTrack.BridgeGateway`, a standalone bridge gateway that accepts provider-native JSON on `/bridge/{feedKey}`, normalizes it through pluggable adapters, and posts it into `POST /api/integrations/{feedId}/observations`.

See [`docs/integrations.md`](docs/integrations.md) for provider playbooks, payload examples, bridge worker requirements, and the remaining third-party plumbing steps.

### UI Access Control

The frontend adapts its interface based on the authenticated key's roles:

| Role | UI Behaviour |
|------|-------------|
| `admin` | Includes operator capabilities plus destructive maintenance and access administration capabilities |
| `operator` | Full access: all nav items visible, Settings and Webhooks pages accessible, delete buttons shown on Assets/Devices/Geofences |
| `ingest` | Restricted access: Settings and Webhooks nav links hidden; those pages show an access-denied message if navigated to directly; delete buttons hidden on list pages |

The frontend queries `GET /api/auth/me` on startup to determine roles, access tier, and capability flags. If the request fails, the UI defaults to the most restrictive behaviour.

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
- **Real-time Updates**: Map positions update live via SSE (`observation` events). Trails and device health refresh automatically when the selected device receives an event. A 30-second polling interval acts as a reconcile fallback.

## Webhook Alert Delivery

AssTrack can deliver alert events to an external HTTP endpoint the moment they are created, giving operators immediate integration value with their own tooling (Slack, PagerDuty, IFTTT, custom dashboards, etc.) without polling the API.

### Configuration

Set `Webhooks:Url` to your target endpoint. Leave it empty (the default) to disable webhook delivery entirely — there is no impact on ingest when unconfigured.

**appsettings.json / environment variables:**

```json
{
  "Webhooks": {
    "Url": "https://hooks.yourcompany.example/asstrack",
    "TimeoutSeconds": 10,
    "MaxRetries": 3,
    "RetryBaseDelayMs": 1000,
    "RetryMaxDelayMs": 30000,
    "SigningSecret": "optional-hmac-secret"
  }
}
```

Or via environment variables:
```
Webhooks__Url=https://hooks.yourcompany.example/asstrack
Webhooks__TimeoutSeconds=10
Webhooks__MaxRetries=3
Webhooks__RetryBaseDelayMs=1000
Webhooks__RetryMaxDelayMs=30000
Webhooks__SigningSecret=optional-hmac-secret
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

## Settings & System Status

The frontend includes a **Settings** page at `/settings` for operators who need a safe view of environment-level configuration and a built-in simulation runner.

### Authenticated status endpoint

`GET /api/system/status` returns a sanitized configuration snapshot for authenticated clients only. It reports:

- Current environment name
- Whether simulation is enabled
- Whether webhook delivery is configured
- Whether an API key is configured
- Whether Swagger is enabled
- Ingest rate-limit settings
- Database provider detection

This endpoint intentionally avoids returning secrets or raw connection strings.

### Simulation runner

When simulation is enabled, the Settings page can trigger the existing `POST /api/observations/simulate` workflow with these presets:

- `NormalRoute`
- `SpeedViolation`
- `GeofenceEntryExit`

Operators can optionally provide a device identifier, inspect the result summary, and expand the event log returned by the API.

### Delivery semantics

- All payloads are sent as `HTTP POST` with `Content-Type: application/json`.
- Failures (non-2xx responses, timeouts, network errors) are logged and never fail the ingest request.
- Retryable failures (`429`, `500`, `502`, `503`, `504`, and network timeouts) are queued in memory and retried with exponential backoff when `Webhooks:MaxRetries` is greater than `0`.
- Retry queues are best-effort and in-memory. They are suitable for demos and single-instance deployments, but production environments that require guaranteed delivery should point `Webhooks:Url` at a durable intermediary.
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
      "requestPayloadSummary": "{\"eventType\":\"speed_alert\", ...}",
      "attemptNumber": 2,
      "correlationId": "e66125f7-62dd-491d-bd79-4f90820c3953"
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
  "avgDurationMs": 312.5,
  "retryQueueDepth": 0,
  "signingEnabled": true
}
```

| Field | Description |
|---|---|
| `configured` | `true` when `Webhooks:Url` is set |
| `last24hDeliveries` | Total attempts in the last 24 hours |
| `last24hFailures` | Failed attempts (non-2xx or exception) in the last 24 hours |
| `lastDeliveredAt` | Timestamp of the most recent attempt (any outcome) |
| `avgDurationMs` | Average response time (ms) over the last 24 hours |
| `retryQueueDepth` | Number of in-memory retry jobs waiting to run |
| `signingEnabled` | `true` when `Webhooks:SigningSecret` is set |

### POST /api/webhooks/test

Fire a synthetic test webhook event to verify your delivery configuration without requiring live data.

#### Request body (optional)

```json
{ "eventType": "speed_alert" }
```

| Field | Values | Default |
|---|---|---|
| `eventType` | `speed_alert`, `geofence_breach` | `speed_alert` |

#### Response

```json
{
  "fired": true,
  "eventType": "speed_alert",
  "configured": true,
  "message": "Test webhook event sent. Check delivery logs for outcome."
}
```

When `configured` is `false` (no `Webhooks:Url` set), the synthetic payload is still processed internally but no outbound HTTP request is made.

#### Operator workflow

1. Set `Webhooks:Url` in your configuration (see [Configuration](#configuration) above).
2. Navigate to **Settings → Webhooks** (`/webhooks`) in the AssTrack UI.
3. Click **Send test event** — this calls `POST /api/webhooks/test`.
4. Check the delivery log table on the same page (populated from `GET /api/webhooks/deliveries`) to confirm delivery outcome.



- `POST /api/speed-alerts/{id}/acknowledge` — body: `{ "acknowledgedBy": "operator-name" }`
- `POST /api/geofences/breaches/{id}/acknowledge` — body: `{ "acknowledgedBy": "operator-name" }`

The `acknowledgedAtUtc` and `acknowledgedBy` fields are returned in list responses so the UI can show acknowledgement status.

## Live Events (SSE)

AssTrack streams real-time telemetry events to browser clients using Server-Sent Events (SSE). The frontend connects once and receives push notifications for new observations, speed alerts, and geofence breaches — no polling required for these events.

### Authentication — token exchange flow

Because `EventSource` cannot send custom headers, a **short-lived SSE token** is used instead of exposing the master API key in the URL.

#### Step 1 — issue a token

```http
POST /api/events/token
X-Api-Key: <master-api-key>
```

Response:

```json
{
  "token": "abc123...",
  "expiresAt": "2025-06-15T14:40:00Z"
}
```

Tokens are cryptographically random, URL-safe strings. They expire after **10 minutes** by default (configurable via `SseToken:TtlMinutes`). Tokens are stored in memory only — they are not persisted to the database.

#### Step 2 — connect with the token

```
GET /api/events?token=<short-lived-token>
```

This endpoint is unauthenticated at the transport layer; access is gated solely by the short-lived token. Requests without a valid `?token=` parameter receive `401 Unauthorized`.

The master API key is **never** exposed in SSE connection URLs.

#### Frontend behaviour

The built-in SSE client (`sseClient.ts`) handles the token exchange transparently:
1. On first subscription, calls `POST /api/events/token` with `X-Api-Key` in the request header.
2. Connects `EventSource` with `?token=` in the URL.
3. On disconnection or reconnect, fetches a fresh token.

When no API key is configured (development), the token endpoint still requires auth but the permissive dev-mode allows the exchange to succeed.

### GET /api/events

Long-lived `text/event-stream` connection. On connect, the server sends a `: connected` keepalive comment, then streams events as they occur.

#### Event types

| SSE event name | Trigger | Key fields |
|---|---|---|
| `observation` | New telemetry observation ingested | `id`, `deviceId`, `assetId`, `latitude`, `longitude`, `speedKmh`, `observedAt` |
| `speed_alert` | Speed threshold exceeded | `id`, `deviceId`, `assetId`, `observedSpeedKmh`, `thresholdKmh`, `triggeredAt` |
| `geofence_breach` | Device enters or exits a geofence | `id`, `deviceId`, `assetId`, `geofenceId`, `eventType` (`Enter`/`Exit`), `detectedAt` |

#### Example stream

```
: connected

event: observation
data: {"id":"...","deviceId":"...","latitude":51.5074,"longitude":-0.1278,"speedKmh":85,"observedAt":"2025-06-15T14:30:00Z"}

event: speed_alert
data: {"id":"...","deviceId":"...","observedSpeedKmh":148.3,"thresholdKmh":120,"triggeredAt":"2025-06-15T14:31:00Z"}
```

#### Capacity and backpressure

Each connected client gets a bounded channel (capacity 100). If the channel is full when an event is published, the **oldest** event is dropped (DropOldest). Publishing is always non-blocking — ingest is never delayed by slow consumers. A warning is logged per dropped event.

#### nginx configuration

The frontend Nginx config already includes a dedicated `/api/events` location with:
- `proxy_buffering off` — disables output buffering so events flow immediately
- `proxy_read_timeout 86400s` / `proxy_send_timeout 86400s` — 24-hour connection lifetime
- `chunked_transfer_encoding on`

#### Frontend behaviour

The frontend maintains a **singleton SSE connection** (`sseClient.ts`) shared across all pages:
- **Map page**: Live positions update on each `observation` event; the selected device's trail and health panel refresh automatically.
- **Alert badge**: Increments immediately on `speed_alert` and `geofence_breach` events.
- **Alerts page**: Reloads on `speed_alert` and `geofence_breach` events.
- **Status indicator**: Map page shows `● Live` when the SSE connection is open, `○ Polling` when disconnected.
- **Polling fallback**: All pages retain a 30-second polling interval as a reconcile mechanism for reconnect gaps or missed events.
- **Automatic reconnect**: On connection error, the client waits 5 seconds and reconnects. Polling continues during the gap.

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

When `Auth:ApiKey` is empty in Development or Testing, requests are allowed with admin/operator/ingest roles. In Production, `Auth:ApiKey` is required. When keys are configured, `/api/*` endpoints require `X-Api-Key: <your-key>` and policies are evaluated from the matching key's roles. Health check endpoints (`/healthz/*` and `/api/health`) are always public.

> **SSE exception**: The browser `EventSource` API cannot send custom headers. The frontend uses a short-lived token-based authentication: it fetches a token via `POST /api/events/token` (using the API key) and then connects to the SSE endpoint with `?token=<short-lived-token>`. This approach avoids exposing long-lived API keys in URLs. See [Live Events Authentication](#live-events-authentication) for details.

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

The API key is now injected at container runtime via the `ASSTRACK_API_KEY` environment variable, which the backend serves to the frontend as `/config.json`. The frontend loads this config before React mounts. During local development with the Vite dev server, you can optionally set `VITE_API_BASE_URL` to override the API base URL:

```
VITE_API_BASE_URL=http://localhost:5019
```

## Local startup

### Full stack with Aspire

The easiest development startup path is the Aspire AppHost. It is pinned to Aspire `13.2.2` and starts the API, bridge gateway, and Vite frontend together.

```powershell
dotnet restore
dotnet run --project src\AssTrack.AppHost\AssTrack.AppHost.csproj
```

The AppHost starts the API, bridge gateway, and Vite frontend. On a clean clone, it creates a local `.env` from `.env.example` when needed, loads it, and lets Aspire's JavaScript/Vite integration install frontend packages before Vite starts.

Default endpoints:

| Service | URL |
|---|---|
| Aspire dashboard | `https://localhost:17231` |
| API | `http://localhost:5019` |
| Bridge gateway | `http://localhost:5056` |
| Frontend | Aspire-assigned Vite endpoint shown in the dashboard |
| Swagger | `http://localhost:5019/swagger` |

Useful AppHost overrides:

| Variable | Purpose | Default |
|---|---|---|
| `ASSTRACK_API_KEY` | Operator API key also passed to the dev frontend | `local-dev-key-asstrack` |
| `ASSTRACK_ADMIN_API_KEY` | Optional separate admin key for destructive maintenance and future access administration | Empty; operator key receives admin |
| `ASSTRACK_INGEST_API_KEY` | Ingest-only key for devices and bridge gateway posts | Same as `ASSTRACK_API_KEY` |
| `ASSTRACK_ACCESS_TIER` | Deployment tier surfaced by identity and system status APIs | `enterprise` |
| `ASSTRACK_CONNECTION_STRING` | SQLite connection string for local API storage | `Data Source=<repo>\asstrack-dev.db` |
The frontend still loads `/config.json` in hosted/containerized deployments. Under the Vite dev server, the AppHost also supplies `VITE_DEV_API_KEY` so the local UI can call the API without a separate config file.

### Messaging bridge workers

Signal and Telegram run as separate worker executables so provider credentials and long-running polling stay outside the API process.

```powershell
dotnet run --project src\AssTrack.SignalWorker\AssTrack.SignalWorker.csproj -- `
  --BridgeWorker:BridgeBaseUrl http://localhost:5056 `
  --BridgeWorker:FeedKey signal-local `
  --BridgeWorker:SharedSecret bridge-secret `
  --SignalWorker:SignalBaseUrl http://localhost:8080 `
  --SignalWorker:Account +15551234567

dotnet run --project src\AssTrack.TelegramWorker\AssTrack.TelegramWorker.csproj -- `
  --BridgeWorker:BridgeBaseUrl http://localhost:5056 `
  --BridgeWorker:FeedKey telegram-local `
  --BridgeWorker:SharedSecret bridge-secret `
  --TelegramWorker:BotToken 123456:bot-token
```

Create the matching feed and shared secret in `/integrations`; the workers use the bridge gateway message handoff endpoints for inbound messages, outbound queues, and delivery status updates.

### Integrated production publish

The API project can publish a production-ready single-origin app with the built SPA under `wwwroot`. The publish target runs `npm ci` and `npm run build` automatically.

```powershell
dotnet publish src\AssTrack.Api\AssTrack.Api.csproj -c Release -o artifacts\production-app
$env:ASPNETCORE_ENVIRONMENT = "Production"
$env:ASPNETCORE_URLS = "http://localhost:5099"
$env:Auth__ApiKey = "local-production-key"
$env:Frontend__ApiKey = "local-production-key"
dotnet artifacts\production-app\AssTrack.Api.dll
```

Open `http://localhost:5099`. Use `BuildIntegratedFrontend=false` when publishing an API-only artifact for split frontend/API deployments.

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
| **Managed backend** | Starts on `http://localhost:5099` with a temporary isolated SQLite DB |
| **Managed frontend** | Vite dev server on `http://localhost:5174`, proxying `/api` to port 5099 |
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
| `docker` | Builds API, bridge gateway, and frontend Docker images to validate Dockerfiles |
| `e2e-managed` | Starts the real backend and Vite dev server from the test harness |
| `e2e-apphost` | Runs the app through the Aspire AppHost from a clean checkout; AppHost auto-installs frontend packages |
| `e2e-production` | Publishes the integrated production app and runs the browser scenarios against the built artifact |

The E2E jobs run on `ubuntu-latest` after `backend` and `frontend` succeed. You can also run the managed E2E tests locally on Windows with `.\tests\run-e2e.ps1`.

## Capabilities

- Full CRUD (create, read, update, delete) for assets, devices, and geofences
- Ingest telemetry observations (lat/lon, altitude, speed, heading, metadata)
- Automatic speed alerts triggered above the per-asset `SpeedThresholdKmh` (nullable; defaults to 120 km/h if not set)
- Automatic geofence breach records when an observation lands inside an active circular geofence
- **Out-of-band webhook delivery** on speed alert and geofence breach creation (opt-in via `Webhooks:Url`)
- Alert acknowledgement for both speed alerts and geofence breaches (`POST .../acknowledge`)
- Latest-position feed per device for live-map consumption
- **Real-time SSE stream** (`GET /api/events`) pushing `observation`, `speed_alert`, and `geofence_breach` events to the browser; polling retained as reconcile fallback
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

### CORS

For split frontend/API deployments, set at least one allowed origin via:
- `appsettings.Production.json` → `"Cors": { "AllowedOrigins": ["https://your-frontend.example.com"] }`
- Environment variable: `Cors__AllowedOrigins__0=https://your-frontend.example.com`

For the integrated production publish, the SPA and API run on the same origin, so CORS can stay empty.

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

## Simulation / Demo Mode

The simulation feature generates synthetic telemetry so you can quickly populate the system with realistic data for demos, exploratory testing, or smoke-checking a fresh deployment — without needing real GPS devices.

### POST /api/observations/simulate

**Request body:**

```json
{
  "preset": "NormalRoute",
  "deviceIdentifier": "optional-custom-device-id"
}
```

| Field | Type | Description |
|---|---|---|
| `preset` | string | One of `NormalRoute`, `SpeedViolation`, `GeofenceEntryExit` |
| `deviceIdentifier` | string? | Optional. Uses an existing device with this identifier, or creates a new one. If omitted, a temporary device and asset are auto-created. |

**Response (200):**

```json
{
  "observationsCreated": 10,
  "speedAlertsTriggered": 0,
  "geofenceBreaches": 0,
  "deviceId": "3fa85f64-...",
  "deviceIdentifier": "sim-NormalRoute-20250601120000",
  "assetId": "b2f91a...",
  "eventLog": [
    "Created temporary device 'sim-NormalRoute-20250601120000' (id=...) with asset 'Sim Asset - NormalRoute'.",
    "..."
  ]
}
```

### Presets

| Preset | Points | Description |
|---|---|---|
| `NormalRoute` | 10 | Central London route, speeds 30–80 km/h. No speed violations. |
| `SpeedViolation` | 8 | Route with speeds 50/50/50/140/155/160/60/60 km/h. First violation fires a speed alert; the next two are suppressed by the 5-minute cooldown. |
| `GeofenceEntryExit` | 10 | Creates a temporary geofence (center 51.5100, -0.1000, radius 1000 m), runs a route that enters around point 4 and exits around point 8, then deactivates the geofence for test isolation. Generates ≥ 2 breach events. |

### Example curl calls

```bash
# NormalRoute – 10 observations, no alerts
curl -s -X POST http://localhost:5019/api/observations/simulate \
  -H "Content-Type: application/json" \
  -H "X-Api-Key: your-api-key" \
  -d '{"preset":"NormalRoute"}' | jq .

# SpeedViolation – triggers at least one speed alert
curl -s -X POST http://localhost:5019/api/observations/simulate \
  -H "Content-Type: application/json" \
  -H "X-Api-Key: your-api-key" \
  -d '{"preset":"SpeedViolation"}' | jq .

# GeofenceEntryExit – creates geofence, generates enter + exit breaches
curl -s -X POST http://localhost:5019/api/observations/simulate \
  -H "Content-Type: application/json" \
  -H "X-Api-Key: your-api-key" \
  -d '{"preset":"GeofenceEntryExit"}' | jq .

# Use a custom / existing device identifier
curl -s -X POST http://localhost:5019/api/observations/simulate \
  -H "Content-Type: application/json" \
  -H "X-Api-Key: your-api-key" \
  -d '{"preset":"NormalRoute","deviceIdentifier":"my-test-device"}' | jq .
```

### Enabling / disabling simulation

- **Enabled by default** in Development (`appsettings.json`: `"Simulation": { "Enabled": true }`).
- **Disabled in Production** (`appsettings.Production.json`: `"Simulation": { "Enabled": false }`). Calling the endpoint when disabled returns HTTP 403.
- Override via environment variable: `Simulation__Enabled=false`

> **Note:** `Simulation:Enabled=false` disables the endpoint entirely in production to prevent synthetic data from contaminating live environments.

## Live Updates (SSE)

AssTrack streams real-time events to connected browser clients over **Server-Sent Events (SSE)**.

### Endpoint

```
GET /api/events
```

### Event Types

| Event | Payload fields |
|---|---|
| `observation` | `id`, `deviceId`, `assetId`, `latitude`, `longitude`, `speedKmh`, `observedAt` |
| `speed_alert` | `id`, `deviceId`, `assetId`, `observedSpeedKmh`, `thresholdKmh`, `triggeredAt` |
| `geofence_breach` | `id`, `deviceId`, `assetId`, `geofenceId`, `eventType`, `detectedAt` |

### Authentication

The browser-facing SSE endpoint uses the short-lived token exchange documented above. Long-lived API keys are not accepted in the event-stream URL.

1. `POST /api/events/token` with `X-Api-Key: <operator-key>`.
2. Connect to `GET /api/events?token=<short-lived-token>`.

Tokens are opaque, expire after `SseToken:TtlMinutes`, and are stored in memory only.

### Frontend Behaviour

- **Map page**: device positions update live as observations arrive; no page reload needed
- **Alert badge**: the nav badge increments immediately on `speed_alert` or `geofence_breach` events
- **Alerts page**: refreshes automatically when alert events arrive
- **Status indicator**: the Map page shows "● Live" (green) when SSE is connected, "○ Polling" otherwise

### Fallback

30-second polling is retained in all pages as a reconciliation and fallback path.

### Limitations

- SSE tokens are in-memory and are lost on API restart; clients automatically request a fresh token.
- Multi-instance deployments need sticky sessions or a shared token store for SSE token validation.
- Each connected client holds one open HTTP connection; ensure your load balancer / reverse proxy is configured for long-lived connections (the nginx config in `frontend/nginx.conf` handles this)

## Demo Data

Use `POST /api/system/seed` to load realistic demo assets, devices, geofences, and observations for exploring the app.

- `reset=false` is idempotent and returns an already-seeded result instead of duplicating demo records.
- `reset=true` wipes and recreates seeded records only.
- `IsSeeded=true` marks demo-owned records so non-seeded manual data stays safe during reset operations.
- In the UI, operators can use **Settings → Demo Data** when `Simulation:Enabled=true`.
- Seeded records display a **Demo** badge on the Assets, Devices, and Geofences pages.
