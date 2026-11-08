<#
.SYNOPSIS
    Immutable test/tool/workflow inventory for guarded repair verification.
.DESCRIPTION
    Dot-source before any candidate build. Keep the returned baseline IN MEMORY.
    Recheck immediately after every MSBuild stage, before test compilation and
    after the regression harness. Includes untracked test/tool inputs; ignores
    unrelated application bin/obj outputs.
.PARAMETER troubleshoot
    Optional diagnostic logging to script directory, falling back to TEMP.
.EXAMPLE
    . ./tools/RepairInputIntegrity.ps1
    $original = Get-RepairInputSnapshot -RepositoryRoot (Get-Location).Path
    Assert-RepairInputSnapshot -RepositoryRoot (Get-Location).Path -Baseline $original -Phase 'after-build'
#>
[CmdletBinding()]
param([switch]$troubleshoot)
$script:RepairInputTroubleshoot = [bool]$troubleshoot

function Write-RepairInputDiagnostic([string]$Message) {
    if (!$script:RepairInputTroubleshoot) { return }
    $filename = 'RepairInputIntegrity.log'
    try {
        Add-Content -LiteralPath (Join-Path $PSScriptRoot $filename) -Value $Message -ErrorAction Stop
    }
    catch {
        $fallback = if ($env:TEMP) { $env:TEMP } else { [IO.Path]::GetTempPath() }
        Add-Content -LiteralPath (Join-Path $fallback $filename) -Value $Message -ErrorAction Stop
    }
}

function Get-RepairInputSnapshot {
    [CmdletBinding()]
    param([Parameter(Mandatory = $true)][string]$RepositoryRoot)
    $root = [IO.Path]::GetFullPath($RepositoryRoot).TrimEnd(
        [IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar)
    $prefix = $root + [IO.Path]::DirectorySeparatorChar
    $rows = New-Object 'System.Collections.Generic.List[string]'
    $hash = [System.Security.Cryptography.SHA256]::Create()
    try {
        foreach ($relativeDir in @('tests', 'tools', '.github/workflows')) {
            $directory = Join-Path $root $relativeDir
            if (!(Test-Path -LiteralPath $directory -PathType Container)) {
                throw "Trusted repair input directory missing: $relativeDir"
            }
            foreach ($item in @(Get-ChildItem -LiteralPath $directory -File -Force -Recurse -ErrorAction Stop)) {
                if (!$item.FullName.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) {
                    throw "Trusted repair input escaped its root: $($item.FullName)"
                }
                $relative = $item.FullName.Substring($prefix.Length).Replace('\', '/')
                if ($relative -match '^(tests|tools)/(bin|obj|__pycache__)/') { continue }
                if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
                    throw "Trusted repair input is a reparse point: $relative"
                }
                $stream = [IO.File]::OpenRead($item.FullName)
                try {
                    $digest = [BitConverter]::ToString($hash.ComputeHash($stream)).Replace('-', '').ToLowerInvariant()
                    $rows.Add("$relative|$($item.Length)|$digest")
                }
                finally { $stream.Dispose() }
            }
        }
    }
    finally { $hash.Dispose() }
    if ($rows.Count -lt 3) { throw 'Trusted repair input inventory is unexpectedly empty.' }
    $result = (@($rows | Sort-Object -CaseSensitive) -join "`n")
    Write-RepairInputDiagnostic "Snapshot $($rows.Count) files under $root"
    return $result
}

function Assert-RepairInputSnapshot {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][string]$RepositoryRoot,
        [Parameter(Mandatory = $true)][string]$Baseline,
        [Parameter(Mandatory = $true)][string]$Phase
    )
    $current = Get-RepairInputSnapshot -RepositoryRoot $RepositoryRoot
    if ($current -cne $Baseline) {
        $before = @($Baseline -split "`n")
        $after = @($current -split "`n")
        $difference = @(Compare-Object -ReferenceObject $before -DifferenceObject $after -CaseSensitive |
            Select-Object -First 8 | ForEach-Object { $_.SideIndicator + ' ' + $_.InputObject })
        throw "Trusted repair test/tool/workflow inputs mutated during $Phase. Evidence invalid. $($difference -join '; ')"
    }
    Write-RepairInputDiagnostic "Verified immutable inputs at $Phase"
}
