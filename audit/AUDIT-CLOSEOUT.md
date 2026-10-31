# Audit closeout: youtube-dl-gui / audit-fixes

Date: September 8, 2026, America/Boise (verification jobs extend into September 9 UTC).

## A. Scope and revision boundaries

This is the authorized audit-and-fix work on `Proponent-8247/youtube-dl-gui`, not the separate read-only review and not the download-history feature. The `master` comparison source is `c6ce1e6421c6bf785f30f41496b745489bcc2bab`. Changes were committed only to `audit-fixes`, using forward-only history. No merge into `master`, release publication, or download-history feature changes were performed.

The recovery supplied by the user named `1ec95a17f44c838a213f342c07c195f306887095`. The live branch had advanced beyond it. This pass resumed from `430c69edf82e3392a4da7f8b26050424a6329f75`, reconciled already-published repairs, and completed fourteen additional conceptual source/build corrections. The final production-source checkpoint is `a011f83b1335297f10c065e5f12ef20b676d6db1`; subsequent commits add tests, verification controls, and this report without changing production behavior.

The projects retain Windows Forms, .NET Framework **4.7.2**, and AnyCPU application targets. A Windows-aware project inventory resolves 182 unique declared production C# files, including 26 designer files and 156 other files, plus generated updater-hash and build-date sources. Of those declared files, 76 differ from the inspected master snapshot. Shared files compiled into both executables are counted once. These counts describe scope, **not statement or branch coverage**.

The review covered startup, arguments and protocol/IPC intake, argument generation, authentication, progress parsing, queue ownership, download/conversion cancellation, child processes and redirected pipes, HTTP, thumbnails, update discovery/installation, settings and persistence, localization, exception logging, merger/miscellaneous tools, release packaging, and CI. Designer/native-wrapper/polyfill files received structural and compatibility inspection, not exhaustive interactive testing of every control.

## B. Recovered evidence and history

The supplied `audit-baseline-a15cc311(1).zip` was inspected. It provides source/build evidence for the earlier checkpoint. It does not contain the complete original numbered audit narrative. The separate five-finding C1-C5 review was recovered and reconciled against source and tests. Historical D/H/R identifiers below are used only where their meanings were recoverable; missing identifier meanings were not invented.

[RECOVERED-FIX-HISTORY.md](RECOVERED-FIX-HISTORY.md) indexes 50 earlier source-fix commits recovered from the post-`b734c05944eeffccfde3208cd1ea45ef2ae401f9` history. This is not 50 independent defects: later process and lifecycle work intentionally strengthens earlier repairs. Older request, withdrawal, compilation-failure, and test-fixture commits remain in published history. A request commit is never treated as proof that its implementation was published or verified.

The fourteen corrections below were published through a guarded workflow. It pins the base revision and source hashes, builds before and after each correction, requires the same executed test inventory, rejects new failures, and checks that each declared regression is resolved. It publishes only after the final failure count reaches zero and uses a normal fast-forward push. Compilation failures or invalid fixtures were corrected and the relevant checks rerun; they were not counted as successful evidence.

## C. Corrections completed in this recovery

All entries in this table are fixed in the production checkpoint. Each has executable regression coverage against the compiled application or updater, rather than a copied implementation of the algorithm.

| ID | Confirmed defect and correction | Source-fix commit | Regression evidence |
| --- | --- | --- | --- |
| N001 | Extended-converter dropdown indices did not match enum values, and preset/profile/sample-rate state did not round-trip. Correct the offset and restore enable-state selections. | `53e508a4b618404609fdbc71b970d2c480aa3de8` | Three option/enum round-trip cases. |
| N002 | The `slow` FFmpeg preset was misspelled as `slopw`. Correct the emitted enum name without changing its numeric position. | `0aefa5a6c41ebd6aedafb570d36eec09cb0b663e` | Slow-preset argument case. |
| N003 | Error responses and decompressed HTTP metadata could grow without a size bound or fail to respond to cancellation. Bound decoded data, cap error diagnostics, and interrupt stalled reads. | `efe6958339ef9b5630b41bdfe08c1fc9bbb18411` | Cancellation, bounded diagnostics, and compressed-body expansion cases. |
| N004 | Thumbnail download/conversion could outlive cancellation, leave helper processes/files behind, or target the wrong selected media object. Carry cancellation through the operation, capture model identity, own the converter, and clean temporary files. | `1679fe1c4c982a01a38e46bfe316773b7a2a7577` | Delayed HTTP cancellation and conversion-process/file cleanup. |
| N005 | A normal Release build depended on unavailable external helper executables and unsafe packaging-directory sweeping. Generate build metadata and package/hash an explicit staging directory using MSBuild. | `a2ab69c59e624bc86a64d1748263068177f2b629` | Generated date, archive contents, language bytes, executable bytes, and hash manifest. |
| N006 | Download IPC accepted malformed/oversized headers before allocating its payload. Validate pointer, length, UTF-16 alignment, and supported message kind first. | `18cfeb968429a817622c4818a3a5f92c49fcc45c` | Valid UTF-16 messages plus malformed-header cases. |
| N007 | Update IPC dropped the last filename byte, breaking final Unicode characters, and accepted incomplete packet layouts. Preserve the complete packet and validate its fixed header/hash widths. | `69c0b1c2c5618c5fbe7da76bdbbac6a26b4c574` | ASCII/Unicode round trips and malformed packet/hash cases. |
| N008 | Synchronous updater acknowledgement arrived before the receiver enabled the acknowledgement state. Enable it before `SendMessage` and clear it after the handshake. | `7c14762c814b044a72930d29942ba7e6e416215a` | Real synchronous Windows-message acknowledgement. |
| N009 | ImageMagick detection waited before draining output and could hang on inherited pipe handles. Use the bounded owned-process runner. | `407fa31781c73333332800ad0adf354c6aadf50b` | Large-output and orphaned-pipe helper cases. |
| N010 | A directly disposed processing form remained registered as active work. Unregister it on disposal as well as closure. | `f8bc926dc5377de755f2a3bbb4219167df9a6ce3` | Show, directly dispose, and inspect actual active-form registry. |
| N011 | Changing the stable/beta setting while a request was pending could parse/cache the response under the wrong channel. Snapshot the channel for the full operation. | `f6e69ac66d2ed298e83a1342898f0c94cbfd9b76` | Deferred-response races in both directions. |
| N012 | Update consumption could use cached release metadata for a different selected downloader provider. Reject a mismatched cache before opening a download dialog or making a request. | `43e50edf6b1cc5d1c1a407cbb49fecbd76d856f2` | Switch provider with stale metadata and assert no download starts. |
| N013 | The clipboard link heuristic backtracked excessively on long text without a dot. Use an equivalent linear-time pattern without tightening accepted input syntax. | `3f14fd846e50354c01619d3c943b824801b82847` | Isolated 100,000-character input plus accepted/rejected input compatibility cases. |
| N014 | Both exception-log writers retried forever after a successful file write. Return immediately on success while retaining failure handling. | `2c773bf519ac55382acc6202bb0f834ce5b24d43` | Isolated application and updater log writes must finish and retain their contents. |

The extended converter is exposed through the existing Debug-only route. Its concrete argument/state defects were fixed, but incomplete experimental tools were not silently converted into new release features.

### Reconciliation of the separate C1-C5 review

| Finding | Current disposition | Existing correction retained | Executed validation |
| --- | --- | --- | --- |
| C1: queue resolution overwrites an active transfer's worker/status | Resolved; ownership and active-worker guards retained and strengthened. | `4aacc7a`, `4f68841` | Resolve another item while a transfer worker is alive; preserve worker identity and post-processing state. |
| C2: a quoted URL can still become a downloader option | Resolved at the option/operand boundary. | `f542bd2` | Windows argv round trips, quick/extended option-looking sources, search expressions and collection inputs. |
| C3: first-run synchronous waits can deadlock the UI | Resolved by an active asynchronous setup lifecycle. | `3018958` | Complete real first-run dialogs with delayed downloader/FFmpeg responses, prove UI timers continue, inspect installed fixture bytes and cleanup. |
| C4: checksum Retry deletes a file still open for hashing | Resolved; hashing finishes and closes before Retry, and replacement bytes are reverified. | `d1c7c91` | Drive the native Retry dialog, acquire an exclusive file handle, fetch replacement bytes, and verify completion. |
| C5: first no-audio binding saves designer defaults over model state | Resolved with first-binding state. | `969dbad` | Ordinary and authenticated initial no-audio binding both preserve the request. |

### Other recovered findings and current dispositions

| Area / recovered identifiers | Disposition and evidence |
| --- | --- |
| D001, D005: active-worker ownership and terminal cancellation | Retained `4f68841` / `13eefec`; tests prevent retry from replacing a live worker, prevent removal during post-processing, and prevent progress from erasing cancellation. |
| D006, D007, R006: helper lifetime, output and close behavior | Earlier partial repairs are superseded/strengthened by `bd75617`, `869cebd`, and `e8075e2`. Tests cover timeout, pre-cancellation, stdin EOF, root/descendant cleanup, bounded metadata, bounded live lines, carriage returns, callback faults, inherited-pipe drain deadlines, real form closure, and concurrent-output stress. |
| H001, H004-H006: UNC paths, fractional time, bounded joins, range equality | Retained `ccbff3e`, `5632a47`, `01f2cc7`, `dc2139f`; direct compiled-method boundary tests pass. |
| D017 and D032: explicit playlists and custom whitespace | Retained `f4c1383` / `9874be0`; explicit selection does not add conflicting `--no-playlist`, the single-video default remains, and quoted repeated spaces survive. |
| D034: language URLs, equals signs and quoted slashes | Retained `a0ddad3`; URL/query, inline-comment and quoted-slash parsing cases pass. |
| Requested remux container, configured FFmpeg, extended audio format/VBR, copied media type | Retained `1ec95a1`, `9601f99`, `9d89883`, `f0d9e8f`, `e7adc1d`. Current argument-generation branches were re-inspected; no blanket codec/container policy change was introduced. |
| Quick authentication and password cloning | Retained `b6b85ec` and protected-storage/independent-buffer design. Authentication propagation was re-inspected; clone independence, password recovery and authenticated binding are tested. |
| Update-check ownership, cache keys, internal provider `-U`, retry cancellation | Existing ownership and internal-update behavior retained. N011/N012 close remaining request/consumer cache races; N003 strengthens HTTP cancellation and bounds. |
| Settings rollback, INI write errors, localization dispatch/registration | Existing fixes retained after source reconciliation; N010 completes processing-form direct-disposal behavior. These are not claimed to have exhaustive settings/UI-path coverage. |
| Archive clipboard contention, confirmed overwrite, merger/GIF work, tool resolution | Existing targeted fixes retained after source review. N009 replaces the unsafe ImageMagick probe. Deliberately external console tools are not forcibly redesigned as in-form tasks. |
| Output-message races raised as an unconfirmed review lead | Current synchronized state and output-reader ownership were reviewed. Real quick/converter/extended forms are each closed during concurrent stdout/stderr three times. No failure was observed in these bounded runs; this is not proof against every possible scheduling race. |

## D. Behavioral decisions preserved

No forced-MP4 policy, library scan, download-archive rebuild, filename migration, or change to deduplication semantics was introduced. The independent download-history feature remains separate. Search expressions, playlists/channels, explicit custom arguments, and the existing default single-video behavior are retained. The new URL check improves the existing heuristic's running time; it is not a newly restrictive URL allowlist.

The application continues to consult the existing upstream release sources. Whether this fork should distribute only its own releases is a product/update-policy decision; that destination was not changed silently. A packaged executable is not an automatic GitHub Release and no release was published by this task.

No mass cosmetic cleanup was performed. Three legacy files not included by the current project files were left alone: `Classes/ConvertInfo.cs`, `Classes/DownloadInfo.cs`, and `Controls/UpdateMessageHandler.cs` under the main application. Existing Debug-only unfinished subtitle/extended-tool routes were distinguished from shipping behavior instead of inventing requirements for them.

## E. Executed verification and reproducibility

The regression harness loads the actual compiled application/updater. It uses temporary working directories, controlled child executables, loopback/fake HTTP responses, and real Windows Forms/message dispatch. It does not download a real channel, use the user's credentials, or modify their media library.

Key checkpoints:

| Run | Result and provenance |
| --- | --- |
| `34291862744`, job `102279923813` | Nine-repair guarded run: 59-test inventory; 20 expected baseline failures reduced to zero through separately built/tested corrections; published through `8954a8b`. |
| `34293209541`, job `102284078503` | Five-repair guarded run: 76-test inventory; seven expected baseline failures reduced to zero; published through `a011f83`. |
| `34294048859`, job `102286653621`, revision `9c7e658654b0b84a080aa265f4d5b786da03b698` | Independent clean checkout: 80 tests passed both before and after the full Release build; compiled and embedded updater identities matched. |
| `34293800015`, job `102285891860`, revision `8b0972807ca209d171c75f4157ceab0b3c010da7` | Independent clean-checkout verification: **80 tests, zero failures**, Debug solution, Release updater/application, and full normal Release solution/packaging all passed. |

The final `audit-verify.yml` extends clean-checkout verification: rerun the same regression inventory after the normal Release build in the default and explicit x86 harness processes, verify the embedded updater equals the separately compiled updater, check package bytes/hashes, reject source mutations and missing/duplicate tests, and retain a machine-readable summary tied to `github.sha`. The final report commit must itself have successful exact-revision Audit verification before operational sign-off; a green request-parent run is not a substitute.

Important scenarios include:

* Cancellation before start, during helper execution, during stalled HTTP bodies, during thumbnail conversion, and while real transfer forms process concurrent output.
* Child descendants and inherited pipes, finite metadata/output budgets, callback exception containment, and start/cleanup failures.
* Option/operand separation without corrupting Windows quoting, Unicode, UNC roots, search inputs, or explicit custom whitespace.
* Update packet boundaries/Unicode, synchronous acknowledgement, checksum Retry, delayed channel switching, and stale provider caches.
* First-run installation through actual dialogs/message loops while three controlled network operations are delayed.
* Generated build date, embedded updater SHA-256, archive entry inventory, language bytes, packaged executable bytes, and checksum manifest.

For a local supported Windows toolchain, from the repository root:

```powershell
msbuild youtube-dl-gui.sln /m /p:Configuration=Debug '/p:Platform=Any CPU'
msbuild youtube-dl-gui.sln /m /p:Configuration=Release '/p:Platform=Any CPU'
./tests/Run-AuditRegression.ps1 -EvidenceDirectory ./audit-evidence
./tests/Run-AuditRegression.ps1 -EvidenceDirectory ./audit-evidence-x86 -Platform x86
```

The workflow retains JUnit-style XML, console/stderr output, MSBuild binary logs, the full-Release log, exact source/master snapshots and diff, commit history, and release output. Downloaded copies and the source inventory are also retained in the delivered evidence bundle because Actions artifacts have finite retention.

## F. Remaining risks and limits

**Not a security certification.** Source review and finite regression tests cannot establish absence of all bugs or races. The UI stress cases are controlled, repeated close-during-output tests, not a multi-hour real-world channel-download soak. Existing nullable warnings and legacy design remain; a passing build is not described as warning-free.

**OS/provider matrix.** Executed Windows tests use the hosted Windows Server 2022 toolchain/runtime. Retaining the 4.7.2 target and compatibility fallbacks does not prove behavior on every old Windows release, GPU/display environment, or installed downloader/FFmpeg version. Live YouTube rate limits, authentication expiry, network restrictions and JavaScript-runtime requirements are not simulated as complete service integrations.

**Memory limits are local, not global.** Metadata and diagnostic bodies have explicit bounds, and long live-output lines are bounded. Cumulative visible console history and large queues still retain data; this audit does not claim a globally fixed memory budget for arbitrarily long sessions. Truncating all retained history or limiting queue sizes would require an explicit behavior policy. Thumbnail byte/pixel checks reduce exposure but do not make the native image decoder a sandbox.

**Owned processes are not a sandbox.** Windows job objects and process-tree fallback provide lifetime cleanup for helpers the application starts. On systems that reject nested jobs, cleanup is best effort. This does not authorize untrusted custom executables or make arbitrary custom arguments safe. User-selected providers, FFmpeg, and update endpoints remain trusted execution inputs.

**Integrity versus publisher authentication.** Matching hashes catch corruption/mismatch and are checked against the built updater/package. They do not independently authenticate a compromised publisher or release source. No new code-signing infrastructure or signing identity was introduced.

## G. Open policy questions and non-blocking review limits

The known, reproduced correctness/security defects listed above have concrete dispositions and fixes. The unresolved items are policy or validation boundaries: fork-versus-upstream update destination, cumulative log/queue retention, completing Debug-only experimental tools, and broader legacy-OS/live-provider soak coverage. They are not silently marked fixed, nor used to justify unrelated feature work.

The original full numbered narrative was unavailable in the supplied baseline artifact, so this closeout explicitly reconciles the recoverable findings, the five-item read-only report, observed source history, and newly reproduced defects rather than claiming a verbatim reconstruction of missing text.

## H. Sign-off criteria

**Source-fix status:** fourteen additional conceptual corrections published; earlier recovered fixes retained or explicitly strengthened; no unapplied repair manifest at the production checkpoint.

**Measured runtime evidence:** 80 distinct regression cases passed at the independent checkpoint above. Later verification artifacts must match the final branch revision and retain the same inventory after full packaging. A final green result, not this document alone, is the completion authority.

**Compatibility status:** .NET Framework 4.7.2/Windows Forms/AnyCPU design preserved; no claim that every supported historical machine or third-party service was exercised.

**Repository status:** forward-only commits on `audit-fixes`; `master` and the download-history branch are separate. No force push, release publication, or merge is part of this sign-off.
