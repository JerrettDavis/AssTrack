[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$PublishDirectory,

    [Parameter(Mandatory)]
    [string]$OutputPath
)

$ErrorActionPreference = 'Stop'

$publishRoot = (Resolve-Path $PublishDirectory).Path
$outputDirectory = Split-Path -Parent $OutputPath
New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null

function New-StableId {
    param(
        [Parameter(Mandatory)]
        [string]$Prefix,

        [Parameter(Mandatory)]
        [string]$Value
    )

    $bytes = [System.Text.Encoding]::UTF8.GetBytes($Value.ToLowerInvariant())
    $hashBytes = [System.Security.Cryptography.SHA1]::HashData($bytes)
    $hash = [Convert]::ToHexString($hashBytes).Substring(0, 16)
    return "${Prefix}_$hash"
}

function New-TreeNode {
    param([string]$Name, [string]$RelativePath)

    [pscustomobject]@{
        Name = $Name
        RelativePath = $RelativePath
        Directories = [System.Collections.Generic.List[object]]::new()
        Files = [System.Collections.Generic.List[object]]::new()
    }
}

$root = New-TreeNode -Name '' -RelativePath ''
$directoryIndex = @{
    '' = $root
}

$componentRefs = [System.Collections.Generic.List[string]]::new()

foreach ($file in Get-ChildItem -Path $publishRoot -File -Recurse | Sort-Object FullName) {
    $relativePath = [IO.Path]::GetRelativePath($publishRoot, $file.FullName)
    $relativeDirectory = Split-Path -Path $relativePath -Parent
    if ($relativeDirectory -eq '.') {
        $relativeDirectory = ''
    }

    if (-not $directoryIndex.ContainsKey($relativeDirectory)) {
        $segments = @()
        if ($relativeDirectory) {
            $segments = $relativeDirectory -split '[\\/]'
        }

        $currentRelative = ''
        $currentNode = $root

        foreach ($segment in $segments) {
            $nextRelative = if ($currentRelative) { Join-Path $currentRelative $segment } else { $segment }
            if (-not $directoryIndex.ContainsKey($nextRelative)) {
                $child = New-TreeNode -Name $segment -RelativePath $nextRelative
                $currentNode.Directories.Add($child)
                $directoryIndex[$nextRelative] = $child
            }

            $currentNode = $directoryIndex[$nextRelative]
            $currentRelative = $nextRelative
        }
    }

    $node = $directoryIndex[$relativeDirectory]
    $componentId = New-StableId -Prefix 'CMP' -Value $relativePath
    $fileId = New-StableId -Prefix 'FIL' -Value $relativePath
    $source = $file.FullName.Replace('&', '&amp;')

    $node.Files.Add([pscustomobject]@{
        ComponentId = $componentId
        FileId = $fileId
        Source = $source
        RelativePath = $relativePath
    })

    $componentRefs.Add($componentId)
}

$lines = [System.Collections.Generic.List[string]]::new()

function Add-Line {
    param([string]$Text)
    $script:lines.Add($Text)
}

function Write-DirectoryContent {
    param(
        [Parameter(Mandatory)]
        $Node,

        [Parameter(Mandatory)]
        [string]$DirectoryId,

        [int]$Indent = 4
    )

    $padding = ' ' * $Indent
    foreach ($file in $Node.Files) {
        Add-Line "$padding<Component Id=""$($file.ComponentId)"" Directory=""$DirectoryId"" Guid=""*"">"
        Add-Line "$padding  <File Id=""$($file.FileId)"" Source=""$($file.Source)"" />"
        Add-Line "$padding</Component>"
    }

    foreach ($child in $Node.Directories | Sort-Object Name) {
        $childId = New-StableId -Prefix 'DIR' -Value $child.RelativePath
        $escapedName = $child.Name.Replace('&', '&amp;')
        Add-Line "$padding<Directory Id=""$childId"" Name=""$escapedName"">"
        Write-DirectoryContent -Node $child -DirectoryId $childId -Indent ($Indent + 2)
        Add-Line "$padding</Directory>"
    }
}

Add-Line '<Wix xmlns="http://wixtoolset.org/schemas/v4/wxs">'
Add-Line '  <Fragment>'
Add-Line '    <DirectoryRef Id="INSTALLFOLDER">'
Write-DirectoryContent -Node $root -DirectoryId 'INSTALLFOLDER' -Indent 6
Add-Line '    </DirectoryRef>'
Add-Line '  </Fragment>'
Add-Line ''
Add-Line '  <Fragment>'
Add-Line '    <ComponentGroup Id="AppFiles">'
foreach ($componentId in $componentRefs) {
    Add-Line "      <ComponentRef Id=""$componentId"" />"
}
Add-Line '    </ComponentGroup>'
Add-Line '  </Fragment>'
Add-Line '</Wix>'

Set-Content -Path $OutputPath -Value $lines

Write-Output $OutputPath
