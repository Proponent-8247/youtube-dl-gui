<#
.SYNOPSIS
    Remove only the validated request wrapper and its exact chunk files.
.DESCRIPTION
    Dot-source from the trusted guarded repair runner before candidate code runs.
    The parser has already decoded and validated the request file inventory.
    This function checks the filenames and tracked identity again before deleting.
.PARAMETER troubleshoot
    Optional diagnostic log in the script directory with TEMP fallback.
.EXAMPLE
    . ./tools/RepairRequestLifecycle.ps1
    Complete-GuardedRepairRequest -ValidatedFiles @('.audit-repairs.json', '.audit-repairs.parts/part-000.txt')
#>
[CmdletBinding()]
param([switch]$troubleshoot)
$script:RepairLifecycleTroubleshoot = [bool]$troubleshoot

function Write-RepairLifecycleDiagnostic([string]$Message) {
    if (!$script:RepairLifecycleTroubleshoot) { return }
    $name = 'RepairRequestLifecycle.log'
    try { Add-Content -LiteralPath (Join-Path $PSScriptRoot $name) -Value $Message -ErrorAction Stop }
    catch {
        $dir = if ($env:TEMP) { $env:TEMP } else { [IO.Path]::GetTempPath() }
        Add-Content -LiteralPath (Join-Path $dir $name) -Value $Message -ErrorAction Stop
    }
}

function Complete-GuardedRepairRequest {
    [CmdletBinding()]
    param([Parameter(Mandatory = $true)][string[]]$ValidatedFiles)
    if ($ValidatedFiles.Count -lt 1 -or $ValidatedFiles[0] -cne '.audit-repairs.json') {
        throw 'Validated repair cleanup must begin with the exact request wrapper.'
    }
    $distinct = @($ValidatedFiles | Sort-Object -Unique -CaseSensitive)
    if ($distinct.Count -ne $ValidatedFiles.Count) {
        throw 'Validated repair cleanup contains duplicate request paths.'
    }
    foreach ($requestPath in $ValidatedFiles) {
        if ($requestPath -cne '.audit-repairs.json' -and
            $requestPath -cnotmatch '^\.audit-repairs\.parts/part-[0-9]{3}\.txt$') {
            throw "Repair cleanup refused an undeclared/non-request path: $requestPath"
        }
        if (!(Test-Path -LiteralPath $requestPath -PathType Leaf)) {
            throw "Repair cleanup request file is unavailable: $requestPath"
        }
        $item = Get-Item -LiteralPath $requestPath -Force -ErrorAction Stop
        if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw "Repair cleanup refused a reparse-point request file: $requestPath"
        }
        git ls-files --error-unmatch -- $requestPath | Out-Null
        if ($LASTEXITCODE -ne 0) {
            throw "Repair cleanup file is not tracked by Git: $requestPath"
        }
    }
    foreach ($requestPath in $ValidatedFiles) {
        Remove-Item -LiteralPath $requestPath -Force -ErrorAction Stop
    }
    git --literal-pathspecs add -u -- @ValidatedFiles
    if ($LASTEXITCODE -ne 0) { throw 'Could not stage the exact validated request payload removals.' }
    Write-RepairLifecycleDiagnostic "Removed $($ValidatedFiles.Count) validated repair request paths."
}
