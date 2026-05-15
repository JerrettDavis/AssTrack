[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$ArtifactDirectory
)

$ErrorActionPreference = 'Stop'

$artifactPath = (Resolve-Path $ArtifactDirectory).Path
$checksumFile = Join-Path $artifactPath 'SHA256SUMS.txt'

$lines = foreach ($file in Get-ChildItem -Path $artifactPath -File | Sort-Object Name) {
    if ($file.Name -eq 'SHA256SUMS.txt') {
        continue
    }

    $hash = Get-FileHash -Path $file.FullName -Algorithm SHA256
    '{0}  {1}' -f $hash.Hash.ToLowerInvariant(), $file.Name
}

Set-Content -Path $checksumFile -Value $lines

Write-Output $checksumFile
