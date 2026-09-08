[CmdletBinding()]
param(
    [string]$EvidenceDirectory = (Join-Path $PSScriptRoot '..\audit-evidence'),
    [string]$ApplicationPath = (Join-Path $PSScriptRoot '..\youtube-dl-gui\bin\Release\youtube-dl-gui.exe')
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$EvidenceDirectory = [IO.Path]::GetFullPath($EvidenceDirectory)
$ApplicationPath = [IO.Path]::GetFullPath($ApplicationPath)
New-Item -ItemType Directory -Force -Path $EvidenceDirectory | Out-Null
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (!(Test-Path $csc)) { $csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
if (!(Test-Path $csc)) { throw '.NET Framework C# compiler was not found.' }
if (!(Test-Path $ApplicationPath)) { throw "Application assembly was not found: $ApplicationPath" }
$exe = Join-Path $EvidenceDirectory 'AuditRegression.exe'
$sources = @(Get-ChildItem (Join-Path $PSScriptRoot 'AuditRegression*.cs') | ForEach-Object { $_.FullName })
& $csc /nologo /langversion:5 /target:exe "/out:$exe" /reference:System.Core.dll /reference:System.Xml.Linq.dll /reference:System.Windows.Forms.dll /reference:System.Drawing.dll @sources
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
$stdout = Join-Path $EvidenceDirectory 'regression-console.txt'
$stderr = Join-Path $EvidenceDirectory 'regression-stderr.txt'
$result = Join-Path $EvidenceDirectory 'regression-results.xml'
$p = Start-Process -FilePath $exe -ArgumentList @("`"$ApplicationPath`"", "`"$result`"") -PassThru -RedirectStandardOutput $stdout -RedirectStandardError $stderr
try {
    # A modal dialog or broken UI callback must not consume the entire Actions job.
    if (!$p.WaitForExit(240000)) {
        & taskkill.exe /PID $p.Id /T /F | Out-Host
        $p.WaitForExit(5000) | Out-Null
        'Regression harness exceeded its four-minute deadline.' | Add-Content $stderr
        $code = 124
    } else {
        $p.WaitForExit()
        $code = $p.ExitCode
    }
} finally { $p.Dispose() }
if (Test-Path $stdout) { Get-Content $stdout | Out-Host }
if (Test-Path $stderr) { Get-Content $stderr | Out-Host }
if ($code -ne 0) { Write-Host "Compiled-application regressions failed (exit $code)." }
exit $code
