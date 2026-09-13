[CmdletBinding()]
param(
    [string]$ExpectedVersion = '',
    [string]$EvidenceDirectory = (Join-Path $PSScriptRoot '..\release-evidence')
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$Root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$EvidenceDirectory = [IO.Path]::GetFullPath($EvidenceDirectory)
$AuditedBase = '7283444431e1243d83b86bf54838d22e3048cae9'

function Get-ProgramVersion {
    param([Parameter(Mandatory = $true)][string]$Path)

    $Content = Get-Content $Path -Raw
    $Pattern = 'public\s+static\s+Version\s+CurrentVersion\s*\{\s*get;\s*\}\s*=\s*new\(\s*(\d+)\s*,\s*(\d+)\s*,\s*(\d+)(?:\s*,\s*(\d+))?\s*\);'
    $Match = [regex]::Match($Content, $Pattern)
    if (!$Match.Success) {
        throw "Could not parse CurrentVersion from $Path"
    }

    $Major = [int]$Match.Groups[1].Value
    $Minor = [int]$Match.Groups[2].Value
    $Revision = [int]$Match.Groups[3].Value
    $Beta = if ($Match.Groups[4].Success) { [int]$Match.Groups[4].Value } else { 0 }
    $Tag = if ($Beta -gt 0) { "$Major.$Minor.$Revision-$Beta" } else { "$Major.$Minor.$Revision" }

    [pscustomobject]@{
        Major = $Major
        Minor = $Minor
        Revision = $Revision
        Beta = $Beta
        Tag = $Tag
        FileVersion = "$Major.$Minor.$Revision.$Beta"
    }
}

function Assert-AssemblyMetadata {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$FileVersion,
        [Parameter(Mandatory = $true)][string]$InformationalVersion
    )

    $Content = Get-Content $Path -Raw
    $FilePattern = '\[assembly:\s*AssemblyFileVersion\("' + [regex]::Escape($FileVersion) + '"\)\]'
    $InfoPattern = '\[assembly:\s*AssemblyInformationalVersion\("' + [regex]::Escape($InformationalVersion) + '"\)\]'
    if ($Content -notmatch $FilePattern) {
        throw "$Path does not declare AssemblyFileVersion $FileVersion"
    }
    if ($Content -notmatch $InfoPattern) {
        throw "$Path does not declare AssemblyInformationalVersion $InformationalVersion"
    }
}

Push-Location $Root
try {
    $HeadCommit = (git rev-parse HEAD).Trim()
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($HeadCommit)) {
        throw 'Could not determine the candidate commit.'
    }
    if (![string]::IsNullOrWhiteSpace($env:GITHUB_SHA) -and $env:GITHUB_SHA -cne $HeadCommit) {
        throw "Checked-out commit '$HeadCommit' does not match GITHUB_SHA '$env:GITHUB_SHA'."
    }
    git merge-base --is-ancestor $AuditedBase HEAD
    if ($LASTEXITCODE -ne 0) {
        throw "Audited base $AuditedBase is not an ancestor of release candidate $HeadCommit."
    }

    $AppVersion = Get-ProgramVersion 'youtube-dl-gui\Program.cs'
    $UpdaterVersion = Get-ProgramVersion 'youtube-dl-gui-updater\Program.cs'

    if ([string]::IsNullOrWhiteSpace($ExpectedVersion)) {
        $ExpectedVersion = $AppVersion.Tag
    }
    if ($ExpectedVersion -cne $AppVersion.Tag) {
        throw "Release version mismatch: expected '$ExpectedVersion' but application source is '$($AppVersion.Tag)'."
    }

    Assert-AssemblyMetadata 'youtube-dl-gui\Properties\AssemblyInfo.cs' $AppVersion.FileVersion $AppVersion.Tag
    Assert-AssemblyMetadata 'youtube-dl-gui-updater\Properties\AssemblyInfo.cs' $UpdaterVersion.FileVersion $UpdaterVersion.Tag

    $ReleaseNotesPath = Join-Path $Root "release-notes\$($AppVersion.Tag).md"
    if (!(Test-Path $ReleaseNotesPath)) {
        throw "Release notes are missing: $ReleaseNotesPath"
    }
    $ReleaseNotes = Get-Content $ReleaseNotesPath -Raw
    $ExpectedHeading = '^#\s+youtube-dl-gui\s+' + [regex]::Escape($AppVersion.Tag) + '\s*$'
    if ($ReleaseNotes -notmatch "(?m)$ExpectedHeading") {
        throw "Release notes do not begin with the expected $($AppVersion.Tag) heading."
    }
    if ($ReleaseNotes -match '(?im)^(?:exe|zip)\s+sha(?:[- ]?)256\s*:') {
        throw 'Static release notes must not contain hash lines; CI appends the authoritative hashes exactly once.'
    }

    if (Test-Path $EvidenceDirectory) { Remove-Item $EvidenceDirectory -Recurse -Force }
    New-Item -ItemType Directory -Force -Path $EvidenceDirectory | Out-Null
    $ReleaseRoot = Join-Path $Root 'Release'
    if (Test-Path $ReleaseRoot) { Remove-Item $ReleaseRoot -Recurse -Force }

    $DebugBinlog = Join-Path $EvidenceDirectory 'debug.binlog'
    msbuild youtube-dl-gui.sln /m /p:Configuration=Debug '/p:Platform=Any CPU' "/bl:$DebugBinlog"
    if ($LASTEXITCODE -ne 0) { throw "Debug build failed with exit code $LASTEXITCODE." }

    $UpdaterBinlog = Join-Path $EvidenceDirectory 'release-updater.binlog'
    msbuild youtube-dl-gui-updater/youtube-dl-gui-updater.csproj /m /p:Configuration=Release /p:Platform=AnyCPU "/bl:$UpdaterBinlog"
    if ($LASTEXITCODE -ne 0) { throw "Release updater build failed with exit code $LASTEXITCODE." }

    $MainBinlog = Join-Path $EvidenceDirectory 'release-main.binlog'
    msbuild youtube-dl-gui/youtube-dl-gui.csproj /m /p:Configuration=Release /p:Platform=AnyCPU /p:BuildProjectReferences=false /p:PreBuildEvent= /p:PostBuildEvent= "/bl:$MainBinlog"
    if ($LASTEXITCODE -ne 0) { throw "Release application baseline build failed with exit code $LASTEXITCODE." }

    & ./tests/Run-AuditRegression.ps1 -EvidenceDirectory (Join-Path $EvidenceDirectory 'before-full-release')
    if ($LASTEXITCODE -ne 0) { throw "Baseline regression harness failed with exit code $LASTEXITCODE." }

    $FullBinlog = Join-Path $EvidenceDirectory 'release-full.binlog'
    $FullLog = Join-Path $EvidenceDirectory 'release-full.txt'
    msbuild youtube-dl-gui.sln /m /p:Configuration=Release '/p:Platform=Any CPU' "/bl:$FullBinlog" 2>&1 | Tee-Object $FullLog
    if ($LASTEXITCODE -ne 0) { throw "Full Release build failed with exit code $LASTEXITCODE." }

    & ./tests/Run-AuditRegression.ps1 -EvidenceDirectory (Join-Path $EvidenceDirectory 'after-full-release')
    if ($LASTEXITCODE -ne 0) { throw "Post-package regression harness failed with exit code $LASTEXITCODE." }

    & ./tests/Run-AuditRegression.ps1 -EvidenceDirectory (Join-Path $EvidenceDirectory 'x86-full-release') -Platform x86
    if ($LASTEXITCODE -ne 0) { throw "x86 regression harness failed with exit code $LASTEXITCODE." }

    [xml]$Before = Get-Content (Join-Path $EvidenceDirectory 'before-full-release\regression-results.xml') -Raw
    [xml]$After = Get-Content (Join-Path $EvidenceDirectory 'after-full-release\regression-results.xml') -Raw
    [xml]$X86 = Get-Content (Join-Path $EvidenceDirectory 'x86-full-release\regression-results.xml') -Raw
    $BeforeNames = @($Before.SelectNodes('/testsuite/testcase') | ForEach-Object { $_.GetAttribute('name') } | Sort-Object)
    $AfterNames = @($After.SelectNodes('/testsuite/testcase') | ForEach-Object { $_.GetAttribute('name') } | Sort-Object)
    $X86Names = @($X86.SelectNodes('/testsuite/testcase') | ForEach-Object { $_.GetAttribute('name') } | Sort-Object)

    if ($BeforeNames.Count -lt 80) { throw 'Regression inventory unexpectedly contains fewer than 80 tests.' }
    if (($BeforeNames -join "`n") -cne ($AfterNames -join "`n")) { throw 'Full Release build changed the regression inventory.' }
    if (($AfterNames -join "`n") -cne ($X86Names -join "`n")) { throw 'x86 run changed the regression inventory.' }
    if (@($AfterNames | Select-Object -Unique).Count -ne $AfterNames.Count) { throw 'Duplicate regression names were detected.' }
    if ($Before.SelectNodes('/testsuite/testcase/failure').Count -ne 0 -or
        $After.SelectNodes('/testsuite/testcase/failure').Count -ne 0 -or
        $X86.SelectNodes('/testsuite/testcase/failure').Count -ne 0) {
        throw 'Regression failures prevent release-candidate creation.'
    }

    git diff --exit-code -- '*.cs' '*.csproj' '*.projitems' '.github/workflows/*.yml' 'tools/*.ps1'
    if ($LASTEXITCODE -ne 0) { throw 'Build or tests modified reviewed source/release tooling files.' }

    $ExePath = Join-Path $ReleaseRoot 'youtube-dl-gui.exe'
    $ZipPath = Join-Path $ReleaseRoot 'youtube-dl-gui.zip'
    $HashesPath = Join-Path $ReleaseRoot 'Release-hashes.md'
    $UpdaterPath = Join-Path $Root 'youtube-dl-gui-updater\bin\Release\youtube-dl-gui-updater.exe'
    foreach ($Path in @($ExePath, $ZipPath, $HashesPath, $UpdaterPath)) {
        if (!(Test-Path $Path)) { throw "Release output is missing: $Path" }
    }

    $AppVersionInfo = (Get-Item $ExePath).VersionInfo
    if ($AppVersionInfo.FileVersion -cne $AppVersion.FileVersion) {
        throw "Built application file version '$($AppVersionInfo.FileVersion)' does not match '$($AppVersion.FileVersion)'."
    }
    if ($AppVersionInfo.ProductVersion -cne $AppVersion.Tag) {
        throw "Built application product version '$($AppVersionInfo.ProductVersion)' does not match '$($AppVersion.Tag)'."
    }
    $UpdaterVersionInfo = (Get-Item $UpdaterPath).VersionInfo
    if ($UpdaterVersionInfo.FileVersion -cne $UpdaterVersion.FileVersion) {
        throw "Built updater file version '$($UpdaterVersionInfo.FileVersion)' does not match '$($UpdaterVersion.FileVersion)'."
    }
    if ($UpdaterVersionInfo.ProductVersion -cne $UpdaterVersion.Tag) {
        throw "Built updater product version '$($UpdaterVersionInfo.ProductVersion)' does not match '$($UpdaterVersion.Tag)'."
    }

    $ExeHash = (Get-FileHash $ExePath -Algorithm SHA256).Hash.ToLowerInvariant()
    $ZipHash = (Get-FileHash $ZipPath -Algorithm SHA256).Hash.ToLowerInvariant()
    $UpdaterHash = (Get-FileHash $UpdaterPath -Algorithm SHA256).Hash.ToLowerInvariant()
    $HashesText = Get-Content $HashesPath -Raw
    $ExeHashMatch = [regex]::Match($HashesText, '(?im)^exe sha256:\s*([0-9a-f]{64})\s*$')
    $ZipHashMatch = [regex]::Match($HashesText, '(?im)^zip sha256:\s*([0-9a-f]{64})\s*$')
    if (!$ExeHashMatch.Success -or $ExeHashMatch.Groups[1].Value.ToLowerInvariant() -cne $ExeHash) {
        throw 'Packaged executable hash does not match Release-hashes.md.'
    }
    if (!$ZipHashMatch.Success -or $ZipHashMatch.Groups[1].Value.ToLowerInvariant() -cne $ZipHash) {
        throw 'Packaged ZIP hash does not match Release-hashes.md.'
    }

    $SignatureStatus = (Get-AuthenticodeSignature -FilePath $ExePath).Status.ToString()
    if ($SignatureStatus -notin @('Valid', 'NotSigned')) {
        throw "Release executable has an unacceptable Authenticode state: $SignatureStatus"
    }

    $ReleaseBodyPath = Join-Path $ReleaseRoot 'Release-body.md'
    $ReleaseBody = $ReleaseNotes.TrimEnd() + "`r`n`r`nexe sha256: $ExeHash`r`nzip sha256: $ZipHash`r`n"
    Set-Content -Path $ReleaseBodyPath -Value $ReleaseBody -Encoding utf8 -NoNewline
    $GeneratedBody = Get-Content $ReleaseBodyPath -Raw
    $BodyExeHashes = [regex]::Matches($GeneratedBody, '(?im)^exe sha256:\s*[0-9a-f]{64}\s*$')
    $BodyZipHashes = [regex]::Matches($GeneratedBody, '(?im)^zip sha256:\s*[0-9a-f]{64}\s*$')
    if ($BodyExeHashes.Count -ne 1 -or $BodyZipHashes.Count -ne 1) {
        throw 'Generated release body must contain exactly one executable hash line and one ZIP hash line.'
    }
    if ($GeneratedBody -notmatch ('(?im)^exe sha256:\s*' + [regex]::Escape($ExeHash) + '\s*$')) {
        throw 'Generated release body does not expose the executable SHA-256 in updater-compatible form.'
    }
    if ($GeneratedBody -notmatch ('(?im)^zip sha256:\s*' + [regex]::Escape($ZipHash) + '\s*$')) {
        throw 'Generated release body does not expose the ZIP SHA-256 in publication-compatible form.'
    }

    Copy-Item $ReleaseNotesPath (Join-Path $ReleaseRoot "Release-notes-$($AppVersion.Tag).md") -Force

    $Manifest = [ordered]@{
        version = $AppVersion.Tag
        prerelease = ($AppVersion.Beta -gt 0)
        commit = $HeadCommit
        audited_base = $AuditedBase
        application_file_version = $AppVersionInfo.FileVersion
        application_product_version = $AppVersionInfo.ProductVersion
        updater_version = $UpdaterVersion.Tag
        updater_file_version = $UpdaterVersionInfo.FileVersion
        updater_product_version = $UpdaterVersionInfo.ProductVersion
        target_framework = 'net472'
        tests_per_pass = $AfterNames.Count
        regression_passes = 3
        runtime_platforms = @('AnyCPU on x64 runner', 'x86')
        failures = 0
        application_signature_status = $SignatureStatus
        exe_sha256 = $ExeHash
        zip_sha256 = $ZipHash
        updater_sha256 = $UpdaterHash
    }
    $Manifest | ConvertTo-Json -Depth 4 | Set-Content (Join-Path $ReleaseRoot 'Release-manifest.json') -Encoding utf8
    $Manifest | ConvertTo-Json -Depth 4 | Set-Content (Join-Path $EvidenceDirectory 'release-candidate-summary.json') -Encoding utf8

    Write-Host "Release candidate $($AppVersion.Tag) is ready."
    Write-Host "EXE SHA-256: $ExeHash"
    Write-Host "ZIP SHA-256: $ZipHash"
    Write-Host "Updater SHA-256: $UpdaterHash"
}
finally {
    Pop-Location
}
