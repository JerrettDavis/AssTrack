# AssTrack

AssTrack is an initial proof-of-concept asset tracking platform with a .NET minimal API backend, EF Core persistence, xUnit coverage, and a React + TypeScript frontend shell.

## Solution structure

- `src\AssTrack.Domain` - domain models, DTOs, and evaluation services
- `src\AssTrack.Infrastructure` - EF Core SQLite context and repositories
- `src\AssTrack.Api` - minimal APIs, Swagger/OpenAPI, and runtime composition
- `tests\AssTrack.Tests` - domain and API integration tests
- `frontend` - Vite React TypeScript client
- `docs\openapi` - exported OpenAPI artifacts

## Run backend

```powershell
dotnet restore
cd src\AssTrack.Api
dotnet run
```

Swagger UI is available at `http://localhost:5000/swagger` or the assigned ASP.NET Core port.

## Run frontend

```powershell
cd frontend
npm install
npm run dev
```

## Validation

```powershell
dotnet build --no-restore
dotnet test --no-build --verbosity normal
cd frontend
npm run build
```

## Capabilities

- Manage assets and devices
- Ingest telemetry observations
- Compute geofence inclusion with haversine distance
- Trigger speed alerts above 120 km/h
- Browse API contracts with Swagger/OpenAPI
