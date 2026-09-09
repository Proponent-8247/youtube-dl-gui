# End-to-end audit findings checklist

Catalog v1.0 | repository `Proponent-8247/youtube-dl-gui` | branch **audit-fixes**

Reviewed production snapshot: `41637f315a42b0d878fbca09160548afd62e79ce`. Catalog ancestry: `49d6fb05e0b5fd5a083daa552c6aba9769bebb85`. Master comparison: `c6ce1e6421c6bf785f30f41496b745489bcc2bab`. Date: September 8, 2026 (America/Boise); export and CI timestamps use UTC.

## How to use this checklist

**This checklist supersedes blanket completion language in the earlier closeout. A green 80-case regression suite is not an end-to-end sign-off for every item below.** This commit records findings and evidence; it does not implement the open code corrections.

- `[x] REGRESSION-EVIDENCED` means the named historical correction has corresponding passing retained tests for the stated scope. It does not assert exhaustive coverage.
- `[ ] HISTORICAL-FIX-VERIFY` means corrective code was committed, but complete finding-specific acceptance evidence is not established. An unchecked historical item is **not** a claim that the original bug is still present. Partial test evidence is shown rather than hidden.
- `[ ] SOURCE-CONFIRMED`, `VALIDATION-LEAD`, `DESIGN-RISK`, and validation/policy items require the specific follow-up stated. A lead is not a reproduced exploit or user crash.
- Preserve IDs and evidence when resolving an item. Record implementing commit, exact test/reproducer, result and tested revision before checking it. Do not close an item merely because a request workflow or unrelated test passed.

**Inventory:** 797 audit-branch commits since master; **347 commits changing production C#/.csproj/.projitems files**, mapped to **143 historical checklist items** plus 4 withdrawn records. 17 historical items have the stated regression evidence; 126 need further acceptance verification. There are also 8 open code/risk items and 16 validation/policy tasks. These are **tracking counts, not independent-vulnerability counts**.

Severity on new open items is triage, not CVSS. Historical severities are intentionally unrated when the original report/reproducer is unavailable. Related repair iterations are consolidated, but all source-changing commits are retained under their item. Helper fixes, excluded source copies and later strengthening are not silently multiplied into independent defects.

## Evidence baseline

- [Full ancestry export and patches, run 34309996767](https://github.com/Proponent-8247/youtube-dl-gui/actions/runs/34309996767) produced the complete 797-commit ledger. Export artifact: `audit-catalog-49d6fb05e0b5fd5a083daa552c6aba9769bebb85`.
- [Final prior verification, run 34294360723](https://github.com/Proponent-8247/youtube-dl-gui/actions/runs/34294360723): 80 distinct tests in three passes, zero failures, default and x86 harnesses, full Release packaging and updater identity checks.
- [Catalog-basis verification, run 34309996589](https://github.com/Proponent-8247/youtube-dl-gui/actions/runs/34309996589) also passed on `49d6fb05e0b5fd5a083daa552c6aba9769bebb85`. The evidence-export workflow added no production changes.
- Source links below are pinned. Each historical item links its last implementation patch and lists any earlier related correction IDs. Full SHAs, subjects and patches are retained by the ancestry export; the short IDs are unique there.
- **Master is comparison input only.** Its head was not modified; `feature/download-history-20260908` is not included in this task.

## 1. Open code findings and review leads

- [ ] **O001 — TimePicker.TimeSpan loses long durations and its setter does not refresh UI**
  - **Priority / state:** Medium / public control API; SOURCE-CONFIRMED; runtime reproducer pending.
  - **Finding:** The getter substitutes zero for hours greater than 24; the setter takes the Hours component rather than total hours and does not refresh the display. A 25-hour value can become 0 or 1 hour through this property. No production call to this property was found in the inspected application; Value is a separate path.
  - **Evidence:** [youtube-dl-gui/Controls/TimePicker.cs:83](https://github.com/Proponent-8247/youtube-dl-gui/blob/41637f315a42b0d878fbca09160548afd62e79ce/youtube-dl-gui/Controls/TimePicker.cs#L83-L94).
  - **Acceptance:** Round-trip 0, 24, 25 and 100-hour values through TimeSpan and Value; assert display consistency, negatives/overflow policy and intended DateBasedTime behavior.

- [ ] **O002 — TimePicker.SetValue does not update hour-field width**
  - **Priority / state:** Low / public control API; SOURCE-CONFIRMED; runtime reproducer pending.
  - **Finding:** SetValue updates the display without recomputing HourToMinuteSeparator. With DateBasedTime=false and hours >=100, field selection retains the previous width; Value setter already handles this. No application call to SetValue was found.
  - **Evidence:** [youtube-dl-gui/Controls/TimePicker.cs:148](https://github.com/Proponent-8247/youtube-dl-gui/blob/41637f315a42b0d878fbca09160548afd62e79ce/youtube-dl-gui/Controls/TimePicker.cs#L148-L156).
  - **Acceptance:** Use SetValue(100,12,34,0), then select/edit every field and compare with Value assignment; preserve current duration semantics.

- [ ] **O003 — Time editor assumes separators exist and can overflow hours**
  - **Priority / state:** Medium; VALIDATION-LEAD; do not call it a reproduced user crash.
  - **Finding:** UpdateControl indexes colon/dot splits without length checks; several event routes call it outside the LostFocus catch. Hours++ can overflow an accepted int.MaxValue. The context-menu/edit sequence and exact user-visible failure still need a real-control test.
  - **Evidence:** [youtube-dl-gui/Controls/TimePicker.cs:155](https://github.com/Proponent-8247/youtube-dl-gui/blob/41637f315a42b0d878fbca09160548afd62e79ce/youtube-dl-gui/Controls/TimePicker.cs#L155-L179); [youtube-dl-gui/Controls/TimePicker.cs:461](https://github.com/Proponent-8247/youtube-dl-gui/blob/41637f315a42b0d878fbca09160548afd62e79ce/youtube-dl-gui/Controls/TimePicker.cs#L461-L466).
  - **Acceptance:** Exercise context-menu paste/cut, missing fields, missing milliseconds separator, Shift+digit input, wide-hour boundaries and Up at int.MaxValue; ensure invalid input cannot escape the UI event or change unrelated fields.

- [ ] **O004 — Forced thumbnail replacement drops the previous cached Image without disposal**
  - **Priority / state:** Low / currently unused forced-refresh path; SOURCE-CONFIRMED; runtime reproducer pending.
  - **Finding:** A successful ForceRedownload assigns a new image without disposing the old one. Current form calls use false; the dormant true path still has a resource-ownership defect. Do not dispose an image while a PictureBox still references it.
  - **Evidence:** [youtube-dl-gui/Classes/DataClasses/ExtendedMediaDetails.cs:194](https://github.com/Proponent-8247/youtube-dl-gui/blob/41637f315a42b0d878fbca09160548afd62e79ce/youtube-dl-gui/Classes/DataClasses/ExtendedMediaDetails.cs#L194-L201).
  - **Acceptance:** Repeat forced refresh with old/new images and a bound PictureBox; establish one owner, detach old display references safely, dispose replaced images exactly once, and preserve the old image on failed refresh.

- [ ] **O005 — Updater parent-exit wait is uncancellable and outside RunUpdate recovery**
  - **Priority / state:** Medium; SOURCE-CONFIRMED missing cancellation/exception boundary; behavior policy needed.
  - **Finding:** WaitForApplication sends a synchronous message and awaits ProgramProcess.WaitForExit without the cancellation token. RunUpdate awaits it before entering its catch/rollback region. Waiting for active user work is intentional; a deadline must not kill that work automatically.
  - **Evidence:** [youtube-dl-gui-updater/Forms/frmUpdater.cs:216](https://github.com/Proponent-8247/youtube-dl-gui/blob/41637f315a42b0d878fbca09160548afd62e79ce/youtube-dl-gui-updater/Forms/frmUpdater.cs#L216-L230); [youtube-dl-gui-updater/Forms/frmUpdater.cs:100](https://github.com/Proponent-8247/youtube-dl-gui/blob/41637f315a42b0d878fbca09160548afd62e79ce/youtube-dl-gui-updater/Forms/frmUpdater.cs#L100-L129).
  - **Acceptance:** Test cancellation/closing the updater while the main process stays alive, a stale/hung message target, and main-process exit races; contain exceptions and cancel the wait without terminating user downloads.

- [ ] **O006 — Whole GIF conversion still lacks owned cancellation and robust failure cleanup**
  - **Priority / state:** Medium; VALIDATION-LEAD / lifecycle decision.
  - **Finding:** The ImageMagick version probe is bounded, but the real FFmpeg conversion uses an unbounded WaitForExit. The async-void handler rethrows failures and cleanup itself can throw; form closure does not own/cancel the full conversion. External-console tool lifetime is partly intentional, so probe tests must not be presented as proof of full conversion cleanup.
  - **Evidence:** [youtube-dl-gui/Forms/frmMiscTools.cs:135](https://github.com/Proponent-8247/youtube-dl-gui/blob/41637f315a42b0d878fbca09160548afd62e79ce/youtube-dl-gui/Forms/frmMiscTools.cs#L135-L210).
  - **Acceptance:** Run failing/hanging FFmpeg and ImageMagick fixtures, close the owner, deny workspace deletion, and check files/processes/UI recovery. Decide whether launched external work should survive form closure before changing behavior.

- [ ] **O007 — Main updater-request handler does not bind the requester to the launched updater**
  - **Priority / state:** Medium / local IPC trust boundary; VALIDATION-LEAD; no remote exploit claim.
  - **Finding:** The main receiver responds to m.WParam and temporarily permits acknowledgement without checking an expected updater process/window. The opposite-direction receiver has checks. A local forged request/acknowledgement requires a separate adversarial test; no privilege escalation or remote-code-execution claim is established.
  - **Evidence:** [youtube-dl-gui/Controls/MessageHandler.cs:81](https://github.com/Proponent-8247/youtube-dl-gui/blob/41637f315a42b0d878fbca09160548afd62e79ce/youtube-dl-gui/Controls/MessageHandler.cs#L81-L119).
  - **Acceptance:** Send unsolicited and forged requests with/without cached release metadata, plus valid reentrant acknowledgement. Bind an updater session without breaking synchronous delivery; reject unrelated senders and preserve active transfers.

- [ ] **O008 — Update/download temporary and backup names are deterministic and shared**
  - **Priority / state:** Medium / replacement integrity; DESIGN-RISK; collision reproducer pending.
  - **Finding:** The updater uses update.part; generic downloads use Output.tmp/Output.bck and remove/move those paths without per-operation ownership. Retries/rollback were fixed, but concurrent operations or pre-existing sidecars are a distinct collision/data-loss risk.
  - **Evidence:** [youtube-dl-gui-updater/Forms/frmUpdater.cs:118](https://github.com/Proponent-8247/youtube-dl-gui/blob/41637f315a42b0d878fbca09160548afd62e79ce/youtube-dl-gui-updater/Forms/frmUpdater.cs#L118-L120); [youtube-dl-gui/Forms/frmGenericDownloadProgress.cs:27](https://github.com/Proponent-8247/youtube-dl-gui/blob/41637f315a42b0d878fbca09160548afd62e79ce/youtube-dl-gui/Forms/frmGenericDownloadProgress.cs#L27-L30).
  - **Acceptance:** Use two operations on the same destination and sentinel sidecar files in a temporary directory. Define lock/ownership and atomic-replacement rules; preserve unrelated files and a recoverable prior output on failure.

## 2. Validation gaps and policy decisions

- [ ] **V001 — All earlier source corrections need individual acceptance evidence** (High / sign-off). The prior 50-commit history begins after b734c059 and omitted much older audit work. Every source-changing record is mapped below; unchecked historical rows need targeted tests or documented, reviewed acceptance evidence, not another blanket green-build claim.

- [ ] **V002 — Original complete D/H/R narrative and numbering need reconciliation** (Medium / traceability). The full original numbered report was not recovered. Known C/N/D/H aliases are mapped where supported; do not fabricate missing IDs or interpret commit count as defect count.

- [ ] **V003 — Exercise real supported provider/media workflows end to end** (High / integration coverage). The retained tests use controlled executables and HTTP fixtures. Add authorized live or recorded integration cases for direct video, channel/playlist/mixed input, authentication, zero/error retries, custom options, no-audio, remux/recode, subtitles, thumbnails and large batches. Do not modify the separate history feature.

- [ ] **V004 — Validate the supported OS/runtime/tool-version matrix** (Medium / compatibility). Retaining Framework 4.7.2 and running default/x86 harnesses on Windows Server 2022 does not execute every historical supported OS, display/DPI setup or actual downloader/FFmpeg/ImageMagick version. Record exact tested versions and exceptions.

- [ ] **V005 — Run sustained queue/output/process resource tests** (Medium / robustness). Three closes per form under controlled concurrent output are not a multi-hour soak. Measure memory, handles, GDI objects, child processes and responsiveness across repeated retries, failures and large channels.

- [ ] **V006 — Fault-inject persistence and replacement paths** (Medium / storage safety). Add denied-write, disk-full, locked-file, cancellation, rename/move failure, recovery-after-crash and concurrent-destination tests for INI/registry, language/tool updates, generic replacement and updater rollback.

- [ ] **V007 — Review nullable warnings and async-void/unobserved task paths** (Medium / diagnostic coverage). The build is not warning-free. Triage each warning and fire-and-forget task by reachable behavior; warnings are not automatically proven defects, and catching every exception without recovery is not acceptance.

- [ ] **V008 — Inventory dependencies and shipped binaries and assess advisories** (Medium / supply chain). The evidence does not include a complete dependency/SBOM/license/current-advisory review or reproducibility proof for every external binary. Do not claim vulnerabilities without checking exact versions and applicability.

- [ ] **V009 — Validate remaining controls and interop under real message dispatch** (Medium / UI and native coverage). Many controls/designer/native paths were only structurally inspected. Include taskbar, clipboard, DWM, dialogs, setting changes, localization, exceptions and repeated create/dispose on both native widths.

- [ ] **V010 — Separate shipping code from excluded copies and experimental routes** (Low / audit hygiene). Classes/DownloadInfo.cs, Classes/ConvertInfo.cs and Controls/UpdateMessageHandler.cs are excluded legacy copies; several extended tools are Debug-only. Their edits are recorded, but cannot prove a shipping fix. Verify project inclusion before changing duplicates.

- [ ] **P001 — Choose fork versus upstream application-update destination** (Medium / product policy). Current updater URLs continue to point to the upstream release source. Document the intended distribution policy; do not silently redirect updates or overwrite the fork with a different product build.

- [ ] **P002 — Define cumulative console-history and queue retention** (Medium / resource policy). Per-response and per-line bounds do not cap the total visible output or queued model/image data. Agree on retention/export/truncation behavior before imposing a global bound.

- [ ] **P003 — Distinguish integrity checks from publisher authenticity** (Medium / trust). SHA-256 matching does not independently authenticate a compromised release source. Decide whether signing/publisher verification is required; selected executables and explicit custom arguments remain trusted execution inputs.

- [ ] **P004 — Define fallback guarantees where Windows job attachment fails** (Medium / child ownership). Process-tree fallback is best effort and not a sandbox. Record expectations for nested-job restrictions, escaping descendants and cancellation without terminating unrelated user processes.

- [ ] **P005 — Assess native image decoder exposure separately from byte/pixel bounds** (Medium / image decoding). Byte/pixel limits and cancellable downloads reduce resource risk but are not decoder isolation. Record decoder trust assumptions and evaluate hostile-image handling before claiming thumbnail security completeness.

- [ ] **P006 — Keep experimental-tool completion and download-history design separate** (Low / feature scope). Do not count absent implementation of deliberate Debug-only tools or the separate download-history feature as repaired behavior. Record intentional omissions and use a separate authorized feature scope.


## 3. Historical corrections and remaining verification

**TESTED** (`REGRESSION-EVIDENCED`) = the stated correction has named passing tests; **VERIFY** (`HISTORICAL-FIX-VERIFY`) = code correction exists, but finding-specific acceptance is incomplete. Unchecked rows are not automatically still-broken code. The common task is to reproduce the pre-fix failure and verify corrected behavior on the included compiled path. Patch links identify exact changed files. Earlier correction IDs are unique in the full exported ancestry and must not be reapplied blindly.

Related iterations are consolidated for tracking. Each checked item remains limited to its named test scope. Missing test names mean no dedicated acceptance test was established in the retained 80-case inventory. Historical severities are unrated, not assumed harmless.

### Download execution

- [ ] **F001 — Malformed downloader progress rows and culture-sensitive percentages** (VERIFY). [Patch c1714ff44e](https://github.com/Proponent-8247/youtube-dl-gui/commit/c1714ff44e). Earlier: `73df6ea81f`, `17e755518f`.
  Tests: `Progress.MalformedRowsCannotThrow`, `Progress.PercentageUsesInvariantCulture`. **Only partial family coverage; further verification is required.**
- [x] **F068 — Redirected output is not bounded, drained or fault-observed** (TESTED). [Patch 869cebd4c7](https://github.com/Proponent-8247/youtube-dl-gui/commit/869cebd4c7). Earlier: `5d371092a2`, `f8aa8580c9`, `86643639b5`.
  Tests: `R006.LiveOutputLineStorageIsBounded`, `D006.LiveOutputHandlesCarriageReturns`, `D006.LiveOutputCallbackFaultIsContained`, `D007.LiveOutputDrainHasDeadline`, `D006.LiveOutputStartIsSingleUse`.
- [ ] **F120 — Concurrent stdout/stderr shares output-message state unsafely** (VERIFY). [Patch 7b7886fe70](https://github.com/Proponent-8247/youtube-dl-gui/commit/7b7886fe70). Earlier: `b3fe1f226d`.
  Tests: `Stress.quickCloseDuringConcurrentOutput`, `Stress.extendedCloseDuringConcurrentOutput`. **Only partial family coverage; further verification is required.**

### HTTP and replacement

- [ ] **F002 — Copy deflate output to destination buffer** (VERIFY). [Patch c6e77314f6](https://github.com/Proponent-8247/youtube-dl-gui/commit/c6e77314f6).
- [ ] **F020 — Generic download form closes before cancellation/completion cleanup** (VERIFY). [Patch 3899f1855f](https://github.com/Proponent-8247/youtube-dl-gui/commit/3899f1855f). Earlier: `89dd03568a`.
- [ ] **F021 — Generic replacement failure loses the prior output or retry backup** (VERIFY). [Patch d2dad0afd5](https://github.com/Proponent-8247/youtube-dl-gui/commit/d2dad0afd5). Earlier: `056abebd96`.
- [ ] **F060 — HTTP decoding/content metadata assumptions corrupt downloads** (VERIFY). [Patch 15a6d5671b](https://github.com/Proponent-8247/youtube-dl-gui/commit/15a6d5671b). Earlier: `3b0559bbc8`, `52dd228f39`.
- [ ] **F061 — HTTP progress percentages and callback state are inconsistent** (VERIFY). [Patch a99846f11e](https://github.com/Proponent-8247/youtube-dl-gui/commit/a99846f11e). Earlier: `2af88e6b1b`.
- [ ] **F100 — Legacy TLS protocols remain explicitly enabled** (VERIFY). [Patch 8f78791778](https://github.com/Proponent-8247/youtube-dl-gui/commit/8f78791778). Earlier: `5c432fd97d`, `f71a1bcd96`, `b3087738cf`.
- [ ] **F127 — HTTP retries/body reads ignore cancellation or decoded-size limits** (VERIFY). [Patch 39a6a1db2c](https://github.com/Proponent-8247/youtube-dl-gui/commit/39a6a1db2c). Earlier: `2d1d7c48f2`, `a135a2efee`.
  Tests: `N003.HttpErrorBodyHonorsCancellation`, `N003.HttpErrorDiagnosticsAreBounded`, `N003.HttpGzipStringLimitAppliesAfterDecompression`, `N003.HttpSmallGzipContentPreserved`. **Only partial family coverage; further verification is required.**

### Settings and persistence

- [ ] **F003 — Load skipped beta version from its own key** (VERIFY). [Patch ff1257a83a](https://github.com/Proponent-8247/youtube-dl-gui/commit/ff1257a83a).
- [ ] **F019 — Saved/tray argument and format indices escape valid bounds** (VERIFY). [Patch 3304af5aba](https://github.com/Proponent-8247/youtube-dl-gui/commit/3304af5aba). Earlier: `ddf775dfb6`, `4b8766dce2`, `f900279724`, `a88fffc07c`, `bc61ab4595`, `edd6924b91`, `d44cbe2794`, `e02a3ced2f`.
- [ ] **F023 — Generated file-dialog filters malformed, stale or mismatched** (VERIFY). [Patch a61df725d3](https://github.com/Proponent-8247/youtube-dl-gui/commit/a61df725d3). Earlier: `d53ac1979d`, `127ecd7a57`.
- [ ] **F038 — Cancelling settings commits or leaves stale provider state** (VERIFY). [Patch 7be9d90b20](https://github.com/Proponent-8247/youtube-dl-gui/commit/7be9d90b20). Earlier: `3c9f5097e8`.
- [ ] **F039 — Download-root handling breaks relative or relocated folders** (VERIFY). [Patch 836f8946eb](https://github.com/Proponent-8247/youtube-dl-gui/commit/836f8946eb). Earlier: `14c4c25a78`, `1cdcbcf80a`.
- [ ] **F040 — Custom extension collections or removal selections are inconsistent** (VERIFY). [Patch 5071febe7c](https://github.com/Proponent-8247/youtube-dl-gui/commit/5071febe7c). Earlier: `0a960545a9`.
- [ ] **F041 — Allow backspace in proxy port field** (VERIFY). [Patch e0a212cc54](https://github.com/Proponent-8247/youtube-dl-gui/commit/e0a212cc54).
- [ ] **F092 — UI preferences are not persisted** (VERIFY). [Patch a518cbed42](https://github.com/Proponent-8247/youtube-dl-gui/commit/a518cbed42). Earlier: `1dd31eaf6a`, `57023a4fc9`.
- [ ] **F095 — Custom-argument and filename-schema history corrupts order or storage mode** (VERIFY). [Patch 2e549f5d8d](https://github.com/Proponent-8247/youtube-dl-gui/commit/2e549f5d8d). Earlier: `e6c955870a`, `67f101e7bc`.
- [ ] **F103 — Stale numeric settings and column widths are not range-checked** (VERIFY). [Patch 82a8277dc3](https://github.com/Proponent-8247/youtube-dl-gui/commit/82a8277dc3). Earlier: `5933d40448`.
- [ ] **F104 — Startup cleanup targets wrong backup path or fails fatally** (VERIFY). [Patch 86357b4d86](https://github.com/Proponent-8247/youtube-dl-gui/commit/86357b4d86). Earlier: `9a0aa18952`.
- [ ] **F106 — Persist main form state on tray exit** (VERIFY). [Patch ce65ce2579](https://github.com/Proponent-8247/youtube-dl-gui/commit/ce65ce2579).
- [ ] **F109 — Reject schema history delimiter entries** (VERIFY). [Patch 91cc13d323](https://github.com/Proponent-8247/youtube-dl-gui/commit/91cc13d323).
- [ ] **F113 — Batch state restoration/localization changes active controls** (VERIFY). [Patch 855fc18889](https://github.com/Proponent-8247/youtube-dl-gui/commit/855fc18889). Earlier: `ea72281730`, `27ccff165d`, `0d59853df3`, `dc97e2e139`.
- [ ] **F128 — Surface INI persistence failures** (VERIFY). [Patch b584c240d0](https://github.com/Proponent-8247/youtube-dl-gui/commit/b584c240d0).

### Arguments and media selection

- [ ] **F004 — Download-rate units separated from numeric values** (VERIFY). [Patch 155ac9718a](https://github.com/Proponent-8247/youtube-dl-gui/commit/155ac9718a). Earlier: `002bea65ba`.
- [ ] **F009 — Unavailable-fragment setting ignored or inverted** (VERIFY). [Patch 84a82b4d63](https://github.com/Proponent-8247/youtube-dl-gui/commit/84a82b4d63). Earlier: `2b5738a5aa`, `ee5ea7e7db`, `4745253b9c`.
- [ ] **F010 — Persisted proxy protocol index used without validation** (VERIFY). [Patch 207810eef8](https://github.com/Proponent-8247/youtube-dl-gui/commit/207810eef8). Earlier: `1d7774406a`.
- [ ] **F018 — CBR/VBR selection and labels drift across main and batch modes** (VERIFY). [Patch 6d54aaf6f8](https://github.com/Proponent-8247/youtube-dl-gui/commit/6d54aaf6f8). Earlier: `10cc15dbfb`, `4f3327e671`, `79794a7b59`, `aa61dc5356`, `bee7f75084`.
- [ ] **F036 — Playlist end/selection arguments change intended traversal** (VERIFY). [Patch f4c1383861](https://github.com/Proponent-8247/youtube-dl-gui/commit/f4c1383861). Earlier: `a6a8fdbb3a`, `d7f96b7fdd`.
  Tests: `D017.ExplicitPlaylistDoesNotDisablePlaylist`, `D017.SingleVideoDefaultPreserved`. **Only partial family coverage; further verification is required.**
- [ ] **F037 — Unsupported annotations flag emitted for yt-dlp** (VERIFY). [Patch 0953a46b12](https://github.com/Proponent-8247/youtube-dl-gui/commit/0953a46b12). Earlier: `db986bca51`, `a69506ca91`.
- [ ] **F044 — Extended audio conversion uses a video operation or unsupported format** (VERIFY). [Patch 9d89883e3f](https://github.com/Proponent-8247/youtube-dl-gui/commit/9d89883e3f). Earlier: `8ddf64e789`.
- [ ] **F058 — Avoid double-quoting mostly-custom output template** (VERIFY). [Patch f03fae3304](https://github.com/Proponent-8247/youtube-dl-gui/commit/f03fae3304).
- [ ] **F062 — Abort-on-error configuration ignored across request builders** (VERIFY). [Patch 3567720256](https://github.com/Proponent-8247/youtube-dl-gui/commit/3567720256). Earlier: `e5d690c67d`, `57c671e328`.
- [ ] **F063 — Concurrent-fragment option emitted to unsupported providers** (VERIFY). [Patch 86a30c4f4e](https://github.com/Proponent-8247/youtube-dl-gui/commit/86a30c4f4e). Earlier: `5d28bec121`.
- [ ] **F069 — Audio VBR quality serialized incorrectly or omitted** (VERIFY). [Patch f0d9e8f67c](https://github.com/Proponent-8247/youtube-dl-gui/commit/f0d9e8f67c). Earlier: `8d6a7157ff`, `5695fdf5fb`.
- [ ] **F072 — Keep no-sound fallbacks video-only** (VERIFY). [Patch 988f315693](https://github.com/Proponent-8247/youtube-dl-gui/commit/988f315693).
- [ ] **F088 — Thumbnail embedding ignores setting or effective output container** (VERIFY). [Patch 4431e6b84a](https://github.com/Proponent-8247/youtube-dl-gui/commit/4431e6b84a). Earlier: `ff12f9334d`.
- [ ] **F091 — Website folder/Reddit detection mishandles case or unsafe path characters** (VERIFY). [Patch afad0571cb](https://github.com/Proponent-8247/youtube-dl-gui/commit/afad0571cb). Earlier: `244a73a0f5`, `fe2f5531b6`.
- [ ] **F114 — Zero-retry setting is treated as unset** (VERIFY). [Patch aa68ea1fe5](https://github.com/Proponent-8247/youtube-dl-gui/commit/aa68ea1fe5). Earlier: `fdf8248e65`.
- [ ] **F122 — Forward configured ffmpeg location for post-processing** (VERIFY). [Patch 9601f9929d](https://github.com/Proponent-8247/youtube-dl-gui/commit/9601f9929d).
- [ ] **F123 — Enforce requested yt-dlp remux containers** (VERIFY). [Patch 1ec95a17f4](https://github.com/Proponent-8247/youtube-dl-gui/commit/1ec95a17f4).
- [x] **F131 — Preserve custom argument whitespace** (TESTED). [Patch 9874be0467](https://github.com/Proponent-8247/youtube-dl-gui/commit/9874be0467).
  Tests: `D032.QuotedWhitespacePreserved`.
- [x] **F143 — Keep clipboard URL checks linear on long non-link text** (TESTED). [Patch 3f14fd846e](https://github.com/Proponent-8247/youtube-dl-gui/commit/3f14fd846e).
  Tests: `N013.LongClipboardTextCompletesWithinDeadline`, `N013.ClipboardHeuristicPreservesAcceptedInputs`.

### Metadata and extended selection

- [ ] **F005 — Use video encoder index for recode format** (VERIFY). [Patch 8c795485d5](https://github.com/Proponent-8247/youtube-dl-gui/commit/8c795485d5).
- [ ] **F043 — Guard unknown-only custom media selection** (VERIFY). [Patch a4666b706f](https://github.com/Proponent-8247/youtube-dl-gui/commit/a4666b706f).
- [ ] **F048 — Metadata parsing does not handle null results or remember parsed state** (VERIFY). [Patch 593dfffba7](https://github.com/Proponent-8247/youtube-dl-gui/commit/593dfffba7). Earlier: `07d3197229`, `fbbd64e088`.
- [ ] **F065 — Separate extended censored auth arguments** (VERIFY). [Patch 65738f84ce](https://github.com/Proponent-8247/youtube-dl-gui/commit/65738f84ce).
- [ ] **F070 — Include selected audio in separate format selector** (VERIFY). [Patch cfc97c6187](https://github.com/Proponent-8247/youtube-dl-gui/commit/cfc97c6187).
- [ ] **F080 — Extended metadata resolver interferes with transfers or loses work/failures** (VERIFY). [Patch 4aacc7add8](https://github.com/Proponent-8247/youtube-dl-gui/commit/4aacc7add8). Earlier: `66503ed0e0`, `b72b720f57`, `1f6f67fe50`, `9ade58ee8d`, `18ec079862`, `cf9c0c8bb6`, `b185159027`, `add72b70d6`.
  Tests: `C1.ResolverCompletionPreservesActiveDownload`. **Only partial family coverage; further verification is required.**
- [ ] **F081 — Extended initial binding or item switching overwrites options** (VERIFY). [Patch e7adc1da68](https://github.com/Proponent-8247/youtube-dl-gui/commit/e7adc1da68). Earlier: `971e81bdf1`, `81e6e22c88`, `969dbad1c8`.
  Tests: `C5.FirstBindingPreservesNoAudio`, `C5.FirstAuthenticatedBindingPreservesNoAudio`. **Only partial family coverage; further verification is required.**
- [x] **F134 — Preserve UNC roots in media source paths (H001)** (TESTED). [Patch ccbff3ea96](https://github.com/Proponent-8247/youtube-dl-gui/commit/ccbff3ea96).
  Tests: `H001.UncSourcePreserved`.

### Conversion and miscellaneous tools

- [ ] **F006 — Clear attachment selection before regenerating conversion args** (VERIFY). [Patch d6f9f0cb64](https://github.com/Proponent-8247/youtube-dl-gui/commit/d6f9f0cb64).
- [ ] **F007 — Extended conversion selects or disables the wrong stream classes** (VERIFY). [Patch 5b0f7213b2](https://github.com/Proponent-8247/youtube-dl-gui/commit/5b0f7213b2). Earlier: `26fe20065f`.
- [ ] **F008 — Avoid dividing by zero for unknown frame rates** (VERIFY). [Patch a0e124058d](https://github.com/Proponent-8247/youtube-dl-gui/commit/a0e124058d).
- [ ] **F034 — Merger attachment categories and bulk queue state become inconsistent** (VERIFY). [Patch 93650e2aa8](https://github.com/Proponent-8247/youtube-dl-gui/commit/93650e2aa8). Earlier: `beb72cc0fe`, `8bd46f17db`.
- [ ] **F042 — Invalid high422 profile spelling emitted/displayed** (VERIFY). [Patch fd95201f32](https://github.com/Proponent-8247/youtube-dl-gui/commit/fd95201f32). Earlier: `4ff7162489`.
- [ ] **F047 — Handle extensionless paths in extended converter** (VERIFY). [Patch 4b9a9af132](https://github.com/Proponent-8247/youtube-dl-gui/commit/4b9a9af132).
- [ ] **F057 — Miscellaneous tool paths/output names or shell invocation are wrong** (VERIFY). [Patch 884127ce35](https://github.com/Proponent-8247/youtube-dl-gui/commit/884127ce35). Earlier: `b151f20e29`, `0f6276e3bc`, `b93a05b3e3`, `17cafc0636`.
- [ ] **F059 — GIF conversion filter, workspace and tool detection are unsafe** (VERIFY). [Patch 407fa31781](https://github.com/Proponent-8247/youtube-dl-gui/commit/407fa31781). Earlier: `aca04ffc86`, `673647fe1b`, `066d3ae3ed`, `79fa2d931b`.
  Tests: `N009.ImageMagickProbeDrainsOutputBeforeWaiting`, `N009.ImageMagickProbeBoundsInheritedPipes`. **Only partial family coverage; further verification is required.**
- [ ] **F075 — Suggest batch output name from input file** (VERIFY). [Patch e18b7b7661](https://github.com/Proponent-8247/youtube-dl-gui/commit/e18b7b7661).
- [ ] **F076 — Use ffmpeg faststart muxer flag** (VERIFY). [Patch 0591d7851d](https://github.com/Proponent-8247/youtube-dl-gui/commit/0591d7851d).
- [ ] **F084 — Classify conversion extensions exactly** (VERIFY). [Patch 366376a744](https://github.com/Proponent-8247/youtube-dl-gui/commit/366376a744).
- [ ] **F086 — Allow full-custom converter arguments** (VERIFY). [Patch cccccb1ecc](https://github.com/Proponent-8247/youtube-dl-gui/commit/cccccb1ecc).
- [ ] **F132 — Make confirmed ffmpeg overwrites noninteractive** (VERIFY). [Patch 98bd961ba9](https://github.com/Proponent-8247/youtube-dl-gui/commit/98bd961ba9).
- [ ] **F133 — Probe merger inputs off the UI thread** (VERIFY). [Patch 22ca857835](https://github.com/Proponent-8247/youtube-dl-gui/commit/22ca857835).
- [x] **F139 — Round trip converter preset profile and sample-rate selections** (TESTED). [Patch db466c3e9b](https://github.com/Proponent-8247/youtube-dl-gui/commit/db466c3e9b).
  Tests: `N001.ConverterPresetDropdownMatchesEnum`, `N001.ConverterProfileDropdownMatchesEnum`, `N001.ConverterOptionsRoundTrip`.
- [x] **F140 — Emit the valid ffmpeg slow preset** (TESTED). [Patch 259c0c7c30](https://github.com/Proponent-8247/youtube-dl-gui/commit/259c0c7c30).
  Tests: `N002.ConverterSlowPresetIsValid`.

### Tool discovery and installation

- [ ] **F011 — Return directories from system PATH discovery** (VERIFY). [Patch fb2cfca78e](https://github.com/Proponent-8247/youtube-dl-gui/commit/fb2cfca78e).
- [ ] **F012 — Configured FFmpeg/FFprobe path not consistently refreshed or updated** (VERIFY). [Patch 34ada55337](https://github.com/Proponent-8247/youtube-dl-gui/commit/34ada55337). Earlier: `e43273ef2d`, `b05364f625`, `b0acef2172`.
- [ ] **F082 — FFmpeg pair replacement is incomplete or non-transactional** (VERIFY). [Patch 4000449677](https://github.com/Proponent-8247/youtube-dl-gui/commit/4000449677). Earlier: `7e763fed14`.

### Startup, IPC and authentication

- [ ] **F013 — Archived-download input normalization and dispatch failures** (VERIFY). [Patch 83c82ff355](https://github.com/Proponent-8247/youtube-dl-gui/commit/83c82ff355). Earlier: `feea83e63e`, `7bfe5233f8`, `6ca1b311be`, `8464d83475`, `a6ee4cc5c3`.
- [ ] **F014 — Protocol registry keys missing or left undisposed** (VERIFY). [Patch ae4a40f000](https://github.com/Proponent-8247/youtube-dl-gui/commit/ae4a40f000). Earlier: `201b0d0dfb`.
- [ ] **F026 — Authenticated startup/IPC variants lose authentication or requested mode** (VERIFY). [Patch 62eec9f5e6](https://github.com/Proponent-8247/youtube-dl-gui/commit/62eec9f5e6). Earlier: `b01f46236e`, `e894773f06`.
- [ ] **F030 — Download IPC headers/operands are malformed or unbounded** (VERIFY). [Patch d40cb1e75a](https://github.com/Proponent-8247/youtube-dl-gui/commit/d40cb1e75a). Earlier: `b2bd74643b`.
  Tests: `N006.DownloadIpcAcceptsSupportedUtf16Packets`, `N006.DownloadIpcRejectsMalformedHeadersBeforeAllocation`. **Only partial family coverage; further verification is required.**
- [ ] **F046 — Dynamic input quoting and downloader option/operand boundary are unsafe** (VERIFY). [Patch f542bd2b76](https://github.com/Proponent-8247/youtube-dl-gui/commit/f542bd2b76). Earlier: `f235abb2e3`, `9500e85196`, `71129a8bf4`, `f293610632`, `b29f7f5e23`, `704d67214d`, `a2a2728fbc`.
  Tests: `D002.WindowsArgumentRoundTrip`, `D002.QuickSourceIsOperand`, `C2.ExtendedCustomSourceIsOperand`, `C2.SearchAndCollectionOperandsRemainSupported`. **Only partial family coverage; further verification is required.**
- [ ] **F054 — Custom/type/authentication options lost between UI, tray, batch and startup** (VERIFY). [Patch 514bbe42cf](https://github.com/Proponent-8247/youtube-dl-gui/commit/514bbe42cf). Earlier: `e5dab3a4f0`, `9540dcc2e3`, `fd88631039`, `ffd689e18f`, `2ad1422f85`, `c7762498a7`, `2902c44a1c`, `d986d7ffd2`, `7855085417`, `8d419e434d`, `0da896ae05`, `b6b85ecfa9`.
- [ ] **F066 — Protocol installation routing/elevation failure handled incorrectly** (VERIFY). [Patch 00f2c18a00](https://github.com/Proponent-8247/youtube-dl-gui/commit/00f2c18a00). Earlier: `a4e9b5fb56`.
- [ ] **F079 — Queue authentication copy/cancel/retry corrupts request credentials** (VERIFY). [Patch 7856277db1](https://github.com/Proponent-8247/youtube-dl-gui/commit/7856277db1). Earlier: `ed112d5b33`, `9bfd343365`.
  Tests: `Authentication.PasswordsAndCloneAreIndependent`. **Only partial family coverage; further verification is required.**
- [ ] **F085 — Extended CLI request type/no-sound option is discarded** (VERIFY). [Patch 02586eaf99](https://github.com/Proponent-8247/youtube-dl-gui/commit/02586eaf99). Earlier: `35500fd93c`.
- [ ] **F093 — Skip payloadless second-instance arguments** (VERIFY). [Patch 85eb8a9671](https://github.com/Proponent-8247/youtube-dl-gui/commit/85eb8a9671).
- [ ] **F101 — Dispose process-tree query resources** (VERIFY). [Patch 462894be5e](https://github.com/Proponent-8247/youtube-dl-gui/commit/462894be5e).
- [ ] **F112 — Clear plaintext authentication buffers** (VERIFY). [Patch ae80e9ce73](https://github.com/Proponent-8247/youtube-dl-gui/commit/ae80e9ce73).
  Tests: `Authentication.PasswordsAndCloneAreIndependent`. **Only partial family coverage; further verification is required.**
- [ ] **F115 — Tolerate malformed URI encoding in arguments** (VERIFY). [Patch eadf1d23c4](https://github.com/Proponent-8247/youtube-dl-gui/commit/eadf1d23c4).
- [x] **F119 — Keep first-run async work on a live UI loop** (TESTED). [Patch 30189587aa](https://github.com/Proponent-8247/youtube-dl-gui/commit/30189587aa).
  Tests: `C3.FirstRunPumpsUiDuringDelayedToolInstallation`.
- [x] **F141 — Preserve complete update packets and validate their layout** (TESTED). [Patch 836512228d](https://github.com/Proponent-8247/youtube-dl-gui/commit/836512228d).
  Tests: `N007.UpdatePacketPreservesLastFilenameByte`, `N007.UpdatePacketPreservesFinalUnicodeCharacter`, `N007.UpdatePacketRejectsTruncatedLayout`, `N007.UpdatePacketWriterRequiresFixedHashWidth`.

### Controls and native resources

- [ ] **F015 — Allow successful string version construction** (VERIFY). [Patch 18df8310c4](https://github.com/Proponent-8247/youtube-dl-gui/commit/18df8310c4).
- [ ] **F016 — Taskbar progress ownership and empty-queue access** (VERIFY). [Patch b6d9fc9d97](https://github.com/Proponent-8247/youtube-dl-gui/commit/b6d9fc9d97). Earlier: `e175ec7162`.
- [ ] **F017 — Time-picker setters ignore hours or fail to refresh displayed values** (VERIFY). [Patch a519c90f27](https://github.com/Proponent-8247/youtube-dl-gui/commit/a519c90f27). Earlier: `147ea6405d`.
- [ ] **F027 — Allow valid numeric clipboard paste** (VERIFY). [Patch d2021bcb8b](https://github.com/Proponent-8247/youtube-dl-gui/commit/d2021bcb8b).
- [ ] **F028 — Repeated native text-hint or progress drawing resources leak** (VERIFY). [Patch 77e4c2ad7f](https://github.com/Proponent-8247/youtube-dl-gui/commit/77e4c2ad7f). Earlier: `b4d90c9c1f`.
- [ ] **F032 — Preserve ExplorerTreeView extended styles** (VERIFY). [Patch f401b58347](https://github.com/Proponent-8247/youtube-dl-gui/commit/f401b58347).
- [ ] **F074 — Transfer controls localized before request context is initialized** (VERIFY). [Patch cc4a8b9c01](https://github.com/Proponent-8247/youtube-dl-gui/commit/cc4a8b9c01). Earlier: `49e2007938`.
- [ ] **F090 — Initialize IPC marshal buffers safely** (VERIFY). [Patch aacde97ec8](https://github.com/Proponent-8247/youtube-dl-gui/commit/aacde97ec8).
- [ ] **F096 — Process wrappers are not consistently disposed** (VERIFY). [Patch dd822bfce0](https://github.com/Proponent-8247/youtube-dl-gui/commit/dd822bfce0). Earlier: `13ed922d09`, `6fd8d51175`, `f56a1b5f44`, `f5718331d4`, `92a5c5723d`.
- [ ] **F105 — Allow extended download starts beyond 24 hours** (VERIFY). [Patch 4f11ab9d95](https://github.com/Proponent-8247/youtube-dl-gui/commit/4f11ab9d95).
- [ ] **F116 — Initialize container folder browser dialog** (VERIFY). [Patch e0b7b3d62e](https://github.com/Proponent-8247/youtube-dl-gui/commit/e0b7b3d62e).
- [ ] **F118 — DWM native interop sizes and DC/bitmap ownership are wrong** (VERIFY). [Patch a31600f208](https://github.com/Proponent-8247/youtube-dl-gui/commit/a31600f208). Earlier: `2fe16a9acd`, `205d36da6b`, `2b3db542a9`.
- [ ] **F129 — Form registry is populated too early or retained after disposal** (VERIFY). [Patch f8bc926dc5](https://github.com/Proponent-8247/youtube-dl-gui/commit/f8bc926dc5). Earlier: `0382cb3c77`, `1ee26e7230`.
  Tests: `N010.DisposedProcessingFormIsUnregistered`. **Only partial family coverage; further verification is required.**
- [x] **F135 — Serialize millisecond time offsets without changing their magnitude (H004)** (TESTED). [Patch 5632a47683](https://github.com/Proponent-8247/youtube-dl-gui/commit/5632a47683).
  Tests: `H004.FractionalSeconds`.
- [x] **F136 — Include separators within bounded joins (H005)** (TESTED). [Patch 01f2cc70f9](https://github.com/Proponent-8247/youtube-dl-gui/commit/01f2cc70f9).
  Tests: `H005.JoinSeparatorAndLimit`.
- [x] **F137 — Validate forward time ranges and implement value equality (H006)** (TESTED). [Patch dc2139faf1](https://github.com/Proponent-8247/youtube-dl-gui/commit/dc2139faf1).
  Tests: `H006.ForwardRangeAndEquality`.

### Queues and transfer lifetime

- [ ] **F022 — Batch parent/child close and stop races** (VERIFY). [Patch d2ed62b72f](https://github.com/Proponent-8247/youtube-dl-gui/commit/d2ed62b72f). Earlier: `2880de9a1f`, `3f37d9469d`, `9d66e249ae`, `19df4be615`.
- [ ] **F050 — Running-action and live batch queues mutate without synchronization** (VERIFY). [Patch b3279ac26d](https://github.com/Proponent-8247/youtube-dl-gui/commit/b3279ac26d). Earlier: `d73b8229ec`.
- [ ] **F055 — Downloader/converter UI callbacks race form disposal** (VERIFY). [Patch 2da3e01104](https://github.com/Proponent-8247/youtube-dl-gui/commit/2da3e01104). Earlier: `7edb467396`, `b138da82cc`, `0a58e5d541`.
  Tests: `Stress.quickCloseDuringConcurrentOutput`, `Stress.converterCloseDuringConcurrentOutput`. **Only partial family coverage; further verification is required.**
- [ ] **F067 — Cancellation, postprocessing and retry can lose worker ownership** (VERIFY). [Patch e8075e2920](https://github.com/Proponent-8247/youtube-dl-gui/commit/e8075e2920). Earlier: `c5b248eeea`, `8ea8dcc886`, `ec83d8d972`, `ca38011cca`, `a040bc2b7d`, `e5b3d1f5df`, `13eefec545`, `4f68841899`.
  Tests: `D005.ProgressCannotEraseCancellation`, `D005.ProgressCannotEraseCloseCancellation`, `D001.RetryCannotResetLiveWorker`, `D001.QueueRemovalBlockedDuringPostProcessing`, `D006.QuickCloseCompletesAfterCleanup`, `D006.ConverterCloseCompletesAfterCleanup`, `D006.ExtendedCloseCompletesAfterCleanup`. **Only partial family coverage; further verification is required.**
- [ ] **F071 — Main-form batch worker uses mutable UI state or loses format/timestamp** (VERIFY). [Patch 5f1b79b0cb](https://github.com/Proponent-8247/youtube-dl-gui/commit/5f1b79b0cb). Earlier: `73800a4f25`, `6c9f0b7448`.
- [ ] **F073 — Quick/converter cancellation races process startup** (VERIFY). [Patch 53f1e22fae](https://github.com/Proponent-8247/youtube-dl-gui/commit/53f1e22fae). Earlier: `ec825e5284`, `c58f7c23bd`.
- [ ] **F077 — Clipboard scanner subscription and displayed state are incorrect** (VERIFY). [Patch cb6d23342a](https://github.com/Proponent-8247/youtube-dl-gui/commit/cb6d23342a). Earlier: `3885e7d391`, `387a3b0068`.
- [ ] **F078 — Generate arguments for extended batch downloads** (VERIFY). [Patch c49f33b42b](https://github.com/Proponent-8247/youtube-dl-gui/commit/c49f33b42b).
- [ ] **F089 — Ignore blank batch download rows** (VERIFY). [Patch d5ce5b3685](https://github.com/Proponent-8247/youtube-dl-gui/commit/d5ce5b3685).
- [ ] **F094 — Metadata/transfer helpers leak or survive cancellation/failure** (VERIFY). [Patch bd75617fd7](https://github.com/Proponent-8247/youtube-dl-gui/commit/bd75617fd7). Earlier: `d4f616daf7`, `1da17fec91`, `636ac6deeb`, `09ce211ca8`, `9a7d94016e`, `7a591448ed`.
  Tests: `D007.YoutubeProbeAbortStopsRoot`, `D007.FfprobeAbortStopsRoot`, `D007.OwnedRunnerClosesStdin`, `D007.OwnedRunnerPreservesExitAndError`, `D007.OwnedRunnerTimeoutStopsRoot`, `D007.OwnedRunnerCancellationStopsRoot`, `D007.PreCancelledProbeNeverStarts`, `D006.OwnedRunnerStopsDescendants`, `R006.MetadataOutputIsBounded`, `D006.StartFailurePreservesOriginalError`. **Only partial family coverage; further verification is required.**
- [ ] **F098 — Dispose batch import reader** (VERIFY). [Patch 59d730bae6](https://github.com/Proponent-8247/youtube-dl-gui/commit/59d730bae6).
- [ ] **F099 — Extended launch failures leave controls/state inconsistent** (VERIFY). [Patch d769a5acae](https://github.com/Proponent-8247/youtube-dl-gui/commit/d769a5acae). Earlier: `5f5b6673ec`.
- [ ] **F108 — Terminal child states prevent exit or batch abort** (VERIFY). [Patch 2cb09f72a8](https://github.com/Proponent-8247/youtube-dl-gui/commit/2cb09f72a8). Earlier: `e7d9d1c9fc`, `7ef0dc3b99`, `a3285d295e`.
- [ ] **F117 — Clipboard contention becomes an application error** (VERIFY). [Patch a7ea9bf9e4](https://github.com/Proponent-8247/youtube-dl-gui/commit/a7ea9bf9e4). Earlier: `9b6d1860a3`, `64bd995888`.
- [ ] **F121 — Run batch downloader UI worker as STA** (VERIFY). [Patch dbaa20e4f8](https://github.com/Proponent-8247/youtube-dl-gui/commit/dbaa20e4f8).

### Updater

- [x] **F024 — Updater checksum retry reuses a locked or unverified file** (TESTED). [Patch d1c7c91b71](https://github.com/Proponent-8247/youtube-dl-gui/commit/d1c7c91b71). Earlier: `b9008d30b7`.
  Tests: `C4.ChecksumRetryReleasesAndReverifiesFile`.
- [ ] **F025 — Updater metadata failure or null deserialization is not contained** (VERIFY). [Patch aec030e225](https://github.com/Proponent-8247/youtube-dl-gui/commit/aec030e225). Earlier: `e74a52cfe7`, `c229568e54`.
- [ ] **F031 — Validate updater copy-data messages** (VERIFY). [Patch 807f33fd71](https://github.com/Proponent-8247/youtube-dl-gui/commit/807f33fd71).
- [ ] **F045 — Updater rollback assumes backups exist or lets rollback failures escape** (VERIFY). [Patch 00d8916163](https://github.com/Proponent-8247/youtube-dl-gui/commit/00d8916163). Earlier: `4b772ea618`.
- [ ] **F049 — Markdown-wrapped release hashes parsed incorrectly** (VERIFY). [Patch 0edbf5ad75](https://github.com/Proponent-8247/youtube-dl-gui/commit/0edbf5ad75). Earlier: `65b9d6409a`.
- [ ] **F102 — Application update proceeds without a valid expected hash** (VERIFY). [Patch 1e6f937c73](https://github.com/Proponent-8247/youtube-dl-gui/commit/1e6f937c73). Earlier: `5704db60e1`.
- [ ] **F111 — Updater UI callback lifetime or window-handle width is wrong** (VERIFY). [Patch 92d50aab37](https://github.com/Proponent-8247/youtube-dl-gui/commit/92d50aab37). Earlier: `f29a7ec924`.
- [ ] **F124 — Update checks lose gate ownership or stable/beta channel identity** (VERIFY). [Patch f6e69ac66d](https://github.com/Proponent-8247/youtube-dl-gui/commit/f6e69ac66d). Earlier: `4b068c212b`, `37e754830a`.
  Tests: `N011.StableReleaseRequestKeepsItsChannel`, `N011.BetaReleaseRequestKeepsItsChannel`. **Only partial family coverage; further verification is required.**
- [ ] **F125 — Cached provider release metadata consumed under another provider** (VERIFY). [Patch 43e50edf6b](https://github.com/Proponent-8247/youtube-dl-gui/commit/43e50edf6b). Earlier: `e998aceaf5`.
  Tests: `N012.StaleProviderMetadataCannotStartDownload`. **Only partial family coverage; further verification is required.**
- [ ] **F126 — Allow provider internal updater without release metadata** (VERIFY). [Patch 00656f3651](https://github.com/Proponent-8247/youtube-dl-gui/commit/00656f3651).
- [x] **F142 — Receive synchronous updater acknowledgement before closing handshake** (TESTED). [Patch e5244df966](https://github.com/Proponent-8247/youtube-dl-gui/commit/e5244df966).
  Tests: `N008.UpdaterSynchronousAcknowledgementClosesMainForm`, `N008.UnsolicitedUpdaterAcknowledgementIsIgnored`.

### Thumbnails

- [x] **F029 — Thumbnail image, conversion and cancellation lifetimes are unsafe** (TESTED). [Patch 89ac9e6a77](https://github.com/Proponent-8247/youtube-dl-gui/commit/89ac9e6a77). Earlier: `9c1de9fc2b`, `d9b98d0187`.
  Tests: `N004.ThumbnailDownloadHonorsCancellation`, `N004.ThumbnailCloneSurvivesResponseDisposal`, `N004.ThumbnailConversionCancellationCleansProcessAndFiles`.
- [ ] **F087 — Thumbnail retrieval blocks UI or fails during late UI dispatch** (VERIFY). [Patch ebc0fd5f57](https://github.com/Proponent-8247/youtube-dl-gui/commit/ebc0fd5f57). Earlier: `999d40a3ad`, `bead0e5a37`.
- [ ] **F097 — Cached thumbnail resources are not consistently owned** (VERIFY). [Patch 173555905d](https://github.com/Proponent-8247/youtube-dl-gui/commit/173555905d).

### Localization

- [ ] **F033 — Language enumeration/download recursively reenters or blocks UI** (VERIFY). [Patch 6cfe87d674](https://github.com/Proponent-8247/youtube-dl-gui/commit/6cfe87d674). Earlier: `df1f19e5bb`.
- [ ] **F035 — Internal English fallback disappears with missing language files/keys** (VERIFY). [Patch 4668f20252](https://github.com/Proponent-8247/youtube-dl-gui/commit/4668f20252). Earlier: `59f5589e5d`, `2caf230e5a`.
- [ ] **F064 — Load extended custom arguments hint text** (VERIFY). [Patch de2b8066d8](https://github.com/Proponent-8247/youtube-dl-gui/commit/de2b8066d8).
- [ ] **F083 — Updater language file/key/retry/newline handling is incorrect** (VERIFY). [Patch 97f2094fd5](https://github.com/Proponent-8247/youtube-dl-gui/commit/97f2094fd5). Earlier: `aa325c3e29`, `07d40cb0b5`, `f07d341827`.
- [ ] **F110 — Language/About asynchronous UI lifecycle is not guarded** (VERIFY). [Patch 80de85893e](https://github.com/Proponent-8247/youtube-dl-gui/commit/80de85893e). Earlier: `5d1b2f88b4`, `b11824033f`.
- [ ] **F130 — Marshal localization changes to form threads** (VERIFY). [Patch 97c0cd1321](https://github.com/Proponent-8247/youtube-dl-gui/commit/97c0cd1321).
- [x] **F138 — Preserve URLs and quoted slashes in translation values (D034)** (TESTED). [Patch a0ddad33cd](https://github.com/Proponent-8247/youtube-dl-gui/commit/a0ddad33cd).
  Tests: `D034.LanguageUrlAndEquals`, `D034.LanguageInlineComment`, `D034.LanguageQuotedSlashes`.

### Logging

- [x] **F051 — Exception logging uses wrong directory or loops after a successful write** (TESTED). [Patch 2c773bf519](https://github.com/Proponent-8247/youtube-dl-gui/commit/2c773bf519). Earlier: `fdeace49a4`.
  Tests: `N014.ApplicationLogWriteReturnsAfterSuccess`, `N014.UpdaterLogWriteReturnsAfterSuccess`.
- [ ] **F052 — Log viewer threading, disposal and localization lifecycle are wrong** (VERIFY). [Patch 91a4c7a47a](https://github.com/Proponent-8247/youtube-dl-gui/commit/91a4c7a47a). Earlier: `13bf500b6d`, `bc29099977`.
- [ ] **F053 — Diagnostics fail while gathering WMI data or reporting language errors** (VERIFY). [Patch 9b3a39f8e6](https://github.com/Proponent-8247/youtube-dl-gui/commit/9b3a39f8e6). Earlier: `70dc46324c`.

### Build and delivery

- [x] **F056 — Embedded updater identity and Release packaging are not reproducible** (TESTED). [Patch a7c43c6e06](https://github.com/Proponent-8247/youtube-dl-gui/commit/a7c43c6e06). Earlier: `d32414d9ca`.
  Tests: `N005.BuildDateGeneratedWithoutExternalHelper`, `Packaging.EmbeddedUpdaterHashMatchesActualResource`, `N005.ReleaseArchiveAndChecksumsMatchBuiltFiles`.
- [ ] **F107 — Restore current application version** (VERIFY). [Patch c5dc4b3fa3](https://github.com/Proponent-8247/youtube-dl-gui/commit/c5dc4b3fa3).

## 4. Withdrawn changes and supersession

These four source-changing records are retained for accountability, not counted as additional findings. No published history was rewritten.

- **W002**: [22d97c63af](https://github.com/Proponent-8247/youtube-dl-gui/commit/22d97c63afe327418b4a99a9de2f9e4e95fa9980) — fix: guard quick downloader progress line parsing Reverted by 003; focused replacement is 006.
- **W003**: [a790750326](https://github.com/Proponent-8247/youtube-dl-gui/commit/a7907503267ea7c10ffbdf185076818847ac4d04) — revert: discard noisy quick downloader patch Reverts 002; not an independent defect.
- **W248**: [2da1f6587b](https://github.com/Proponent-8247/youtube-dl-gui/commit/2da1f6587b10788842e578705b1f7892b0d28af7) — fix: dispose completed batch converter dialogs Reverted by 249 as redundant disposal.
- **W249**: [5bacabb2f3](https://github.com/Proponent-8247/youtube-dl-gui/commit/5bacabb2f30d877ad9c04f60d03f66a6f4e93afe) — revert: redundant batch converter disposal Reverts 248; not an independent defect.

## 5. Legacy finding aliases and corrected evidence

Legacy identifiers are preserved only where their meaning is supported. This catalog uses F/O/V/P IDs to avoid inventing the missing original D/H/R sequence.

| Legacy ID | Current checklist | Scope |
| --- | --- | --- |
| C1 | F080 | resolver/transfer isolation |
| C2 / D002 | F046 | quoting and source operand boundary |
| C3 | F119 | first-run installation UI liveness |
| C4 | F024 | checksum Retry |
| C5 | F081 | initial no-audio binding |
| D001 / D005 | F067 | active worker/cancellation ownership |
| D006 / D007 / R006 | F094 | owned processes and metadata budgets |
| R006 / D006 / D007 | F068 | live output and pipe draining |
| D017 | F036 | explicit playlist selection |
| D032 | F131 | quoted custom whitespace |
| D034 | F138 | language parser |
| H001 | F134 | UNC source root |
| H004 | F135 | fractional seconds |
| H005 | F136 | bounded joins |
| H006 | F137 | time range/equality |
| N001 | F139 | latest repair batch |
| N002 | F140 | latest repair batch |
| N003 | F127 | latest repair batch |
| N004 | F029 | latest repair batch |
| N005 | F056 | latest repair batch |
| N006 | F030 | latest repair batch |
| N007 | F141 | latest repair batch |
| N008 | F142 | latest repair batch |
| N009 | F059 | latest repair batch |
| N010 | F129 | latest repair batch |
| N011 | F124 | latest repair batch |
| N012 | F125 | latest repair batch |
| N013 | F143 | latest repair batch |
| N014 | F051 | latest repair batch |

**Evidence correction:** the previous closeout cited eight SHAs that are not the corresponding published implementation commits in this ancestry. The canonical implementation mappings are below; the prior build/test artifacts remain historical evidence, not proof for a different SHA.

| ID | Previous attribution | Published implementation |
| --- | --- | --- |
| N001 | `53e508a4b618404609fdbc71b970d2c480aa3de8` | [db466c3e9bec2a3d19edd04790fb031de1cc2b66](https://github.com/Proponent-8247/youtube-dl-gui/commit/db466c3e9bec2a3d19edd04790fb031de1cc2b66) |
| N002 | `0aefa5a6c41ebd6aedafb570d36eec09cb0b663e` | [259c0c7c304d30c61e3bb50bb5cd3a0dc0ed3ee8](https://github.com/Proponent-8247/youtube-dl-gui/commit/259c0c7c304d30c61e3bb50bb5cd3a0dc0ed3ee8) |
| N003 | `efe6958339ef9b5630b41bdfe08c1fc9bbb18411` | [39a6a1db2c7db07ac3b5f0d1a5833bbeae9d1f28](https://github.com/Proponent-8247/youtube-dl-gui/commit/39a6a1db2c7db07ac3b5f0d1a5833bbeae9d1f28) |
| N004 | `1679fe1c4c982a01a38e46bfe316773b7a2a7577` | [89ac9e6a77d04a1f5d7ebed4848329c6055c353f](https://github.com/Proponent-8247/youtube-dl-gui/commit/89ac9e6a77d04a1f5d7ebed4848329c6055c353f) |
| N005 | `a2ab69c59e624bc86a64d1748263068177f2b629` | [a7c43c6e06b94cf99d4b11d004e10bb4f9742e8b](https://github.com/Proponent-8247/youtube-dl-gui/commit/a7c43c6e06b94cf99d4b11d004e10bb4f9742e8b) |
| N006 | `18cfeb968429a817622c4818a3a5f92c49fcc45c` | [d40cb1e75a799183bd7a348236b5b3249a6d4853](https://github.com/Proponent-8247/youtube-dl-gui/commit/d40cb1e75a799183bd7a348236b5b3249a6d4853) |
| N007 | `69c0b1c2c5618c5fbe7da76bdbbac6a26b4c574` | [836512228d440fba01e20b461f882e52eba4b5f0](https://github.com/Proponent-8247/youtube-dl-gui/commit/836512228d440fba01e20b461f882e52eba4b5f0) |
| N008 | `7c14762c814b044a72930d29942ba7e6e416215a` | [e5244df96653706dd77ce5febf43fc5d4c98732a](https://github.com/Proponent-8247/youtube-dl-gui/commit/e5244df96653706dd77ce5febf43fc5d4c98732a) |

The previous 50-commit file is a **partial recovery index**, not a complete finding register. This checklist covers every production C#/project/shared-project change record in the full exported audit ancestry, including excluded source copies and the pre-`b734c059` history and withdrawn records. Tests and administrative request/CI commits remain in the full 797-entry ancestry rather than being counted as production defects.

## 6. Completion gate

- [ ] Resolve or explicitly disposition all open O/V/P items; document accepted risks rather than marking them fixed.
- [ ] Supply finding-specific acceptance evidence for every `HISTORICAL-FIX-VERIFY` row, including excluded/Debug-only disposition where applicable.
- [ ] Reconcile any recovered original audit narrative against this catalog without renumbering existing IDs or dropping duplicate/withdrawn evidence.
- [ ] Re-run supported builds, regression suite and defined end-to-end integration/fault/soak tests against the final implementation revision.
- [ ] Record final revision, environment, exact results, remaining risk owner/decision and review coverage. Do not use a green documentation-only build as proof that O001-O008 were fixed.

Path shorthand in historical rows: `app/` = `youtube-dl-gui/`; `updater/` = `youtube-dl-gui-updater/`. `Controls/` is shared. No runtime code changes, merge to master, release publication or download-history implementation are part of this checklist commit.
