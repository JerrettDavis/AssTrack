#!/usr/bin/env powershell
Set-StrictMode -Version Latest
$ErrorActionPreference = "Continue"

$startTime = Get-Date
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "E2E TEST VALIDATION AND EXECUTION" -ForegroundColor Cyan
Write-Host "Start Time: $startTime" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

# STEP 1: Verify files exist
Write-Host "`n=== STEP 1: VERIFY KEY E2E FILES ===" -ForegroundColor Yellow
$e2ePath = "tests\AssTrack.E2ETests"
$files_to_check = @(
  "ApiClient.cs",
  "Hooks",
  "PageObjects",
  "Steps",
  "Features\CoreFlows.feature",
  "reqnroll.json",
  "AssTrack.E2ETests.csproj"
)

$all_exist = $true
foreach ($file in $files_to_check) {
  $fullPath = Join-Path $e2ePath $file
  $exists = Test-Path $fullPath
  $status = if ($exists) { "✓" } else { "✗" }
  Write-Host "$status $file"
  if (-not $exists) { $all_exist = $false }
}

# Check solution file
$sln_exists = Test-Path "AssTrack.sln"
Write-Host "$(if ($sln_exists) { '✓' } else { '✗' }) AssTrack.sln exists"

# Check if E2E in solution
if ($sln_exists) {
  $sln_content = Get-Content "AssTrack.sln" -Raw
  $e2e_in_sln = $sln_content -match "AssTrack\.E2ETests"
  Write-Host "$(if ($e2e_in_sln) { '✓' } else { '✗' }) AssTrack.E2ETests in solution"
}

# STEP 2: Check frontend dependencies
Write-Host "`n=== STEP 2: CHECK FRONTEND DEPENDENCIES ===" -ForegroundColor Yellow
if (Test-Path "frontend\node_modules") {
  $moduleCount = (Get-ChildItem "frontend\node_modules" | Measure-Object).Count
  Write-Host "✓ Frontend dependencies installed ($moduleCount modules)"
} else {
  Write-Host "✗ Frontend node_modules NOT found"
  Write-Host "Installing frontend dependencies..."
  cd frontend
  npm install
  cd ..
}

# STEP 3: dotnet restore E2E
Write-Host "`n=== STEP 3: DOTNET RESTORE E2E TESTS ===" -ForegroundColor Yellow
$restore_start = Get-Date
$restore_output = & dotnet restore tests\AssTrack.E2ETests 2>&1
$restore_time = ((Get-Date) - $restore_start).TotalSeconds
if ($LASTEXITCODE -eq 0) {
  Write-Host "✓ dotnet restore succeeded in ${restore_time}s"
} else {
  Write-Host "✗ dotnet restore FAILED"
  Write-Host $restore_output
  exit 1
}

# STEP 4: Install Playwright chromium
Write-Host "`n=== STEP 4: INSTALL PLAYWRIGHT CHROMIUM ===" -ForegroundColor Yellow
# Check for existing playwright script
$playWrightScript = Get-ChildItem -Path "tests\AssTrack.E2ETests" -Recurse -Filter "*playwright*" -Type File | Where-Object {$_.Extension -eq ".ps1" -or $_.Extension -eq ".sh" -or $_.Name -match "playwright"}
if ($playWrightScript) {
  Write-Host "Found Playwright script: $($playWrightScript.FullName)"
  Write-Host "Running: $($playWrightScript.FullName)"
  & $playWrightScript.FullName
  if ($LASTEXITCODE -eq 0) {
    Write-Host "✓ Playwright chromium installed"
  } else {
    Write-Host "⚠ Playwright install had issues, continuing..."
  }
} else {
  Write-Host "No custom playwright script found. Using dotnet pwsh..."
  $pw_start = Get-Date
  $pw_output = & dotnet exec (Get-ChildItem -Path "tests\AssTrack.E2ETests\bin" -Recurse -Filter "playwright.ps1" | Select-Object -First 1 -ExpandProperty FullName) 2>&1
  if ($LASTEXITCODE -ne 0) {
    Write-Host "Attempting standard Playwright install via dotnet tool..."
    dotnet tool install --global Microsoft.Playwright.CLI 2>&1
    playwright install chromium 2>&1
  }
}

# STEP 5: dotnet test E2E
Write-Host "`n=== STEP 5: DOTNET TEST E2E TESTS ===" -ForegroundColor Yellow
$test_e2e_start = Get-Date
$test_e2e_output = & dotnet test tests\AssTrack.E2ETests --no-restore -v minimal 2>&1
$test_e2e_time = ((Get-Date) - $test_e2e_start).TotalSeconds

$test_e2e_text = $test_e2e_output | Out-String

# Extract test counts from output
$passed = [regex]::Matches($test_e2e_text, '(\d+)\s+passed') | Select-Object -First 1
$failed = [regex]::Matches($test_e2e_text, '(\d+)\s+failed') | Select-Object -First 1
$skipped = [regex]::Matches($test_e2e_text, '(\d+)\s+skipped') | Select-Object -First 1

if ($LASTEXITCODE -eq 0) {
  Write-Host "✓ E2E Tests PASSED in ${test_e2e_time}s"
  if ($passed) { Write-Host "  Passed: $($passed.Groups[1].Value)" }
  if ($failed) { Write-Host "  Failed: $($failed.Groups[1].Value)" }
  if ($skipped) { Write-Host "  Skipped: $($skipped.Groups[1].Value)" }
} else {
  Write-Host "✗ E2E Tests FAILED in ${test_e2e_time}s"
  Write-Host "Output:" 
  Write-Host $test_e2e_output
}

# STEP 6: Run non-E2E tests for regression check
Write-Host "`n=== STEP 6: RUN NON-E2E REGRESSION TESTS ===" -ForegroundColor Yellow
$test_unit_start = Get-Date
$test_unit_output = & dotnet test tests\AssTrack.Tests --no-restore -v minimal 2>&1
$test_unit_time = ((Get-Date) - $test_unit_start).TotalSeconds

$test_unit_text = $test_unit_output | Out-String

# Extract test counts
$unit_passed = [regex]::Matches($test_unit_text, '(\d+)\s+passed') | Select-Object -First 1
$unit_failed = [regex]::Matches($test_unit_text, '(\d+)\s+failed') | Select-Object -First 1
$unit_skipped = [regex]::Matches($test_unit_text, '(\d+)\s+skipped') | Select-Object -First 1

if ($LASTEXITCODE -eq 0) {
  Write-Host "✓ Unit Tests PASSED in ${test_unit_time}s"
  if ($unit_passed) { Write-Host "  Passed: $($unit_passed.Groups[1].Value)" }
  if ($unit_failed) { Write-Host "  Failed: $($unit_failed.Groups[1].Value)" }
  if ($unit_skipped) { Write-Host "  Skipped: $($unit_skipped.Groups[1].Value)" }
} else {
  Write-Host "✗ Unit Tests FAILED in ${test_unit_time}s"
  Write-Host "Output:"
  Write-Host $test_unit_output
}

# SUMMARY
Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "TEST EXECUTION SUMMARY" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
$totalTime = ((Get-Date) - $startTime).TotalSeconds
Write-Host "Total Execution Time: ${totalTime}s"
Write-Host "End Time: $(Get-Date)" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
