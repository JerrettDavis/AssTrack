[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$ProjectPath,

    [Parameter(Mandatory)]
    [string]$RuntimeIdentifier,

    [Parameter(Mandatory)]
    [string]$Version,

    [Parameter(Mandatory)]
    [string]$OutputRoot,

    [Parameter(Mandatory)]
    [string]$AssetName,

    [string[]]$AdditionalPublishProperties = @()
)

$ErrorActionPreference = 'Stop'

$publishRoot = Join-Path $OutputRoot $AssetName
$publishDir = Join-Path $publishRoot 'publish'
$archivePath = Join-Path $OutputRoot "$AssetName.zip"

if (Test-Path $publishRoot) {
    Remove-Item $publishRoot -Recurse -Force
}

if (Test-Path $archivePath) {
    Remove-Item $archivePath -Force
}

New-Item -ItemType Directory -Force -Path $publishDir | Out-Null

$versionParts = $Version.Split('.')
if ($versionParts.Length -lt 3) {
    throw "Version '$Version' is not a valid SemVer value."
}

$fileVersion = "$($versionParts[0]).$($versionParts[1]).$($versionParts[2]).0"

$publishArgs = @(
    'publish',
    $ProjectPath,
    '--configuration', 'Release',
    '--runtime', $RuntimeIdentifier,
    '--self-contained', 'true',
    '--output', $publishDir,
    '/p:ContinuousIntegrationBuild=true',
    "/p:Version=$Version",
    "/p:AssemblyVersion=$fileVersion",
    "/p:FileVersion=$fileVersion",
    "/p:InformationalVersion=$Version"
)

foreach ($property in $AdditionalPublishProperties) {
    $publishArgs += "/p:$property"
}

& dotnet @publishArgs
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed for $ProjectPath ($RuntimeIdentifier)."
}

Compress-Archive -Path (Join-Path $publishRoot '*') -DestinationPath $archivePath -CompressionLevel Optimal

Write-Output $archivePath
