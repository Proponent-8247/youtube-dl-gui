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
- Existing media and companion files such as `.info.json`, `.description`, thumbnails, and subtitles must remain in place.
- **No existing library file may be renamed, moved, or rewritten during inventory, reconciliation, archive generation, or validation.**
- The library scan therefore has to be strictly non-destructive with respect to the user's media tree. Archive/history files maintained by the application are the only files that may be created/updated as part of this feature.

Representative layout supplied during review:

`Downloads\YT-DL\x.com\Video\Ryomen\`

- `Ryomen - ...-2084613745251106816.description`
- `Ryomen - ...-2084613745251106816.info.json`
- `Ryomen - ...-2084613745251106816.jpg`
- `Ryomen - ...-2084613745251106816.mp4`

and nested trees such as:

`Downloads\YT-DL\youtube.com\Video\Casual Geographic\...`

## Review scope / status

The review is covering the complete feature delta and its interactions, including:

- `youtube-dl-gui/Classes/DownloadHistory.cs`
- `youtube-dl-gui/Forms/frmDownloadHistory.cs`
- standard and extended downloader argument generation/execution
- settings integration and provider/library/schema transitions
- non-destructive recursive inventory/recovery behavior and on-disk archive integrity
- concurrency/lease behavior
- custom-argument escape/bypass paths
- dedicated Download History regression coverage
- build/packaging integration and externally defined yt-dlp archive semantics

Review remains in progress. Findings are appended only after they survive source/caller/test reconciliation.

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
3. Reconciliation must not write or report `Healthy` for a recovered entry that `TryReadArchive` would reject.
4. Add regression cases for malicious/corrupt extractor and ID values, including a payload capable of creating a second otherwise-valid archive line.
5. Re-run the complete existing regression/build gates after the eventual fix; do not weaken existing recovery behavior.

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
5. Add regression coverage proving that representative media families (`.mp4`, `.info.json`, `.description`, thumbnail, subtitles) and nested directory layouts are byte/path unchanged before and after inventory/reconciliation.
6. Existing migration-specific tests and UI language must be revised to the non-destructive model rather than retained as intended behavior.

## Leads still under review

The following are review leads, not findings yet, and must not be treated as defects until reconciled:

- cross-session first-use locking while the default library directory is being created;
- current yt-dlp option/config escape surface and archive-identity compatibility;
- UI save/cancel and partial-failure transaction boundaries;
- large-library and archive performance/resource behavior;
- treatment of unidentified legacy files when no authoritative `.info.json` is available.
