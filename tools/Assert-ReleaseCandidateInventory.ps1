<#
.SYNOPSIS
Fail-closed allowlist and digest verifier for release-candidate uploads.
.DESCRIPTION
Requires exactly the six expected files and verifies the manifest's five
payload hashes/sizes. This independent step runs immediately before upload.
.EXAMPLE
./tools/Assert-ReleaseCandidateInventory.ps1 -ReleaseRoot ./Release -ExpectedVersion 3.3.0-2
.EXAMPLE
./tools/Assert-ReleaseCandidateInventory.ps1 -ReleaseRoot ./Release -ExpectedVersion 3.3.0-2 -Troubleshoot
.NOTES
On failure, writes diagnostic logs beside this script, with TEMP fallback.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$ReleaseRoot,
    [Parameter(Mandatory=$true)][string]$ExpectedVersion,
    [switch]$Troubleshoot
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

# Windows PowerShell launched from pwsh may inherit a PSModulePath that omits
# Microsoft.PowerShell.Utility. SHA-256 must work without module auto-import.
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

try {
    if ($ExpectedVersion -cnotmatch '^[0-9]+\.[0-9]+\.[0-9]+(?:-[0-9]+)?$') {
        throw 'Release version does not match the supported exact prerelease tag.'
    }
    $root = [IO.Path]::GetFullPath($ReleaseRoot)
    if (!(Test-Path -LiteralPath $root -PathType Container)) {
        throw 'Release candidate output directory is unavailable.'
    }
    $nameList = @(
        'youtube-dl-gui.exe',
        'youtube-dl-gui.zip',
        'Release-hashes.md',
        'Release-body.md',
        "Release-notes-$ExpectedVersion.md",
        'Release-manifest.json'
    )
    $items = @(Get-ChildItem -LiteralPath $root -Force)
    if ($items.Count -ne $nameList.Count) {
        throw "Unexpected release artifact inventory count: $($items.Count), expected $($nameList.Count)."
    }
    foreach ($item in $items) {
        if ($item.Name -cnotin $nameList -or $item -isnot [IO.FileInfo] -or
            (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0)) {
            throw "Unapproved release artifact path or type: $($item.Name)"
        }
    }
    foreach ($name in $nameList) {
        if (!(Test-Path -LiteralPath (Join-Path $root $name) -PathType Leaf)) {
            throw "Missing declared release artifact: $name"
        }
    }

    $manifest = Get-Content -LiteralPath (Join-Path $root 'Release-manifest.json') -Raw |
        ConvertFrom-Json
    if ($manifest.version -cne $ExpectedVersion) {
        throw 'Release artifact manifest version does not match the required tag.'
    }
    $payloadNames = @($nameList | Where-Object { $_ -cne 'Release-manifest.json' })
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
        $file = Get-Item -LiteralPath (Join-Path $root $name)
        $digest = Get-ReleaseSha256 -Path $file.FullName
        if ($record.sha256 -cne $digest -or [long]$record.bytes -ne $file.Length) {
            throw "Release candidate payload digest/size differs from manifest: $name"
        }
    }
    foreach ($name in $payloadNames) {
        if (!$seen.ContainsKey($name)) { throw "Missing release artifact digest entry: $name" }
    }
    $exe = @($records | Where-Object { $_.path -ceq 'youtube-dl-gui.exe' })[0]
    $zip = @($records | Where-Object { $_.path -ceq 'youtube-dl-gui.zip' })[0]
    if ($exe.sha256 -cne $manifest.exe_sha256 -or
        $zip.sha256 -cne $manifest.zip_sha256) {
        throw 'Release payload digests contradict the legacy executable/ZIP hash fields.'
    }
    if ($Troubleshoot) {
        Write-Host "Validated inventory: $($nameList -join ', ')"
        Write-Host "Manifest candidate commit: $($manifest.commit)"
    }
    Write-Host "Release candidate inventory verified: $($nameList.Count) exact files."
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
