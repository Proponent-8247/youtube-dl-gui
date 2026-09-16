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
| DH-A001 | High | Verified | Recovered `.info.json` identity is not guaranteed to be exactly one valid archive record; control characters/newlines can corrupt or inject archive entries. |
| DH-A002 | High | Verified | Existing-library migration can rename/move files, violating the required strictly non-destructive inventory model. |
| DH-A003 | High | Verified | Explicit `Rebuild Archive` does not force a full physical-library rescan when the current archive is valid, so existing recoverable media absent from that archive can remain undiscovered. |
| DH-A004 | High | Verified | Download History is hard-bound to `Downloads.downloadPath` as its only library root and treats the media path as part of archive validity instead of supporting path-agnostic identity plus separate scan roots. |
| DH-A005 | Medium | Verified | Large-library management performs synchronous/repeated full scans and first materializes the complete media-file list, causing avoidable memory use and UI stalls for the intended large-library workflow. |
| DH-L002 | — | Closed / no defect found | Archive mutation is serialized by the archive-derived mutex plus an on-disk exclusive lock; first-use directory creation acquires the file lock immediately after creation, and existing regression coverage verifies serialization and cancellation. |
| DH-L003 | — | Closed / no bypass found | Protected commands inject `--ignore-config`, reject custom config locations/aliases and conflicting archive/output hooks, and append the app-owned archive option after user custom arguments. The later app option therefore remains authoritative; the later app-owned authentication config contains authentication options only. |
| DH-L004 | — | Closed / no UI transaction defect found | Save/enable uses `CommitSettings` with rollback of settings writes; Rebuild and Reset are explicit archive-management actions whose app-owned ledger effects intentionally do not depend on pressing Save afterward. No media-tree mutation is acceptable after DH-A002 is fixed. |
| DH-L006 | — | Closed / intended fail-safe behavior | Media without authoritative `.info.json` identity or an unambiguous filename match to a known native archive identity is reported unresolved; the implementation deliberately does not infer YouTube merely from an 11-character ID shape. |
| DH-L007 | — | Closed / classifier verified | Completed-media enumeration is allowlisted to media extensions. Known sidecars including `.info.json`, generic `.json`/`.live_chat.json`, descriptions, common thumbnails, subtitles, `.part`, `.ytdl`, and text files are not counted as media. Regression coverage will be expanded with the supplied real-world family shape. |

Fix status and validating commit/test evidence will be added to the verified rows during implementation.

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
