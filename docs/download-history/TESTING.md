# Verification and acceptance coverage

## Scope and baseline

The feature branch starts from `audit-fixes` commit
`d1c7c91b712708a0df0fa019409acc07bc369bec`, not the original master. The parallel
review branch is not modified by this work. No published history is rewritten.
The existing WinForms/.NET Framework 4.7.2 target and C# language setting remain.
New source files are isolated; existing downloader changes are limited to shared
launch/postflight hooks, censored previews, protected metadata-probe configuration,
Settings integration and project compile entries.

The clean baseline was built in Windows CI before feature changes. Each candidate
change was reviewed and subjected to the Windows build matrix and available tests.
Failed patch checks and tests invalidated that candidate's results. Corrections
were new commits; failing assertions were retained. Intermediate commits used an
exact, pinned-base patch transport because the editing container did not have a
Windows build toolchain. The delivery workflow is an ordinary read-only checkout,
build and test workflow, with no auto-commit or patch-application behavior.

## Reproduce

Use Visual Studio 2022 Build Tools with a C# 12-capable compiler and the .NET
Framework 4.7.2 targeting pack on Windows. From a developer PowerShell in the repo:

```powershell
msbuild youtube-dl-gui\youtube-dl-gui.csproj /m /p:Configuration=Debug /p:Platform=AnyCPU
msbuild youtube-dl-gui-updater\youtube-dl-gui-updater.csproj /m /p:Configuration=Debug /p:Platform=AnyCPU
msbuild youtube-dl-gui.sln /m /p:Configuration=Debug "/p:Platform=Any CPU"
msbuild youtube-dl-gui-updater\youtube-dl-gui-updater.csproj /m /p:Configuration=Release /p:Platform=AnyCPU
msbuild youtube-dl-gui\youtube-dl-gui.csproj /m /p:Configuration=Release /p:Platform=AnyCPU /p:BuildProjectReferences=false /p:PreBuildEvent= /p:PostBuildEvent=
msbuild tests\DownloadHistory.Tests\DownloadHistory.Tests.csproj /m /p:Configuration=Release /p:Platform=AnyCPU
& tests\DownloadHistory.Tests\bin\Release\DownloadHistory.Tests.exe
```

Check each command's exit code before proceeding. The Release application build
suppresses the repository's external packaging pre/post-build hooks in CI; this is
not a claim that an independent release/signing toolchain has been exercised.
The updater is built separately first. No application dependencies are replaced.

The offline integration suite also needs Python 3.12 and the pinned yt-dlp test
dependency. These are test dependencies, not new dependencies of the GUI:

```powershell
python -m pip install yt-dlp==2026.6.9
New-Item -ItemType Directory -Force test-evidence | Out-Null
$python = (Get-Command python).Source
& tests\DownloadHistory.Tests\bin\Release\DownloadHistory.Tests.exe --native-suite youtube-dl-gui\bin\Release\youtube-dl-gui.exe $python tests\DownloadHistory.Tests\native_fixture.py test-evidence
```

The core suite compiles production history classes directly and treats test
compilation warnings as errors. The native suite loads the actual built GUI
assembly and calls its configuration, launch, argument-generation, maintenance
and dialog code. A small adapter substitutes only deterministic extraction/network
fixtures; it uses the real yt-dlp CLI parser, HTTP downloader, post-processing flow
and download archive. The local HTTP request count proves whether an additional
media transfer happened. No YouTube/channel content is downloaded by these tests.
Tiny fixture bytes test transport/identity behavior, not container codec validity.

## Acceptance matrix

| Requested case | Automated evidence | Remaining boundary |
| --- | --- | --- |
| 1. Container change | Real yt-dlp skips same source after title/extension change; one HTTP transfer. | Not a live YouTube codec-format test. |
| 2. Channel refresh | Fixture collection re-enumerates, skips old entries, downloads one new entry. | YouTube service/rate-limit behavior is external. |
| 3. Mixed sources | Nested playlist/channel/direct inputs share one archive; repeated batch adds no media transfers. | Full interactive queue clicking is not automated. |
| 4. Deleted archive | Delete primary, backup and checkpoint; metadata rebuild prevents another media transfer. | Disk corruption beyond fixtures remains operational testing. |
| 5. Missing storage | Missing custom-archive parent stops without creating a directory or archive. | Physical unplug, ACL denial and remote-share failure need manual tests. |
| 6. Enable without IDs | Unescaped ID, basename and relative-path validation; suggestion generation. | Interactive enable warning needs manual accessibility review. |
| 7. Remove IDs while enabled | Same policy guards Settings save and every effective launch template. | Manual verification of all keyboard/UI paths is advised. |
| 8. Metadata migration | Media and metadata/subtitle rename; collision preflight; native rerun causes no transfer. | Broad unusual sidecar naming coverage remains manual. |
| 9. Large unidentified library | 10,000 unidentified media files are all reported and no empty ledger created. | Not a throughput benchmark for the full production library. |
| 10. Partial migration | Mixed identifiable/unidentified input reports Partial and blocks activation. | Bulk rename currently waits until unresolved files are addressed. |
| 11. Failed download | Actual failing yt-dlp postprocessor does not archive the source; retry succeeds. Core tests retain successes in a partially failed batch. | Power loss / all postprocessor variants are not exhaustively fault-injected. |
| 12. Disable | Native runtime and built dialog preserve archive/backup bytes and omit native archive use. | Existing manual archive arguments remain user-owned when disabled. |
| 13. Disabled interval with IDs | Namespace-bearing filenames reconcile on re-enable; no duplicate transfer. | Bare multi-provider IDs require metadata or namespace evidence. |
| 14. Disabled interval without IDs | Metadata-backed ID-less files migrate; unknown files block without changing old ledger. | Unknown identities require manual remediation. |
| 15. Never enabled | Native run permits no-ID template; no archive and no forced metadata. | Existing unrelated baseline behavior was not exhaustively re-audited. |
| 16. Concurrent jobs | Parallel native launch calls produce one HTTP transfer; exclusive lease, orphan child and failed-start tests. | Multiple machines, SMB aliases and unprotected writers need manual validation. |

Additional regressions cover Windows argument quoting, both actual GUI argument
builders, the `--` URL terminator, custom archive/output overrides, explicit legacy
namespace declarations, ambiguous IDs, malformed metadata, damaged primary/backup
recovery, per-library interrupted markers, failed evidence across archive scope
changes, cancellation and service-file classification. The native suite renders
the actual Download History dialog and exercises its disable/Apply operation.

## Review findings addressed during implementation

The initial archive engine could lose failed-media records when a custom archive
moved to another root; a library-local retry manifest fixes that. An interrupted
run could previously be bypassed by switching archive paths; a library-wide marker
now binds it to the original archive. An old backup was initially misclassified
as unknown media; the regression remains. Actual audit-branch argument builders
use a `--` delimiter, so native flags must precede that delimiter; native tests now
execute both builders. Temporary-file classification is restricted to suffixes
rather than matching ordinary title components.

Mixed original source line endings were preserved by exact-byte patches. A failed
patch application is not a passed build. A test-adapter stdout forwarding defect
was fixed in the adapter rather than weakening the application's capability check.

## Delivery gate and known unverified behavior

Read the final workflow run and included logs for exact counts and revision.
A successful source snapshot, binary artifact and `tested-revision.txt` must all
name the same delivery commit. Do not install an intermediate source snapshot or
a binary from a failed run as the final feature build.

No claim is made of a complete fresh audit of every unrelated application file.
This is a reviewed feature delta on the already audited baseline. Unit/native tests
do not establish universal correctness or replace a backup. Before a broad rollout,
use a copied representative library and manually test Windows 10 LTSC, any retained
Windows 7 configuration, high DPI, SMB/mapped drives, permission changes, power loss,
manual filename changes, very long paths and the production downloader version.

The feature intentionally fails closed on unsupported providers/arguments/templates,
reparse points, ambiguous identities and inaccessible storage. It does not verify
legacy-media decoding, protect against uncoordinated external file writers, or
operate as a security sandbox for a malicious downloader executable.
