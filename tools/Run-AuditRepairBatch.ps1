[CmdletBinding()]
param([Parameter(Mandatory = $true)][string]$EvidenceDirectory)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$EvidenceDirectory = [IO.Path]::GetFullPath($EvidenceDirectory)
New-Item -ItemType Directory -Force -Path $EvidenceDirectory | Out-Null
'Unverified repair request; publication has not completed.' | Set-Content (Join-Path $EvidenceDirectory 'status.txt')
if (!(Test-Path .audit-repairs.json)) { return }
$plan = Get-Content .audit-repairs.json -Raw | ConvertFrom-Json
$initialHead = (git rev-parse HEAD).Trim()
$parent = (git rev-parse HEAD^).Trim()
if ($parent -ne $plan.base_sha) { throw 'Repair request is not based on the pinned source revision.' }
if (Test-Path .audit-patch-request.json) { throw 'Legacy and guarded repair requests cannot overlap.' }
if (git status --porcelain) { throw 'The initial checkout is not clean.' }
python tools/apply-audit-repairs.py --preflight
if ($LASTEXITCODE -ne 0) { throw 'Repair preflight failed; no source was changed.' }

function Build-And-Test([string]$label) {
    $dir = Join-Path $EvidenceDirectory $label
    New-Item -ItemType Directory -Force -Path $dir | Out-Null
    msbuild youtube-dl-gui.sln /m /nologo /v:minimal /p:Configuration=Debug '/p:Platform=Any CPU' "/bl:$dir/debug.binlog" | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "Debug build failed at $label" }
    msbuild youtube-dl-gui-updater/youtube-dl-gui-updater.csproj /m /nologo /v:minimal /p:Configuration=Release /p:Platform=AnyCPU "/bl:$dir/updater.binlog" | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "Updater build failed at $label" }
    msbuild youtube-dl-gui/youtube-dl-gui.csproj /m /nologo /v:minimal /p:Configuration=Release /p:Platform=AnyCPU /p:BuildProjectReferences=false /p:PreBuildEvent= /p:PostBuildEvent= "/bl:$dir/application.binlog" | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "Application build failed at $label" }
    & ./tests/Run-AuditRegression.ps1 -EvidenceDirectory $dir | Out-Host
    $testExit = $LASTEXITCODE
    $result = Join-Path $dir 'regression-results.xml'
    if (!(Test-Path $result) -or $testExit -notin @(0, 1)) { throw "Harness did not produce valid results at $label" }
    [xml]$xml = Get-Content $result -Raw
    $cases = @($xml.SelectNodes('/testsuite/testcase'))
    if ($cases.Count -eq 0) { throw 'No tests were executed.' }
    return [pscustomobject]@{
        Names = @($cases | ForEach-Object { $_.GetAttribute('name') } | Sort-Object)
        Failed = @($xml.SelectNodes('/testsuite/testcase[failure]') | ForEach-Object { $_.GetAttribute('name') } | Sort-Object)
    }
}

try {
    $previous = Build-And-Test '00-before'
    $expected = @($plan.expected_failures | Sort-Object)
    if (($expected -join "`n") -cne ($previous.Failed -join "`n")) { throw 'The baseline failures differ from the reviewed request.' }
    git config user.name 'github-actions[bot]'
    git config user.email '41898282+github-actions[bot]@users.noreply.github.com'
    for ($i = 0; $i -lt $plan.repairs.Count; $i++) {
        $repair = $plan.repairs[$i]
        python tools/apply-audit-repairs.py --apply $i
        if ($LASTEXITCODE -ne 0) { throw "Exact application failed for $($repair.id)" }
        git -c core.whitespace=cr-at-eol diff --check
        if ($LASTEXITCODE -ne 0) { throw 'Whitespace validation failed.' }
        $next = Build-And-Test ('{0:D2}-{1}' -f ($i + 1), $repair.id)
        if (($previous.Names -join "`n") -cne ($next.Names -join "`n")) { throw 'A repair changed the executed test inventory.' }
        $introduced = @($next.Failed | Where-Object { $_ -notin $previous.Failed })
        if ($introduced.Count -gt 0) { throw "New regression failures: $introduced" }
        foreach ($test in $repair.resolves) {
            if ($test -notin $previous.Names -or $test -in $next.Failed) { throw "Repair did not satisfy its required regression: $test" }
        }
        $paths = @($repair.files | ForEach-Object { $_.path })
        git --literal-pathspecs add -- @paths 'youtube-dl-gui/Resources/youtube-dl-gui-updater.exe'
        if ($LASTEXITCODE -ne 0) { throw 'Staging failed.' }
        git diff --cached --stat | Out-Host
        git commit -m $repair.message
        if ($LASTEXITCODE -ne 0) { throw 'Verified repair commit failed.' }
        $previous = $next
    }
    if ($previous.Failed.Count -ne 0) { throw 'Remaining regressions prevent publication of this repair batch.' }
    Remove-Item .audit-repairs.json
    git add -- .audit-repairs.json
    git commit -m 'audit: close regression-verified repair batch'
    if ($LASTEXITCODE -ne 0) { throw 'Request cleanup commit failed.' }
    git push origin HEAD:audit-fixes
    if ($LASTEXITCODE -ne 0) { throw 'Branch moved or push failed; no force update was attempted.' }
    'All repairs and the complete regression suite passed.' | Set-Content (Join-Path $EvidenceDirectory 'status.txt')
}
finally {
    git rev-parse HEAD | Set-Content (Join-Path $EvidenceDirectory 'commit.txt')
    git log --format='%H %s' "$initialHead..HEAD" | Set-Content (Join-Path $EvidenceDirectory 'commits.txt')
    git archive --format=zip "--output=$EvidenceDirectory/source.zip" HEAD
    git diff | Set-Content (Join-Path $EvidenceDirectory 'uncommitted.diff')
    git format-patch --stdout "$initialHead..HEAD" | Set-Content (Join-Path $EvidenceDirectory 'verified-commits.patch')
}
