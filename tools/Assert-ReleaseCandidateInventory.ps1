<#
.SYNOPSIS
Validate the exact release candidate payload immediately before upload.
.DESCRIPTION
Requires six approved root files plus lang/ containing exactly the canonical
Languages/*.ini source files. The manifest must hash every upload file except
itself. Unknown files, folders, reparse points and mismatched locale bytes fail
closed. This guard never runs repository-supplied executables.
.EXAMPLE
./tools/Assert-ReleaseCandidateInventory.ps1 -ReleaseRoot ./Release -ExpectedVersion 3.3.0-2
.EXAMPLE
./tools/Assert-ReleaseCandidateInventory.ps1 -ReleaseRoot ./Release -ExpectedVersion 3.3.0 -Troubleshoot
.PARAMETER SourceLanguageDirectory
Optional isolated fixture source; defaults to sibling Languages/ directory.
.NOTES
Logs errors alongside this script with TEMP fallback. Supports PS 5.1 and 7.x.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$ReleaseRoot,
    [Parameter(Mandatory=$true)][string]$ExpectedVersion,
    [string]$SourceLanguageDirectory = '',
    [switch]$Troubleshoot
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

# Do not depend on module auto-import: child Windows PowerShell 5.1 launched
# from pwsh may inherit a PSModulePath without Microsoft.PowerShell.Utility.
function Get-ReleaseSha256 {
    param([Parameter(Mandatory=$true)][string]$Path)
    $stream = [IO.File]::Open($Path, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::Read)
    try {
        $sha = [Security.Cryptography.SHA256]::Create()
        try {
            return ([BitConverter]::ToString($sha.ComputeHash($stream))).Replace('-', '').ToLowerInvariant()
        }
        finally { $sha.Dispose() }
    }
    finally { $stream.Dispose() }
}

function Assert-OrdinaryFile {
    param([Parameter(Mandatory=$true)][string]$Path)
    $file = Get-Item -LiteralPath $Path -Force -ErrorAction Stop
    if ($file -isnot [IO.FileInfo] -or
        (($file.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0)) {
        throw "Upload path is not an ordinary file: $Path"
    }
    return $file
}

try {
    if ($ExpectedVersion -cnotmatch '^[0-9]+\.[0-9]+\.[0-9]+(?:-[0-9]+)?$') {
        throw 'Unsupported release version format.'
    }
    $root = [IO.Path]::GetFullPath($ReleaseRoot)
    if (!(Test-Path -LiteralPath $root -PathType Container)) {
        throw 'Release candidate output directory is unavailable.'
    }
    $source = if ([string]::IsNullOrWhiteSpace($SourceLanguageDirectory)) {
        Join-Path (Split-Path -Parent $root) 'Languages'
    }
    else { [IO.Path]::GetFullPath($SourceLanguageDirectory) }
    if (!(Test-Path -LiteralPath $source -PathType Container)) {
        throw 'Canonical source Languages directory is unavailable.'
    }
    $sourceInfo = Get-Item -LiteralPath $source -Force
    if (($sourceInfo.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
        throw 'Canonical source Languages directory is a reparse point.'
    }
    $sourceContents = @(Get-ChildItem -LiteralPath $source -Force)
    $sourceLocales = @($sourceContents | Where-Object { $_.Name -like '*.ini' })
    if ($sourceLocales.Count -eq 0 -or $sourceLocales.Count -ne $sourceContents.Count) {
        throw 'Canonical Languages directory has no locales or unexpected source entries.'
    }
    foreach ($locale in $sourceLocales) {
        $null = Assert-OrdinaryFile -Path $locale.FullName
    }
    $sourceLocales = @($sourceLocales | Sort-Object -Property Name)
    $localeNames = @($sourceLocales | ForEach-Object { $_.Name })

    $baseNames = @(
        'youtube-dl-gui.exe',
        'youtube-dl-gui.zip',
        'Release-hashes.md',
        'Release-body.md',
        "Release-notes-$ExpectedVersion.md",
        'Release-manifest.json'
    )
    $rootNames = @($baseNames + @('lang'))
    $items = @(Get-ChildItem -LiteralPath $root -Force)
    if ($items.Count -ne $rootNames.Count) {
        throw "Unexpected release artifact inventory count: $($items.Count), expected $($rootNames.Count)."
    }
    foreach ($item in $items) {
        if ($item.Name -cnotin $rootNames) {
            throw "Unapproved release artifact root path: $($item.Name)"
        }
        if ($item.Name -ceq 'lang') {
            if (!$item.PSIsContainer -or
                (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0)) {
                throw 'Release lang path is not an ordinary directory.'
            }
        }
        else { $null = Assert-OrdinaryFile -Path $item.FullName }
    }
    $langRoot = Join-Path $root 'lang'
    $packagedLocales = @(Get-ChildItem -LiteralPath $langRoot -Force)
    if ($packagedLocales.Count -ne $sourceLocales.Count) {
        throw "Unexpected release locale inventory count: $($packagedLocales.Count), expected $($sourceLocales.Count)."
    }
    foreach ($item in $packagedLocales) {
        if ($item.Name -cnotin $localeNames) {
            throw "Undeclared release locale path: $($item.Name)"
        }
        $null = Assert-OrdinaryFile -Path $item.FullName
        $sourcePath = Join-Path $source $item.Name
        if ((Get-ReleaseSha256 -Path $sourcePath) -cne (Get-ReleaseSha256 -Path $item.FullName)) {
            throw "Packaged locale differs from canonical source: $($item.Name)"
        }
    }

    $payloadNames = @(
        @($baseNames | Where-Object { $_ -cne 'Release-manifest.json' }) +
        @($sourceLocales | ForEach-Object { 'lang/' + $_.Name })
    )
    $manifest = Get-Content -LiteralPath (Join-Path $root 'Release-manifest.json') -Raw |
        ConvertFrom-Json
    if ($manifest.version -cne $ExpectedVersion) {
        throw 'Release artifact manifest version does not match the required tag.'
    }
    $records = @($manifest.artifacts)
    if ($records.Count -ne $payloadNames.Count) {
        throw 'Release manifest does not declare all payload artifact hashes.'
    }
    $seen = @{}
    foreach ($record in $records) {
        $name = [string]$record.path
        if ($name -cnotin $payloadNames -or $seen.ContainsKey($name)) {
            throw "Unexpected/duplicate artifact digest entry: $name"
        }
        $seen[$name] = $true
        $file = Assert-OrdinaryFile -Path (Join-Path $root $name)
        $digest = Get-ReleaseSha256 -Path $file.FullName
        if ($record.sha256 -cne $digest -or [long]$record.bytes -ne $file.Length) {
            throw "Release candidate payload digest/size differs from manifest: $name"
        }
    }
    foreach ($name in $payloadNames) {
        if (!$seen.ContainsKey($name)) {
            throw "Missing release artifact digest entry: $name"
        }
    }
    $exe = @($records | Where-Object { $_.path -ceq 'youtube-dl-gui.exe' })[0]
    $zip = @($records | Where-Object { $_.path -ceq 'youtube-dl-gui.zip' })[0]
    if ($exe.sha256 -cne $manifest.exe_sha256 -or $zip.sha256 -cne $manifest.zip_sha256) {
        throw 'Release payload digests contradict the legacy executable/ZIP hash fields.'
    }
    if ($Troubleshoot) {
        Write-Host "Canonical languages: $($localeNames -join ', ')"
        Write-Host "Candidate source SHA: $($manifest.commit)"
    }
    Write-Host "Release candidate inventory verified: $($rootNames.Count) root entries, $($localeNames.Count) locales."
}
catch {
    $message = "Release inventory validation failed: $($_.Exception.Message)"
    $log = Join-Path $PSScriptRoot 'Assert-ReleaseCandidateInventory.log'
    try { Add-Content -LiteralPath $log -Value ("{0:O} {1}" -f (Get-Date), $message) }
    catch {
        $tempRoot = if ($env:TEMP) { $env:TEMP } else { [IO.Path]::GetTempPath() }
        try { Add-Content -LiteralPath (Join-Path $tempRoot 'Assert-ReleaseCandidateInventory.log') -Value $message }
        catch { }
    }
    throw $message
}
