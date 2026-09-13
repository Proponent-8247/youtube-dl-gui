# Current whole-repository review: findings checklist

Review v2.0. Source snapshot: **e5858408008e17a34241779f18e7dfa1fed6a057**, branch **audit-fixes**. Review performed September 8, 2026 America/Boise (September 9 UTC). Master comparison: `c6ce1e6421c6bf785f30f41496b745489bcc2bab`.

This is a fresh source/control-flow, resource, add-on, build and test review, not just a list of old commits. The application/updater source was not changed during this review. Characterization probes and their workflow were added separately. **A reproduced defect remains unchecked until corrected and its acceptance tests pass.** The green characterization workflow means that expected defects were observed, not that they were fixed.

## Catalog relationship and evidence levels

This file extends, and does not replace or renumber, [FINDINGS-CHECKLIST.md](FINDINGS-CHECKLIST.md). The prior 143 F-items, 16 V/P-items and withdrawn records remain there. O001-O008 below are reassessments of the same prior items, not eight additional findings. **O009-O036 are 28 additional current observations/findings; V011 is one additional verification gap.** The combined catalog therefore has 196 identified tracking items: 143 historical F-items, 36 current O-items, and 17 V/P-items. Related items are cross-referenced; these totals are not counts of independent vulnerabilities.

- **REPRODUCED**: the compiled application or actual repository script exhibited the stated behavior in a controlled probe. Reflection-level tests prove only the boundary called, not every UI route or exploit chain.
- **SOURCE-CONFIRMED**: the included source and caller contract establish the defect, but no dedicated runtime reproduction is claimed.
- **VALIDATION-LEAD / DESIGN-RISK**: the suspicious path or trust/lifetime gap exists, but the stated scenario or policy still needs validation. These are not established exploits.
- Priority is operational triage (High/Medium/Low), not a CVSS assessment. Debug-only and unused public-API paths are identified explicitly.

The source references below are pinned to the reviewed snapshot. `app/` means `youtube-dl-gui/`; `updater/` means `youtube-dl-gui-updater/`; `Controls/` is shared. Exact files and line ranges are listed even where the link points to the whole file.

### Executed evidence

[Windows characterization run 34315957693](https://github.com/Proponent-8247/youtube-dl-gui/actions/runs/34315957693), revision `b4536ae7b322a0c524dd24a53a2ec1560deaf83d`, established **12/12 observations across 11 O-items** on unchanged production code. The duration item has two probes. Artifact ID `10090076552`, SHA-256 `38b14beb507150e3eb36ffbf3c35cae93e5560a0d80c25f873a010835c48d9da`. See [retained summary](evidence/current-observations.json) and [probe source](repro/ReviewObservations.cs).

| Probe | Observation | Item |
| --- | --- | --- |
| time-span | A 25-hour TimeSpan round trip returns one hour. | O001 |
| time-width | SetValue(100,12,34,0) retains a two-character hour-field boundary. | O002 |
| time-malformed | UpdateControl with text `1` throws IndexOutOfRangeException. | O003 |
| batch-quotes | One accepted quote-only row throws an unhandled ArgumentNullException on the real batch worker. | O010 |
| configured-schema | First media binding clears the configured schema text. | O011 |
| format-boundary | A format identifier containing spaces becomes multiple downloader arguments. | O012 |
| duration-rounding | 59.9 seconds displays as `0:60`. | O015 |
| duration-budget | A duration of 10^12 seconds does not finish formatting within eight seconds. | O015 |
| ini-retry | Failed read-only-file save changes memory; same-value retry does not persist it. | O016 |
| error-logging-preference | Errors.logErrors=true leaves Log.AllowWritingToFile=false. | O017 |
| numeric-after-arrow | A valid digit following a navigation key is suppressed. | O019 |
| version-format | `1-2-3` is accepted and becomes `1.0.0-2`. | O020 |

Separately, [the repository userscript probe](repro/userscript-characterization.js) executed under Node against a minimal old-Reddit-shaped DOM. It observed O034 with a single-post control and a two-post listing. This is not a browser/userscript-manager installation test.

The first characterization attempt failed to compile because of its source-path argument; that harness error was corrected. A subsequent batch probe was blocked by the application's earlier exception-dialog handler; the observer was moved before initialization, and the final probe captured the unhandled exception. Neither unsuccessful attempt is represented as remediation or successful reproduction.

## Reassessed existing open items

- [ ] **O001 — TimePicker.TimeSpan loses long durations and does not synchronize display**
  - **Medium; REPRODUCED; public control API, not a found application caller.** The setter takes the Hours component instead of total hours; the getter substitutes zero above 24, and the setter does not refresh the display. The 25-hour round-trip defect is observed; other boundary/UI paths remain to test.
  - **Source:** [app/Controls/TimePicker.cs][time], 83-94. Related F017/F105.
  - **Acceptance:** Round-trip 0, 24, 25 and 100 hours via TimeSpan and Value; assert display and field-selection agreement and document negative/overflow/DateBasedTime behavior.

- [ ] **O002 — TimePicker.SetValue leaves the hour-field width stale**
  - **Low; REPRODUCED; public control API with no application caller found.** Unlike Value assignment, SetValue does not recompute HourToMinuteSeparator. A 100-hour value retains width two and subsequent field navigation targets the wrong characters.
  - **Source:** [app/Controls/TimePicker.cs][time], 148-156.
  - **Acceptance:** Compare SetValue and Value assignment for two-, three- and wider-hour values, then select and edit every field.

- [ ] **O003 — Time editor assumes separators exist and has unchecked hour increment**
  - **Medium; parser failure REPRODUCED, full user-input and overflow sequences remain VALIDATION-LEADS.** Private UpdateControl indexes split components without checking their count. The probe supplies malformed display text and observes IndexOutOfRangeException. That does not prove every paste/key route accepts it. Hours++ at int.MaxValue is a separate boundary within the same editor.
  - **Source:** [app/Controls/TimePicker.cs][time], 155-179 and 461-466.
  - **Acceptance:** Drive actual paste/cut, missing-colon/dot, Shift+digit and maximum-hour increment events. Invalid input must not escape the event handler or corrupt another field.

- [ ] **O004 — Forced thumbnail refresh abandons the old cached Image**
  - **Low; SOURCE-CONFIRMED; dormant forced-refresh API path.** DownloadThumbnail(true) assigns a new image without disposing the replaced image. Current form calls use false, so ordinary thumbnail download must not be described as reproducing this leak. A PictureBox may still hold the old reference.
  - **Source:** [app/Classes/DataClasses/ExtendedMediaDetails.cs][extended-model], 194-201. Related F097/N004.
  - **Acceptance:** Define one owner, detach any display reference, dispose each replaced image exactly once, and preserve the old image when refresh fails.

- [ ] **O005 — Updater's parent-exit wait is not cancellable and sits outside recovery**
  - **Medium; SOURCE-CONFIRMED cancellation/recovery gap; full hang scenario not reproduced.** WaitForApplication uses synchronous SendMessage and a tokenless WaitForExit. RunUpdate awaits it before its catch/rollback region. Waiting for active user work is intentional; automatically killing the main application is not an acceptable inferred fix.
  - **Source:** [updater/Forms/frmUpdater.cs][updater-form], 100-129 and 216-230.
  - **Acceptance:** Cancel/close the updater while the parent remains alive; exercise stale/hung message targets and exit races. Cancel only the wait, preserve active downloads, and contain failure.

- [ ] **O006 — Full GIF conversion has no complete owned cancellation/failure lifecycle**
  - **Medium; VALIDATION-LEAD / lifecycle policy.** The fixed ImageMagick version probe does not cover actual conversion. The real FFmpeg WaitForExit remains unbounded, exceptions are rethrown from an async-void handler, and workspace cleanup can itself fail. Some external-console lifetime is intentional.
  - **Source:** [app/Forms/frmMiscTools.cs][misc], 135-210. Related F059/N009.
  - **Acceptance:** Exercise hanging/failing FFmpeg and ImageMagick, owner closure, and denied cleanup. Decide whether launched external work survives owner closure before altering that behavior.

- [ ] **O007 — Main-side update IPC is not bound to the launched updater session**
  - **Medium; local IPC VALIDATION-LEAD, not a remote-execution or privilege-escalation claim.** The receiver responds to m.WParam and temporarily accepts acknowledgement without checking the expected updater window/process. Existing opposite-direction checks do not establish this side's identity.
  - **Source:** [app/Controls/MessageHandler.cs][message-handler], 81-119. Related N008 and V011 below.
  - **Acceptance:** Exercise forged/unsolicited request plus reentrant acknowledgement, both with and without release metadata. Accept only the intended session without breaking legitimate synchronous delivery or active transfers.

- [ ] **O008 — Predictable partial/backup paths have no per-operation ownership**
  - **Medium; DESIGN-RISK; collision/data-loss scenario not yet reproduced.** Update uses `update.part`; generic downloads use `Output.tmp`/`Output.bck`. Existing deletion/move code can encounter another operation's or a pre-existing sidecar. This is distinct from the repaired rollback case.
  - **Source:** [updater/Forms/frmUpdater.cs][updater-form], 118-120; [app/Forms/frmGenericDownloadProgress.cs][generic-download], 27-28 and 64-81.
  - **Acceptance:** Use concurrent same-destination operations and sentinel sidecars in a disposable directory. Define locking/ownership and replacement rules; preserve unrelated data and a recoverable prior output.

## Additional findings from this review

- [ ] **O009 — Ordinary warm-start video requests ignore the saved no-audio preference**
  - **Medium; SOURCE-CONFIRMED; shipping quick-download dispatch.** Cold-start dispatch sets SkipAudioForVideos from Downloads.VideoDownloadSound. WM_COPYDATA dispatch instead sets it solely from the explicit no-sound argument kind. An ordinary `v` request therefore behaves differently when the GUI is already running.
  - **Source:** [app/Program.cs][program], 380-388 versus 471-478. Related F026/F054/F085.
  - **Acceptance:** Compare cold and already-running dispatch for ordinary/authenticated video with sound preference on/off, plus explicit no-sound requests. Preserve the deliberate explicit override. The apparent custom-argument issue was rejected: line 525 already restores LastUsedYtdlArgument.

- [ ] **O010 — Accepted quote-only batch input escapes the worker as an unhandled exception**
  - **High availability priority; REPRODUCED; real shipping batch worker.** AddItemToList accepts `""` as nonblank. MediaData later strips the quotes and rejects the now-empty source. Construction occurs outside a worker-wide catch/finally, so the actual batch thread raises an unhandled ArgumentNullException. The observer exits after recording it rather than letting a diagnostic dialog hide the result.
  - **Source:** [app/Forms/frmBatchDownloader.cs][batch-download], 332-430 and 474 onward; [app/Classes/DataClasses/Bases/MediaData.cs][media-data]. Related F089.
  - **Acceptance:** Reject/normalize unusable rows before enqueue; contain faults per item and restore state in finally. Run valid-invalid-valid batches. Also fault-inject the main form's file-batch worker (frmMain.cs:903-1025) and resolver post-processing (frmExtendedDownloader.cs:1920-1941), whose outer exception/cleanup boundaries remain narrower than the whole operation.

- [ ] **O011 — Extended media binding clears a non-list filename schema**
  - **Medium; REPRODUCED; shipping extended downloader.** LoadMediaOptions assigns cbSchema.Text, then assigns SelectedIndex. A model index of -1 clears the text. Initial model defaults also do not carry the configured schema reliably into first binding. The observed configured `AUDIT_%(id)s.%(ext)s` becomes empty, so the generated output can fall back instead of using the user's request.
  - **Source:** [app/Forms/frmExtendedDownloader.cs][extended-form], 83-93 and 1260-1290; [ExtendedMediaDetails.cs][extended-model], 127-131. Related F081/C5, but no-audio binding tests do not cover this field.
  - **Acceptance:** Preserve arbitrary typed schemas, configured defaults, selected list schemas and per-item schemas through first binding, selection changes, retry and generated arguments. Do not impose a new mandatory filename policy.

- [ ] **O012 — Provider format identifiers cross the command-argument boundary unescaped**
  - **Medium security/correctness priority; REPRODUCED at argument generation.** Selected format IDs from metadata are concatenated directly after `-f`. The probe ID `fixture --print AUDIT_BOUNDARY` produces separate tokens. URL operand separation does not protect this earlier option value. No shell, malicious downloader execution, remote exploit or elevated privilege was exercised; exposure depends on metadata/provider trust.
  - **Source:** [app/Classes/DataClasses/ExtendedMediaDetails.cs][extended-model], 559-574, 583-603. Related F046, whose URL/Windows-quoting tests do not exercise metadata-derived selectors.
  - **Acceptance:** Build the complete selector as one escaped argument; record actual child argv for spaces, quotes, Unicode and multiple selected streams. Do not quote away intended selector syntax or restrict explicit custom arguments silently.

- [ ] **O013 — Extended format fallback can violate no-audio or audio-only intent**
  - **Medium; SOURCE-CONFIRMED against provider selector contract; real-media test pending.** The no-extra-audio video branch falls back to `/best`; the audio branch also does so without guaranteed extraction when AudioEncoderIndex=0. The provider's `best` can contain both video and audio. Disabling the Sound checkbox therefore does not ensure a soundless fallback; an audio-only request can similarly fall back to muxed media.
  - **Source:** [ExtendedMediaDetails.cs][extended-model], 557-599; [yt-dlp selector documentation][yt-dlp-doc]. Related F072/F044; distinguish this extended path from the corrected quick downloader.
  - **Acceptance:** Make the selected format unavailable and inspect resulting streams with ffprobe. Cover video-only, audio-only, muxed choices and separate-audio mode; define behavior explicitly rather than silently adding a forced codec/container policy.

- [ ] **O014 — Extended time selection emits a yt-dlp-only option for other supported providers**
  - **Medium; SOURCE-CONFIRMED compatibility mismatch.** Fragment options are provider-gated, but StartTime/EndTime unconditionally add `--download-sections`. The inspected yt-dlp contract supports it; the inspected youtube-dl option parser does not. Selecting a time range with that provider can fail command parsing.
  - **Source:** [ExtendedMediaDetails.cs][extended-model], 729-741; [yt-dlp options][yt-dlp-doc] and [youtube-dl parser][youtube-dl-options].
  - **Acceptance:** Exercise each offered provider with start-only/end-only/both ranges. Use a supported equivalent or explicit capability feedback; do not silently discard requested times. Record tested provider versions.

- [ ] **O015 — Duration formatting is proportional to untrusted duration and can display invalid seconds**
  - **High availability priority for the unbounded loop; REPRODUCED.** Duration repeatedly subtracts 60 rather than using constant-time division. A metadata value of 10^12 seconds exceeded the eight-second probe deadline. The getter is used during UI binding. Separately, fractional formatting rounds 59.9 to `0:60` without carrying the minute.
  - **Source:** [app/Classes/DataClasses/YoutubeDlData.cs][youtube-data], 204-237; [frmExtendedDownloader.cs][extended-form], 1440 and 1928.
  - **Acceptance:** Bound/validate unreasonable inputs and format in constant time. Test null, zero, fractions near 60/3600, long streams and large decimals; assert valid display and responsive UI. The probe proves the getter's timeout, not a live hostile-service exploit.

- [ ] **O016 — Failed INI persistence changes the cache and makes same-value retry a no-op**
  - **Medium data-integrity priority; REPRODUCED.** Setters assign the backing field before IniProvider.Write. When the write throws, memory holds the new value; a retry with that same value fails the change guard and never retries disk persistence. The read-only-INI probe establishes this with General.UseStaticYtdl. The pattern appears across configuration classes.
  - **Source:** [app/Config/General.cs][general], 28-37; [app/Config/Interfacing/IniProvider.cs][ini], 21-25; analogous Downloads/Converts/Saved setters. Related F128/V006.
  - **Acceptance:** Fault writes, restore access, retry the same value and restart to verify persistence. Commit cache only after success or roll it back safely; preserve error reporting and storage-mode behavior.

- [ ] **O017 — The log-errors preference never enables the actual file logger**
  - **Medium supportability priority; REPRODUCED at configuration-to-logger boundary.** The UI stores Errors.logErrors, but the runtime logging gate is Log.AllowWritingToFile, which is not wired to that preference. Setting the preference true leaves the gate false. N014 explicitly enables the gate in its helper test and thus does not verify the user feature.
  - **Source:** [app/Config/Errors.cs][errors], [app/Forms/frmSettings.cs][settings], 501/624; [app/Logging/Log.cs][log]. Related F051/N014.
  - **Acceptance:** Toggle the actual setting, restart, provoke a controlled caught error and inspect the configured log file; verify off suppresses writing. Preserve secret redaction. The separate detailedErrors option is hidden/disabled and is not claimed as a shipped active feature here.

- [ ] **O018 — Three shipped translations make the About dialog's format call fail**
  - **Medium; SOURCE-CONFIRMED by resource/caller arity inspection.** Dutch, German and Spanish lbAboutBody contain `{2}`, but frmAbout supplies only two format arguments. This is not merely an untranslated label: loading one of those languages and creating About reaches an invalid format call.
  - **Source:** [Dutch.ini][dutch], line 15; [German.ini][german], line 76; [Spanish.ini][spanish], line 16; [app/Forms/frmAbout.cs][about], 10-27. All 11 language files were checked for placeholder-index drift; these three were found.
  - **Acceptance:** Construct/localize About under every shipped language and assert no FormatException and correct attribution/date. Validate resource placeholders in CI, including fallback and language reload.

- [ ] **O019 — Numeric text boxes suppress a valid digit after navigation**
  - **Low; REPRODUCED through the actual control event handlers.** A navigation key enters the default OnKeyDown branch and sets fCheckChar, but produces no KeyPress to clear it. A subsequent valid digit does not reset the state and is suppressed by OnKeyPress. The main playlist-number inputs use this NumericOnly control.
  - **Source:** [Controls/ExtendedTextBox.cs][textbox], 509-529; app/Forms/frmMain.Designer.cs, numeric playlist fields. Related F027/F041.
  - **Acceptance:** Drive Left/Right/Home/End followed by digits, numpad input, backspace and paste through real UI events. Fix state handling without admitting invalid playlist numbers.

- [ ] **O020 — Version parsing accepts malformed multi-hyphen input and discards a component**
  - **Low; REPRODUCED.** TryParse accepts `1-2-3` and returns `1.0.0-2`; the tail is lost. The validation regex uses unescaped separators and the hyphen split does not enforce one suffix. This concerns parsing, not an established defect in all comparison operators.
  - **Source:** [Controls/Version.cs][version], 179-187 and TryParse. Related F015.
  - **Acceptance:** Specify supported release-tag syntax; reject malformed extra separators without throwing, and round-trip valid stable/prerelease forms. Keep existing supported version semantics unless deliberately changed.

- [ ] **O021 — Exposed conversion preferences are stored but not consumed**
  - **Medium; SOURCE-CONFIRMED call/reference review.** detectFiletype, clearInput and clearOutput are bound in Settings and persisted, but no included execution-path consumer applies them. The main conversion path reads the controls and launches the converter without honoring those preferences. Separately, the tray's automatic dispatch selects FfmpegDefault rather than the main form's automatic file-type classification; parity needs an explicit test.
  - **Source:** [app/Config/Converts.cs][converts], 27-58; [frmSettings.cs][settings], 478-480 and save counterparts; [frmMain.cs][main], 1310-1339/1363-1370.
  - **Acceptance:** Toggle each exposed option and execute successful, failed and cancelled conversions from main/tray/batch as applicable. Confirm documented field clearing/type selection; do not erase inputs on failure or invent new automatic behavior.

- [ ] **O022 — Main-window close hides the UI before fallible, unguarded persistence**
  - **Medium; SOURCE-CONFIRMED ordering/error-boundary gap; fault-injected UI outcome pending.** FormClosing sets Opacity=0, then writes INI/configuration and args.txt without a containing recovery path. Denied/locked/full storage can interrupt the close flow after the window has become invisible.
  - **Source:** [app/Forms/frmMain.cs][main], 183-200 and remaining FormClosing persistence. Related F106/F128/V006.
  - **Acceptance:** Fault each close-time write and verify a visible, actionable recovery or deliberate safe exit; preserve retryable settings and active transfers. Do not turn persistence failures into silent success.

- [ ] **O023 — Updater download/install failure can still return process success**
  - **Low; SOURCE-CONFIRMED status-reporting gap.** Program.ExitCode defaults to zero and is returned after the UI loop, while important failure/rollback paths do not set a failure result. The no-update-selection path already returns nonzero, so outcome reporting is inconsistent. No caller's automated response was demonstrated.
  - **Source:** [updater/Program.cs][updater-program], 25 and 86-91; [frmUpdater.cs][updater-form], 129-201.
  - **Acceptance:** Establish success, cancelled, download-failed, verification-failed and rollback-failed outcomes; test actual updater exit codes without mislabeling a successful rollback as a successful install.

- [ ] **O024 — Generic-download cancellation/disposal does not own all cleanup**
  - **Low for retained partial files; Medium VALIDATION-LEAD for forced disposal.** Normal FormClosing correctly requests cancellation and waits for Finished. However, cancelled/abandoned downloads have no terminal TempFile cleanup, CancelToken is not disposed, and direct Dispose does not cancel the discarded RunDownload task; its final Invoke/catch-dialog paths can outlive the handle. Do not confuse this with the already-repaired normal Close behavior.
  - **Source:** [frmGenericDownloadProgress.cs][generic-download], 16-53 and 59-104; [Controls/ManagedHttpClient.cs][http], 201-242. Related F020/O008/V006.
  - **Acceptance:** Cancel normally and dispose an owner during stalled reads/errors. Ensure owned task/client/token/partial-file cleanup, no late UI dispatch, and preservation of unrelated or recovery files.

- [ ] **O025 — Removing an extended queue row does not cancel/remove the pending model work**
  - **Medium; SOURCE-CONFIRMED queue divergence; full race/resource scenario pending.** mQueueRemoveSelected removes only the ListView row. QueueList/resolver ownership and MediaDetails selection are not cleared or disposed there. A removed pending item can still undergo metadata retrieval, though the later UI-membership check correctly prevents the final update of its missing row. This is not a claim that a removed row necessarily downloads media; batch thumbnails are disabled.
  - **Source:** [frmExtendedDownloader.cs][extended-form], 1841-1941, 1996 onward and 2137-2149. Related F080/F097.
  - **Acceptance:** Remove current, pending and selected-last items during slow metadata retrieval; inspect work cancellation, references, authentication/model disposal and UI selection. Preserve the active-transfer removal guard.

- [ ] **O026 — Extended converter synchronously probes input on the UI thread**
  - **Medium; SOURCE-CONFIRMED; Debug-only/experimental entry.** ChangeInputFile directly invokes GetMediaDetails from the input-selection path. Owned-process timeout limits total lifetime but does not keep the UI responsive during a slow ffprobe operation.
  - **Source:** [app/Forms/frmExtendedConverter.cs][extended-converter], 24-42; ExtendedConversionDetails.GetMediaDetails/FfprobeData. Related F133, which fixed merger rather than this path.
  - **Acceptance:** Delay/fail ffprobe while pumping real UI input, cancel/change input and close the form. Publish only the selected operation's result and preserve the experimental feature boundary.

- [ ] **O027 — Editable extended-converter input text can disagree with the executed source**
  - **Medium; SOURCE-CONFIRMED; Debug-only/experimental entry.** Selecting a file initializes SelectedConversion.InputFilePath. The editable txtInput has no equivalent update route; conversion uses that model rather than rereading/revalidating the displayed input. Browse A, edit text to B, then convert can operate on A.
  - **Source:** [frmExtendedConverter.cs][extended-converter], 24-45 and 294-299; frmExtendedConverter.Designer.cs, 737-752.
  - **Acceptance:** Drive browse, typed edits, invalid paths and retry; display and model must agree before launching the tool. Either support validated editing or make the field explicitly read-only.

- [ ] **O028 — Experimental substream extraction is not a complete execution path**
  - **Medium; SOURCE-CONFIRMED incomplete experimental behavior, not a shipped-feature promise.** ExtractSubstreams requires subtitles, attachments and data simultaneously through AND conditions. When extraction is selected, corresponding mapping can be omitted, but GenerateExtractionArguments has no execution caller; the form launches only the primary command. The exposed choices therefore do not establish requested extraction.
  - **Source:** [ExtendedConversionDetails.cs][conversion-model], 418-421, 439 onward and 684 onward; [frmExtendedConverter.cs][extended-converter], 294-299. Related F006/F007/P006.
  - **Acceptance:** Decide whether the experimental option is supported or disabled. Test each stream category alone and in combinations, output bytes and failures, not just argument generation. Do not silently turn unfinished tooling into a new release feature.

- [ ] **O029 — Several localized forms do not apply the current language on first display**
  - **Low; SOURCE-CONFIRMED initialization gap.** frmMiscTools, frmDownloadLanguage and frmGenericDownloadProgress define LoadLanguage but their constructors/load path do not invoke it. LocalizedForm.OnLoad registers the instance; registration itself does not apply current strings. A later global language change can mask the first-display problem.
  - **Source:** [frmMiscTools.cs][misc], constructor; [frmDownloadLanguage.cs][download-language], 11-54; [frmGenericDownloadProgress.cs][generic-download], 23-58; [LocalizedForm.cs][localized], 12-17; Language.RegisterForm. Related F130.
  - **Acceptance:** Open each form under a nondefault language without switching languages afterward; test first-run fallback and later reload separately.

- [ ] **O030 — Updater and application parse inline language comments differently**
  - **Low; SOURCE-CONFIRMED format inconsistency; current shipped string failure not demonstrated.** The updater cuts at the first `//` without respecting quoted text or URLs; the application has the corrected parser. A valid shared-language value containing those characters can be truncated only in the updater. The existing D034 tests load the application parser, not both.
  - **Source:** [updater/Language.cs][updater-language], 300-310 versus app/Language.cs GetControlInfo. Related F083/F138/D034.
  - **Acceptance:** Run the same quoted-slash, URL/query, equals-sign, whitespace and comment corpus through both actual parsers; preserve intentional format differences explicitly.

- [ ] **O031 — Progress-control brush replacement/disposal leaves owned brushes undisposed**
  - **Low; SOURCE-CONFIRMED deterministic resource-lifetime defect; sustained leak rate not measured.** DropShadowColor and ForeColor allocate new SolidBrush objects without disposing replaced brushes. Dispose releases TextGraphics but not these brushes. Garbage collection/finalization may eventually reclaim resources; permanent leakage is not asserted.
  - **Source:** [Controls/ExtendedProgressBar.cs][progress], 112-117, 160-165 and 419-444. Related F028/V005.
  - **Acceptance:** Repeatedly change colors and create/dispose controls; verify deterministic cleanup of only owned brushes and stable resource counts without disposing shared framework brushes.

- [ ] **O032 — Folder-browser wrapper lacks complete disposal of owned dialogs/native wrappers**
  - **Low; SOURCE-CONFIRMED ownership omissions; native resource impact requires measurement.** BetterFolderBrowserDialog owns an OpenFileDialog without a disposal implementation; its fallback FolderBrowserDialog is not disposed. The reflected Vista-dialog path unregisters events but does not explicitly release its dialog wrapper. Unadvise is not disposal of every owner.
  - **Source:** [Controls/BetterFolderBrowser.cs][folder], 443 and 501-545, plus outer wrapper ownership. Related V009/F116.
  - **Acceptance:** Repeated open/cancel/close under normal and fallback paths, including exceptions; define ownership and release event subscriptions/dialogs/COM wrappers exactly once without breaking Framework compatibility.

- [ ] **O033 — CLI/build/custom-argument documentation differs from the included implementation**
  - **Low; SOURCE-CONFIRMED contract drift, not a request for cosmetic edits.** ARGUMENTS advertises archive aliases not recognized by RetrieveArguments. README says C# 11 Preview although projects select C# 12, and its downloader example/order predates the final `--` operand boundary. Following these contracts can select no action or an insufficient compiler.
  - **Source:** [ARGUMENTS.md][arguments-doc], [README.md][readme], [Arguments.cs][arguments], 95-130; both csproj LangVersion values. Related V010.
  - **Acceptance:** Test documented aliases/examples against actual parsing and builds, then reconcile documentation or intentional compatibility aliases. Keep archived-video behavior and user custom-argument semantics unchanged unless explicitly chosen.

- [ ] **O034 — Reddit userscript targets the listing URL and only the first post controls**
  - **Medium; REPRODUCED against the actual script in a DOM-shaped fixture.** The script checks whether any player exists, then builds three links from document.URL and appends only to TagLines[0]. On a two-post subreddit listing, observed button counts are `[3,0]`, and all three URLs refer to the listing rather than an individual post. A single-post fixture supplies a positive control.
  - **Source:** [Addons/reddit video download button.user.js][userscript], 17 onward; [probe](repro/userscript-characterization.js). Browser installation and current live Reddit DOM were not exercised.
  - **Acceptance:** Bind controls to each supported old-layout post's canonical link, including multiple posts and delayed content; avoid duplicates. Preserve the explicitly old-layout scope instead of promising new-Reddit support.

- [ ] **O035 — Userscript @match scheme is not a standard match pattern**
  - **Low; standards-confirmed VALIDATION-LEAD for actual userscript-manager behavior.** `http*://*.reddit.com/r/*` puts a wildcard inside the scheme. The documented match-pattern syntax permits an entire `*` scheme, not `http*`. A strict manager may reject the script's match rule. No claim is made that every userscript manager rejects it.
  - **Source:** [userscript][userscript], line 4; [Mozilla match-pattern reference][match-patterns].
  - **Acceptance:** Install under each supported manager/browser and verify matching on HTTP/HTTPS old-layout pages. Use supported match patterns without broadening host access unnecessarily.

- [ ] **O036 — Protocol argument decoding can change a percent-encoded URL before tokenization**
  - **Medium; SOURCE-CONFIRMED parser boundary; dedicated compiled protocol probe pending.** RetrieveArguments unescapes the entire first argument before splitting it into command and URL. In protocol payloads, encoded reserved characters inside the URL can become separators or quotes; for example `%2F` changes path structure and `%22` can alter token boundaries. This is distinct from Windows process-argument escaping later in the pipeline.
  - **Source:** [app/Arguments.cs][arguments], 54-83. Related F046/F115.
  - **Acceptance:** Round-trip protocol and ordinary CLI inputs containing encoded slashes, quotes, spaces, percent signs, Unicode and query delimiters through registry launch, parser and child argv. Decode only the intended protocol layer and preserve the URL's own encoding semantics.

## Additional end-to-end evidence gap

- [ ] **V011 — Test the actual updater wire format and both receivers together**
  - **High verification priority, not an independently proven vulnerability.** The N007 tests exercise UTF-8 packet helper methods. The live main receiver instead uses Marshal.SizeOf/NintAlloc on UpdateData with fixed marshalled fields; the N008 reentrant stub acknowledges without validating the real updater's decode/install flow. Those green tests cannot be cited as end-to-end proof for the separate live representation.
  - **Source:** [MessageHandler.cs][message-handler], 86-108; Controls/CopyData.cs UpdateData and conversion helpers; tests/AuditRegression.Ipc.cs; [frmUpdater.cs][updater-form] receiver.
  - **Acceptance:** Start the actual two compiled executables in a disposable directory; verify version/hash/Unicode filename fields, sender/session identity, native-width layouts, malformed headers, acknowledgement, cancellation and replacement/rollback. Do not target a user's installed executable or media library.

## Resolution rules and remaining review limits

All O-items in this document remain open. The 12 Windows observations and userscript probe add evidence, not fixes. Historical F-checkboxes retain their prior limited scope; new related findings do not erase older corrections or convert all unchecked historical entries into still-broken code.

The baseline repository contains 274 tracked files, including 182 declared compiled C# files, 3 excluded legacy C# copies, 17 test files, 11 translations, 26 resx files and 13 binary assets. See [scope and verification](REVIEW-SCOPE.md). Structural resource/designer inspection, code review, controlled characterization and live service/OS testing are different coverage levels. This is not exhaustive path coverage, a multi-hour live-channel soak, a complete external-binary reverse engineering exercise or a security certification.

The existing V001-V010 and P001-P006 tasks remain open as recorded. In particular: legacy OS/provider integration; denied-write/replacement crash recovery; global history/queue retention; selected executable/publisher trust; native image-decoder exposure; and experimental-feature/product-policy decisions are not silently closed. No arbitrary custom-argument restriction, mandatory MP4 conversion, library migration, deduplication change or download-history feature was introduced.

## Pinned source references

[time]: https://github.com/Proponent-8247/youtube-dl-gui/blob/e5858408008e17a34241779f18e7dfa1fed6a057/youtube-dl-gui/Controls/TimePicker.cs
[extended-model]: https://github.com/Proponent-8247/youtube-dl-gui/blob/e5858408008e17a34241779f18e7dfa1fed6a057/youtube-dl-gui/Classes/DataClasses/ExtendedMediaDetails.cs
[extended-form]: https://github.com/Proponent-8247/youtube-dl-gui/blob/e5858408008e17a34241779f18e7dfa1fed6a057/youtube-dl-gui/Forms/frmExtendedDownloader.cs
[updater-form]: https://github.com/Proponent-8247/youtube-dl-gui/blob/e5858408008e17a34241779f18e7dfa1fed6a057/youtube-dl-gui-updater/Forms/frmUpdater.cs
[updater-program]: https://github.com/Proponent-8247/youtube-dl-gui/blob/e5858408008e17a34241779f18e7dfa1fed6a057/youtube-dl-gui-updater/Program.cs
[misc]: https://github.com/Proponent-8247/youtube-dl-gui/blob/e5858408008e17a34241779f18e7dfa1fed6a057/youtube-dl-gui/Forms/frmMiscTools.cs
[message-handler]: https://github.com/Proponent-8247/youtube-dl-gui/blob/e5858408008e17a34241779f18e7dfa1fed6a057/youtube-dl-gui/Controls/MessageHandler.cs
[generic-download]: https://github.com/Proponent-8247/youtube-dl-gui/blob/e5858408008e17a34241779f18e7dfa1fed6a057/youtube-dl-gui/Forms/frmGenericDownloadProgress.cs
[program]: https://github.com/Proponent-8247/youtube-dl-gui/blob/e5858408008e17a34241779f18e7dfa1fed6a057/youtube-dl-gui/Program.cs
[batch-download]: https://github.com/Proponent-8247/youtube-dl-gui/blob/e5858408008e17a34241779f18e7dfa1fed6a057/youtube-dl-gui/Forms/frmBatchDownloader.cs
[media-data]: https://github.com/Proponent-8247/youtube-dl-gui/blob/e5858408008e17a34241779f18e7dfa1fed6a057/youtube-dl-gui/Classes/DataClasses/Bases/MediaData.cs
[youtube-data]: https://github.com/Proponent-8247/youtube-dl-gui/blob/e5858408008e17a34241779f18e7dfa1fed6a057/youtube-dl-gui/Classes/DataClasses/YoutubeDlData.cs
[general]: https://github.com/Proponent-8247/youtube-dl-gui/blob/e5858408008e17a34241779f18e7dfa1fed6a057/youtube-dl-gui/Config/General.cs
[ini]: https://github.com/Proponent-8247/youtube-dl-gui/blob/e5858408008e17a34241779f18e7dfa1fed6a057/youtube-dl-gui/Config/Interfacing/IniProvider.cs
[errors]: https://github.com/Proponent-8247/youtube-dl-gui/blob/e5858408008e17a34241779f18e7dfa1fed6a057/youtube-dl-gui/Config/Errors.cs
[settings]: https://github.com/Proponent-8247/youtube-dl-gui/blob/e5858408008e17a34241779f18e7dfa1fed6a057/youtube-dl-gui/Forms/frmSettings.cs
[log]: https://github.com/Proponent-8247/youtube-dl-gui/blob/e5858408008e17a34241779f18e7dfa1fed6a057/youtube-dl-gui/Logging/Log.cs
[about]: https://github.com/Proponent-8247/youtube-dl-gui/blob/e5858408008e17a34241779f18e7dfa1fed6a057/youtube-dl-gui/Forms/frmAbout.cs
[dutch]: https://github.com/Proponent-8247/youtube-dl-gui/blob/e5858408008e17a34241779f18e7dfa1fed6a057/Languages/Dutch.ini
[german]: https://github.com/Proponent-8247/youtube-dl-gui/blob/e5858408008e17a34241779f18e7dfa1fed6a057/Languages/German.ini
[spanish]: https://github.com/Proponent-8247/youtube-dl-gui/blob/e5858408008e17a34241779f18e7dfa1fed6a057/Languages/Spanish.ini
[textbox]: https://github.com/Proponent-8247/youtube-dl-gui/blob/e5858408008e17a34241779f18e7dfa1fed6a057/Controls/ExtendedTextBox.cs
[version]: https://github.com/Proponent-8247/youtube-dl-gui/blob/e5858408008e17a34241779f18e7dfa1fed6a057/Controls/Version.cs
[converts]: https://github.com/Proponent-8247/youtube-dl-gui/blob/e5858408008e17a34241779f18e7dfa1fed6a057/youtube-dl-gui/Config/Converts.cs
[main]: https://github.com/Proponent-8247/youtube-dl-gui/blob/e5858408008e17a34241779f18e7dfa1fed6a057/youtube-dl-gui/Forms/frmMain.cs
[http]: https://github.com/Proponent-8247/youtube-dl-gui/blob/e5858408008e17a34241779f18e7dfa1fed6a057/Controls/ManagedHttpClient.cs
[extended-converter]: https://github.com/Proponent-8247/youtube-dl-gui/blob/e5858408008e17a34241779f18e7dfa1fed6a057/youtube-dl-gui/Forms/frmExtendedConverter.cs
[conversion-model]: https://github.com/Proponent-8247/youtube-dl-gui/blob/e5858408008e17a34241779f18e7dfa1fed6a057/youtube-dl-gui/Classes/DataClasses/ExtendedConversionDetails.cs
[download-language]: https://github.com/Proponent-8247/youtube-dl-gui/blob/e5858408008e17a34241779f18e7dfa1fed6a057/youtube-dl-gui/Forms/frmDownloadLanguage.cs
[localized]: https://github.com/Proponent-8247/youtube-dl-gui/blob/e5858408008e17a34241779f18e7dfa1fed6a057/youtube-dl-gui/Controls/LocalizedForm.cs
[updater-language]: https://github.com/Proponent-8247/youtube-dl-gui/blob/e5858408008e17a34241779f18e7dfa1fed6a057/youtube-dl-gui-updater/Language.cs
[progress]: https://github.com/Proponent-8247/youtube-dl-gui/blob/e5858408008e17a34241779f18e7dfa1fed6a057/Controls/ExtendedProgressBar.cs
[folder]: https://github.com/Proponent-8247/youtube-dl-gui/blob/e5858408008e17a34241779f18e7dfa1fed6a057/Controls/BetterFolderBrowser.cs
[arguments-doc]: https://github.com/Proponent-8247/youtube-dl-gui/blob/e5858408008e17a34241779f18e7dfa1fed6a057/ARGUMENTS.md
[readme]: https://github.com/Proponent-8247/youtube-dl-gui/blob/e5858408008e17a34241779f18e7dfa1fed6a057/README.md
[arguments]: https://github.com/Proponent-8247/youtube-dl-gui/blob/e5858408008e17a34241779f18e7dfa1fed6a057/youtube-dl-gui/Arguments.cs
[userscript]: https://github.com/Proponent-8247/youtube-dl-gui/blob/e5858408008e17a34241779f18e7dfa1fed6a057/Addons/reddit%20video%20download%20button.user.js
[yt-dlp-doc]: https://github.com/yt-dlp/yt-dlp/blob/master/README.md
[youtube-dl-options]: https://github.com/ytdl-org/youtube-dl/blob/master/youtube_dl/options.py
[match-patterns]: https://developer.mozilla.org/en-US/docs/Mozilla/Add-ons/WebExtensions/Match_patterns

External provider and match-pattern references were consulted during this review; their live branches are not frozen provider-version integration results. Pin actual tested tool versions when closing O013/O014/O035.
