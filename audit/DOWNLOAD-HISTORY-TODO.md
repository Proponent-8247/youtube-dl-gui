# Download History Full-Audit TODO

Branch: \`feature/download-history-archive-3.3.0-2\`  
Release base: \`49a290d07b220e4a9cd98d44c6db7f45a5bbbc98\`  
Mode: **AUDIT ONLY — DO NOT APPLY PRODUCTION FIXES UNTIL THE FULL AUDIT IS COMPLETE**  
Canonical historical audit: \`audit/DOWNLOAD-HISTORY-FEATURE-REVIEW.md\`

## Working rules

- Record every verified finding here immediately when discovered.
- Do not queue \`.audit-repairs.json\` or commit production fixes during this pass.
- Test-only commits are permitted when necessary to prove a finding, but the finding must be recorded here as soon as it is verified.
- Preserve published branch history; do not rewrite or squash.
- Distinguish verified findings from investigation leads.
- After the full audit is complete, work this TODO top-to-bottom with one conceptual repair per guarded batch where practical.

## Verified findings awaiting repair

### DH-A070 — Reset can delete an unowned archive candidate

**Severity:** High  
**Status:** Verified / TODO  
**Area:** state ownership, disabled-mode settings, Reset History

**Summary:** While protection is disabled, a configured archive path can diverge from the durable bound archive. \`ResetHistory\` currently deletes \`EffectiveArchivePath\` without first proving that the target was actually established as application-owned Download History state. This can delete an unrelated file selected while history was never enabled, or target a newly configured disabled-mode path instead of the true bound ledger.

**Required repair constraints:**

1. Refuse physical Reset unless Download History was previously successfully enabled and a durable non-empty bound archive exists.
2. Reset only the durable bound archive, never a merely configured future candidate.
3. While preserved history is disabled, reject physical archive rebinding away from the bound path.
4. Allow representation-only normalization when it resolves to the same physical bound path.
5. Keep real archive relocation in the existing enabled reconciliation/union flow.
6. Never-enabled users may configure a future archive while disabled, but Reset must not claim/delete it.
7. Preserve reset-after-active-root-change behavior and companion-file protections.
8. Keep media/library files untouched.

**Evidence:** Regression \`DOWNLOAD_HISTORY.ResetRequiresOwnedBoundArchive\` from commit \`87df874d83533fd720abcfe3b165309da991779c\`. Canonical details are in DH-A070 of the feature review. The pre-audit-mode repair request at \`91ee75c39db8c55bf9105c8261b94b557be32377\` failed its guarded run; no A070 production repair landed. The stale repair request was removed when audit-only mode began.

### DH-A071 — Protected outputs can escape through an in-tree reparse directory

**Severity:** High  
**Status:** Verified / TODO  
**Area:** filesystem containment, output paths, rebuild integrity

**Summary:** Physical inventory deliberately rejects reparse-point files/directories, but normal protected downloads intentionally avoid a full library scan. Protected filename validation permits safe nested directory components such as `creator\\%(id)s.%(ext)s` (and the application itself can add website/type/batch subdirectories). If an existing directory component beneath the active root is a Windows junction/symlink/reparse point, yt-dlp follows it and can create media outside the physical active library even though the lexical output path is beneath that root. `TryGetArchiveArguments`/prepared execution do not validate those output-path components for reparse traversal.

**Impact:** A protected download can publish a native archive identity for media written outside the inventory namespace. A later explicit inventory/rebuild then fails closed on the reparse point or cannot see the actual target file, breaking the core “protected output remains rebuildable from configured roots” invariant.

**Required repair constraints:**

1. Protected output creation must not traverse an untrusted reparse-point component beneath the active library root.
2. Preserve explicitly configured library roots as roots; the issue is traversal through descendant components, not whether the selected root itself is implemented by a user-chosen mount/junction.
3. Cover app-generated batch/site/type subdirectories as well as nested filename-schema directories.
4. Handle metadata-dependent directory components safely; do not claim containment based only on lexical `Path.GetFullPath`.
5. Preserve the normal no-full-library-scan performance requirement.
6. Keep disabled-history behavior unchanged.
7. Add Windows regression coverage using a junction/reparse descendant and prove protected argument preparation/launch fails before media can be written through it.

**Source proof:** `EnumerateCompletedMedia` rejects descendant `FileAttributes.ReparsePoint`, but the normal protected path performs ledger-only validation and output-template checks without traversing/checking descendant output components. Static nested protected schemas remain accepted.

### DH-A072 — Existing unarchived final media can be promoted to completed history without a real retry

**Severity:** High  
**Status:** Verified / TODO  
**Area:** false completion, retry semantics, native archive integrity

**Summary:** Download History intentionally treats a final-looking media file that is absent from a valid native archive as possible residue from a failed prior provider run; ordinary validation does not promote it into history. Current yt-dlp execution defeats that conservative rule when the next protected run resolves to the same output filename. In `process_info`, `existing_video_file(...)` uses `default_overwrite=False`; when the final file already exists, yt-dlp reports it as already downloaded instead of transferring it. `success` remains true, postprocessing runs on that existing file, and the code then sets `info_dict['__write_download_archive'] = True`. `process_video_result` records the source identity when all requested downloads carry that true flag.

This happens under yt-dlp's normal overwrite defaults; it does not require the user to request `--no-overwrites`.

**Impact:** A truncated, stale, corrupt, or otherwise failed-run residue with the expected final filename can become a native archive success on the next protected invocation without a real media transfer. Future runs then skip that identity as completed, contradicting the feature's fail-closed retry model.

**Required repair constraints:**

1. A protected run must not publish a new native archive identity solely because an unarchived final output file already exists.
2. Preserve legitimate native-archive skips: if the identity is already in the protected ledger, yt-dlp should continue to skip normally.
3. Do not solve this by full-scanning all library roots before every download.
4. Do not rewrite or delete scan-only library media.
5. Define explicit behavior for a collision in the active writable download destination: either force a real retry safely, fail closed and require user action, or otherwise prove completion before allowing the identity into the ledger.
6. Preserve ordinary disabled-history yt-dlp overwrite/existing-file behavior.
7. Cover single-file and merged/multi-format existing-final paths; both use the same upstream existing-file success flow.
8. Add an execution-level regression showing an existing final file absent from the ledger cannot become a new archive record without a real successful retry.

**Upstream proof:** Current pinned yt-dlp `c7fb478d...` initializes `success=True`, returns an existing final file from `existing_video_file` without a real download, then after successful postprocessing sets `__write_download_archive=True`; `process_video_result` records the native identity when the requested download flags contain true and no false value.

## Investigation leads

### LEAD-001 — Reparse-point state files may bypass ownership boundaries

**Status:** Needs verification  
**Area:** archive/backup/lock ownership, Windows filesystem semantics

Media inventory explicitly rejects reparse-point files/directories, but the archive lifecycle currently opens/replaces/deletes the configured archive, `.bak`, and `.lock` paths without an equivalent visible reparse-point rejection. Verify Windows/.NET behavior for file symlinks/junction-adjacent state paths and whether a valid linked target could cause Download History to mutate or delete unrelated data. Existing `DOWNLOAD_HISTORY.RejectsReparsePointTraversal` covers only library traversal.

## Full re-audit coverage

Use this checklist as the resume point after any timeout. A section is not complete until current-head source, relevant tests, and affected upstream yt-dlp semantics have been reviewed.

- [x] Branch/base diff inventory: 18 changed files classified against release base `49a290d0...`.
  - Production: `DownloadHistory.cs`, `frmDownloadHistory.cs`, `DownloadInfo.cs`, `ExtendedMediaDetails.cs`, `frmDownloader.cs`, `frmExtendedDownloader.cs`, `frmSettings.cs`, project file/resource updater binary.
  - Audit/test tooling: three audit workflows, `Run-AuditRepairBatch.ps1`, `apply-audit-repairs.py`, Download History regression suite, one existing repair-wave test, canonical audit, this TODO.
  - No unrelated production files are changed by the feature branch.
- [x] Download History state model: re-audited enable/disable/re-enable, configured/bound/effective archive transitions, relocation union, prepared-report commit flow, and fail-closed persistence. **Open finding: DH-A070.**
- [ ] Archive lifecycle: initialize, validate, rebuild, reconcile, relocate, reset, backup, corruption, truncation, encoding.
- [ ] Ownership/non-destructive guarantees: primary, backup, lock/temp companions, media, metadata, scan-only roots.
- [ ] Concurrency/TOCTOU: mutexes, file locks, prepared snapshots, provider drift, settings drift, concurrent sessions.
- [ ] Path semantics: active root, archive, cache, cookies, inventory roots, relative paths, \`~\`, \`$VAR\`, \`\${VAR}\`, \`%VAR%\`, escaped sigils.
- [ ] Filesystem containment: parent traversal, reparse points/junctions/symlinks, UNC/network roots, case/normalization behavior.
- [ ] Output containment: standard, mostly-custom, extended downloader, batch mode, split chapters, retained intermediates.
- [ ] yt-dlp custom-argument parser parity: long abbreviations, short clusters, dangling value options, aliases/presets, terminator handling.
- [ ] Unsafe execution/write surfaces: config, plugins, exec hooks, external downloaders, FFmpeg, JS runtimes, cache, cookies, page dumps, print-to-file, self-update.
- [ ] False-completion/native-archive semantics: test/simulate/skip/error/fragment/filter/no-format/existing-file flows.
- [ ] Provider identity recovery: extractor_key/ie_key/id validation, controls, archive grammar, case semantics.
- [ ] Physical inventory allowlist: current yt-dlp media/audio/storyboard classes and safe compatibility extensions.
- [ ] Sidecars/derivatives: info JSON, thumbnails, playlist metafiles, subtitles, chapters, components, \`.orig\`, \`.uncut\`, manifests.
- [ ] Total archive-loss rebuild: metadata recovery, filename recovery, derivative ownership, ambiguity/fail-closed cases.
- [ ] Multiple roots/path-agnostic identity: active root + zero/one/many scan-only roots, moves between runs, unavailable roots.
- [ ] Large-library behavior: streaming, memory growth, matcher complexity, duplicate scans, UI responsiveness.
- [ ] Settings/UI safety: enable/save/cancel/validation/rebuild/reset/open/browse, warning text, disabled-mode transitions.
- [ ] Standard downloader integration: argument ordering, authentication config, output construction, source terminator.
- [ ] Extended downloader integration: argument ordering, schemas, batch queue, authentication, range downloads.
- [ ] Compatibility: Windows/.NET target behavior, legacy youtube-dl disabled-history paths, existing settings migration.
- [ ] Regression harness quality: source-text assertions vs behavioral assertions, stale anchors, negative controls.
- [ ] CI/guard workflow: exact-base enforcement, allowed flakies, repair isolation, build/package gates, failure preservation.
- [ ] Security/privacy: secret handling, auth temp files, log/preview censorship, unsafe user-controlled filesystem/process inputs.
- [ ] Final current-head build/test/package verification after audit-only documentation is complete.
- [ ] Final canonical audit/TODO consistency pass; no unrecorded open leads.

## Completed/closed historical findings

DH-A001 through DH-A069 are documented in \`audit/DOWNLOAD-HISTORY-FEATURE-REVIEW.md\`. They are **not assumed correct merely because they are closed**; this full re-audit will re-check affected boundaries and reopen/add findings here if current-head evidence warrants it.
