<#
.SYNOPSIS
Create a reproducible ZIP from a staged release directory.
.DESCRIPTION
Entries are sorted by ordinal relative path, timestamped at the stable ZIP
source epoch 2000-01-01T00:00:00Z, and compressed at .NET Optimal. No filesystem
mtime, locale-sensitive listing order, or wall-clock value enters the archive.
The .NET runtime/zlib version remains a separately documented toolchain input.
.EXAMPLE
./tools/New-DeterministicReleaseZip.ps1 -SourceDirectory ./obj/Release/Package -DestinationFile ./Release/youtube-dl-gui.zip
.EXAMPLE
./tools/New-DeterministicReleaseZip.ps1 -SourceDirectory ./staging -DestinationFile ./test.zip -Troubleshoot
.NOTES
Supports Windows PowerShell 5.1 and PowerShell 7.x. Error log beside script,
falling back to TEMP. No helper binaries or cloud dependency.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$SourceDirectory,
    [Parameter(Mandatory=$true)][string]$DestinationFile,
    [switch]$Troubleshoot
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$epoch = [DateTimeOffset]::new(2000, 1, 1, 0, 0, 0, [TimeSpan]::Zero)
try {
    Add-Type -AssemblyName System.IO.Compression
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $sourceRoot = [IO.Path]::GetFullPath($SourceDirectory).TrimEnd([IO.Path]::DirectorySeparatorChar)
    $destination = [IO.Path]::GetFullPath($DestinationFile)
    if (!(Test-Path -LiteralPath $sourceRoot -PathType Container))
        { throw 'Release packaging source directory does not exist.' }
    $rootInfo = Get-Item -LiteralPath $sourceRoot -Force
    if (($rootInfo.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0)
        { throw 'Release packaging source is a reparse point.' }
    if ($destination.StartsWith($sourceRoot + [IO.Path]::DirectorySeparatorChar,
        [StringComparison]::OrdinalIgnoreCase))
        { throw 'ZIP destination must not be nested inside its own source directory.' }

    $all = @(Get-ChildItem -LiteralPath $sourceRoot -Recurse -Force)
    if ($all.Count -eq 0) { throw 'Release staging directory is empty.' }
    foreach ($item in $all) {
        if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0)
            { throw "Release staging contains a reparse point: $($item.FullName)" }
        if ($item -isnot [IO.FileInfo] -and $item -isnot [IO.DirectoryInfo])
            { throw "Release staging contains an unsupported path type: $($item.FullName)" }
    }

    $allFiles = @($all | Where-Object { $_ -is [IO.FileInfo] })
    if ($allFiles.Count -eq 0) { throw 'Release staging contains no payload files.' }
    $relativePaths = @($allFiles | ForEach-Object {
        $_.FullName.Substring($sourceRoot.Length + 1).Replace('\\', '/')
    })
    [Array]::Sort($relativePaths, [StringComparer]::Ordinal)

    $parent = [IO.Path]::GetDirectoryName($destination)
    if (!(Test-Path -LiteralPath $parent -PathType Container))
        { [IO.Directory]::CreateDirectory($parent) | Out-Null }
    $zipStream = [IO.File]::Open($destination, [IO.FileMode]::Create,
        [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
    try {
        $archive = [IO.Compression.ZipArchive]::new($zipStream,
            [IO.Compression.ZipArchiveMode]::Create, $true)
        try {
            foreach ($relative in $relativePaths) {
                $sourcePath = [IO.Path]::Combine($sourceRoot,
                    $relative.Replace('/', [IO.Path]::DirectorySeparatorChar))
                $entry = $archive.CreateEntry($relative,
                    [IO.Compression.CompressionLevel]::Optimal)
                $entry.LastWriteTime = $epoch
                $entry.ExternalAttributes = 0
                $inputStream = [IO.File]::Open($sourcePath, [IO.FileMode]::Open,
                    [IO.FileAccess]::Read, [IO.FileShare]::Read)
                try {
                    $entryStream = $entry.Open()
                    try { $inputStream.CopyTo($entryStream) }
                    finally { $entryStream.Dispose() }
                }
                finally { $inputStream.Dispose() }
                if ($Troubleshoot) { Write-Host "Packed $relative" }
            }
        }
        finally { $archive.Dispose() }
    }
    finally { $zipStream.Dispose() }
    Write-Host "Deterministic ZIP created: $destination ($($relativePaths.Count) files, fixed UTC epoch)."
}
catch {
    $message = "Deterministic ZIP failed: $($_.Exception.Message)"
    $log = Join-Path $PSScriptRoot 'New-DeterministicReleaseZip.log'
    try { Add-Content -LiteralPath $log -Value ("{0:O} {1}" -f (Get-Date), $message) }
    catch {
        $tmp = if ($env:TEMP) { $env:TEMP } else { [IO.Path]::GetTempPath() }
        try { Add-Content -LiteralPath (Join-Path $tmp 'New-DeterministicReleaseZip.log') -Value $message }
        catch { }
    }
    throw $message
}
