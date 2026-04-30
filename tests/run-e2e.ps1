$ErrorActionPreference = "Stop"

Write-Host "Ensuring frontend dependencies..." -ForegroundColor Cyan
if (-not (Test-Path "frontend\node_modules")) {
    & "C:\Program Files\nodejs\npm.cmd" install --prefix frontend
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Frontend npm install failed" -ForegroundColor Red
        exit 1
    }
}

Write-Host "Restoring E2E tests..." -ForegroundColor Cyan
dotnet restore tests\AssTrack.E2ETests\AssTrack.E2ETests.csproj
if ($LASTEXITCODE -ne 0) {
    Write-Host "E2E restore failed" -ForegroundColor Red
    exit 1
}

Write-Host "Building E2E tests..." -ForegroundColor Cyan
dotnet build tests\AssTrack.E2ETests\AssTrack.E2ETests.csproj --no-restore
if ($LASTEXITCODE -ne 0) {
    Write-Host "E2E test build failed" -ForegroundColor Red
    exit 1
}

Write-Host "Installing Playwright browsers..." -ForegroundColor Cyan
powershell -File tests\AssTrack.E2ETests\bin\Debug\net10.0\playwright.ps1 install chromium
if ($LASTEXITCODE -ne 0) {
    Write-Host "Playwright install failed" -ForegroundColor Red
    exit 1
}

Write-Host "Running E2E tests..." -ForegroundColor Cyan
dotnet test tests\AssTrack.E2ETests\AssTrack.E2ETests.csproj --no-build
if ($LASTEXITCODE -ne 0) {
    Write-Host "E2E tests failed" -ForegroundColor Red
    exit 1
}

Write-Host "All E2E tests passed!" -ForegroundColor Green
