# Download History / Duplicate Prevention feature review

Review started: 2026-09-16

## Pinned review source

- Repository: `Proponent-8247/youtube-dl-gui`
- Feature branch: `feature/download-history-archive-3.3.0-2`
- Production/source revision under review: `1bf2112eafb3f9083efbd68f22a41318103c09e0`
- Base revision: `49a290d07b220e4a9cd98d44c6db7f45a5bbbc98` (`release/3.3.0-2`)
- The feature revision is 87 commits ahead and 0 behind the base.
- This audit document is deliberately outside production/application source. Later documentation commits do not change the pinned source revision being reviewed.

## Baseline evidence

The retained Windows CI repair artifact for the immediately preceding guarded repair batch was reconciled before this review:

- Before the final extractor-identity repair: 217 regression cases, 0 failures.
- After the repair: 218 regression cases, 0 failures.
- The guarded repair workflow also completed Debug solution, Release updater, and Release application builds before and after the repair.
- The final repair commit was `e89f194b6728b52b8085ef2031117defa456d033` (`fix: require authoritative extractor identity for recovery`), followed by the request-cleanup commit at the pinned review source.

This is the pre-review baseline, not proof that the feature is defect-free.

## User acceptance constraints recovered during review

- The feature is intended to inventory a large, pre-existing, recursively organized media library and then use the resulting history/archive to prevent duplicate future downloads.
- Existing directory hierarchy must be preserved.
- Existing media and companion files such as `.info.json`, `.description`, thumbnails, subtitles, and live-chat metadata must remain in place.
- **No existing library file may be renamed, moved, or rewritten during inventory, reconciliation, archive generation, or validation.**
- The library scan therefore has to be strictly non-destructive with respect to the user's media tree. Archive/history files maintained by the application are the only files that may be created/updated as part of this feature.
- The active download destination and the pre-existing media library may be different directories. Download History must be able to inventory both simultaneously into one native yt-dlp archive; existing-library roots are scan-only, while new downloads continue to be written only to the application's active download destination.
- Media locations are not stable identifiers. The contents of the active download directory may be moved or reorganized without notice, and existing-library paths may change. History must remain authoritative by native provider identity rather than by the current filesystem path of a media file.
- Changing the active download directory, moving previously inventoried media, or changing the scan-only root set must not invalidate already-recorded archive identities or require the old media path to remain online during normal protected downloading.
- The design should not require the user to merge, rename, move, or reorganize existing media merely to participate in duplicate prevention.

Representative layout supplied during review:

`Downloads\YT-DL\x.com\Video\Ryomen\`

- `Ryomen - ...-2084613745251106816.description`
- `Ryomen - ...-2084613745251106816.info.json`
- `Ryomen - ...-2084613745251106816.jpg`
- `Ryomen - ...-2084613745251106816.mp4`

and nested trees such as:

`Downloads\YT-DL\youtube.com\Video\Casual Geographic\...`

The supplied real-world listing also contains same-stem `.description`, `.info.json`, `.webm`/`.mkv`, `.webp`, `.live_chat.json`, and playlist-level metadata without a media file. Those companions must remain sidecars rather than independent completed media.

## Feature implementation issue register

This is the canonical running list of issues relevant to this feature implementation. Verified findings receive a stable `DH-A###` ID. Closed leads remain recorded so the review result is explicit rather than silently dropping investigated concerns.

| ID | Severity | State | Summary |
| --- | --- | --- | --- |
| DH-A001 | High | Fixed / regression-verified | Recovered `.info.json` identity is validated as exactly one native archive record before publication. |
| DH-A002 | High | Fixed / regression-verified | Existing-library inventory is non-destructive; rename/move migration paths were removed. |
| DH-A003 | High | Fixed / regression-verified | Explicit `Rebuild Archive` now forces authoritative physical inventory while preserving prior archive identities. |
| DH-A004 | High | Fixed / regression-verified | History is decoupled from media paths and supports additional scan-only roots; the residual normal-execution dependency was repaired under DH-A006. |
| DH-A005 | Medium | Fixed / regression-verified | Inventory now streams media, uses a reusable filename-identity matcher, avoids redundant management rescans, and runs long scans off the WinForms UI thread. |
| DH-A006 | High | Fixed / regression-verified | Normal protected execution validates only the application-owned ledger/backup and no longer scans or requires media roots after restart/cache loss. |
| DH-A007 | High | Fixed / regression-verified | Protected arguments now disable ambient/default yt-dlp plugin discovery and reject positive custom plugin-directory overrides. |
| DH-A008 | Medium | Fixed / regression-verified | Target and rollback settings writes now persist `Enabled=false` first and restore the intended enabled state only after every dependent key succeeds. |
| DH-A009 | High | Fixed / regression-verified | Explicit management reconciliation now treats a changed active download root as a new inventory input without rebinding normal protected downloads to media paths. |
| DH-A010 | High | Fixed / regression-verified | Unbound custom invalid archive targets are refused without mutation, the archive path is excluded from media inventory, and established default-archive corruption recovery remains intact. |
| DH-A011 | High | Fixed / regression-verified | Cluster-aware short-option parsing now detects hidden `-o` / `-P` overrides without misreading attached values belonging to earlier value-taking options. |
| DH-A012 | High | Fixed / regression-verified | Invalid custom archive primaries without a valid backup are never overwritten automatically, even when the path was previously bound; reserved default-archive recovery remains intact. |
| DH-A013 | High | Fixed / regression-verified | Companion handling preserves pre-existing lock/temp files, uses unique create-new temp files, and refuses invalid backup collisions before primary mutation. |
| DH-A014 | High | Fixed / regression-verified | Archive relocation preserves the union of the previous ledger/backup and prepared candidate while holding both leases; the dialog now distinguishes the bound implicit archive from a new active root's default path. |
| DH-A015 | High | Fixed / regression-verified | Protected custom arguments now reject source replacement and extractor-selection overrides while leaving ordinary multi-source input mechanisms available. |
| DH-A016 | Medium | Verified | A transient provider change in the parent Settings dialog can be used to enable Download History, then parent Cancel restores the previous incompatible youtube-dl provider while leaving history enabled. |
| DH-L002 | — | Closed / no defect found | Archive mutation is serialized by the archive-derived mutex plus an on-disk exclusive lock; first-use directory creation acquires the file lock immediately after creation, and existing regression coverage verifies serialization and cancellation. |
| DH-L003 | — | Closed / config bypass not found; plugin gap promoted to DH-A007 | Protected commands isolate config locations/aliases and conflicting archive/output hooks. A separate ambient-plugin isolation gap discovered during final re-audit is tracked as DH-A007. |
| DH-L004 | — | Closed for ordinary UI semantics; failure-atomicity gap promoted to DH-A008 | Rebuild and Reset are explicit archive-management actions and media remains non-destructive. A separate fail-closed persistence issue under partial INI-write/rollback failure is tracked as DH-A008. |
| DH-L006 | — | Closed / intended fail-safe behavior | Media without authoritative `.info.json` identity or an unambiguous filename match to a known native archive identity is reported unresolved; the implementation deliberately does not infer YouTube merely from an 11-character ID shape. |
| DH-L007 | — | Closed / classifier verified | Completed-media enumeration is allowlisted to media extensions. Known sidecars including `.info.json`, generic `.json`/`.live_chat.json`, descriptions, common thumbnails, subtitles, `.part`, `.ytdl`, and text files are not counted as media. Regression coverage will be expanded with the supplied real-world family shape. |

Repair evidence recorded so far:
- DH-A001: `8de689b98653f02975ae8559da85018e8b45a739` (`fix: validate recovered download archive identities`), closed by guarded batch `ba28f8a634510615b2c421d6c02d2b73c84fca6a`.
- DH-A002: `d7225e8771e4628f0245aafe4a10a3c01fb9bcbd` (`fix: inventory existing media without renaming files`), closed by guarded batch `270fd58e2d54da82ed20efb70fdde1922b9ff7b5`.
- DH-A003: `007044f2e372c0bb8d29c2dbf2011263d4455d7b` (`fix: make explicit archive rebuild inventory authoritative media`), closed by guarded batch `7250344c32e0c0e6573103dad009c46e5591a163`.
- DH-A004: `b2f1201825acc11f31e3c4e2b343e33553562a59` and `d84afa98890a346e578abe6409d4de641dc94ae8`, closed by guarded batches `af044a477ef1395465f859fef0502d4a027ed240` and `1e30865ecba0c698401b4ec7239d5fe973999bf2`.
- DH-A006: `9860f740be48cba18650c1f7b08b95f313545070` (`fix: decouple protected downloads from inventory roots`).
- DH-A005: `a238742ce63ba6e890c27e24f4e895e32ce90a0a` (`perf: make download history inventory single-pass`).
- DH-A005/A006 were closed by guarded workflow run `35296863602`, cleanup commit `9313543d0fd31dcd1130bfb46289aa1b2f0f0fa3`. The guard demonstrated all three new regressions failing before the repairs and passing afterward while also completing Debug solution, Release updater, Release application, and full regression gates.
- The first A005/A006 repair request was deliberately discarded at `76d58d3616e0845f7ec00a0d222f52b2dbfa17ce` after re-audit caught a cross-thread WinForms control read before the repair was accepted. The corrected request then captured UI values on the UI thread before `Task.Run`.

- DH-A007: `b40025159650351bea3cde60932e395ed554fa37` (`fix: isolate protected downloads from yt-dlp plugins`).
- DH-A008: `3c642ee130693b2c3662447a7493782343622713` (`fix: persist download history settings fail closed`).
- DH-A007/A008 were closed by guarded workflow run `35297339353`, cleanup commit `a46af90a604bbeddda532151986d670a4f0ffae1`. The guard demonstrated `DOWNLOAD_HISTORY.DisablesAmbientYtDlpPlugins` and `DOWNLOAD_HISTORY.SettingsPersistenceFailsClosed` failing before their repairs and passing afterward, with the full Debug/Release/regression gates completing successfully. Evidence artifact `10528675973` has SHA-256 `738f9c4b2840c6be273373235863fbb6681aa7ce16774dafad8c622da9ed9820`.

- DH-A009: `46e6ed3c580aedf0704d46cc3d7163797a281c18` (`fix: reconcile changed active download roots`).
- DH-A010: `1ee40ba4f1f9ae4adfd063548083ca9d308a874b` (`fix: protect custom archive targets from inventory overwrite`).
- DH-A009/A010 were closed by guarded workflow run `35298023958`, cleanup commit `3b9c5c46e6171bcd0ab34ffa0d079f2a27f88de3`. The guard showed all three new regressions failing at baseline, only the active-root regression passing after A009, and both collision regressions passing after A010 while `DOWNLOAD_HISTORY.CorruptArchiveDoesNotTrustPartialLines` remained green.
- Evidence artifact `10529081282` has SHA-256 `179df0591bdfaaa8dbeff782d02100e3f5d157651a9479e1a82a788fbc7c723c`.
- The first A010 attempt was deliberately rejected by guarded run `35297700397` because it introduced a failure in `DOWNLOAD_HISTORY.CorruptArchiveDoesNotTrustPartialLines`; request `8b740e92e41d6e32cd7783b97d9d0ec2489b19b8` was discarded by `c18f577dcfcc4f45aa53cd7c5750de06830405ee` without rewriting history. The corrected retry preserved established default-archive corruption recovery while refusing only unbound custom target collisions.

- DH-A011: `5a2855e59316075ffdf728fa5815b972ce3fff1a` (`fix: detect clustered short output overrides`), closed by guarded workflow run `35298299355`, cleanup commit `c70c3c41178510b63def7fb7d21e8ff498ecb059`.
- The guard showed `DOWNLOAD_HISTORY.RejectsClusteredShortOutputOverrides` failing at baseline and passing after the repair while completing all Debug/Release/regression gates.
- Evidence artifact `10528557720` has SHA-256 `a5dc1496005185f330c341a7ea4c63fbc7fdcb0352f1fe5b9089c410da6b8bc9`.

- DH-A012: `45df7f257ff92b4653867289f77dd39e186c92d0` (`fix: refuse automatic recovery over corrupt custom archives`), closed by guarded workflow run `35298602701`, cleanup commit `52eea0ee2db151b2ef19812f06ab1b37062b7650`.
- The guard showed `DOWNLOAD_HISTORY.BoundCustomArchiveCorruptionRequiresExplicitReset` failing before the repair and passing afterward while `DOWNLOAD_HISTORY.CorruptArchiveDoesNotTrustPartialLines` and the previously repaired clustered-option regression remained green.
- Evidence artifact `10529380115` has SHA-256 `a9d2f84d459cd4cd769dbce35b48db342856b5880e2efb1a53cd5bd74fc5c978`.

- DH-A013: `575820834084e922069d3a11617f2c2eae4e9d76` (`fix: make download history companion files collision-safe`), closed by guarded workflow run `35310088642`, cleanup commit `ddb28cd8634c37bd5e5527ecf2483b1dd7cb64f9`.
- The guard showed all three A013 regressions failing at baseline and passing after repair: pre-existing lock/temp preservation, Reset preserving unowned fixed temp collisions, and invalid-backup refusal before primary mutation.
- Evidence artifact `10533027131` has SHA-256 `cb4abd134a719edcb15ab5a05507b4b48206ce40bf2cdcbfa305f841447d9afc`.
- The first A013 test commit used the wrong archive for the lease collision; it was corrected by `a0aa50a9846b3a7b7489ee5ba4a0123be0ad07a3` before any production repair request was submitted.

- DH-A014: `051f8ffc3942e4026c86fb90655b52e12fa124f6` (`fix: preserve history across archive relocation`), closed by guarded workflow run `35310465691`, cleanup commit `d2aae491fe7d5d65583668a2909c59ac5d6d95ce`.
- The guard showed all three A014 regressions failing at baseline and passing after repair: ledger-only identities survive relocation, an unreadable prior ledger blocks rebinding, and the new active root's default archive can be selected explicitly.
- Evidence artifact `10532908230` has SHA-256 `e2445a0acac55a00c483c661244e63c08f0ca0649aa98f7a08d348a270d42261`.

- DH-A015: `8287110ed75f0bcaebee556fbfd9527bcbcae858` (`fix: reject protected source and extractor identity overrides`), closed by guarded workflow run `35310737158`, cleanup commit `4dd786cbd35f4c610a7b01f66cd4f099703f8e65`.
- The A015 regression failed at baseline and passed after repair, covering `--load-info-json`, `--use-extractors`/`--ies`, and `--force-generic-extractor`, including accepted long-option abbreviations and disabled-history availability.
- Evidence artifact `10533347524` has SHA-256 `2655c922e9af70d54cdfc6586753b0278742310261c350a1892b6c6d4de392f7`.

DH-A016 remains open until its guarded repair batch and final re-audit pass.

## Review scope / status

The review covered the complete feature delta and its interactions, including:

- `youtube-dl-gui/Classes/DownloadHistory.cs`
- `youtube-dl-gui/Forms/frmDownloadHistory.cs`
- standard and extended downloader argument generation/execution
- settings integration and provider/schema transitions
- non-destructive recursive inventory/recovery behavior and on-disk archive integrity
- separate active-download and scan-only existing-library roots sharing one history archive
- path-agnostic native identity when media is later moved or reorganized
- concurrency/lease behavior
- custom-argument/config escape paths
- sidecar/media-family handling
- dedicated Download History regression coverage
- build/packaging integration and externally defined yt-dlp archive semantics

The source-review pass is complete. Implementation remains in progress until every verified finding is repaired and the complete guarded before/after build and regression gates pass.

## Findings

### DH-A001 — Recovered metadata identity can inject malformed or additional archive records

**Priority / state:** High integrity risk / VERIFIED IN SOURCE; regression coverage missing.

**Affected code:** `youtube-dl-gui/Classes/DownloadHistory.cs`, primarily `TryRecoverFromInfoJson`, `AnalyzeCore`, and `ReconcileAnalysis`/`WriteArchiveAtomically`.

**Finding:** `TryRecoverFromInfoJson` treats top-level `.info.json` identity as authoritative and constructs an archive entry as `extractor.ToLowerInvariant() + " " + sourceId`. The recovered extractor is not rejected when it contains CR, LF, or NUL. The source ID is also not unconditionally validated as an archive-field value; the CR/LF/NUL check currently exists only inside `TryPlanMigration`, so it can be bypassed whenever the filename is already considered to contain a recoverable/sanitized ID and no migration is planned. `AnalyzeCore` adds the resulting string to `RecoveredEntries`, and `ReconcileAnalysis` writes those entries directly with `WriteArchiveAtomically` without passing them through the archive parser/validator.

**Impact:** Corrupt or tampered `.info.json` can make reconciliation write a malformed archive while returning `Healthy`. More seriously, embedded line breaks can construct multiple syntactically valid archive lines, seeding unrelated history entries and causing later media to be skipped as already downloaded. This violates the feature's fail-safe archive-integrity model.

**Why this survived earlier tests:** Existing tests verify that top-level `id`/`extractor_key` wins over nested values, that provider IDs are filename-sanitized safely, and that malformed existing archive files are rejected. They do not exercise control characters/newlines in recovered metadata fields or require recovered entries to round-trip through the native archive validator before publication.

**Required acceptance:**

1. Every metadata-derived archive identity must be validated as exactly one native archive record before it can enter `RecoveredEntries`.
2. Extractor and ID must reject CR, LF, NUL and any representation that can alter record boundaries; validation must apply regardless of filename state.
3. Reconciliation must not write or report `Healthy` for a recovered entry that the native archive parser would reject.
4. Add regression cases for malicious/corrupt extractor and ID values, including a payload capable of creating a second otherwise-valid archive line.
5. Re-run the complete existing regression/build gates after the fix; do not weaken existing recovery behavior.

### DH-A002 — Existing-library migration/rename behavior violates the required non-destructive inventory model

**Priority / state:** High behavioral incompatibility / VERIFIED IN SOURCE and clarified by user requirement.

**Affected code:** `youtube-dl-gui/Classes/DownloadHistory.cs`, especially migration planning/application/rollback paths (`TryPlanMigration`, migration execution, and related tests/UI wording).

**Finding:** The current feature contains an explicit migration mechanism that can rename existing media files to embed source IDs and can also rename an associated `.info.json`. That behavior is incompatible with the required operating model: the user's existing library is authoritative and must be inventoried in place without path changes.

**Impact:** On a large established library, enabling/reconciling Download History can mutate the media tree merely to make future archive reconstruction easier. Even if technically successful, this breaks path stability for external references, backups, media managers, hashes, synchronization systems, and the user's existing organization. It also makes enable/disable/rebuild operations unexpectedly destructive.

**Required acceptance:**

1. Remove/disable all automatic rename/move/rewrite behavior against existing library content.
2. Inventory must derive identity in-place from authoritative `.info.json` where available, and may fall back to supported filename parsing only without modifying the file.
3. Files that cannot be identified safely must be reported as unresolved/ambiguous; the feature must not "fix" them by renaming.
4. Reconciliation/validation/archive rebuild must only modify application-owned history/archive state, never the media tree.
5. Add regression coverage proving that representative media families (`.mp4`/`.webm`, `.info.json`, `.description`, thumbnail, subtitles/live-chat metadata) and nested directory layouts are byte/path unchanged before and after inventory/reconciliation.
6. Existing migration-specific tests and UI language must be revised to the non-destructive model rather than retained as intended behavior.

### DH-A003 — Explicit rebuild does not actually rebuild from the physical library when the archive is valid

**Priority / state:** High duplicate-prevention correctness risk / VERIFIED IN SOURCE and tests.

**Affected code:** `DownloadHistory.RebuildLibrary`, `CandidateRequiresLibraryRecovery`, `AnalyzeCore`, and the regression case `DownloadHistoryValidArchiveDoesNotPromoteUnarchivedFile`.

**Finding:** `RebuildLibrary` calls `AnalyzeCore(libraryRoot, archive, CandidateRequiresLibraryRecovery(...))`, exactly like ordinary reconciliation. When Download History is enabled, previously initialized, and not marked as needing reconciliation, `CandidateRequiresLibraryRecovery` returns `false`. `AnalyzeCore` then treats the valid archive as completion authority and skips any physical media whose recovered identity is absent from that archive. The explicit Rebuild operation therefore does not inventory the complete library in that state.

**Impact:** A user pointing Download History at a large pre-existing library can press `Rebuild Archive` and still receive a healthy result that omits recoverable existing media. Those omitted IDs can later be downloaded again. This contradicts the explicit rebuild/inventory use case.

**Required acceptance:**

1. Keep ordinary protected validation conservative: a new final-looking file absent from a valid archive must not automatically become trusted history merely because it exists.
2. Make the explicit Rebuild operation a separate intentional recovery mode that scans all configured inventory roots and reconstructs/cross-checks archive identities from authoritative in-place evidence.
3. Explicit rebuild must report unresolved/ambiguous media instead of silently skipping it.
4. Rebuild must union newly recovered identities with valid existing/backup archive identities; moving a previously inventoried file outside the currently scanned roots must not silently prune its historical identity. Reset History remains the explicit way to discard history.
5. Add regression coverage distinguishing ordinary validation from explicit rebuild: ordinary validation must not promote an unarchived residue; explicit rebuild must discover authoritative `.info.json` media missing from a valid archive.
6. Rebuild remains non-destructive to all library files.

### DH-A004 — History identity is coupled to physical media paths and only one library root is supported

**Priority / state:** High feature-model incompatibility / VERIFIED IN SOURCE and clarified by user requirement.

**Affected code:** `GetLibraryRoot`, path/binding state (`BoundLibraryRoot`), `DefaultArchivePath`/archive resolution, `TryResolvePaths`, `CandidateRequiresLibraryRecovery`, `ValidatePreparedExecution`, `AnalyzeCore` callers, `frmSettings` download-path guard, `frmDownloadHistory`, and regression fixtures.

**Finding:** The implementation defines the library root solely as `ResolveLibraryRoot(Downloads.downloadPath)`. Analysis, reconciliation, archive binding, prepared execution, and the Settings UI assume that one path is both the active destination for new downloads and the stable complete library. `frmSettings` explicitly blocks changing the download folder while protection is enabled, and the prepared-execution check rejects a command when the bound media path changes.

**Impact:** The required deployment model cannot be represented when an established library lives elsewhere, and history becomes unnecessarily dependent on where media currently resides. Moving or reorganizing already-inventoried media can force artificial rebinding/reconciliation even though yt-dlp archive identity itself is path-independent.

**Required acceptance:**

1. Preserve the application's current download directory as the only destination for new downloads.
2. Allow one or more additional existing-library roots to be configured as scan-only inventory roots.
3. Build one native yt-dlp archive from the union of the active download root and configured scan-only roots, de-duplicated by native archive identity.
4. Treat inventory roots as inputs to deliberate inventory/rebuild, not as permanent identity bindings. Already-recorded archive entries remain valid if files are later moved, roots become temporarily unavailable, or the active download path changes.
5. Normal protected execution must depend on the prepared archive/settings state, not on the continued existence or equality of an old media-library path.
6. A deliberate change to the active download path or configured scan roots should make a later inventory/reconciliation discover recoverable media in the newly selected roots, but must never prune older archive identities solely because files are no longer present there.
7. Never create, rename, move, or rewrite media/sidecars in scan-only roots.
8. Avoid double-scanning duplicate or nested-equivalent configured roots where practical.
9. Remove the Settings UI requirement to disable history merely to change the download destination.
10. Add regression coverage for a separate download directory plus existing library, root changes after enablement, nested folders, duplicate identities present in multiple roots, and preservation of archive identity after media is moved out of an old root.

### DH-A005 — Large-library management blocks the UI and repeats avoidable scans

**Priority / state:** Medium performance/usability risk / VERIFIED IN SOURCE.

**Affected code:** `AnalyzeCore` and `frmDownloadHistory` management flows, plus `CommitSettings` final validation.

**Finding:** `AnalyzeCore` calls `EnumerateCompletedMedia(...).ToList()`, retaining every completed-media path before processing. The WinForms dialog invokes analysis/reconciliation synchronously on the UI thread. `RebuildArchive` performs an Analyze pass and then a Rebuild pass; Save/enable performs Analyze, Reconcile, and then `CommitSettings` performs another complete `AnalyzeCore` pass. Each media item with adjacent metadata also causes the complete `.info.json` file to be read and deserialized.

**Impact:** The intended large existing library can consume avoidable memory and make the settings dialog appear hung for the duration of one to three full recursive scans. The repeated work scales directly with media count and metadata size.

**Required acceptance:**

1. Enumerate/process media incrementally rather than first materializing the complete media-path list.
2. Do not perform redundant full scans merely to repeat the same management decision; Rebuild and Save/enable should perform one deliberate inventory pass per requested operation where practical.
3. Run long management scans off the WinForms UI thread and prevent unsafe re-entry/closing while the operation is active.
4. Preserve existing archive locking and fail-safe error handling while moving work off the UI thread.
5. Keep regression/build coverage and add source-level coverage that guards against reintroducing `ToList()` materialization/redundant synchronous management flow.
6. Avoid per-media scans across the complete archive when filename-only recovery is needed; build a reusable identity lookup/matcher once per inventory operation and preserve the existing ambiguity checks and schema/sanitization semantics.


### DH-A006 — Normal protected execution still depends on physical inventory roots

**Priority / state:** High path-agnostic correctness risk / VERIFIED after DH-A004 guarded repairs.

**Affected code:** `EnsureReady`, `ValidateAndReconcile`, `TryResolveInventoryRoots`, and `AnalyzeCore`.

**Finding:** The DH-A004 repairs correctly made the archive identity and prepared execution context path-agnostic, but `TryGetArchiveArguments` still calls `EnsureReady`, which calls `ValidateAndReconcile(false)`. Whenever the in-memory `PreparedKey` cache is empty (notably after application restart), that path resolves every configured inventory root and enters `AnalyzeCore`. `AnalyzeCore` then requires every scan root to exist and enumerates the media tree even when the primary native archive and last-good backup are already valid. A temporarily offline scan-only library therefore blocks protected downloads, and the first protected download after restart can trigger a full library walk.

**Impact:** A media path that is explicitly supposed to be only an inventory input remains a runtime dependency. This violates the acceptance model that recorded provider+ID history survives moves, reorganizations, root outages, and application restarts. It also amplifies DH-A005 on large libraries.

**Required acceptance:**

1. Normal protected command generation validates/reconciles the application-owned native archive and backup only; it must not enumerate media or require configured scan-only roots to be online.
2. A valid primary archive remains authoritative after restart/cache loss. A valid last-good backup may restore/repair the primary without physical inventory; if neither ledger is usable, normal download preparation fails closed and directs the user to an explicit Rebuild/Inventory action.
3. `NeedsReconciliation` continues to fail closed until the user performs an explicit management reconciliation; normal downloading must not silently perform a physical-library rebuild.
4. Explicit Validate/Reconcile/Rebuild operations may require configured inventory roots, and Rebuild remains the full authoritative inventory operation.
5. Add regression coverage that takes a previously inventoried scan-only root offline, clears the in-memory prepared cache to simulate restart, and proves protected argument generation still succeeds from the valid archive/backup without recreating or scanning that root.
6. Re-run the complete Windows Debug/Release/regression gates and re-audit the resulting normal-execution path.


### DH-A007 — Protected yt-dlp invocations do not isolate ambient plugins

**Priority / state:** High integrity/security-boundary risk / VERIFIED in final re-audit.

**Affected code:** `TryGetArchiveArguments` and the app-owned protected yt-dlp argument prefix.

**Finding:** Protected commands currently inject `--ignore-config` and reject custom config locations, aliases, exec hooks, and explicit plugin postprocessor hooks, but they do not disable yt-dlp's default plugin search. Current upstream yt-dlp initializes `plugin_dirs` to `['default']`; the default search covers yt-dlp plugin/config folders, executable-adjacent plugin locations, and Python import paths. yt-dlp then loads all registered plugin types before constructing `YoutubeDL`, and plugin extractor classes are merged ahead of built-ins. Upstream's `--no-plugin-dirs` option clears the complete plugin-directory list and prevents plugin loading.

**Impact:** A locally installed ambient extractor or postprocessor plugin can execute during a run that the application otherwise treats as protected and isolated from identity-changing hooks. An extractor override can change which extractor owns a URL and therefore alter native archive identity; arbitrary plugin code can also mutate files/state outside the assumptions enforced by the app's custom-argument filter. This weakens both duplicate-prevention correctness and the meaning of the protected mode.

**Required acceptance:**

1. App-owned protected arguments must include `--no-plugin-dirs` before the source operand so ambient/default plugins are disabled whenever Download History protection is enabled.
2. Reject custom positive `--plugin-dirs` options while protection is enabled; a user who explicitly requires plugins must disable Download History rather than silently weakening its integrity model.
3. Do not reject a user-supplied `--no-plugin-dirs`; it is compatible with and only reinforces the protected mode.
4. Keep `--ignore-config`, app-owned native archive arguments, authentication config handling, and standard/extended argument ordering intact.
5. Add regression coverage for plugin isolation and rerun the complete guarded Windows build/regression gates.

### DH-A008 — Settings persistence can fail into a partially enabled on-disk state

**Priority / state:** Medium fail-safe persistence risk / VERIFIED in final re-audit.

**Affected code:** `CommitSettings` INI persistence and its rollback block.

**Finding:** `CommitSettings` writes `Enabled` before `EverEnabled`, `NeedsReconciliation`, bound archive/root data, and inventory roots. If a later INI write fails, the catch block attempts rollback, but rollback likewise restores `Enabled` before restoring all remaining old keys. If the underlying storage failure also interrupts rollback, the current process keeps its old in-memory state but the INI can be left with `Enabled=true` and a mixture of new, old, or missing dependent settings. A restart then consumes that partially committed state.

**Impact:** The feature's normal runtime validation is fail-closed, but its persistence transaction is not. A disk/permission/I/O failure at the wrong point can transform a rejected settings change into a partially enabled configuration on next launch.

**Required acceptance:**

1. Persist Download History settings with `Enabled=false` as the first guard write.
2. Persist every dependent setting while protection is disabled on disk.
3. Persist the intended final `Enabled` value only as the last write, after every other key succeeds.
4. Rollback must use the same ordering: write `Enabled=false` first, restore all old dependent keys, and restore the previous enabled state only last. If rollback itself fails, the durable state must therefore remain disabled rather than partially enabled.
5. In-memory state remains unchanged until the complete target write succeeds.
6. Add regression/source-structure coverage for the fail-closed persistence ordering and rerun the complete guarded Windows build/regression gates.


### DH-A009 — Management reconciliation misses authoritative media after the active download root changes

**Priority / state:** High duplicate-prevention correctness risk / VERIFIED in post-repair re-audit.

**Affected code:** `CandidateRequiresLibraryRecovery`, management `AnalyzeLibrary`/`ReconcileLibrary` flows, and the persisted `BoundLibraryRoot` inventory marker.

**Finding:** DH-A006 correctly removed physical-root checks from normal protected command generation. However, management reconciliation now decides whether to recover identities using archive binding and configured additional-root changes only. `CandidateRequiresLibraryRecovery(libraryRoot, archive)` receives the current active download root but no longer compares it with `BoundLibraryRoot`. If the user changes `Downloads.downloadPath` to a directory that already contains authoritative media, ordinary Analyze/Reconcile sees a valid existing archive, runs with `RecoverMissingEntries=false`, and deliberately ignores those unarchived final-looking files. Only the stronger explicit Rebuild currently discovers them.

**Impact:** Normal downloads remain correctly path-agnostic, but a later deliberate management reconciliation can falsely report Healthy while omitting recoverable media already present in the newly selected active destination. A future download of that identity can therefore occur again even though the user performed the expected reconciliation step.

**Required acceptance:**

1. Keep normal protected command generation independent of media-root existence/equality; do not undo DH-A006.
2. Treat a changed active download root as an inventory-input change only inside explicit management Analyze/Reconcile logic.
3. Use `BoundLibraryRoot` only as the last successfully committed active inventory root marker, not as part of native history identity or execution validation.
4. Reconciliation against a changed active root must recover authoritative identities there and union them into the existing native archive without pruning identities from the previous root.
5. A successful settings commit updates the stored active-root marker; normal protected downloading continues to work if the old root later moves or disappears.
6. Add regression coverage for an enabled archive, a subsequent active-root change containing new authoritative media, and successful ordinary reconciliation of that identity.

### DH-A010 — Archive target collision can overwrite an unrecognized file and the archive can inventory itself

**Priority / state:** High non-destructive data-integrity risk / VERIFIED in post-repair re-audit.

**Affected code:** `AnalyzeCore`, invalid-archive recovery, completed-media enumeration, and `ReconcileAnalysis`.

**Finding:** If a newly selected archive path already exists but is not a valid native archive and has no valid backup, `AnalyzeCore` marks it invalid yet can still declare it reconcilable when the physical library contains enough authoritative media. `ReconcileAnalysis` then atomically replaces that path with reconstructed archive text. For an unbound/new archive path, the existing file has never been established as application-owned state and may be an ordinary user media/sidecar file selected by mistake. Separately, if a new app-owned archive is deliberately given a supported media extension such as `.mp4`, a later full Rebuild enumerates that archive file as completed media and can fail as unresolved.

**Impact:** The first case violates the hard requirement that existing library content never be rewritten: a mistaken archive target can be destroyed during a nominally non-destructive rebuild. The second makes a valid app-owned archive capable of poisoning its own inventory solely because of its filename extension.

**Required acceptance:**

1. If a candidate archive file already exists, is invalid, has no valid backup, and is not the previously bound application-owned archive namespace, refuse reconciliation/rebuild and leave the file byte-for-byte untouched.
2. Continue allowing recovery of a previously bound damaged archive from a valid backup or authoritative library evidence.
3. Completed-media inventory must always exclude the exact current archive path, regardless of its extension or location under a scan root.
4. Do not rename, move, truncate, or rewrite the colliding user file while reporting the refusal.
5. Add regressions proving an unbound existing media/archive-path collision remains unchanged and an app-created archive with a media extension never inventories itself.
6. Re-run the complete guarded Windows build/regression gates and re-audit archive/backup mutation paths.


### DH-A011 — Clustered short options bypass protected output/path filtering

**Priority / state:** High protection-boundary correctness risk / VERIFIED against current yt-dlp option parsing.

**Affected code:** `ContainsShortOption` and the custom-argument checks for `-o` / `-P`.

**Finding:** Protected mode intentionally rejects custom output templates and paths because the application must keep recoverable `%(id)s` filenames inside its controlled output namespace. The current short-option detector only recognizes tokens that begin with the target option. yt-dlp uses Python `optparse`, which processes clustered short flags one character at a time until an option that takes a value is encountered. Consequently, a token such as `-qooutside-%(id)s.%(ext)s` is parsed by yt-dlp as `-q` followed by `-o outside-%(id)s.%(ext)s`, while the app sees a token beginning with `-q` and misses the `-o`. The same bypass exists for `-P`, for example `-qPelsewhere`. Standard and Extended download generation append user custom arguments after the app-generated output path, so the hidden later output/path option can override the protected location/template.

Current upstream yt-dlp short options that consume the remainder/next token include `-t -I -u -p -2 -f -S -N -r -R -O -a -P -o`; options such as `-q` are value-less and may legally precede another option in the same short cluster.

**Impact:** A user custom-argument string can bypass an already-established integrity check and redirect protected downloads outside the validated namespace or replace the required ID-bearing output template. The native archive may still record the source ID, but physical-library reconstruction after ledger loss can then be incomplete or impossible, defeating the feature's protected recovery model.

**Required acceptance:**

1. Detect `-o` and `-P` anywhere they are actually parsed as short options inside a cluster, not merely at the start of the token.
2. Stop scanning a short-option token once an earlier recognized short option consumes the remainder as its value, so legitimate values such as `-fbestvideo` are not misread because their value contains the letter `o`.
3. Continue detecting attached target values (`-ofile`, `-Pdir`) and ordinary standalone forms.
4. Preserve current long-option/abbreviation filtering and Windows argument tokenization.
5. Add regression coverage for clustered `-o` / `-P` bypasses plus a legitimate attached-value control.
6. Re-run the complete guarded Windows Debug/Release/regression gates, then re-audit the final custom-argument boundary.


### DH-A012 — Bound custom path is not sufficient proof to overwrite an invalid archive target

**Priority / state:** High non-destructive data-integrity risk / VERIFIED in final archive-mutation re-audit.

**Affected code:** invalid-primary/no-valid-backup handling inside `AnalyzeCore`.

**Finding:** DH-A010 refuses an invalid existing custom archive when the path is unbound, but still permits automatic reconstruction when `IsPreviouslyInitializedNamespace` says the path was previously bound. That marker is useful for deciding whether a **missing** archive represents unexpected history loss, but it is not strong enough evidence to overwrite an existing invalid custom file. Download History settings are deliberately persisted fail-closed rather than through an atomic multi-key transaction; an interrupted target write/rollback can leave dependent metadata such as `EverEnabled`/`BoundArchivePath` changed while durable `Enabled` remains false. A stale or partially persisted binding can therefore point at a user file that the application never safely established as archive-owned.

**Impact:** If that custom file is invalid as a native archive and no valid `.bak` exists, Rebuild/Reconcilation can still replace it with reconstructed archive text solely because the path matches the durable binding marker. This violates the strict non-destructive rule for existing files.

**Required acceptance:**

1. A valid backup remains sufficient evidence to restore a damaged custom primary.
2. The reserved default `yt-dlp-archive.txt` namespace may retain the established corruption-recovery behavior required by `DOWNLOAD_HISTORY.CorruptArchiveDoesNotTrustPartialLines`.
3. An existing invalid **custom** archive target with no valid backup must never be overwritten automatically, even if it matches the bound archive path.
4. Recovery for a genuinely damaged bound custom archive without backup must require an explicit destructive reset/removal step before rebuilding; do not infer ownership from settings metadata alone.
5. Add a regression that first establishes a real bound custom archive, corrupts it, removes its backup, then proves Rebuild leaves the corrupt file byte-for-byte unchanged and refuses automatic reconciliation.
6. Preserve DH-A010's unbound-collision and archive-self-inventory tests plus the default corruption-recovery test.
7. Re-run the complete guarded Windows Debug/Release/regression gates and re-audit every archive mutation path.


### DH-A013 — Archive companion paths can overwrite or delete unrelated files

**Priority / state:** High non-destructive data-integrity risk / VERIFIED in archive-mutation re-audit.

**Affected code:** `DownloadHistoryLease`, `ResetHistory`, `WriteArchiveAtomically`, `CopyArchiveToBackupAtomically`, and backup preflight in reconciliation/normal protected execution.

**Finding:** The primary archive collision rules now fail closed, but the adjacent implementation-owned companion names are still treated as automatically disposable. Archive leases open `<archive>.lock` and unconditionally delete that path on dispose, even if it existed before the application acquired the lease. Atomic primary writes use the fixed name `<archive>.tmp` with `File.WriteAllText`, overwriting any pre-existing file before replacing/moving it. Backup writes similarly use `<archive>.bak.tmp` and allow overwrite. `ResetHistory` explicitly deletes both fixed temp names. Finally, when backup retention is enabled, an existing invalid `<archive>.bak` is not recognized as trusted history but can still be overwritten by `CopyArchiveToBackupAtomically`.

**Impact:** A user file that merely collides with an implementation companion suffix can be deleted or replaced even when the selected primary archive itself is safe. This violates the feature's strict non-destructive rule and extends the same collision class fixed by DH-A010/DH-A012 to adjacent files.

**Required acceptance:**

1. Lease disposal must never delete a lock file solely because it was opened for synchronization. A lock file may persist as application synchronization state; pre-existing content must remain unchanged.
2. Atomic archive and backup writes must use unique sibling temporary files opened with create-new semantics, never a fixed reusable `.tmp` pathname.
3. `ResetHistory` must delete only the explicitly saved primary archive and its recognized backup, not legacy fixed temp names that may belong to the user.
4. If backup retention is requested and `<archive>.bak` already exists but is not a valid native archive, fail before changing the primary or backup. Do not overwrite the colliding backup automatically.
5. Backup refresh after protected execution must likewise refuse to overwrite an invalid existing backup and leave it byte-for-byte unchanged.
6. Preserve existing valid-backup merge/restore behavior, archive locking/serialization, and the A010/A012 primary-collision rules.
7. Add regressions for pre-existing lock/temp companion files, Reset preserving fixed temp collisions, and invalid backup collision refusing before primary mutation.
8. Re-run the complete guarded Windows Debug/Release/regression gates and re-audit every remaining file mutation path.


### DH-A014 — Archive relocation can silently discard durable history

**Priority / state:** High duplicate-prevention correctness risk / VERIFIED in final path-state re-audit.

**Affected code:** `frmDownloadHistory.NormalizeConfiguredPath`, `ReconcileLibrary`/prepared reports, and `CommitSettings` archive-path transition handling.

**Finding:** The native archive is path-agnostic with respect to media, but its own storage path is user-configurable. When a user changes that archive path, reconciliation prepares the candidate ledger solely from the candidate archive/backup plus currently scanned media. `CommitSettings` acquires the previous archive lease but never reads or unions the previously bound ledger before switching `BoundArchivePath`. Historical identities that exist only in the old ledger—because their media was moved away, the old scan root is offline, or the files were intentionally removed—can therefore disappear from the newly selected ledger. Separately, the dialog's `NormalizeConfiguredPath` converts any path equal to the **current active root's** `DefaultArchivePath` to an empty configuration. Once history has already been bound elsewhere, an empty configuration resolves to the old `BoundArchivePath`, so explicitly choosing the new active root's default archive path silently keeps the old archive.

**Impact:** A user can intentionally move the application-owned history file and receive a healthy/save-success result while losing provider+ID records that no longer have physical evidence in current scan roots. Those identities can then be downloaded again. In the default-path case, the requested relocation may not happen at all.

**Required acceptance:**

1. Changing archive storage must preserve the union of all valid identities in the previously bound ledger and the newly prepared candidate ledger before the binding is committed.
2. Read the previous primary and valid backup while holding the previous archive lease; if neither is usable, fail the path transition rather than switching to an incomplete ledger. Reset History remains the explicit way to discard prior history.
3. Do not require historical media files or old media roots to remain online; the old application-owned ledger is the preservation source.
4. Keep both old and new archive paths locked through the transition so a protected download cannot append to the old ledger between migration and settings commit.
5. Update the prepared report/digest after the union and refresh the new backup according to the selected backup policy.
6. `NormalizeConfiguredPath` may collapse a literal path to an empty/default configuration only when that empty configuration would resolve to the same archive path. After an active download-root change, choosing that root's default archive path must remain an explicit path while the old bound archive differs.
7. Add regressions with an archive-only historical identity absent from all current media roots, relocate the archive, and prove the identity survives; separately prove the dialog can select the new active root's default archive path.
8. Preserve DH-A006 path-agnostic normal execution and all primary/companion collision protections.
9. Re-run the complete guarded Windows Debug/Release/regression gates and re-audit archive path transitions.


### DH-A015 — Custom extractor/source overrides can bypass the protected identity model

**Priority / state:** High duplicate-prevention integrity risk / VERIFIED against current app source and upstream yt-dlp option flow.

**Affected code:** `TryGetArchiveArguments` custom-argument safety filter.

**Finding:** Protected mode currently blocks direct archive replacement, metadata identity rewriting, plugin injection, output/path replacement, and arbitrary postprocessor hooks, but it still accepts yt-dlp options that replace the source or alter extractor selection. Current upstream yt-dlp handles `--load-info-json FILE` by ignoring all command-line URLs and invoking `download_with_info_file` on the supplied JSON instead. That file carries the `id`/extractor identity used for the native archive. Upstream also exposes `--use-extractors`/`--ies` and `--force-generic-extractor`, which control the extractor selected for a URL; native archive IDs are constructed from the resulting extractor key plus media ID.

**Impact:** A command prepared for one app-visible source can process a different identity entirely, or the same URL can be archived under a different extractor key than later protected runs. Either case breaks the invariant that the app-owned ledger consistently represents the app's requested source under authoritative native extraction, allowing duplicate prevention to miss an existing download or to seed unrelated identities.

**Required acceptance:**

1. While Download History protection is enabled, reject custom `--load-info-json`.
2. Reject custom `--use-extractors` and its `--ies` alias, including accepted long-option abbreviations.
3. Reject custom `--force-generic-extractor`, including accepted long-option abbreviations.
4. The rejection message must explain that source/extractor identity overrides are incompatible with protected native archive identity.
5. Do not block ordinary multi-URL/batch/playlist input mechanisms merely because they add legitimate sources; the repair is scoped to options that replace the app operand or alter extractor identity.
6. Existing app-owned authentication config, ambient-config/plugin isolation, metadata-rewrite protections, and standard/extended argument ordering must remain intact.
7. Add regressions for each override and prove the same arguments remain available when Download History is disabled.
8. Re-run the complete guarded Windows Debug/Release/regression gates and continue the final custom-argument audit.


### DH-A016 — Parent Settings rollback can leave history enabled with an incompatible provider

**Priority / state:** Medium fail-closed state-consistency risk / VERIFIED in final integration audit.

**Affected code:** `frmSettings.AddDownloadHistorySettingsButton`, immediate provider selection handling, and `RestoreImmediateSettings`.

**Finding:** Provider selection in the main Settings form is applied immediately to `Downloads.YtdlType` and remembered in `YtdlType_Last` so Cancel can restore the provider that was active when Settings opened. The Download History button currently warns only about unsaved download-folder and filename-schema edits. If history starts disabled under an incompatible youtube-dl provider, the user can change the provider dropdown to yt-dlp, open Download History, and successfully enable protection because the transient in-memory provider is compatible. If the user then cancels the parent Settings form, `RestoreImmediateSettings` restores `YtdlType_Last` (youtube-dl) while the independently committed Download History settings remain enabled.

**Impact:** The state fails closed at download preparation, but the application can persist/return to an impossible combination: Download History enabled with a provider it explicitly does not support. The next download errors until the provider or history setting is corrected.

**Required acceptance:**

1. Do not allow the Download History child dialog to commit against a provider selection that the parent Settings dialog may later roll back.
2. If `Downloads.YtdlType` differs from `YtdlType_Last`, require the user to save or cancel/revert the parent Settings provider change before opening Download History.
3. Do not silently make parent Cancel persist an otherwise-unsaved provider change merely because Download History was opened.
4. Preserve the existing guard that prevents switching away from yt-dlp/yt-dlp-nightly while history is already enabled.
5. Keep the existing unsaved download-folder/schema warning behavior.
6. Add regression coverage tying the Download History button to the parent provider rollback state, then rerun the complete guarded Windows gates.
