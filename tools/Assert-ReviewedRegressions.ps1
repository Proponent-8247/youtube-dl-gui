<#
.SYNOPSIS
    Enforce mandatory reviewed regression coverage from an immutable Git commit.
.DESCRIPTION
    Candidate-local test lists are untrusted. This gate reads the reviewed test
    names through git show at an exact approved commit and rejects missing or
    duplicate tests in compiled regression XML. Additional tests are allowed.
    Approving removal/rename requires an independently reviewed manifest revision.
.PARAMETER ResultsXml
    Path to a regression-results.xml emitted by Run-AuditRegression.ps1.
.PARAMETER RepositoryRoot
    Checked-out source repository root. Defaults to parent of this script.
.PARAMETER troubleshoot
    Optional diagnostics; logs under script directory, with TEMP fallback.
.EXAMPLE
    ./tools/Assert-ReviewedRegressions.ps1 -ResultsXml audit-evidence/regression-results.xml
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$ResultsXml,
    [string]$RepositoryRoot = (Join-Path $PSScriptRoot '..'),
    [switch]$troubleshoot
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$ReviewedManifestCommit = 'ec7ee09e5658e8371ef2ac422f805f74411c61c4'
$ReviewedManifestPath = 'tests/REVIEWED-REGRESSION-NAMES.txt'

function Write-ReviewedDiagnostic([string]$Message) {
    if (!$troubleshoot) { return }
    $file = 'ReviewedRegressionIntegrity.log'
    try { Add-Content -LiteralPath (Join-Path $PSScriptRoot $file) -Value $Message -ErrorAction Stop }
    catch {
        $folder = if ($env:TEMP) { $env:TEMP } else { [IO.Path]::GetTempPath() }
        Add-Content -LiteralPath (Join-Path $folder $file) -Value $Message -ErrorAction Stop
    }
}

$RepositoryRoot = [IO.Path]::GetFullPath($RepositoryRoot)
$ResultsXml = [IO.Path]::GetFullPath($ResultsXml)
if (!(Test-Path -LiteralPath $ResultsXml -PathType Leaf)) {
    throw 'Compiled regression XML is missing; reviewed coverage cannot be established.'
}
Push-Location $RepositoryRoot
try {
    $manifestObject = "${ReviewedManifestCommit}:$ReviewedManifestPath"
    $rawNames = @(git show $manifestObject)
    if ($LASTEXITCODE -ne 0) {
        throw "Trusted reviewed regression manifest at $manifestObject cannot be loaded."
    }
    $required = @($rawNames | ForEach-Object { $_.Trim() } | Where-Object { $_.Length -gt 0 })
    if ($required.Count -ne 312) { throw "Unexpected immutable reviewed regression inventory size: $($required.Count)." }
    if (@($required | Sort-Object -Unique -CaseSensitive).Count -ne $required.Count) {
        throw 'Immutable reviewed regression inventory contains duplicate names.'
    }
    [xml]$xml = Get-Content -LiteralPath $ResultsXml -Raw
    $cases = @($xml.SelectNodes('/testsuite/testcase'))
    if ($cases.Count -eq 0) { throw 'Compiled regression XML contains no test cases.' }
    $observed = @($cases | ForEach-Object { $_.GetAttribute('name') })
    if (@($observed | Sort-Object -Unique -CaseSensitive).Count -ne $observed.Count) {
        throw 'Current regression run has duplicate test names.'
    }
    $missing = @($required | Where-Object { $_ -cnotin $observed })
    if ($missing.Count -gt 0) {
        throw "Current suite is missing reviewed regressions: $($missing -join ', ')"
    }
    Write-ReviewedDiagnostic "Candidate regression inventory contains $($observed.Count) tests and all $($required.Count) reviewed names from $ReviewedManifestCommit."
    Write-Host "Reviewed regression coverage verified: $($required.Count) required of $($observed.Count) current cases; trust commit $ReviewedManifestCommit"
}
finally { Pop-Location }
