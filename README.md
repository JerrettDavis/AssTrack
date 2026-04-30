# AssTrack

AssTrack is an asset tracking platform with a .NET minimal API backend, EF Core persistence, xUnit integration tests, and a React + TypeScript frontend with routing, a live map, and speed-alert views.

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
| GET | `/api/geofences` | List geofences |
| POST | `/api/geofences` | Create a geofence |

Speed alerts are created automatically when an observation is ingested with `SpeedKmh > 120`.

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
| `/alerts` | `AlertsPage` | Table of speed alerts |

The Vite dev server proxies `/api` requests to `http://localhost:5000`.

## Run the API

```powershell
dotnet restore
cd src\AssTrack.Api
dotnet run
```

Swagger UI is available at `http://localhost:5000/swagger`.

## Run the frontend

```powershell
cd frontend
npm install
npm run dev
```

The dev server starts on `http://localhost:5173` and proxies `/api` to the backend.

## Run tests

```powershell
dotnet test C:\git\AssTrack
```

## Build

```powershell
# Backend
dotnet build C:\git\AssTrack

# Frontend
cd frontend
npm run build
```

## Capabilities

- Manage assets and devices
- Ingest telemetry observations (lat/lon, altitude, speed, heading, metadata)
- Automatic speed alerts triggered above 120 km/h
- Latest-position feed per device for live-map consumption
- Geofence inclusion checked with haversine distance
- Browse all contracts via Swagger/OpenAPI at `/swagger`
