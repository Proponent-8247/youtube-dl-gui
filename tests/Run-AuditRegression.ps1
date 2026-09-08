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
& $exe $ApplicationPath (Join-Path $EvidenceDirectory 'regression-results.xml') 2>&1 | Tee-Object -FilePath (Join-Path $EvidenceDirectory 'regression-console.txt')
$code = $LASTEXITCODE
if ($code -ne 0) { Write-Host "Compiled-application regressions failed (exit $code)." }
exit $code
