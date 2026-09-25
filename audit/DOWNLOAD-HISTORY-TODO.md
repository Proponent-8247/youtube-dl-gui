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

### DH-A073 — Reparse-point archive state can redirect protected ledger writes

**Severity:** High  
**Status:** Verified / TODO  
**Area:** archive ownership, Windows filesystem semantics, non-destructive state writes

**Summary:** Download History rejects reparse points while scanning media, but it does not reject an exact archive/state file that is itself a Windows symbolic link/reparse point. `TryReadArchive`, writability checks, the execution lease, and yt-dlp's native `locked_file(..., 'a')` archive append all open the configured archive path normally and therefore follow the link target. A symlink placed at the default/custom archive filename can point to an unrelated empty or syntactically valid text file; validation accepts that target as a native archive and a protected download can append identities to the unrelated target.

Backup and lock companion paths likewise lack an explicit exact-path reparse ownership check and need to be included in the repair audit, even where their individual mutation semantics differ.

**Impact:** Merely occupying the application-owned pathname with a reparse-point file can bypass the first-use/unowned-file protections established for ordinary files and redirect protected state I/O outside the selected namespace.

**Required repair constraints:**

1. Reject an exact primary archive file that is a reparse point before adopting, validating, appending, rebuilding, relocating, or resetting it.
2. Apply equivalent ownership checks to pre-existing `.bak` and `.lock` companion files before treating them as application-owned state.
3. Do not follow a reparse point to decide that its target is a valid native archive; validity of the target is not ownership proof.
4. Preserve explicitly selected **root directories** that may themselves be user-managed mounts/junctions; this finding is about state-file redirection, not banning a configured storage root solely because of how it is mounted.
5. Preserve normal ordinary-file archive/backup behavior and cross-session locking.
6. Add Windows regression coverage with an archive-file symlink/reparse point whose target is empty/valid, proving validation/protected execution/reset leave the target byte-for-byte unchanged and fail closed.

**Source proof:** Current app state I/O uses ordinary `File.Exists`/`FileStream`/`StreamReader`/replace operations without an exact state-file `FileAttributes.ReparsePoint` gate, while current yt-dlp appends its native archive through a normal path open. The existing reparse regression covers only media-tree traversal.

### DH-A074 — Interrupted yt-dlp postprocessor temp media can poison total-loss rebuild

**Severity:** High  
**Status:** Verified / TODO  
**Area:** incomplete residue classification, postprocessing, total-loss rebuild

**Summary:** Current yt-dlp built-in FFmpeg postprocessors create working files by inserting markers before the real media extension, including `<stem>.temp.<ext>` and `<stem>.keyframes.temp.<ext>`. Examples include merge, fixup, metadata/embed, audio conversion, chapter splitting/keyframe preparation, and related FFmpeg postprocessing. If the provider is killed, crashes, or is interrupted between creating one of these files and the final `os.replace/os.rename`, the working file can remain in the active library.

Download History excludes names ending in `.tmp` and ordinary downloader `.part` residue, but a postprocessor working file still ends in a valid completed-media extension such as `.mp4`, `.m4a`, or `.webm`. During total archive loss it is therefore enumerated as completed media. It normally has no adjacent `<temp-stem>.info.json`, and the inserted marker makes it fail the canonical protected filename schema/ID matcher, so it becomes unresolved and blocks rebuild even when the canonical media/info family is otherwise recoverable.

**Impact:** A failed or interrupted protected provider run can leave app-recognizable transient working state that later makes an otherwise valid physical library impossible to rebuild after ledger loss.

**Required repair constraints:**

1. Distinguish current yt-dlp postprocessor working-file shapes such as inserted `.temp` and `.keyframes.temp` from completed media during recovery.
2. Do not blindly ignore any user file containing `.temp`; protected schemas are allowed to contain literal text before the terminal `%(ext)s`.
3. Require owner/family evidence: stripping the known working marker must resolve to a protected canonical filename/authoritative owner metadata or another equally strong proof.
4. Preserve fail-closed behavior for arbitrary media-like files that merely contain similar marker text.
5. Preserve legitimate canonical schemas whose real filename itself includes `.temp`; if yt-dlp creates a temp for such a schema it will contain an additional inserted marker.
6. Cover both partial/interrupted and complete-but-unmoved working files; inventory/rebuild must never rewrite/delete them.
7. Add regressions for positive owner-backed `.temp.<ext>` and `.keyframes.temp.<ext>` residue plus literal-schema negative controls.
8. Preserve A049/A069 retained derivative handling and A003's conservative treatment of failed-run residue.

**Upstream proof:** Current pinned yt-dlp `c7fb478d...` uses `prepend_extension(filename, 'temp')` across FFmpeg merger/fixup/embed flows and `prepend_extension(filename, 'keyframes.temp')` for forced-keyframe preparation. These paths rely on subsequent rename/replace rather than a crash-proof cleanup transaction, so process termination can leave media-extension working files.

### DH-A075 — Protected custom arguments can fetch and execute unverified JavaScript from an arbitrary repository

**Severity:** High  
**Status:** Verified / TODO  
**Area:** arbitrary code execution, extractor arguments, remote components, protected process boundary

**Summary:** Protected mode blocks custom config/plugin loading, exec hooks, raw child-process arguments, path-qualified JS runtimes, and arbitrary external downloader executables, but it currently permits both `--extractor-args` and `--remote-components`.

Current pinned yt-dlp's built-in YouTube EJS challenge provider has intentionally undocumented developer extractor arguments under the `youtube-ejs` namespace:

- `dev=true` bypasses solver script version/hash verification.
- `repo=<owner/repo>` replaces the normal `yt-dlp/ejs` GitHub repository.
- `script_version=<tag>` changes the release tag fetched.

With `--remote-components ejs:github`, yt-dlp downloads the selected repository's release JavaScript. In EJS dev mode the normal allowed-version/hash checks are skipped. The downloaded library/core script is then concatenated into the input passed to the enabled JS runtime for execution.

**Impact:** A protected command can cross the intended no-arbitrary-code boundary and execute user-selected remote JavaScript inside the provider operation, despite plugin/config/exec/executable restrictions. That code runs while the protected archive lease is held and can invalidate assumptions about filesystem integrity, source identity, and process trust.

**Required repair constraints:**

1. Protected mode must prevent extractor arguments from enabling unverified/custom EJS code sources.
2. At minimum reject the current `youtube-ejs` developer controls `dev`, `repo`, and `script_version` when they can influence protected execution.
3. Audit all current `--extractor-args` namespaces before deciding whether narrow filtering is sufficient; do not assume only documented arguments exist.
4. Preserve safe extractor tuning arguments when they do not affect identity/trust boundaries, if practical.
5. Ordinary official remote components from the pinned/current yt-dlp-supported source may remain a separate policy decision; the verified defect is the custom-repository + verification-bypass path.
6. Preserve disabled-history behavior.
7. Add regression coverage for the explicit long option and accepted long abbreviations/quoting, including the combined `youtube-ejs:dev=true;repo=...` + `--remote-components ejs:github` path.
8. Re-audit cache interaction because fetched EJS scripts are cached and later read as script sources.

**Upstream proof:** Current yt-dlp `EJSBaseJCP` reads `extractor_args['youtube-ejs']`, documents in source that `dev` bypasses script hashes/versions and `repo` selects a custom GitHub repository, fetches release assets when `ejs:github` is enabled, and executes the resulting library/core code through its configured JS runtime.

### DH-A076 — Audit verification evidence is anchored to the wrong comparison base

**Severity:** Medium  
**Status:** Verified / TODO  
**Area:** audit provenance, CI evidence

**Summary:** This feature audit is explicitly based on release commit `49a290d07b220e4a9cd98d44c6db7f45a5bbbc98` (`release/3.3.0-2`), but `.github/workflows/audit-verify.yml` preserves comparison evidence against `refs/remotes/origin/master` and records history from hard-coded historical SHA `b734c05944eeffccfde3208cd1ea45ef2ae401f9`. The workflow does not persist the actual release base, release-base source snapshot, merge base, or release-base diff used by this review.

**Impact:** A green verification artifact can contain internally consistent build/test evidence while its source-delta provenance describes a different baseline than the audit being signed off. A future reviewer cannot reconstruct the intended feature delta from the retained artifact alone.

**Required repair constraints:**

1. Pin the verification provenance to the reviewed release/base SHA or an explicit workflow input that is validated against an allowed base.
2. Preserve base SHA, base source archive, merge base, diff stat, name-status, and relevant source diff for that exact base.
3. Do not silently substitute `master` when the reviewed branch is based on a release branch.
4. Keep current exact-head source snapshot/build/test/package evidence.
5. Avoid mutable branch-name-only provenance when a concrete reviewed SHA is known.

### DH-A077 — Guarded repair requests can self-whitelist arbitrary regression failures as “flaky”

**Severity:** High  
**Status:** Verified / TODO  
**Area:** guarded repair integrity, regression enforcement

**Summary:** `Run-AuditRepairBatch.ps1` trusts the request-provided `allowed_flaky_failures` array. It verifies only that each named test exists. There is no workflow-owned/canonical allowlist of tests that are actually known nondeterministic. Both introduced-failure detection and final failure validation exempt every request-named “flaky” test.

**Impact:** A mistaken or malicious repair request can name any existing regression test in `allowed_flaky_failures`, introduce a real failure in that test, and still satisfy the guarded batch. The workflow may then commit and push a regression while claiming all unreviewed failures were rejected.

**Required repair constraints:**

1. Define the permitted flaky-test set in trusted runner/workflow code, not in the repair request.
2. Reject request entries outside that fixed set.
3. Prefer requiring a flaky test to demonstrate established nondeterminism independently; do not let a repair newly break a previously passing allowlisted test without additional evidence.
4. Preserve deterministic `expected_failures` / `remaining_failures` semantics.
5. Keep per-repair “no newly failing non-flaky tests” enforcement.

### DH-A078 — Normal verification can lose regression coverage and still pass

**Severity:** Medium  
**Status:** Verified / TODO  
**Area:** regression harness integrity, CI coverage preservation

**Summary:** `audit-verify.yml` compares test names only between the three executions of the **same current revision** and enforces a minimum count of 80. It does not compare the current test-name set against a trusted manifest/base revision. Therefore a normal source/test commit outside `audit-repair.yml` can delete or rename regressions, remain above 80 tests, and pass all three current-revision comparisons. The guarded repair runner prevents test removal only inside a repair batch; that protection does not apply to ordinary pushes.

**Impact:** The branch can obtain green verification after silent regression-coverage erosion, weakening the evidence used for audit sign-off.

**Required repair constraints:**

1. Maintain a trusted expected regression-name manifest or compare against a pinned reviewed baseline with explicit approved additions/removals.
2. Reject missing/renamed tests unless the coverage change itself is explicitly reviewed.
3. Keep duplicate-name checks and AnyCPU/x86 parity checks.
4. Do not rely on a numeric minimum count as the primary coverage-preservation control.

### DH-A079 — Guarded production repairs can weaken the regression tests that are supposed to prove them

**Severity:** High  
**Status:** Verified / TODO  
**Area:** guarded repair integrity, test immutability

**Summary:** `tools/apply-audit-repairs.py` permits version-2 repair edits under the `tests` root. `Run-AuditRepairBatch.ps1` checks that previously existing test **names** are still present, but it does not require regression source/content to remain unchanged during a production repair. A request can therefore edit the assertion/body of a failing test, keep the same name, list that test in `resolves`, and pass the guard without correcting the production defect.

**Impact:** The core evidence loop can be defeated by changing the oracle rather than the implementation while still producing a green “regression-verified repair” commit.

**Required repair constraints:**

1. Production repair batches must not modify regression-test source unless the batch is explicitly designated as a separately reviewed test-contract change.
2. Prefer immutable test-source hashes/name manifest for ordinary repair batches.
3. If a stale test contract genuinely needs revision, require a standalone test-only commit that is reviewed and baseline-run before any subsequent production repair request.
4. Preserve the current removed/renamed/duplicate test-name checks as additional controls.
5. Do not allow a single guarded commit to weaken a regression and claim that same regression as its proof of correctness.

### DH-A080 — A guarded repair is not required to resolve any reviewed baseline failure

**Severity:** Medium  
**Status:** Verified / TODO  
**Area:** guarded repair contract, evidence quality

**Summary:** The repair-plan parser/runner does not require a conceptual repair's `resolves` array to contain at least one deterministic baseline failure. A repair with an empty `resolves` set can modify allowed source files, pass the general build/regression suite, and be committed/pushed by the “guarded repair” workflow even though no pre-existing failing regression proves why the change was needed.

**Impact:** Untested or merely speculative production changes can receive the same workflow provenance as regression-driven fixes, weakening the meaning of a guarded repair commit.

**Required repair constraints:**

1. Ordinary production repair batches must require every conceptual repair to resolve at least one named deterministic baseline failure.
2. Every named resolved test must be in the reviewed baseline failure set at the point it is assigned to the repair.
3. If a non-regression refactor/tooling change is intentionally permitted, use a distinct explicitly reviewed workflow/plan type rather than silently treating it as a regression repair.
4. Keep one-concept-per-commit and full-suite reruns.


### DH-A081 — Total-loss rebuild can promote media left by a failed postprocessor

**Severity:** High  
**Status:** Verified / TODO  
**Area:** total-loss rebuild, false completion, metadata trust, postprocessing

**Summary:** Protected mode forces per-media `.info.json` specifically so identities can be reconstructed after total archive loss. Current yt-dlp writes that info JSON before downloading/postprocessing. A normal media transfer can then succeed while a built-in postprocessor fails (for example FFmpeg audio extraction/recode/embed/fixup). In that failure path yt-dlp reports the postprocessing error and returns before setting `__write_download_archive = True`, so the native archive correctly does **not** mark the source complete. The downloaded source media file and its adjacent info JSON can nevertheless remain on disk.

During explicit recovery/rebuild, Download History currently treats a completed-media extension plus adjacent metadata as authoritative and reconstructs `extractor_key/ie_key + id`. It has no proof that the requested postprocessed artifact completed. Therefore loss of the native ledger can turn a provider failure that was deliberately left unarchived into a completed history identity.

**Impact:** After archive loss, rebuild can permanently suppress a retry that yt-dlp itself considered incomplete. A concrete example is an audio-extraction request where the downloaded `.webm` + `.info.json` remain after FFmpeg fails before producing the requested `.mp3`; rebuilding can add the source ID and later protected runs skip it.

**Required repair constraints:**

1. Total-loss recovery must not treat every adjacent info JSON + media pair as proof that the **requested protected operation** completed.
2. Preserve recovery for genuinely successful direct-media downloads and successful postprocessed outputs.
3. Do not delete, rename, or rewrite failed-run source media or metadata.
4. Preserve A072's normal-run rule: an unarchived existing final-looking file remains eligible for retry and cannot become history merely because it exists.
5. Distinguish successful retained originals/components (A049/A069) from failed postprocessor residue using evidence stronger than the pre-download info JSON alone.
6. Do not require a full library scan during normal protected execution; this is an explicit rebuild/recovery rule.
7. Add an execution/rebuild regression that leaves a real source media + forced info JSON while the requested postprocessor fails, verifies yt-dlp did not archive it, then proves Rebuild does not invent completion.
8. Cover at least audio extraction and one same-extension FFmpeg failure shape because both can leave media-extension files beside authoritative metadata.

**Upstream proof:** In pinned yt-dlp `c7fb478d...`, `process_info` writes info JSON before the media/postprocessor phase. A `PostProcessingError` causes an early return before `info_dict['__write_download_archive'] = True`. `process_video_result` records the native identity only when the requested downloads contain true archive-write flags. FFmpeg extraction/conversion raises before its final replace/move sequence on failure, so the source media can remain while the native ledger intentionally stays unchanged.

## Investigation leads

_No unresolved leads currently._

## Full re-audit coverage

Use this checklist as the resume point after any timeout. A section is not complete until current-head source, relevant tests, and affected upstream yt-dlp semantics have been reviewed.

- [x] Branch/base diff inventory: 18 changed files classified against release base `49a290d0...`.
  - Production: `DownloadHistory.cs`, `frmDownloadHistory.cs`, `DownloadInfo.cs`, `ExtendedMediaDetails.cs`, `frmDownloader.cs`, `frmExtendedDownloader.cs`, `frmSettings.cs`, project file/resource updater binary.
  - Audit/test tooling: three audit workflows, `Run-AuditRepairBatch.ps1`, `apply-audit-repairs.py`, Download History regression suite, one existing repair-wave test, canonical audit, this TODO.
  - No unrelated production files are changed by the feature branch.
- [x] Download History state model: re-audited enable/disable/re-enable, configured/bound/effective archive transitions, relocation union, prepared-report commit flow, and fail-closed persistence. **Open finding: DH-A070.**
- [x] Archive lifecycle: re-audited initialize/validate/rebuild/reconcile/relocate/reset/backup/corruption/truncation/strict UTF-8. **Open findings: DH-A070, DH-A073.**
- [x] Ownership/non-destructive guarantees: re-audited primary/backup/lock/temp ownership, media/metadata immutability, and scan-only roots. **Open findings: DH-A070, DH-A073; output reparse containment tracked separately as DH-A071.**
- [x] Concurrency/TOCTOU: re-audited named mutex + persistent file lock, prepared ledger floor, provider/settings revalidation, per-batch-item leases, and post-run backup refresh. **Open state-path alias risk remains DH-A073.**
- [ ] Path semantics: active root, archive, cache, cookies, inventory roots, relative paths, \`~\`, \`$VAR\`, \`\${VAR}\`, \`%VAR%\`, escaped sigils.
- [ ] Filesystem containment: parent traversal, reparse points/junctions/symlinks, UNC/network roots, case/normalization behavior.
- [ ] Output containment: standard, mostly-custom, extended downloader, batch mode, split chapters, retained intermediates.
- [x] yt-dlp custom-argument parser parity: rechecked current short value-taking table, long-prefix handling, dangling-option bracketing, terminator isolation, user aliases, and current built-in presets. **Open extractor-argument trust bypass: DH-A075.**
- [ ] Unsafe execution/write surfaces: config, plugins, exec hooks, external downloaders, FFmpeg, JS runtimes, cache, cookies, page dumps, print-to-file, self-update.
- [x] False-completion/native-archive semantics: rechecked test/simulate/skip/flat/filter/no-format/error/fragment/archive-write paths against pinned yt-dlp. **Open finding: DH-A072 (existing final file can become a new archive success without a real retry).**
- [ ] Provider identity recovery: extractor_key/ie_key/id validation, controls, archive grammar, case semantics.
- [ ] Physical inventory allowlist: current yt-dlp media/audio/storyboard classes and safe compatibility extensions.
- [ ] Sidecars/derivatives: info JSON, thumbnails, playlist metafiles, subtitles, chapters, components, \`.orig\`, \`.uncut\`, manifests.
- [ ] Total archive-loss rebuild: metadata recovery, filename recovery, derivative ownership, ambiguity/fail-closed cases.
- [x] Multiple roots/path-agnostic identity: rechecked active root + zero/one/many scan-only roots, root minimization, moves between runs, and unavailable-root behavior. Normal protected execution remains ledger-only; explicit management scans fail closed on unavailable configured roots.
- [x] Large-library behavior: rechecked streaming directory enumeration, Aho-style filename identity matching, scan-root minimization, deferred-derivative path storage, no normal-run full scan, and async management UI. No new correctness/performance finding.
- [x] Settings/UI safety: rechecked enable/save/cancel/validate/rebuild/open/browse and fail-closed commit/digest flow. **Open finding: DH-A070 (Reset ownership/rebinding).**
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
