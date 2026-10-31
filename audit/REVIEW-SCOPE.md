# Whole-repository review scope and verification

## Revision and inventory

Reviewed baseline: `e5858408008e17a34241779f18e7dfa1fed6a057` on `audit-fixes`. Master comparison: `c6ce1e6421c6bf785f30f41496b745489bcc2bab`. The source snapshot was recovered from the exact-revision Audit verification artifact, not an assumed default branch. The production source remains unchanged by this review; subsequent commits contain review probes, evidence and documentation.

The baseline has **274 tracked files**. A case-insensitive Windows project-inclusion resolver identifies **182 unique declared compiled C# files** and **2 generated source paths** (UpdaterHash.g.cs and BuildDate.g.cs). Of the declared files, 26 are designer/resource-generated C# and 156 are other C# files. Shared files compiled into both executables are counted once. Three older C# copies are excluded by the projects: app/Classes/ConvertInfo.cs, app/Classes/DownloadInfo.cs and app/Controls/UpdateMessageHandler.cs.

| Baseline category | Files | Review level |
| --- | ---: | --- |
| Compiled C# | 182 | Source/control-flow and caller/ownership review; designer/resource-generated portions inspected structurally. |
| Excluded legacy C# | 3 | Inclusion status and source comparison; not presented as shipping fixes. |
| Tests and test runner | 17 | Actual fixture scenarios, assertions, cleanup and retained CI evidence; coverage gaps cataloged. |
| Translations and resx | 37 | Eleven INI files and 26 XML resources inspected for structure, keys, placeholders and bindings; not a linguistic/visual certification. |
| Binary assets | 13 | Identity and build/resource references; not exhaustive reverse engineering. Includes PNG/ICO and the embedded updater executable. |
| Build/configuration/automation | 12 | Solution, projects, shared-project items, application config, CI and repair/export tooling. |
| Documentation/metadata | 9 | User-facing contracts, earlier review records, license presence and repository metadata. |
| Browser add-on | 1 | Actual userscript source plus local DOM characterization. |

The repository-wide path/size/SHA-256 inventory is retained in the delivered review evidence bundle. It describes files and review levels, **not statement/branch coverage**. No user credentials, installed application or media library were used.

## End-to-end paths traced

Review followed startup/configuration -> direct CLI and registered protocol -> single-instance IPC -> quick/extended/batch request construction -> selector and argument generation -> helper/process output -> completion/cancellation -> UI state and persistence. It also followed conversion/merger/GIF inputs through probe, mapping and output lifetime; update discovery through metadata cache, IPC, download, hash, replacement and rollback; localization through files and initial/reload dispatch; and exception reporting through the real logging gate.

The review additionally covered authentication buffers and cloning, native wrappers/control ownership, included versus excluded files, generated hashes/build dates, package staging, repair workflow branch/hash guards, evidence retention and the difference between helper-level tests and live wire/application paths. The userscript and root CLI/compiler documentation were included rather than excluded as peripheral files.

## Positive baseline and new negative evidence

- Baseline run `34311045175` originally completed the builds, regressions and full-package checks but failed evidence upload after repeated CreateArtifact timeouts. Rerun job `102348304986` completed **all steps successfully**, including evidence retention. This was an artifact-transport failure, not evidence of a code regression.
- Normal Audit verification run `34315539708` passed on review-infrastructure revision `9f5a30ccd79fe1479fc918b7d388b928043f8caa`. Production source was unchanged.
- Windows finding-characterization run `34315957693` on `b4536ae7b322a0c524dd24a53a2ec1560deaf83d` reproduced 12 expected observations. It built the actual Release application and loaded that assembly in isolated probe processes with disposable working directories. One case intentionally times out after entering the slow duration getter. Eleven others return the explicit observed-defect exit code 42.
- The userscript was executed locally under Node with the actual checked-in JavaScript and a minimal old-layout DOM fixture. The two-post negative case and single-post control establish script logic, not current live Reddit/browser integration.
- All 26 resx files parsed as XML; all 11 language files were inspected. Three About-body placeholder arity mismatches were found (O018). Valid XML alone is not semantic validity.
- Both csproj files reference framework assemblies; no NuGet PackageReference/package manifest is present in this snapshot. Actual selected downloader/FFmpeg/ImageMagick binaries are external deployment inputs and are not pinned by this source inventory. No invented CVE applicability is assigned to unknown installed versions.

## Deliberately rejected or narrowed leads

The warm-start custom-argument lead was rejected after reading Program.ParseCopyData line 525, which restores LastUsedYtdlArgument. The no-audio preference mismatch remains O009. The hidden/disabled detailedErrors control was not promoted to a shipped active feature. Batch thumbnails are disabled, so row-removal analysis does not claim that every removed batch item leaks a thumbnail. URL operand separation and genuine hashing/rollback fixes were retained rather than declared absent because other boundaries remain open.

The malformed TimePicker observation is explicitly a call into the actual parser after setting malformed display text; the full key/paste path is still to verify. The format-ID observation proves generated argument splitting, not a demonstrated remote-code-execution chain. The updater sender/session item and predictable-sidecar collisions remain leads/risks, not reproduced exploits.

## Remaining validation boundaries

This is whole-repository **source and end-to-end-path review with selected executed characterization**, not exhaustive live integration testing of every possible path. Current-provider downloads, multi-hour channel/queue soaks, every legacy OS/DPI/device combination, crash/power-loss storage recovery, image decoder isolation, complete external-binary supply-chain review and publisher signing are not established by these results. The historical verification/policy checklist and V011 retain that work explicitly.

Findings should close only with a focused correction or explicit accepted-risk decision, a named test/reproducer, exact revision, environment and outcome. A successful documentation-only build must never check off an unfixed O-item. No cosmetic mass refactoring, history rewrite, forced container policy or separate download-history feature was performed.
