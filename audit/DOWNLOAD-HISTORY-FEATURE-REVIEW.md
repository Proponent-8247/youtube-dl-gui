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
| DH-A016 | Medium | Fixed / regression-verified | The Download History child dialog is blocked while the parent Settings provider selection is transient, preventing Cancel from restoring an incompatible provider after protection is enabled. |
| DH-A017 | Medium | Fixed / regression-verified | Reset History now resolves an implicit/bound archive through the saved `EffectiveArchivePath`, preserving genuine unsaved-path protection. |
| DH-A018 | High | Fixed / regression-verified | Protected filename schemas now reject quotes/control characters before raw output-template interpolation can escape the app-owned argument boundary. |
| DH-A019 | High | Fixed / regression-verified | Execution leases now enforce monotonicity against any existing valid backup even when retention is currently off, while missing/invalid backups remain optional when retention is off. |
| DH-A020 | Medium | Fixed / regression-verified | First-use initialization now idempotently upgrades the lease to the cross-session file lock once the default archive directory exists, including the competing-session race. |
| DH-A021 | High | Fixed / regression-verified | Metadata recovery now accepts only native `extractor_key`/`ie_key` identity; display-style `extractor` alone remains unresolved. |
| DH-A022 | High | Fixed / regression-verified | A retained backup remains a lower-bound check, but it is no longer accepted as the sole automatic ledger when retention is off and the primary is missing/invalid. |
| DH-A023 | High | Fixed / regression-verified | Protected runs now force `--concat-playlist never` and reject conflicting custom concat policies, preserving one physical media family per native identity. |
| DH-A024 | Medium | Fixed / regression-verified | Filename recovery now mirrors current and legacy yt-dlp restricted ID sanitization, including accent transliteration and current boundary normalization. |
| DH-A025 | High | Fixed / regression-verified | GIF is now conservatively inventoried as possible final media, so authoritative GIF outputs rebuild and unidentified GIFs fail safe. |
| DH-A026 | High | Fixed / regression-verified | Invalid first-use default archive collisions are refused unless a valid primary/backup proves native archive ownership; established corruption recovery remains backup-backed. |
| DH-A027 | High | Fixed / regression-verified | Protected mode now rejects unbalanced Windows-style quoting in raw custom arguments before publishing an execution context. |
| DH-A028 | High | Fixed / regression-verified | ID-template validation and historical filename recovery now honor yt-dlp percent-escape semantics, rejecting even escaped runs while supporting active odd runs. |
| DH-A029 | High | Fixed / regression-verified | Physical inventory now covers current yt-dlp safe video/audio direct-media extensions plus `.unknown_video`, while preserving sidecar/manifest exclusions. |
| DH-A030 | High | Fixed / regression-verified | Prepared execution now carries the validated ledger as an immutable lower bound and rejects any pre-start primary shrink while allowing supersets. |
| DH-A031 | High | Fixed / regression-verified | Inventory now uses retained yt-dlp thumbnail-extension metadata to distinguish possible pre-download thumbnail residue from completed media without banning legitimate GIF media/postprocessing. |
| DH-A032 | High | Fixed / regression-verified | Protected mode rejects custom and built-in partial time-range downloads whose source-level native archive identity cannot represent the requested section. |
| DH-A033 | High | Fixed / regression-verified | A shared in-memory per-archive floor now preserves same-session ledger knowledge across preparations and completed runs even when physical backup retention is off. |
| DH-A034 | High | Fixed / regression-verified | Protected commands now retain clean per-media info JSON as authoritative extractor+ID recovery evidence and reject explicit attempts to disable it. |
| DH-A035 | Medium | Fixed / regression-verified | Indexed multi-thumbnail sidecars are now ignored only when neighboring authoritative metadata proves the exact thumbnail ID+extension ownership. |
| DH-A036 | High | Fixed / regression-verified | Protected mode rejects unsafe-extension compatibility and appends a final `-allow-unsafe-ext` compatibility directive. |
| DH-A037 | High | Fixed / regression-verified | Protected mode rejects direct arbitrary state-mutation hooks such as `--print-to-file` and `--netrc-cmd` while preserving ordinary `--print` and `--netrc`. |
| DH-A038 | High | Fixed / regression-verified | Custom and app-configured cookie files are path-checked against the prepared archive/backup/lock before protected execution. |
| DH-A039 | High | Fixed / regression-verified | Protected mode rejects raw postprocessor/external-downloader child arguments while preserving built-in postprocessing and downloader selection. |
| DH-A040 | High | Fixed / regression-verified | Protected mode blocks recursive yt-dlp cache removal while preserving ordinary cache-directory selection. |
| DH-A041 | High | Fixed / regression-verified | Cookie collision normalization now mirrors yt-dlp path expansion for `%VAR%`, `$VAR`, `${VAR}`, and user-home semantics. |
| DH-A042 | High | Fixed / regression-verified | Protected filename schemas now reject literal/environment/dynamic parent traversal while preserving yt-dlp-safe escaped and anchored forms. |
| DH-A043 | High | Fixed / regression-verified | Protected custom arguments now reject embedded NUL before execution-context publication. |
| DH-A044 | High | Fixed / regression-verified | The first repair blocked `--test`/`--tes`, but current yt-dlp also accepts unambiguous `--te`, which still bypasses protected rejection. |
| DH-A045 | High | Fixed / regression-verified | The first repair blocks exact `--ffmpeg-location`, but current yt-dlp also accepts unambiguous `--ffmpeg`, which still reaches the path-qualified executable override. |
| DH-A046 | High | Fixed / regression-verified | The first repair mirrors ordinary expansion but its fixed sentinel deletes a legitimate U+E000 path character, and its literal archive escaping does not preserve yt-dlp's single-quoted `expandvars` semantics. |
| DH-A047 | High | Fixed / regression-verified | A dangling value-taking custom option can consume the first app-owned protected suffix token, weakening config/plugin/archive isolation without using a blocked option. |
| DH-A048 | High | Fixed / regression-verified | Archive validation uses BOM-detecting/replacement-tolerant .NET text decoding instead of yt-dlp's strict UTF-8 archive semantics, so the app can accept a ledger yt-dlp will misread or reject. |
| DH-A049 | High | Fixed / regression-verified | After total archive loss, same-scan authoritative identities now recover only proven split-chapter/retained-format derivatives; unrelated, thumbnail-like, or unselected-format files remain fail-closed. |
| DH-A050 | High | Fixed / regression-verified | Protected mode now rejects error-suppression controls that can publish native history without a completed requested media artifact. |
| DH-A051 | High | Fixed / regression-verified | Protected mode now rejects fragment-skipping requests and appends a final fail-closed unavailable-fragment directive after standard/extended settings. |
| DH-A052 | High | Fixed / regression-verified | Retained `.f<format_id>` recovery now supports filename-valid format IDs only when adjacent authoritative metadata proves the exact selected format, without generic `.f…` promotion. |
| DH-A053 | High | Fixed / regression-verified | Protected mode rejects local extractor page substitution through hidden yt-dlp `--load-pages` while preserving page-dump debugging. |
| DH-A054 | High | Fixed / regression-verified | Clean merged `format_id` recovery now proves selected component IDs against the persisted format catalogue and fails closed on ambiguous `+` decompositions. |
| DH-A055 | High | Fixed / regression-verified | Protected mode blocks deterministic extractor `.dump` filesystem writes while preserving console-only page dumping. |
| DH-A056 | High | Fixed / regression-verified | Playlist/multi-video metadata is excluded from completed-media identity recovery while metadata-proven media-like playlist thumbnails remain sidecars. |
| DH-A057 | High | Fixed / regression-verified | In-tree archives are constrained to `.txt`, preventing collisions with protected provider media/sidecar outputs while preserving out-of-tree custom archive extensions. |
| DH-A058 | High | Fixed / regression-verified | Effective default/custom yt-dlp cache roots are modeled in option order and protected execution rejects archive paths that can be cache JSON targets. |
| DH-A059 | High | Fixed / regression-verified | Protected mode rejects a dangling custom `--cookies` before an app-owned following token can become its cookie writeback filename. |
| DH-A060 | High | Fixed / regression-verified | Protected standard mostly-custom commands establish the app-owned `-o` output constraint before raw custom arguments, preventing dangling options from consuming it. |
| DH-A061 | High | Fixed / regression-verified | Protected schemas require a terminal real extension, while the extended downloader preserves its prior safe `.%(ext)s` normalization before protected validation. |
| DH-A062 | High | Fixed / regression-verified | Required recovery fields and historical schema matching now use yt-dlp’s case-exact output-template key semantics, including exact schema-history deduplication. |
| DH-A063 | High | Fixed / regression-verified | Current yt-dlp `.mhtml` storyboard formats are inventoried as completed media and rebuild through authoritative adjacent metadata without broadening sidecar/manifest classes. |
| DH-A064 | High | Fixed / regression-verified | Protected split-chapter outputs are rooted with an app-owned `chapter:` path beneath the active library while preserving yt-dlp's chapter filename semantics. |
| DH-A065 | High | Fixed / regression-verified | Prepared protected execution rechecks that the selected provider is still yt-dlp/yt-dlp-nightly immediately before process start. |
| DH-A066 | Low | Fixed / regression-verified | Validate Archive explains both required protected filename recovery fields after rejecting a candidate schema. |
| DH-A067 | Medium | Verified / repair pending | Archive Browse/Open/Reset management paths must use the same yt-dlp direct-path expansion semantics as runtime history resolution. |
| DH-L002 | — | Closed / no defect found | Archive mutation is serialized by the archive-derived mutex plus an on-disk exclusive lock; first-use directory creation acquires the file lock immediately after creation, and existing regression coverage verifies serialization and cancellation. |
| DH-L003 | — | Closed / config bypass not found; plugin gap promoted to DH-A007 | Protected commands isolate config locations/aliases and conflicting archive/output hooks. A separate ambient-plugin isolation gap discovered during final re-audit is tracked as DH-A007. |
| DH-L004 | — | Closed for ordinary UI semantics; failure-atomicity gap promoted to DH-A008 | Rebuild and Reset are explicit archive-management actions and media remains non-destructive. A separate fail-closed persistence issue under partial INI-write/rollback failure is tracked as DH-A008. |
| DH-L006 | — | Closed / intended fail-safe behavior | Media without authoritative `.info.json` identity or an unambiguous filename match to a known native archive identity is reported unresolved; the implementation deliberately does not infer YouTube merely from an 11-character ID shape. |
| DH-L007 | — | Closed / classifier verified | Completed-media enumeration is allowlisted to media extensions. Known sidecars including `.info.json`, generic `.json`/`.live_chat.json`, descriptions, common thumbnails, subtitles, `.part`, `.ytdl`, and text files are not counted as media. Regression coverage will be expanded with the supplied real-world family shape. |
| DH-L008 | — | Closed / no separate library-mutation defect | `--force-overwrites` remains an active-download behavior: protected output/path ownership keeps writes inside the active download destination and never targets configured scan-only inventory roots. Banning it would change downloader behavior without proving an inventory/rebuild mutation path. |

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

- DH-A016: `d0e58d273291972f0d6366505f3ac82130daf7c7` (`fix: block history settings on transient provider selection`), closed by guarded workflow run `35311174268`, cleanup commit `b9a24a40a12647da264cb3c27d409c6dd7cef30c`.
- The A016 regression failed at baseline and passed after repair. The first repair request `5f7ef5470fe9ea2a57f2a59fdb358b6eab07fc83` was rejected before applying source because its exact-edit anchors contained CRLF; it was explicitly discarded by `990aea4cb1841ab956de8dada1e8b71493c4c96c` and retried with LF-normalized anchors.
- Evidence artifact `10533448008` has SHA-256 `48703e77a6aadf2460ffbd8723c7623cb15e50325f51b2518750cbfbae6e15b4`.

- DH-A017: `0ed13f6dfb36ac720573a8012a7258a5f1506b3d` (`fix: reset the saved effective history archive`), closed by guarded workflow run `35311455701`, cleanup commit `0e0468b291790d726a0b8cb9b7fb1ad66fad9ad0`.
- The A017 regression failed at baseline and passed after repair while the full guarded Debug/Release/regression gates completed successfully.
- Evidence artifact `10534075054` has SHA-256 `4fb9596673fb30a4ebeb637f6486af642e3bc4c478755bd82d3cd646be958821`.

- DH-A018: `61403c3862bc0ae25fbfa62c595cedb7a6ca5111` (`fix: reject unsafe protected filename schemas`).
- DH-A019: `9054bc74ecf1c6984336f86f84e66976a3eaef26` (`fix: honor existing valid backup at execution`).
- Both were closed by guarded workflow run `35311817535`, cleanup commit `ee0a5b0704b3f56224f1e840295011e40d66e021`. At baseline both new regressions failed; after A018 only the schema regression passed; after A019 both passed, alongside the complete Debug/Release/full-regression gates.
- Evidence artifact `10533770977` has SHA-256 `38dc032cbfed6bd1e6c821cd26736569f2f2ba89426f34f078654c87a3b11337`.

- DH-A020: `64ce7ccc89472610d47cf20099673e293d4abd85` (`fix: upgrade first-use history leases to file locks`), closed by guarded workflow run `35312320192`, cleanup commit `92799f3fda90a071d6991fc3142843bfa79808ea`.
- The guard showed `DOWNLOAD_HISTORY.FirstUseRaceAcquiresFileLockAfterDirectoryAppears` failing before the repair and passing afterward; every Download History regression shown in the final run passed alongside the complete guarded build/test gates.
- Evidence artifact `10534475320` has SHA-256 `de061aba1d1d7189b3b3a751c64b61fb68b837215e8b0ed5360e56af1243eeb8`.
- The first A020 guarded request failed because the new regression used a C# form unsupported by the harness compiler; that request was explicitly discarded and retried after correcting only the test syntax, preserving the source repair concept unchanged.

- DH-A021: `907575f4d81a52e1ae9b0d27ab2015070b8d9676` (`fix: require native extractor keys for metadata recovery`).
- DH-A022: `d249f100075271a162ccc71f6c05e009cbb15212` (`fix: reject stale backup as sole automatic ledger`).
- Both were closed by guarded workflow run `35320123110`, cleanup commit `3ea6a64feb894c17c554440c8f80d328c48a4cba`.
- Baseline evidence showed all three new regressions failing; after A021 only `DOWNLOAD_HISTORY.RejectsDisplayExtractorAsNativeIdentity` passed; after A022 all three passed. The guarded batch completed the full build/regression gates.
- Evidence artifact `10536099487` has SHA-256 `c70dda08c68e3ef859addba675086a8cfe5234096614ffc4b6a6978f59bf07dd`.

- DH-A023: `9c0b1fef33f9c3fcf1eae6d6526289731edd6277` (`fix: preserve per-entry media during protected downloads`), closed by guarded workflow run `35320997928`, cleanup commit `8f345b04eaf5f224105ed9f61a3deec37d03109e`.
- The guard showed `DOWNLOAD_HISTORY.DisablesPlaylistConcatenationWhileProtected` failing before the repair and passing afterward with the complete guarded build/regression gates.
- Evidence artifact `10537620494` has SHA-256 `59b51b62cd5f1def0dddec1254438f80c7b784aea6d8a5469fd7f818b7601586`.

- DH-A024: `88b8f7cc00b461e1b7b1442953d846542d4ddf70` (`fix: mirror yt-dlp restricted ID sanitization`).
- DH-A025: `853c8d9a6e1b1d5d041744fc4a5920d8bf4e71a6` (`fix: inventory GIF recode outputs as media`).
- Both were closed by guarded workflow run `35321525676`, cleanup commit `24a456f100714d92245c45025d5ade2468dfacea`.
- Baseline evidence showed both new regressions failing; after A024 the restricted-sanitization regression passed while GIF inventory still failed; after A025 both passed with the complete guarded build/regression gates.
- Evidence artifact `10537860896` has SHA-256 `232188661c66b02379f2082b880cedc337dbf4f632bb85899bddddc6074685c1`.

- DH-A026: `ab9ecdb8f0192e26e4873abc04fa4784a0ff8e36` (`fix: refuse unowned default archive corruption overwrite`), closed by guarded workflow run `35322402275`, cleanup commit `1f59d51bd327df88a28724795d1d2ba04d04cdf3`.
- The guard showed `DOWNLOAD_HISTORY.RefusesUninitializedDefaultArchiveCollision` failing before repair and passing afterward; `DOWNLOAD_HISTORY.CorruptArchiveDoesNotTrustPartialLines` remained green with trusted backup-backed recovery, and the complete Download History regression set passed after the repair.
- Evidence artifact `10537982047` has SHA-256 `b660fc5cfd38febd0da0545973203903ef4b5c4dcc814417fa941bc21bfc435e`.

- DH-A027: `66b07f65832ec2dd284e503f21b9b983417b23ed` (`fix: reject unbalanced protected custom arguments`), closed by guarded workflow run `35437251231`, cleanup commit `b33a7935e7759c4b922ced3a4de0fd869420b98b`.
- The guard showed `DOWNLOAD_HISTORY.RejectsUnbalancedCustomArgumentQuotes` failing at baseline and passing after repair; the complete Download History suite passed after repair.
- Evidence artifact `10582312597` has SHA-256 `c805a8e6b3d2575e70b29d788735661328a1aa1d3b6b170c6fbf09aa8e90c3b8`.

- DH-A028: `233b6f0f403850a13f53a50d986a31433ad6d634` (`fix: honor escaped percent in protected ID templates`), closed by guarded workflow run `35437468831`, cleanup commit `0d4866ec7a40de20a9173f2e782e05818b3d3d77`.
- The guard showed `DOWNLOAD_HISTORY.HonorsEscapedIdTemplateSemantics` failing at baseline and passing after repair, including protected rejection of `%%(id)s` and historical recovery from an active odd-percent run; the complete Download History suite passed after repair.
- Evidence artifact `10582771909` has SHA-256 `e8a820244ceb0547efa315ae77e679240efc55289398f0dcadf310dc174407e3`.

- DH-A029: `08b81a1d13581bd31ee23fd21f246b25731a2bd0` (`fix: inventory current yt-dlp direct media extensions`), closed by guarded workflow run `35437846492`, cleanup commit `3740b334cb7290a7516c3323898e5b358324ca34`.
- The corrected guard showed `DOWNLOAD_HISTORY.InventoriesCurrentDirectMediaExtensions` failing at baseline with 0/31 enumerated and passing after repair with the complete Download History suite green.
- Evidence artifact `10583480569` has SHA-256 `d77712b45b8ad084d539c4c6b43aa2a7424707d59f8e9e500df7edcf5d504cf1`.
- The first A029 request `caa7c013797fef3f37c87fa6e2e1dc2a2df0dce1` was explicitly discarded by `2bf779d303c3a7b98a56b6e14af79e167570d2a6` because the new regression used target-typed collection syntax unsupported by the audit harness compiler. Only the test syntax was corrected before retrying the same production repair.

- DH-A030: `197a4b29cd315184df8e5169108f057c1f37b5b3` (`fix: retain prepared ledger lower bound through process start`), closed by guarded workflow run `35438168368`, cleanup commit `133927b43c3f92c1cb8f46eb7f2ca5f724876fc9`.
- The guard showed `DOWNLOAD_HISTORY.ExecutionLeaseRejectsBackupFreePreparedTruncation` failing at baseline and passing after repair; the complete Download History regression set passed after repair, including the existing backup monotonicity and stale-backup cases.
- Evidence artifact `10583316283` has SHA-256 `f659602496855fcfbbc04fcebe92ae818eef64bad61cb9c1129269e1ee15df41`.

No verified issue remains open at this checkpoint. Terminal current-head re-audit continues below.

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


### DH-A017 — Reset History cannot target the saved implicit archive after rebinding/path changes

**Priority / state:** Medium management-UI correctness risk / VERIFIED in final integration re-audit.

**Affected code:** `frmDownloadHistory.ResetHistory` and `NormalizeConfiguredPath`.

**Finding:** A014 intentionally changed `NormalizeConfiguredPath` so a literal path collapses to an empty configuration only when it equals the archive path that an empty configuration actually means—normally the existing `BoundArchivePath` after history has been initialized. `ResetHistory` then re-expands an empty normalized candidate incorrectly as `DownloadHistory.DefaultArchivePath`, which is based on the **current** active download root rather than the saved/bound archive. For a bound custom archive, or for an older default archive retained after `Downloads.downloadPath` changes, the visible saved archive therefore normalizes to empty and is immediately reconstructed as a different path. The dialog reports a false unsaved-path change and refuses Reset.

**Impact:** The explicit recovery mechanism required by the fail-safe design can become inaccessible from the UI even though `DownloadHistory.ResetHistory()` itself can reset the correct saved ledger. This is especially problematic after the path-agnostic/root-change behavior added by A004/A014.

**Required acceptance:**

1. When the archive-path field normalizes to the implicit saved configuration, Reset must compare against `DownloadHistory.EffectiveArchivePath`, not `DefaultArchivePath`.
2. Bound custom archives must be resettable without first converting them to a different explicit path representation.
3. An archive originally created as the old root's default must remain resettable after the active download root changes.
4. An actual unsaved archive-path edit must still block Reset.
5. Keep the existing requirement that protection be disabled before Reset and retain A013 companion-file collision protections.
6. Add regression coverage for the Reset candidate mapping and rerun the complete guarded Windows gates.


### DH-A018 — Protected filename schemas can escape the quoted output argument

**Priority / state:** High protected-argument integrity risk / VERIFIED in final input-boundary audit.

**Affected code:** `TryGetArchiveArguments`, standard `DownloadInfo.GenerateArguments`, extended `ExtendedMediaDetails.GenerateArguments`, and filename-schema history input.

**Finding:** Both standard and extended downloaders construct the yt-dlp output argument by opening a literal quote, appending the configured filename schema verbatim, and then appending the closing quote. Protected mode validates that the filename portion contains `%(id)s`, but it does not reject a quote or control character in the schema. The main Settings textbox blocks `"` on ordinary keypresses, but the schema-history editor accepts arbitrary text except `|`, and persisted/history values are not a trustworthy argument boundary. A schema such as an ID-bearing template followed by a raw quote and standalone `--` can terminate the `-o` value before the app later appends `--ignore-config --no-plugin-dirs --download-archive ...`; yt-dlp then sees the later protected options after an option terminator.

**Impact:** A non-`CustomArguments` setting can bypass the very custom-argument restrictions intended to make Download History fail closed, causing the app-owned native archive option to be treated as a positional operand instead of protection.

**Required acceptance:**

1. While Download History is enabled, reject filename schemas containing `"` or control characters before protected arguments are generated.
2. Continue allowing existing supported schema syntax, including formatting expressions and directory separators, so this does not become a general schema refactor.
3. The rejection must be actionable and identify the filename schema as unsafe for protected argument generation.
4. The restriction is scoped to protected mode; disabling Download History must not silently change unrelated legacy schema behavior.
5. Add regressions for quote and record/control characters and rerun the complete Windows gates.

### DH-A019 — Existing last-good backup is ignored by the execution lease when retention is off

**Priority / state:** High duplicate-prevention integrity risk / VERIFIED in final runtime-integrity audit.

**Affected code:** `ValidateArchiveForProtectedExecution` and `AcquireValidatedExecutionLease`.

**Finding:** Normal protected preparation always reads a valid existing `<archive>.bak` and unions entries missing from the primary, regardless of the current `KeepBackup` setting. This makes an existing valid backup an authoritative anti-truncation source. The execution lease, however, performs the backup subset check only inside `if (keepBackup)`. If the user previously kept backups, later turns retention off (leaving the valid backup in place), prepares a command, and the primary is then replaced with a syntactically valid subset before process start, the execution lease accepts the truncated primary even though the existing backup proves entries were lost.

**Impact:** The process can launch with a ledger known to have lost historical identities, allowing already downloaded media to be fetched again. The behavior is inconsistent with the preparation path and with the existing fail-closed truncation test when backup retention is on.

**Required acceptance:**

1. If a backup file exists and parses as a valid native archive, the execution lease must reject a primary that omits any of its identities regardless of the current backup-retention setting.
2. When retention is on, a missing/invalid backup remains a hard failure as today.
3. When retention is off, a missing or invalid backup is not newly required; only an existing **valid** backup contributes the monotonicity check.
4. Regenerating the command after such a rejection must allow normal preparation to union the valid backup into the primary, preserving current behavior.
5. Add regression coverage for a retained valid backup plus `KeepBackup=false`, then rerun the complete guarded Windows gates.


### DH-A020 — First-use directory race can skip the cross-session archive file lock

**Priority / state:** Medium concurrency/integrity risk / VERIFIED in final locking re-audit.

**Affected code:** `DownloadHistoryLease`, `TryAcquireArchiveLease`, and `TryInitializeNewDefaultLibrary`.

**Finding:** The archive-specific named mutex deliberately uses the `Local\` namespace, and the application's single-instance mutex is likewise session-scoped. Cross-session serialization therefore depends on the exclusive `<archive>.lock` file. When the default library/archive parent does not yet exist, `TryAcquireArchiveLease` can only acquire the named mutex and skips the file lock. `TryInitializeNewDefaultLibrary` normally creates the directory and immediately acquires the file lock. However, if another session creates the directory after the lease was acquired but before this method runs, its leading `if (Directory.Exists(libraryRoot)) return true;` exits without acquiring the now-available file lock. Two sessions can then proceed with only independent session-local mutexes.

**Impact:** Simultaneous first enable/reconcile from separate Windows sessions can bypass the intended cross-process archive serialization and race archive/backup creation or replacement.

**Required acceptance:**

1. A lease must be able to idempotently ensure that its cross-session file lock is held once the archive parent exists.
2. `TryInitializeNewDefaultLibrary` must ensure the default archive lock even when the directory is already present by the time initialization runs.
3. Re-acquiring/ensuring the lock on a lease that already obtained it must be a no-op rather than self-deadlocking.
4. Do not create custom archive parent directories as a side effect; the fix is limited to the default-library first-use path.
5. Keep existing same-session mutex behavior, cancellation semantics, persistent lock-file handling, and fail-fast management semantics.
6. Add a runtime regression that acquires the history lease while the default directory is absent, creates the directory to simulate the competing session, runs initialization, and verifies a second exclusive open of the lock file is blocked until the lease is disposed.
7. Re-run the complete guarded Windows gates and then perform the terminal feature-delta re-audit.


### DH-A021 — Display-style metadata extractor is not a native archive key

**Priority / state:** High duplicate-prevention correctness risk / VERIFIED against current app source and upstream yt-dlp identity construction.

**Affected code:** `TryRecoverFromInfoJson`.

**Finding:** Recovery currently chooses the first non-empty value from `extractor_key`, `ie_key`, and finally `extractor`. yt-dlp writes `extractor` from an extractor's human/namespace-style `IE_NAME` and writes `extractor_key` from `ie.ie_key()`. Native archive IDs are constructed from `extractor_key`/ `ie_key`, not from `extractor`. These values are not interchangeable: for example, yt-dlp's `YoutubeClipIE` has `IE_NAME = "youtube:clip"` while its native extractor key is `YoutubeClip`, producing archive prefix `youtubeclip`.

**Impact:** A legacy, stripped, or partially damaged `.info.json` that retains only `extractor` can be accepted as authoritative and seed a syntactically valid but semantically wrong archive entry such as `youtube:clip <id>`. A later native yt-dlp run uses `youtubeclip <id>`, does not recognize the recovered record, and can download the same media again.

**Required acceptance:**

1. Metadata recovery may use `extractor_key` or `ie_key` as native extractor identity.
2. Do not synthesize native archive identity from the display-style `extractor` field when no native key is present.
3. Metadata containing only `id` plus `extractor` must remain unresolved/unsafe rather than being guessed.
4. Preserve existing top-level-field precedence, control-character validation, and non-destructive behavior.
5. Add a regression using a real divergent-style pair such as `extractor: "youtube:clip"` with no native key, and a control proving `ie_key: "YoutubeClip"` recovers `youtubeclip <id>`.
6. Re-run the complete guarded Windows Debug/Release/regression gates.

### DH-A022 — Stale retained backup cannot be the sole automatic ledger after retention is disabled

**Priority / state:** High duplicate-prevention integrity risk / VERIFIED in final ledger-state audit.

**Affected code:** `ValidateArchiveForProtectedExecution` and `TryReadLedgerForArchiveTransition` / archive relocation.

**Finding:** Turning off backup retention intentionally stops refreshing `<archive>.bak`, but the existing valid backup file is preserved and still used as a lower-bound anti-truncation source. If the primary later becomes missing or invalid, normal preparation currently treats that retained backup as sufficient to recreate the primary even though it can be stale by design. The same problem exists during archive relocation: a missing/invalid previous primary plus a retained stale backup is accepted as the complete previous ledger.

**Impact:** Identities appended after backup retention was disabled can disappear silently. Normal protected downloading or an archive-path transition can then continue with a healthy result based on an older subset, allowing those media to be downloaded again.

**Required acceptance:**

1. An existing valid backup may remain a monotonic lower-bound check when the primary is valid, regardless of current retention policy (preserve DH-A019).
2. If the primary is missing or invalid and backup retention is currently off, normal protected preparation must fail closed even if a valid retained backup exists; direct the user to explicit Rebuild/Inventory.
3. Do not rewrite/recreate the primary from that potentially stale backup during normal preparation.
4. During archive relocation, if the previously bound primary is unavailable/invalid and the **previous** backup-retention policy was off, refuse the transition rather than treating the retained backup as complete history. Reset History remains the explicit history-discard path.
5. Keep current automatic backup restoration when retention is on.
6. Add regressions with a valid backup, disable retention, append a newer ledger-only identity to the primary, then remove the primary: normal protected preparation and archive relocation must not silently fall back to the stale backup.
7. Re-run the complete guarded Windows Debug/Release/regression gates.


### DH-A023 — Default multi-video concatenation can destroy per-identity rebuild evidence

**Priority / state:** High archive-rebuild correctness risk / VERIFIED against current app source and upstream yt-dlp defaults/postprocessor behavior.

**Affected code:** protected argument generation in `TryGetArchiveArguments`.

**Finding:** Current yt-dlp defines `--concat-playlist` with default policy `multi_video`. When the playlist/multi-video concat postprocessor runs, it concatenates the entry media files into one playlist-level output and returns the individual inputs for deletion. The individual entries have already been processed for native archive recording, so one aggregate physical file can represent multiple archive IDs. Download History currently does not override this default.

**Impact:** A completely normal protected run of a multi-video extractor can produce a valid native archive containing several provider+ID records while deliberately removing the corresponding per-entry media files. If the primary/backup ledger is later lost and the user invokes explicit Rebuild, the physical library no longer contains one authoritative media family per archived identity; the single aggregate output cannot reconstruct all of those native records. This defeats the feature's physical-library recovery model without any unsafe custom argument from the user.

**Required acceptance:**

1. Protected Download History arguments must explicitly append `--concat-playlist never` so the app retains per-entry media evidence for native identities.
2. Reject a user-supplied `--concat-playlist` while protection is enabled rather than silently overriding an explicit conflicting request; users who require concatenation must disable Download History.
3. Preserve all existing playlist traversal behavior and the `--no-break-on-existing` protection.
4. Keep Download History disabled behavior unchanged; concat remains available when protection is off.
5. Add regression coverage proving the protected prefix includes `--concat-playlist never` and that custom concat policies are rejected clearly.
6. Re-run the complete guarded Windows Debug/Release/regression gates, then continue the terminal current-head audit.


### DH-A024 — Restricted filename recovery is not fully compatible with yt-dlp ID sanitization

**Priority / state:** Medium recovery/availability risk / VERIFIED against current app and upstream yt-dlp sanitization.

**Affected code:** `SourceIdFileNameCandidates` and `SanitizeSourceIdCompat`.

**Finding:** Filename-only recovery deliberately generates several sanitized forms of a known native source ID. The non-restricted current sanitizer is close to current yt-dlp behavior, but restricted candidates are incomplete. yt-dlp's restricted sanitizer transliterates a defined accent set (for example `ä -> a`) and, in current filename-sanitization mode, normalizes/condenses replacement markers and strips replacement markers from the beginning/end. The app currently maps non-ASCII restricted characters generically to `_` and has no current restricted-mode candidate.

**Impact:** If the primary is damaged/missing but a valid ledger/backup still provides the native ID, and the media has no adjacent `.info.json`, a file produced with `--restrict-filenames` can fail filename matching even though its name was generated by yt-dlp from that exact ID. Recovery then reports unresolved/unsafe and can unnecessarily block the user from reconciling a recoverable library.

**Required acceptance:**

1. Generate candidates matching both current and legacy yt-dlp restricted ID sanitization behavior.
2. Include yt-dlp's defined accent transliterations used in restricted mode.
3. Preserve current non-restricted/full-width and legacy compatibility candidates.
4. Do not weaken ambiguity handling: if different native archive entries collapse to the same sanitized filename candidate, recovery remains unresolved.
5. Add regressions using an ID with restricted accent transliteration and an ID whose leading invalid character is stripped by current restricted sanitization.
6. Re-run the complete guarded Windows build/regression gates.

### DH-A025 — GIF can be final media but is always ignored as a sidecar

**Priority / state:** High rebuild correctness risk / VERIFIED against current app and upstream yt-dlp FFmpeg conversion support.

**Affected code:** `EnumerateCompletedMedia`.

**Finding:** The scanner excludes every `.gif` before reaching the media-extension allowlist, treating GIF only as a possible thumbnail. Current yt-dlp's `FFmpegVideoConvertorPP` explicitly supports `gif` as a final recode target. Protected users can therefore legitimately produce `%(id)s.gif` media through custom `--recode-video gif` while the archive records the native identity normally.

**Impact:** After ledger loss, explicit Rebuild can silently see zero completed media for a GIF-only protected library and initialize/reconstruct an archive without that identity, enabling a later duplicate download. This is worse than a conservative unresolved result.

**Required acceptance:**

1. Treat `.gif` as a completed-media candidate rather than unconditionally discarding it as a sidecar.
2. Preserve conservative identity rules: a standalone GIF without authoritative metadata/known archive identity may block explicit rebuild as unresolved rather than being guessed.
3. A GIF thumbnail sharing an authoritative media family may resolve to the same native identity without creating duplicate archive records; archive identity de-duplication remains set-based.
4. Add regression coverage proving authoritative GIF media is inventoried/rebuilt and an unidentified standalone GIF fails safe instead of being silently ignored.
5. Re-run the complete guarded Windows build/regression gates.


### DH-A026 — Default archive filename alone is not proof of application ownership

**Priority / state:** High non-destructive data-integrity risk / VERIFIED in terminal state-machine audit.

**Affected code:** invalid-primary/no-valid-backup handling inside `AnalyzeCore` and the legacy regression `DOWNLOAD_HISTORY.CorruptArchiveDoesNotTrustPartialLines`.

**Finding:** DH-A010/A012 correctly refuse to overwrite invalid custom archive targets without a valid backup, but the current source exempts any file at the default path `<active-root>\yt-dlp-archive.txt`. The existing corruption regression constructs such an invalid file in a fresh fixture with `EverEnabled=false` and no bound archive, then explicitly expects reconciliation to overwrite it. The filename itself is therefore being used as ownership proof before Download History has ever established that file.

**Impact:** A user who already has an unrelated text/media-management file named `yt-dlp-archive.txt` in the download root can lose it merely by first enabling/rebuilding Download History. This contradicts the strict non-destructive first-use model and is the default-path equivalent of the custom collision fixed by DH-A010/A012.

**Required acceptance:**

1. A valid existing native primary or valid backup may still be adopted/reconciled on first use.
2. An invalid existing primary with no valid backup must never be overwritten automatically, even at the default path; settings/binding metadata alone is not sufficient ownership proof.
3. A never-initialized invalid default-path collision must remain byte-for-byte unchanged and return a non-reconcilable Invalid/Unsafe result.
4. Revise the existing corruption-recovery regression (retain its test name) so it first establishes a real default archive with a valid backup, corrupts the primary, and proves recovery ignores poisoned partial primary entries and restores from trusted state.
5. Add a separate first-use default-collision regression proving no overwrite occurs.
6. Do not rely on `EverEnabled` or `BoundArchivePath` to authorize overwrite of an invalid primary without a valid backup; explicit Reset/removal is required before physical rebuild can replace it.
7. Re-run the complete guarded Windows Debug/Release/regression gates.


### DH-A027 — Unbalanced custom quoting can swallow the protected argument suffix

**Priority / state:** High protected-argument integrity risk / VERIFIED in terminal current-head audit.

**Affected code:** `TryGetArchiveArguments`, `TokenizeArguments`, and the standard/extended argument construction order.

**Finding:** Download History validates the filename schema's raw quote/control-character boundary (DH-A018), and it tokenizes custom arguments to reject specific unsafe options. The tokenizer, however, does not reject an unterminated quoted region. Both standard and extended downloaders append the raw custom argument string **before** the app-owned `--ignore-config --no-plugin-dirs --download-archive ...` suffix. A custom value ending with an unmatched quote can therefore leave the Windows command line inside a quoted token when the protected suffix is appended. The safety scan sees no forbidden option, but yt-dlp need not receive the later archive flags as independent argv elements.

**Impact:** The app can return a prepared `DownloadHistoryExecution` and treat a command as protected even though the native archive/anti-concat/plugin-isolation suffix is syntactically consumed by a preceding custom argument. This defeats the core duplicate-prevention boundary without using any already-blocked option.

**Required acceptance:**

1. While Download History is enabled, reject custom argument strings whose Windows-style quote state is unbalanced after accounting for backslash-escaped quotes.
2. Keep correctly balanced quoted custom values available; this is not a ban on quoting.
3. The rejection must occur before `EnsureReady`/execution-context publication and explain that the custom argument boundary is unsafe.
4. Preserve the existing option tokenizer semantics and all A011/A015/A018 protections.
5. Download History disabled behavior remains unchanged.
6. Add regressions for a plain unmatched quote and an odd-backslash escaped quote case, plus a balanced quoted-value control.
7. Re-run the complete guarded Windows Debug/Release/full-regression gates, then continue the terminal input-boundary audit.


### DH-A028 — Escaped percent can satisfy the ID-template check without embedding the ID

**Priority / state:** High rebuild-correctness risk / VERIFIED against current app source and current yt-dlp output-template semantics.

**Affected code:** `HasRequiredIdTemplate`, `BuildSchemaRegex`, protected filename-schema validation, and historical filename recovery.

**Finding:** The feature currently considers a schema recoverable whenever the filename portion contains the raw substring `%(id)s`. yt-dlp output templates use Python-style percent formatting, where `%%` emits a literal percent. Therefore `%%(id)s.%(ext)s` passes the app's mandatory-ID check even though yt-dlp emits the literal text `%(id)s` rather than the media ID. Percent runs also matter for historical recovery: an odd run can contain an active placeholder after literal-percent pairs (for example `%%%(id)s`), while an even run does not.

**Impact:** A protected download can be archived successfully while its filename contains no source ID. If the application-owned ledger/backup is later lost, the physical library may no longer provide the mandatory filename evidence required for safe reconstruction. A validator that is fixed without matching recovery parsing would create a second incompatibility for schemas containing literal percent signs.

**Required acceptance:**

1. Recognize `%(id)s` only when the percent introducing that token is active under yt-dlp/Python percent-escape semantics.
2. `%%(id)s` and other even-percent escaped forms must not satisfy the protected ID requirement.
3. Odd percent runs that represent literal-percent pairs followed by an active `%(id)s` must remain valid.
4. Historical schema regex construction must apply the same percent-run semantics so a valid odd-run schema can later recover its filename.
5. Preserve existing formatted-placeholder parsing and directory-component behavior; do not broaden the requirement beyond the exact ID token.
6. Add regression controls for unescaped, even-escaped, and odd-run ID tokens plus filename recovery from the odd-run form.
7. Re-run the complete guarded Windows Debug/Release/full-regression gates.


### DH-A029 — Inventory allowlist misses valid current yt-dlp direct-media outputs

**Priority / state:** High archive-rebuild correctness risk / VERIFIED against current app source and current yt-dlp Generic/direct extension handling.

**Affected code:** `EnumerateCompletedMedia` completed-media extension allowlist.

**Finding:** The scanner covers yt-dlp's common/current `MEDIA_EXTENSIONS` video/audio set and the GIF recode case fixed under DH-A025, but yt-dlp's current safe-extension table contains additional extensions explicitly categorized as video/audio. The Generic extractor preserves any safe direct-link extension and falls back to the internal final extension `unknown_video` when a non-HTML direct URL has an unrecognized extension. The app currently ignores multiple such possible final media files during inventory, including `.mpga`, `.m4s`, `.mxf`, `.ra`, and `.unknown_video`.

**Impact:** A protected download can complete, receive a native archive record, and leave an ID-bearing media family that explicit Rebuild never enumerates. If the application-owned ledger/backup is later lost, those valid physical identities are silently omitted instead of being recovered or conservatively reported unresolved.

**Required acceptance:**

1. Extend completed-media inventory to current yt-dlp safe extensions categorized as video/audio that are not already covered, plus the Generic extractor's `.unknown_video` fallback.
2. Keep manifests, subtitles, thumbnails/images, metadata, fragments, and other sidecars excluded unless separately proven to be final media.
3. Continue treating an enumerated file without authoritative metadata or unambiguous filename-to-ledger identity as unresolved rather than guessing.
4. Preserve all existing media extensions and A025's conservative GIF handling.
5. Add a regression covering every newly admitted extension with authoritative same-stem metadata and prove explicit Rebuild recovers each identity without modifying the files.
6. Re-run the complete guarded Windows Debug/Release/full-regression gates.


### DH-A030 — Backup-free prepared execution can accept a smaller ledger than the one validated

**Priority / state:** High duplicate-prevention TOCTOU risk / VERIFIED in terminal runtime-integrity audit.

**Affected code:** `DownloadHistoryExecution`, `ValidateArchiveForProtectedExecution`, `TryGetArchiveArguments`, and `AcquireValidatedExecutionLease`.

**Finding:** Protected command preparation validates the current native archive and returns a `DownloadHistoryExecution` containing only the archive path and backup-retention flag. At process start, the execution lease re-reads the primary and checks it against a valid backup when one exists. If backup retention is off and no valid backup exists, however, a primary replaced after preparation with a different but syntactically valid subset/empty native archive passes execution validation. The app therefore launches a command against a ledger that is known to be smaller than the ledger it validated when it advertised the command as protected.

**Impact:** Historical identities can disappear in the prepare-to-start window without detection, allowing the immediately launched yt-dlp process to redownload media that the prepared command was supposed to protect. Existing A019/A022 backup rules close this only when a usable backup happens to exist.

**Required acceptance:**

1. A prepared execution context must carry an immutable snapshot/lower-bound set of the native archive identities validated during command preparation.
2. At `AcquireValidatedExecutionLease`, the current primary archive must contain every identity from that prepared snapshot before the provider starts. Additional entries appended after preparation are allowed.
3. The check applies regardless of backup-retention policy and regardless of whether any backup exists.
4. On prepared-snapshot loss, fail before process start, invalidate the prepared state, and require explicit validation/reconciliation rather than silently accepting the smaller ledger.
5. Keep A019 behavior: any existing valid backup is also a lower-bound source, even when retention is off.
6. Keep A022 behavior: a stale retained backup is not by itself sufficient to recreate a missing/invalid primary when retention is off.
7. Add a regression with `KeepBackup=false`, no backup file, a prepared execution context, and a syntactically valid primary truncation; the lease must reject it. Also prove a post-preparation superset append remains acceptable.
8. Re-run the complete guarded Windows Debug/Release/full-regression gates.


### DH-A031 — GIF thumbnail residue can be mistaken for completed media

**Priority / state:** High archive-rebuild correctness risk / VERIFIED against current app scanner and current yt-dlp thumbnail/write ordering.

**Affected code:** `EnumerateCompletedMedia`, protected custom postprocessing policy, and the A025 GIF inventory behavior.

**Finding:** A025 conservatively admitted `.gif` as possible final media because current yt-dlp can recode video to GIF. But yt-dlp can also write a thumbnail using the source thumbnail's extension, including GIF, and constructs that thumbnail by replacing the media extension with the thumbnail extension. The thumbnail and `.info.json` are written **before** the actual media download. Therefore a failed download may leave `<same stem>.gif` plus `<same stem>.info.json` even though no media completed. The current scanner enumerates the GIF, treats the adjacent metadata identity as authoritative, and can rebuild the native archive from the sidecar residue.

**Impact:** Explicit Rebuild can falsely mark a source as downloaded from a surviving thumbnail alone. The next protected run then skips the source even though the actual media never completed or has been removed. This violates the required sidecar-vs-media distinction and fail-safe recovery model.

**Required acceptance (second revision after terminal source-order audit):**

1. Determine thumbnail ambiguity from the adjacent authoritative info JSON's retained `thumbnails` list, mirroring yt-dlp's thumbnail filename extension choice: prefer each thumbnail's explicit `ext`; otherwise derive the extension from its URL path.
2. If a candidate completed-media path uses an extension that the same metadata says a thumbnail could use, that path may not create a **new** native history identity by itself because the thumbnail can be written before media completion.
3. If the ambiguous candidate's identity already exists in a valid primary archive or trusted backup, it may corroborate that already-known identity; no history is invented.
4. If a same-stem completed-media sibling uses an extension that is **not** listed as a possible thumbnail extension, treat the ambiguous candidate as a sidecar and do not double-count it.
5. Otherwise, during recovery/rebuild, report the ambiguous candidate unresolved and block reconciliation; during ordinary valid-archive validation, continue to ignore unarchived residue.
6. If metadata does **not** list the candidate extension as a possible thumbnail extension, preserve normal media recovery—including legitimate direct/recode/remux GIF outputs. Remove the temporary protected GIF recode/remux restrictions from the superseded retry.
7. Metadata-free media remains governed by the existing conservative rules: it cannot invent a new extractor identity from filename shape alone when no native ledger identity exists.
8. Keep all media/sidecar files byte- and path-unchanged.
9. Preserve the A025/A031 test identity and cover: same-stem real media + GIF thumbnail metadata; failed/orphan GIF whose metadata explicitly lists a GIF thumbnail; direct GIF with no GIF-thumbnail evidence; already-known ambiguous GIF identity; metadata-free orphan; and recode/remux GIF remaining available when otherwise compatible.
10. Re-run the complete guarded Windows Debug/Release/full-regression gates.

**Repair disposition history:**

- `d6703d39fbf6890db0c8e648f4d52d5fc354b06d` passed the original A031 regression in guarded workflow `35461564792`, but the regression assumed top-level `ext=gif` proved completion. Terminal re-audit showed that is not sufficient because thumbnail/info artifacts precede media completion. Superseded.
- `991f7eca9bd623953e03c14e44d081947b176856` passed the first retry regression in guarded workflow `35461893289`, cleanup `98346003001d0c89429480c252306e9538741e8e`, artifact `10590200322`, SHA-256 `7ea70b46ba07527dfae7255be87320f2b41e1ed98013494735a77ab70a0320d4`. The immediately following terminal audit found that yt-dlp retains exact thumbnail extension evidence in `thumbnails`, allowing a narrower and more compatible fix. This retry is therefore also superseded rather than accepted as closure evidence.

- Final accepted repair: `49043bdcf8c64d390430a7b3bb75d8a8a0663c4d` (`fix: classify media-thumbnail extension collisions from metadata`), guarded workflow run `35462135373`, cleanup commit `9a710d04642fca1d6bbe4898b4d18f7dbcf9c193`.
- The final locked regression `DOWNLOAD_HISTORY.InventoriesGifMediaConservatively` failed at baseline and passed after the repair; the complete Download History regression set passed afterward.
- Evidence artifact `10590810082` has SHA-256 `814fe7c935d55b39d6e0b9c5337172291a23ab23eae1858f537ee0efe5d43012`.
- Terminal postprocessor-order re-audit found no additional built-in late-stage media deletion path: app-owned concat is forced off, and user-selectable arbitrary late-stage `--exec` / `--use-postprocessor` hooks are already rejected.

### DH-A032 — Partial-section downloads cannot be represented safely by the native archive identity

**Priority / state:** High duplicate-prevention semantic risk / VERIFIED against current extended argument generation and current yt-dlp archive-write flow.

**Affected code:** protected custom-argument validation and `ExtendedMediaDetails.GenerateArguments` time-range handling.

**Finding:** The extended UI emits `--download-sections` when StartTime/EndTime is set, and protected custom arguments may also supply `--download-sections`. Current yt-dlp processes the requested range(s) but records the parent source through the normal native archive key (extractor + source ID) once the requested downloads succeed. The archive format has no section/range component.

**Impact:** Downloading only 00:10–00:20 under protection can mark the entire source ID as downloaded. A later attempt to fetch the full media, a different range, or another section can then be skipped as an archive duplicate. That is not a path/rebuild problem; it is an identity-granularity mismatch that native archive semantics cannot encode safely.

**Required acceptance:**

1. Reject `--download-sections` in custom arguments while Download History protection is enabled.
2. Reject built-in Extended StartTime/EndTime downloads while Download History is enabled before command execution is prepared.
3. Explain that native Download History is source-ID based and cannot safely distinguish partial ranges; users may disable Download History for intentional clip/range downloads.
4. Download History disabled behavior remains unchanged.
5. Do not change playlist-item, split-chapter, or ordinary full-media behavior unless separately proven unsafe.
6. Add regressions for custom `--download-sections` and the Extended built-in section path.
7. Re-run the complete guarded Windows Debug/Release/full-regression gates.

- DH-A032: `f018a074d31df91bc03aaef71ab0ae7a5efca923` (`fix: reject partial downloads under native history protection`), guarded workflow run `35461564792`, cleanup commit `9d62e6654b9b5dac56ebb285f2865bf481f9a1c8`.
- Baseline evidence showed both A031/A032 locked regressions failing; after the first A031 repair only A032 still failed; after A032 the complete Download History regression set passed.
- Evidence artifact `10590540088` has SHA-256 `6529a1ee1a757973bb23858d05f3f2426499fc83b721fc519b492b744537c9be`.
- A031 is intentionally not closed from that run for the reason recorded above.


### DH-A033 — Same-session ledger knowledge is not monotonic across preparations when backups are off

**Priority / state:** High duplicate-prevention integrity risk / VERIFIED in terminal A030 follow-up audit.

**Affected code:** `ValidateArchiveForProtectedExecution`, `DownloadHistoryExecution`, `AcquireValidatedExecutionLease`, and `RefreshBackupAfterRun`.

**Finding:** DH-A030 correctly pins the native archive entries seen by one prepared command and rejects a shrink between that command's preparation and process start. However, the lower bound lives only inside that one `DownloadHistoryExecution`. When `KeepBackup=false` and no valid backup exists, a later preparation re-reads the current primary and replaces `LastReportInternal.ArchiveSnapshot` with whatever syntactically valid subset exists at that moment. The process therefore forgets identities it successfully validated earlier in the same application session. In addition, `RefreshBackupAfterRun` returns immediately when backup retention is off, so identities appended by a successful provider run do not advance any application-side lower bound that older/later prepared commands can enforce.

**Impact:** A valid archive containing identities A+B can be prepared safely, then truncated to A, and a subsequent command can be prepared and run with B silently forgotten. Likewise, after a successful no-backup run appends C, an older prepared command can later start after C has disappeared because its private A+B snapshot has no knowledge of C. Both cases can redownload media that this process already knew was archived.

**Required acceptance:**

1. Maintain an in-memory, per-effective-archive monotonic lower bound for the current application session.
2. Every protected preparation must reject a valid primary that omits any identity from that same-session lower bound, unless a valid backup/reconciliation path restores the missing identities first.
3. Successful supersets advance the floor; unchanged ledgers may reuse the existing immutable snapshot rather than duplicating it per command.
4. `RefreshBackupAfterRun` must validate and advance the in-memory floor after a successful provider run even when physical backup retention is disabled. It must not start writing a backup when `KeepBackup=false`.
5. A prepared execution lease must enforce both its own preparation-time lower bound and the latest same-archive session floor so an older command cannot forget identities learned from a newer completed run.
6. Reset History clears the floor; archive relocation starts a new floor only after normal relocation preservation has succeeded. Disabling/re-enabling does not silently discard same-session knowledge.
7. Do not claim cross-process or cross-restart monotonicity when backups are disabled; the floor is deliberately an in-memory strengthening, not a hidden persistent backup.
8. Preserve all DH-A019/A022/A030 backup semantics and explicit Reset/Rebuild behavior.
9. Add regressions for (a) later preparation after a no-backup same-session shrink and (b) an old execution after a no-backup provider run advances the archive and the new identity is then removed.
10. Re-run the complete guarded Windows Debug/Release/full-regression gates.

- DH-A033: `841a67c8b73594292b54e4e4f8c1ad3e261875a5` (`fix: preserve same-session native ledger floor`), guarded workflow run `35462423856`, cleanup commit `5a6156feef0ece3ea0a319f029c289cd2a69e0b0`.
- The guard showed `DOWNLOAD_HISTORY.PreservesSameSessionLedgerFloorWithoutBackup` failing at baseline and passing after repair; the complete Download History regression set passed afterward.
- Evidence artifact `10590196558` has SHA-256 `084ae193e915f092cc2d0dc056c1ecaf3bf1bed76b329f39fd51492b1b49db88`.
- The floor is intentionally process-local and does not create a hidden persistent backup when `KeepBackup=false`; Reset History clears it.


### DH-A034 — Protected downloads do not guarantee authoritative metadata for total-loss rebuild

**Priority / state:** High recovery-correctness risk / VERIFIED against standard/extended argument generation and the non-inference recovery policy.

**Affected code:** `TryGetArchiveArguments`, standard/extended generated arguments, and the Download History dialog disclosure.

**Finding:** The feature mandates an active `%(id)s` filename token, but `Downloads.SaveVideoInfo` remains optional and protected arguments do not currently add `--write-info-json`. Filename-only recovery can match a known native archive identity, but after both primary and backup ledgers are lost there is no authoritative extractor/provider key to pair with the filename ID. The implementation correctly refuses to guess YouTube (or any other extractor) merely from ID shape, so protected downloads made without info JSON can become unrebuildable after total ledger loss.

Current yt-dlp writes per-video `.info.json` before media transfer when `--write-info-json` is enabled; clean info JSON retains top-level `id`, `extractor_key` / `ie_key`, and thumbnail metadata used by A031. A failed media transfer leaving only metadata is not itself promoted because inventory enumerates completed-media candidates, not standalone JSON.

**Impact:** The application can advertise/prepare protected downloads whose native archive works normally until both ledger copies are lost, at which point the physical media tree no longer carries enough authoritative identity to reconstruct the archive safely. This conflicts with the feature's explicit rebuild design.

**Required acceptance:**

1. Every protected yt-dlp command must include app-owned `--write-info-json` in the protected suffix, independent of the general `SaveVideoInfo` preference.
2. Reject an explicit custom `--no-write-info-json` (including valid unambiguous abbreviations) while protection is enabled instead of silently overriding a conflicting request.
3. A user-supplied compatible `--write-info-json` remains allowed; duplicate positive flags are harmless.
4. Ambient configs/plugins/aliases must remain unable to turn the app-owned metadata requirement back off (preserve A007/A015 isolation).
5. Download History disabled behavior remains unchanged; the general metadata preference again controls whether info JSON is written.
6. Inventory/rebuild remains strictly non-destructive: forcing metadata applies only to new provider commands and never creates/rewrites sidecars while scanning an existing library.
7. Keep the default clean-info-json behavior; do not force comments or other large/private optional metadata beyond yt-dlp's normal per-video info JSON. The UI must disclose that protected downloads retain an info JSON sidecar for recovery.
8. Add regressions proving the protected suffix contains `--write-info-json`, explicit negative custom options are rejected, compatible positive use is allowed, and disabled mode does not force the sidecar.
9. Re-run the complete guarded Windows Debug/Release/full-regression gates.

- DH-A034: `520bf967b1e861d6884439f424b97a571cb73d9e` (`fix: retain authoritative metadata for protected recovery`), guarded workflow run `35462637348`, cleanup commit `1078bf97978945c0a98b5b582a553f602371cffe`.
- The guard showed `DOWNLOAD_HISTORY.RequiresAuthoritativeMetadataForRebuild` failing at baseline and passing after repair; the complete Download History regression set passed afterward.
- Evidence artifact `10589872709` has SHA-256 `d8d7598ec33ea9b4ccb46b9eb5d7f5f381a34e607327fb7ea9791bf013aa0231`.


### DH-A035 — Indexed multi-thumbnail sidecars can masquerade as unresolved media

**Priority / state:** Medium rebuild-availability risk / VERIFIED against current scanner and current yt-dlp multi-thumbnail naming.

**Affected code:** completed-media inventory and thumbnail-sidecar classification.

**Finding:** yt-dlp's `--write-all-thumbnails` names multiple thumbnail files by replacing the media extension with `<thumbnail-id>.<thumbnail-extension>`. For an output family rooted at `base`, this can produce `base.0.gif` while the authoritative metadata remains `base.info.json`. A031 correctly classifies same-stem thumbnail collisions, but an indexed thumbnail has no adjacent `base.0.info.json`. If its final extension is also in the media allowlist (GIF is the concrete current overlap), the scanner inventories it as a second media candidate. During total-loss Rebuild the real `base.mp4` recovers normally while `base.0.gif` is unresolved, so an otherwise fully recoverable library is reported Partial/Unsafe.

**Impact:** A legitimate yt-dlp sidecar can block archive reconstruction even though the actual completed media and authoritative metadata are intact. The sidecar itself is not unsafe; the bug is failing to associate its indexed thumbnail filename with the owning info JSON.

**Required acceptance:**

1. Preserve `--write-all-thumbnails`; do not solve this by banning a normal yt-dlp feature.
2. For a media-like candidate without its own adjacent info JSON, recognize it as a thumbnail sidecar only when a neighboring `base.info.json` contains a thumbnail whose exact `id` matches the inserted filename suffix and whose explicit `ext` (or URL-derived extension when `ext` is absent) matches the candidate extension.
3. Support dot-bearing media stems and thumbnail IDs by testing candidate dot boundaries rather than assuming a fixed suffix shape.
4. A candidate with its own adjacent authoritative info JSON remains a media candidate; do not let a neighboring file steal ownership.
5. A suffix/extension mismatch must remain unresolved rather than being silently ignored.
6. Keep inventory streaming/non-destructive and avoid a whole-library pre-index solely for this check.
7. Add regression coverage for an exact indexed GIF thumbnail sidecar and a near-match that must not be skipped; assert all media/sidecar bytes and paths remain unchanged.
8. Re-run the complete guarded Windows Debug/Release/full-regression gates.


- DH-A035: `774dc7d18d2cb9a3d1886f0fae4852326fb41eb4` (`fix: identify indexed thumbnail sidecars from metadata`), guarded workflow run `35462891907`, cleanup commit `45043b46462730ee924d3471d0f445016dfa6d2d`.
- The guard showed `DOWNLOAD_HISTORY.IgnoresIndexedThumbnailSidecars` failing at baseline and passing after repair; the complete Download History regression set passed afterward.
- Evidence artifact `10590227152` has SHA-256 `e7213be1e1645e8b621e764a751f73b03e179d8e8d4359119507470f97172f6d`.

- DH-A036: `501621859a6fcec647c9f7fc37d7f7f939c57573` (`fix: keep unsafe extensions outside protected mode`).
- DH-A037: `b840c812bce4b31a422ae7172e993550bf99e4f8` (`fix: reject arbitrary protected state mutation hooks`).
- Both were closed by guarded workflow run `35471396703`, cleanup commit `41b738f8eb7848ce14244038bb0ba5272b437cdc`. Baseline evidence showed both regressions failing; after A036 only the unsafe-extension regression passed; after A037 both passed and the complete Download History regression set was green.
- Evidence artifact `10592449476` has SHA-256 `bff68aae200ba089df7d04a55644d3f4da13ee3c0b46ebc26ce3a20335ba2156`.
- The first combined A036/A037 request was discarded after its A037 abbreviation check collided with safe exact `--print` / `--netrc`; the retry used yt-dlp's actual unique unsafe prefixes and preserved those safe controls.

- DH-A038: `322208dd62253b5ac1896920577de52859628e8a` (`fix: protect history state from cookie writeback`), closed by guarded workflow job `105974270309`, cleanup commit `e4df9a9d8716d80facc7e2d1bae9303fd4661033`.
- The guard showed `DOWNLOAD_HISTORY.RejectsCookieWritebackCollisions` failing at baseline and passing after repair; the complete Download History suite was green after repair.
- Evidence artifact `10593395373` has SHA-256 `0c4be533899a48339ad963cb099cf3615e7bd9cd5d7b95c1797beed3f3aca8cb`.
- Two prior A038 requests were discarded before production code because the new regression used C# null syntax unsupported by the audit harness compiler; only test syntax changed between retries.

- DH-A039: `90464658952eb010126a8b06ba099dccd5a1a8e3` (`fix: reject raw child-process arguments in protected mode`).
- DH-A040: `747df3a7bdb28e145b0bf998fb43de1b3752b342` (`fix: block destructive cache removal in protected mode`).
- Both were closed by guarded workflow job `105974742010`, cleanup commit `c63b966d5e6d04c7f32e6e92979640328dba27ca`. Baseline evidence showed both regressions failing; after A039 only the raw-child regression passed; after A040 both passed and the full Download History suite was green.
- Evidence artifact `10593375218` has SHA-256 `1d5c9c833d8efe5fb9164c8229baee2c7a8787efd471a58392b0ea18606d85dc`.

- DH-A041: `86682dc27da451ed49cf6714cd750792f93f7c74` (`fix: mirror yt-dlp cookie path expansion`).
- DH-A042: `220e45b65008fcbbb053eb8c4d0228808b25edb1` (`fix: contain protected filename schemas`).
- DH-A043: `a65c66faf350f9f187e36fe43af99febbe72542d` (`fix: reject NUL in protected custom arguments`).
- All three were closed by guarded workflow run `35477418322`, cleanup commit `87fc044b0244a2f4eb1f355d4329bc3c4173111b`. The final run proved the three targeted Download History regressions passing and completed the full guarded Windows build/regression gates.
- Evidence artifact `10594472660` has SHA-256 `7fb260e24e968d26c69555bde9c5eb1184e45f5824aa60fae038e29f358d0a74`.
- Several A042 requests were deliberately discarded while the regression was refined against yt-dlp's actual percent/dollar expansion semantics; no discarded production result was accepted. A separate updater rollback regression was observed as flaky and added to the guarded-run allowlist.



**Terminal A044-A046 closure:** Isolated guarded run `35501227990` closed the three reopened terminal gaps while leaving A049 explicitly listed as the only remaining expected Download History failure.

- DH-A044: `be541804fe5f19cdf292c8671f460174f2e3350b` (`fix: close yt-dlp test abbreviation gap`) blocks the current provider's shortest unambiguous `--te` spelling.
- DH-A045: `8533e58d67b2cfcb5d49851959dfd81b020df930` (`fix: close ffmpeg location abbreviation gap`) blocks current yt-dlp's unambiguous `--ffmpeg` abbreviation.
- DH-A046: `ba18fa0558a0d0a5ccae85c6f7310148ba418e05` (`fix: preserve exact protected path expansion`) replaces the fixed U+E000 sentinel with a per-call random sentinel and preserves single-quoted sigil semantics when passing the resolved archive path back to yt-dlp.
- Cleanup: `d7a5b6516b7a1bb8de863d71f250eec30e0ad144`.
- Evidence artifact: `10602940660`, SHA-256 `766ddfd2268e306b959624c0c562ad50f04fa5dbf2ae564c025d10a5bf5cb0eb`.
- Provider-version check for DH-A046: this application's updater selects the official friendly-name asset `yt-dlp.exe` rather than `yt-dlp_arm64.exe`. The official yt-dlp `2026.08.19` stable Windows x64 build job `96261378230` ran CPython 3.10.11. CPython 3.10.11 `ntpath.expandvars` preserves single-quoted text without environment expansion, matching the repaired protected-path logic. This distinction matters because newer ARM64/Python branches use different `expandvars` internals.

Guarded repair run `35500105864` landed the first A044-A048 repair set. Terminal current-head re-audit keeps A047-A048 closed but reopens A044-A046 for narrower provider-equivalence misses documented above; DH-A049 is newly verified.

- DH-A044: `bcd50f33d6f5121ac3e0506f58ae95fd17423085` (`fix: reject partial yt-dlp test downloads under history`).
- DH-A045: `9ca0322015581a763b37f71294d3117bbd2e23fe` (`fix: keep executable overrides outside protected mode`).
- DH-A046: `e3533d15eb748d3736c587081f2a6d0bd970b983` (`fix: mirror yt-dlp protected path expansion`).
- DH-A047: `f5baeeda556ddaecf85449e959c1d6126d2bb0fc` (`fix: establish protected isolation before custom arguments`).
- DH-A048: `5e42efda850fe8d9df7616e01a2a7fcb05a0a1d4` (`fix: validate native archive as strict UTF-8`).
- All five were closed by guarded workflow run `35500105864`, cleanup commit `695cbd636b951e584b0553512a84ef47544f65ca`. The guard proved all five targeted regressions failing at baseline and passing after their respective conceptual repairs while completing the Windows Debug solution, Release updater, Release application, and complete AuditRegression gates after each repair.
- Retained evidence artifact `10601861852` has SHA-256 `cffa24ea476e3f5ffca607255149f02373f831f646bec7c2d91c66a0d414462e`.
- The first strict-UTF-8 pass correctly exposed that older regression fixtures used .NET's BOM-emitting `Encoding.UTF8` rather than yt-dlp's native BOM-less UTF-8. Test-only commit `462068002eb7cc66755b36650b1199a7937d29dd` corrected those fixtures without weakening production validation; its normal Windows Audit build `35500058966` passed before the final guarded retry.
- Terminal regression commit `dae139b9fe5cef205f5390b0ab28d015d6e607bb` has a green normal Audit build (`35500608800`). Full verification `35500608781` fails on exactly four Download History cases: A044, A045, A046, and A049. All other Download History cases pass; the Release packaging step also completes.

### DH-A036 — Unsafe-extension compatibility can escape protected output assumptions

**Priority / state:** High protected-output and rebuild-integrity risk / VERIFIED against current app filtering and current yt-dlp extension-safety code.

**Affected code:** protected custom-argument compatibility filtering and app-owned protected suffix.

**Finding:** Current yt-dlp exposes `--compat-options allow-unsafe-ext`. Its validation explicitly sets the global unsafe-extension guard off; upstream warns that this opens the user to attacks. With the guard disabled, extension values containing normally rejected extensions or even path separators are no longer rejected. Download History relies on an app-owned `%(ext)s` output filename inside a validated namespace and on an allowlisted completed-media scanner. Protected mode currently allows this compatibility option.

**Impact:** A protected command can create output outside the scanner's known media-extension set and, with malicious extractor extension data, can defeat the assumed filename/path boundary. The native archive may still record the identity, while later total-loss Rebuild cannot see the media reliably.

**Required acceptance:**

1. Reject a custom compatibility-option sequence whose final effective state enables `allow-unsafe-ext`.
2. Preserve safe compatibility requests, including yt-dlp's `youtube-dl` / `youtube-dlc` aliases, which explicitly remove `allow-unsafe-ext`.
3. Respect ordered/comma-separated enable/disable semantics: a later `-allow-unsafe-ext` or `-all` can make an earlier request safe; a later positive request makes it unsafe again.
4. App-owned protected arguments must append `--compat-options -allow-unsafe-ext` as defense in depth against current/future option expansion after the custom string.
5. Download History disabled behavior remains unchanged.
6. Add regressions for direct enable, `all`, inverted compatibility aliases, safe aliases, and explicit later disable.
7. Re-run the complete guarded Windows Debug/Release/full-regression gates.

### DH-A037 — Arbitrary custom mutation hooks can tamper with protected state

**Priority / state:** High protected-state integrity risk / VERIFIED against current app filter and current yt-dlp execution/write paths.

**Affected code:** `TryGetArchiveArguments` custom-argument safety filter.

**Finding:** `--print-to-file [WHEN:]TEMPLATE FILE` evaluates a template and opens the resulting arbitrary path in append mode. A user-supplied file target can therefore be the live native archive, allowing a syntactically valid forged record to be appended during the provider process. Because a forged valid record is a superset, the post-run archive/floor validation can accept it as newly learned history. Separately, `--netrc-cmd` is implemented as `Popen.run(..., shell=True)`, making it a direct arbitrary command-execution hook comparable to the already-blocked `--exec`.

**Impact:** Protected mode can be prepared and leased correctly, yet the yt-dlp process itself can mutate the protected ledger or arbitrary state through allowed custom hooks, invalidating the meaning of the pre-start integrity checks.

**Required acceptance:**

1. Reject custom `--print-to-file` while Download History is enabled; ordinary `--print` without a file target remains available.
2. Reject custom `--netrc-cmd` while Download History is enabled; static netrc/cookie/authentication mechanisms are not blocked by this repair.
3. Long-option abbreviations accepted by yt-dlp must not bypass the checks.
4. Keep the existing `--exec`, plugin, alias/config, source/extractor, output/path, and metadata protections intact.
5. Download History disabled behavior remains unchanged.
6. Add regressions for both mutation hooks and safe neighboring controls.
7. Re-run the complete guarded Windows Debug/Release/full-regression gates.

### DH-A038 — Cookie-file writeback can overwrite protected archive state

**Priority / state:** High protected-ledger integrity risk / VERIFIED against current app source and current yt-dlp cookie lifecycle.

**Affected code:** protected custom-argument validation, `ProviderAuthenticationConfig` integration in standard/extended download argument generation, and protected archive path handling.

**Finding:** yt-dlp's `--cookies FILE` option is explicitly a read/write cookie jar. `YoutubeDL.close()` calls `save_cookies()`, and `YoutubeDLCookieJar.save()` opens the configured cookie file for write/truncate. The protected argument filter currently permits custom `--cookies`, and the app's Authentication settings can also emit `--cookies <CookiesFile>` through its trusted temporary config. Neither path checks whether the cookies file resolves to the live Download History primary archive, `.bak`, or `.lock` path.

**Impact:** If the configured cookie file collides with protected state, yt-dlp can truncate/replace that state after the application's pre-start archive validation. The process may have already consumed the correct native archive, but the durable ledger can be destroyed at shutdown; with backup retention off, a crash/restart can lose the only on-disk copy.

**Required acceptance:**

1. Resolve cookie-file paths using Windows/current-process path semantics and compare them case-insensitively to the prepared archive, its `.bak`, and `.lock` companions.
2. Reject only colliding cookie paths; ordinary custom and app-configured cookie files remain supported.
3. Validate raw custom `--cookies FILE` / `--cookies=FILE` before protected execution publication.
4. Validate the app Authentication `CookiesFile` against the actual prepared execution archive before creating the temporary trusted auth config, in both standard and extended download paths.
5. Keep `--cookies-from-browser` supported because yt-dlp does not use it as a writeback filename.
6. Download History disabled behavior remains unchanged.
7. Add regressions for primary, backup, and lock collisions plus a non-colliding cookie control and source-level wiring for both standard/extended authentication paths.
8. Re-run the complete guarded Windows Debug/Release/full-regression gates, then continue the terminal write-target audit.


### DH-A039 — Raw child-process arguments escape the protected write boundary

**Priority / state:** High protected-state integrity risk / VERIFIED against current app filtering and current yt-dlp subprocess argument plumbing.

**Affected code:** protected custom-argument validation in `TryGetArchiveArguments`.

**Finding:** yt-dlp accepts `--postprocessor-args` / `--ppa` and passes the parsed payload directly to ffmpeg/ffprobe/AtomicParsley positions. It likewise accepts `--downloader-args` / `--external-downloader-args` and passes those arguments to external downloader processes. Those child programs expose their own arbitrary file-output switches (for example ffmpeg progress/report targets or downloader trace/log/output controls). Download History currently validates yt-dlp's direct write/exec hooks but does not sandbox these nested command lines.

**Impact:** A command can pass every current protected-mode check, then a child tool invoked by yt-dlp can overwrite/append the native archive, backup, lock, or other protected state outside the application's pre/post validation assumptions.

**Required acceptance:**

1. Reject custom `--postprocessor-args` and alias `--ppa` while Download History is enabled.
2. Reject custom `--downloader-args` and alias `--external-downloader-args` while Download History is enabled.
3. Keep built-in postprocessing controls such as `--remux-video`, `--recode-video`, and `--split-chapters` available.
4. Keep ordinary external-downloader selection available; this repair targets unsandboxed raw child arguments, not downloader choice itself.
5. Long-option abbreviations accepted by yt-dlp must not bypass the long-form checks; aliases must be handled explicitly.
6. Download History disabled behavior remains unchanged.
7. Add regressions for all four names/aliases plus safe built-in controls.
8. Re-run the complete guarded Windows Debug/Release/full-regression gates.

### DH-A040 — Cache-removal options can recursively delete a protected library tree

**Priority / state:** High destructive-data risk / VERIFIED against current app filtering and current yt-dlp `Cache.remove()`.

**Affected code:** protected custom-argument validation.

**Finding:** yt-dlp lets the user choose `--cache-dir DIR` and invoke `--rm-cache-dir`. `Cache.remove()` resolves the chosen root and calls `shutil.rmtree(cachedir)` if the pathname merely contains `cache` or `tmp`. Protected mode currently permits both options. A user can therefore point the cache root at a download/library tree whose path contains one of those substrings and have yt-dlp recursively delete it before/alongside the requested download.

**Impact:** This bypasses the feature's strict non-destructive media-tree requirement more severely than an archive-only mutation: existing media and sidecars can be recursively removed.

**Required acceptance:**

1. Reject `--rm-cache-dir` while Download History is enabled. Cache removal is a maintenance action and is not required for a protected download.
2. Keep ordinary `--cache-dir` available when no removal is requested.
3. yt-dlp long-option abbreviation behavior must not bypass the removal check.
4. Download History disabled behavior remains unchanged.
5. Add a regression proving the destructive pair is rejected while a cache-directory selection alone remains allowed.
6. Re-run the complete guarded Windows Debug/Release/full-regression gates.


### DH-A041 — Cookie collision normalization does not match yt-dlp dollar-variable expansion

**Priority / state:** High protected-ledger integrity risk / VERIFIED against current A038 source, yt-dlp `expand_path`, and CPython Windows `ntpath.expandvars`.

**Finding:** A038 normalizes cookie paths with .NET `Environment.ExpandEnvironmentVariables`, which expands Windows `%NAME%` syntax, then handles `~`. yt-dlp calls Python `expand_path`, whose Windows implementation also expands `$NAME` and `${NAME}`. A custom cookie path such as `$ARCHIVE_COOKIE` can therefore compare as a harmless literal in the app but resolve to the native archive inside yt-dlp.

**Required acceptance:** match Windows yt-dlp expansion for `%NAME%`, `$NAME`, and `${NAME}`; unknown variables remain unchanged; apply the same normalization to custom and app-auth cookie checks; add regressions for both dollar forms plus a non-colliding variable.

### DH-A042 — Filename-schema directory components can escape the active download root

**Priority / state:** High protected-output boundary risk / VERIFIED against current standard/extended output construction and current yt-dlp template/path sanitization.

**Finding:** Both downloaders append the configured filename schema beneath the active download directory. Protected validation requires an active `%(id)s` token and rejects quotes/control characters, but output containment is not guaranteed. There are three proven paths to a parent component: (a) literal `..` / `../`; (b) yt-dlp expands environment variables in the output template before metadata substitution, so a directory component containing `$VAR`, `${VAR}`, or `%VAR%` can inject `..` and separators; and (c) metadata values are filename-sanitized before path normalization, but the exact value `..` remains `..`, so a dynamic-only component such as `%(uploader)s` can become a parent traversal. yt-dlp then calls Windows `sanitize_path`, whose `..` handling pops the previous component.

**Required acceptance:**

1. Reject literal `..` path components in a protected filename schema.
2. Reject active environment-variable expansion syntax anywhere in the protected schema because expansion occurs before path sanitization and can inject separators. Mirror yt-dlp's actual `_outtmpl_expandpath` escape behavior: even percent runs before `%VAR%` are non-expanding; for dollar variables, a single dollar or an even run can still expand, while an odd run of at least three dollars remains literal.
3. Reject a directory component containing active yt-dlp metadata placeholders whenever its static literal portion could still let the final sanitized component become exactly `..` (no literal anchor, or only one/two literal dots). Preserve non-dot anchors such as `creator-%(uploader)s` and dot-only anchors of length three or more, which yt-dlp sanitizes as ordinary names rather than parent traversal.
4. Keep ordinary static nested directories and the safe anchored dynamic forms above available.
5. The final filename component remains governed by the existing ID-template and argument-boundary checks; do not impose the dynamic-directory rule on the filename itself.
6. Apply both slash forms and preserve Download History disabled behavior.
7. Add regressions for literal traversal, dynamic-only `%(uploader)s`, dot-only+dynamic components, active environment expansion, safe static nested directories, and safe anchored dynamic directories.
8. Re-run the complete guarded Windows Debug/Release/full-regression gates.

### DH-A043 — Embedded NUL can truncate the Windows command line before protected suffix arguments

**Priority / state:** High protected-argument boundary risk / VERIFIED from current raw custom-argument construction and Windows NUL-terminated command-line semantics.

**Finding:** Custom arguments are appended before the app-owned Download History suffix. The protected validator checks quote balance and unsafe options but does not reject U+0000. Windows process creation consumes a NUL-terminated command-line buffer; an embedded NUL in the custom string terminates parsing before the later `--download-archive`, plugin/config isolation, and concat safeguards.

**Required acceptance:** reject NUL in custom arguments before preparation/execution publication; do not unnecessarily reject ordinary whitespace/newlines already handled by tokenization; disabled behavior unchanged; add a regression proving the suffix cannot be truncated.


### DH-A044 — Test mode records a partial sample as a completed native identity

**Priority / state:** High duplicate-prevention correctness risk / VERIFIED against current yt-dlp downloader and archive-write flow.

**Finding:** yt-dlp's hidden \`--test\` downloader mode intentionally limits media acquisition to a small sample (for HTTP, the downloader uses \`_TEST_FILE_SIZE\`; FFmpeg test mode also applies a file-size cap). A successful test-mode download still follows the normal success/postprocessing path and sets \`__write_download_archive = True\`. Download History currently permits custom \`--test\`.

**Impact:** A protected test invocation can add the source's native extractor+ID to the archive after only a sample is saved. Later normal protected runs then skip the full media as already downloaded.

**Required acceptance:**

1. Reject \`--test\` and its unambiguous long abbreviation while Download History is enabled.
2. Do not block unrelated format checking/probing mechanisms that do not create a false completed archive record.
3. Download History disabled behavior remains unchanged.
4. Add regression coverage and rerun the complete guarded Windows gates.

**Terminal re-audit:** Initial repair `bcd50f33d6f5121ac3e0506f58ae95fd17423085` used `--tes` as the minimum blocked prefix. Current yt-dlp exposes no competing `--te...` option, so Python optparse accepts `--te` as an unambiguous abbreviation of `--test`. Regression commit `dae139b9fe5cef205f5390b0ab28d015d6e607bb` extends the test to `--te`; Windows verification run `35500608781` fails only at the intended protected rejection for this test family. A follow-up repair must block `--te` without widening unrelated option rejection.

### DH-A045 — Path-qualified executable/runtime overrides escape the protected process boundary

**Priority / state:** High execution-integrity risk / VERIFIED against current yt-dlp external downloader, FFmpeg, JS runtime, and updater code.

**Finding:** Protected mode blocks explicit exec/plugin/raw-child-argument hooks, but still permits options that select executable paths or replace/restart the provider. Current yt-dlp accepts a downloader name **or executable path**; supported downloader classes validate then execute that selected path. \`--ffmpeg-location\` accepts an executable or containing directory. \`--js-runtimes runtime:path\` passes an explicit executable path into runtime discovery/execution. \`-U/--update\` and \`--update-to\` may replace/restart yt-dlp, with \`--update-to\` accepting alternate update channels/repositories.

**Required acceptance:**

1. Reject custom \`-U\`, \`--update\`, and \`--update-to\` while protected; allow \`--no-update\`.
2. Reject custom \`--ffmpeg-location\` while protected; the app-owned verified FFmpeg location remains available.
3. For \`--downloader\` / \`--external-downloader\`, allow only known built-in downloader names (including protocol-prefixed forms and \`native\`); reject path-qualified/arbitrary executable values.
4. For \`--js-runtimes\`, allow supported bare runtime names without an explicit path and keep \`--no-js-runtimes\`; reject \`runtime:path\`.
5. Preserve Download History disabled behavior and add neighboring safe controls.
6. Rerun the complete guarded Windows gates.

**Terminal re-audit:** Initial repair `9ca0322015581a763b37f71294d3117bbd2e23fe` required the `--ffmpeg-` prefix. Current yt-dlp exposes `--ffmpeg-location` as the only `--ffmpeg...` option, so `--ffmpeg` is accepted unambiguously and still bypasses protected rejection. Regression commit `dae139b9fe5cef205f5390b0ab28d015d6e607bb` proves the bypass in Windows run `35500608781`.

### DH-A046 — Download/archive path expansion does not mirror yt-dlp

**Priority / state:** High path-boundary and rebuild-consistency risk / VERIFIED against current app output construction and current yt-dlp \`expand_path\` / \`_outtmpl_expandpath\`.

**Finding:** The app passes \`Downloads.downloadPath\` into the yt-dlp output template, where yt-dlp expands user-home and environment-variable syntax before metadata substitution. Download History resolves the same active root with .NET \`Environment.ExpandEnvironmentVariables\`, which handles \`%NAME%\` but not yt-dlp's \`$NAME\`, \`\${NAME}\`, or \`~\` semantics. Direct archive paths have the same mismatch because yt-dlp applies \`expand_path\` to \`--download-archive\`.

**Impact:** The provider can place media or its archive at a different physical path from the one protected management, locking, inventory, and rebuild logic believes it owns.

**Required acceptance:**

1. Resolve the active download root using the same Windows yt-dlp output-template user/env expansion semantics before history inventory/default-archive decisions.
2. Resolve configured archive paths using yt-dlp direct \`expand_path\` semantics.
3. When passing the already-resolved app-owned archive path back to yt-dlp, quote/escape literal \`%\` / \`$\` so yt-dlp resolves to the exact locked path rather than performing a second expansion.
4. Reject metadata-template fields inside \`Downloads.downloadPath\` while protection is enabled because a single dynamic active inventory root cannot represent per-entry output roots safely.
5. Keep additional user-selected scan-only roots as ordinary app filesystem paths; they are not yt-dlp output templates.
6. Apply the same normalization in the Download History dialog's implicit/custom archive comparisons.
7. Add regressions for \`$VAR\`, \`\${VAR}\`, \`%VAR%\`, \`~\`, literal escaped sigils, and a dynamic-template active root.
8. Rerun the complete guarded Windows gates.

**Terminal re-audit:** The initial repair `e3533d15eb748d3736c587081f2a6d0bd970b983` has two provider-equivalence gaps:

- `ExpandYtDlpOutputTemplateEnvironmentPath` uses fixed sentinel U+E000 and strips every occurrence after expansion. A legitimate Windows path containing U+E000 is therefore changed, while yt-dlp uses a fresh random separator specifically to avoid a fixed user-path collision.
- `EscapeYtDlpLiteralPathForArgument` doubles every `### DH-A046 — Download/archive path expansion does not mirror yt-dlp

**Priority / state:** High path-boundary and rebuild-consistency risk / VERIFIED against current app output construction and current yt-dlp \`expand_path\` / \`_outtmpl_expandpath\`.

**Finding:** The app passes \`Downloads.downloadPath\` into the yt-dlp output template, where yt-dlp expands user-home and environment-variable syntax before metadata substitution. Download History resolves the same active root with .NET \`Environment.ExpandEnvironmentVariables\`, which handles \`%NAME%\` but not yt-dlp's \`$NAME\`, \`\${NAME}\`, or \`~\` semantics. Direct archive paths have the same mismatch because yt-dlp applies \`expand_path\` to \`--download-archive\`.

**Impact:** The provider can place media or its archive at a different physical path from the one protected management, locking, inventory, and rebuild logic believes it owns.

**Required acceptance:**

1. Resolve the active download root using the same Windows yt-dlp output-template user/env expansion semantics before history inventory/default-archive decisions.
2. Resolve configured archive paths using yt-dlp direct \`expand_path\` semantics.
3. When passing the already-resolved app-owned archive path back to yt-dlp, quote/escape literal \`%\` / \`$\` so yt-dlp resolves to the exact locked path rather than performing a second expansion.
4. Reject metadata-template fields inside \`Downloads.downloadPath\` while protection is enabled because a single dynamic active inventory root cannot represent per-entry output roots safely.
5. Keep additional user-selected scan-only roots as ordinary app filesystem paths; they are not yt-dlp output templates.
6. Apply the same normalization in the Download History dialog's implicit/custom archive comparisons.
7. Add regressions for \`$VAR\`, \`\${VAR}\`, \`%VAR%\`, \`~\`, literal escaped sigils, and a dynamic-template active root.
 / `%`. Windows Python `ntpath.expandvars` leaves text inside single quotes unexpanded; doubling a sigil inside a single-quoted path segment therefore changes the physical path instead of protecting it.

Regression commit `dae139b9fe5cef205f5390b0ab28d015d6e607bb` adds both equivalence cases. Windows run `35500608781` reaches and fails the U+E000 case first; the follow-up repair must also make the quote-aware round trip pass in the same conceptual repair.

### DH-A047 — A dangling value-taking custom option can consume the first protected suffix token

**Priority / state:** High protected-argument boundary risk / VERIFIED from current argument ordering and Python optparse value consumption.

**Finding:** Standard and extended builders append raw custom arguments immediately before the app-owned Download History suffix. An otherwise allowed option that requires a value can be left dangling (for example \`--proxy\`); Python's option parser consumes the next argv token as that value even when it begins with \`--\`. The first protected token (\`--ignore-config\`) can therefore be consumed and no longer acts as an option.

**Required acceptance:**

1. Establish the non-negotiable config/plugin isolation prefix before user custom arguments in standard and extended protected commands.
2. Retain the full app-owned suffix after custom arguments so archive/info-json/concat/compat protections remain authoritative after ordinary user options.
3. Handle standard custom/mostly-custom and extended custom paths, including any buffer replacement.
4. Add a regression with a dangling one-value option proving isolation exists before custom input and the protected suffix remains after it.
5. Download History disabled behavior remains unchanged.
6. Rerun the complete guarded Windows gates.

### DH-A048 — Native archive decoding is more permissive than yt-dlp

**Priority / state:** High fail-closed ledger-format risk / VERIFIED against current \`TryReadArchive\` and current yt-dlp archive loading.

**Finding:** yt-dlp opens its archive as strict UTF-8 text and strips each line. The app currently calls \`File.ReadAllLines(path)\`, whose StreamReader defaults may detect UTF BOMs (including UTF-16) and use replacement decoding rather than strict UTF-8. A UTF-8 BOM can likewise be hidden from app validation even though yt-dlp treats its leading U+FEFF as part of the first archive record.

**Impact:** Download History can report an archive healthy, prepare/lock it, and then hand yt-dlp bytes that represent different strings or fail native decoding. Duplicate prevention is no longer based on the ledger the app actually validated.

**Required acceptance:**

1. Read archive bytes/lines with strict UTF-8 decoding, no BOM auto-detection, and fail on invalid byte sequences.
2. Reject a leading UTF-8 BOM/UTF-16 BOM as non-native archive content rather than silently normalizing it.
3. Keep blank-line handling and exact native \`extractor id\` validation.
4. Stream validation rather than materializing the entire file where practical.
5. Add regressions for plain UTF-8, UTF-8 BOM, UTF-16, and malformed UTF-8; protected prep must fail closed for the latter three.
6. Rerun the complete guarded Windows gates.

### DH-A049 — Same-scan recovered identities are unavailable to derivative filename recovery after total archive loss

**Priority / state:** High rebuild correctness risk / VERIFIED against current scanner/matcher construction and current yt-dlp split-chapter / retained-format behavior.

**Finding:** `AnalyzeCore` constructs one immutable `FileNameIdentityMatcher` from the archive identities available **before** scanning. During an explicit rebuild, authoritative parent identities recovered from adjacent `.info.json` are added only to `RecoveredEntries`; the already-built matcher never learns them. With a completely missing archive, the matcher is empty for the entire scan.

Current yt-dlp legitimately creates completed derivative media that does not receive its own adjacent info JSON. `--split-chapters` writes chapter files using a template that includes the parent `%(id)s`. With retained source/merge components, yt-dlp can also preserve files named with `.f<format_id>.<ext>`. The existing matcher can recognize those filenames **when the native identity is already known**, but after total archive loss it cannot use the identity recovered from the canonical parent in the same scan.

**Impact:** A fully recoverable library can report `Partial` and refuse archive reconstruction solely because valid derivatives of an authoritatively identified parent are encountered after total ledger loss. This is fail-safe, not destructive, but it defeats the required rebuild path for supported yt-dlp output modes.

**Required acceptance:**

1. During explicit recovery states, retain unresolved non-thumbnail media candidates until authoritative same-scan identities have been collected.
2. Build one indexed matcher from the union of pre-existing archive entries and same-scan authoritative recoveries, then classify only the deferred candidates; do not rescan the filesystem.
3. Keep unknown/unrelated media unresolved. A derivative may match only a native identity already available from the ledger or authoritative metadata; do not infer providers or identities from filename shape alone.
4. Preserve the existing thumbnail ambiguity safeguards and normal valid-archive retry semantics.
5. Preserve A005 large-library behavior: one filesystem walk, one additional matcher construction, and memory proportional only to unresolved candidates rather than the full media tree.
6. Inventory/rebuild remains byte-for-byte non-destructive to media and sidecars.
7. Add a total-loss regression containing a canonical parent with authoritative metadata plus split-chapter and retained-format derivatives, and a fail-closed control containing unrelated media.
8. Rerun the complete guarded Windows Debug/Release/full-regression gates.

**Baseline proof:** Regression commit `dae139b9fe5cef205f5390b0ab28d015d6e607bb` produces `DOWNLOAD_HISTORY.RebuildsDerivedMediaAfterTotalArchiveLoss: Expected [Healthy]; actual [Partial]` in Windows verification run `35500608781`.

**Discarded first repair wave:** Guarded run `35500914221` proved the terminal A044, A045, and A046 repairs individually satisfy their target regressions, then rejected the proposed A049 repair. The A049 change made `DOWNLOAD_HISTORY.RebuildsDerivedMediaAfterTotalArchiveLoss` pass but regressed `DOWNLOAD_HISTORY.IgnoresIndexedThumbnailSidecars` from the required fail-closed `Partial` state to `Missing`. No production commits from that failed batch were pushed; repair request `b15e5c9592200b2230dae0624c2076d8ce8dba1` was explicitly discarded by `531dbc64fa1c8462ad65e4824886bd7c9d045bb5`. The same run passes every other Download History regression except the three deliberately reopened terminal cases A044-A046; normal Audit build run `35500608800` succeeds.

**Closure:** The regression was strengthened by `01d922df901a842169143dd4ff753795eaa35631` to cover component-only retained formats and an unselected-format fail-closed control. Guarded run `35508998096` then landed `ec0a1e808ab1564926a4dfb219263c0f3a9c702c` (`fix: recover proven same-scan derivative media`) and cleanup `39d0cf98bec9969abde87f9d6ee9b2924e54d7ca`. The accepted repair defers only derivative-shaped candidates, requires adjacent authoritative metadata to prove retained format IDs, performs no second filesystem walk, and preserves the indexed-thumbnail mismatch as unresolved. Evidence artifact `10604743486` has SHA-256 `e03513b000e7b54603651ed3c69c975685e38d19ae305d0ca158d4dee2486447`.

### DH-A050 — Error-suppression controls can create false completed archive records

**Priority / state:** High duplicate-prevention correctness risk / VERIFIED against current yt-dlp postprocessing and no-format flows.

**Finding:** Protected mode currently allows `-i/--ignore-errors` and `--ignore-no-formats-error`. With full `--ignore-errors`, yt-dlp catches a `PostProcessingError`, records an error return code, but returns the info dictionary to `process_info`; execution then reaches `__write_download_archive = True`. Separately, `--ignore-no-formats-error` converts a no-format condition into an empty format placeholder, permits sidecar-only processing to continue, and can likewise reach the normal archive-write success path without a completed requested media file.

**Impact:** A protected run can add the source extractor+ID to the native archive even though the requested postprocessed artifact failed or no downloadable media existed. Later normal protected runs can then skip that source as already downloaded.

**Required acceptance:**

1. Reject `-i`, `--ignore-errors`, and current unambiguous long abbreviations while Download History is enabled.
2. Reject `--ignore-no-formats-error` and current unambiguous long abbreviations while Download History is enabled.
3. Preserve fail-closed neighboring controls: `--abort-on-error`, `--no-abort-on-error` (download-only continuation), `--no-ignore-errors`, and `--no-ignore-no-formats-error`.
4. Download History disabled behavior remains unchanged.
5. Add regression coverage for the unsafe aliases/abbreviations and safe neighboring controls.
6. Rerun the complete guarded Windows Debug/Release/full-regression gates.

**Baseline proof:** Test commit `9af0e2c510f10102d4c3d59016ffd7d0e71c2165` adds `DOWNLOAD_HISTORY.RejectsFalseCompletionErrorControls`. Windows Audit build `35509192480` succeeds, while verification run `35509192471` fails on exactly that Download History regression: expected protected rejection, actual acceptance.

**Closure:** Guarded run `35509324524` landed `f0625eb0970178dae0088db5ab046f8a3f599d4d` (`fix: reject false-completion error controls`) and cleanup `a78deb0c49e1dfaf527bf11d075e393228562c28`. The accepted filter blocks `-i/--ignore-errors` and `--ignore-no-formats-error`, including current unambiguous abbreviations, while preserving the fail-closed neighboring controls and disabled-history behavior. Evidence artifact `10604828868` has SHA-256 `178c5cf4d5d8d872695f6112f3cdb3884c2195a64110e97aa4276962f83a5dc1`.

### DH-A051 — Fragment skipping can archive incomplete fragmented media

**Priority / state:** High duplicate-prevention correctness risk / VERIFIED against current yt-dlp fragment downloader behavior and both app argument builders.

**Finding:** Current yt-dlp defaults `skip_unavailable_fragments` to enabled for fragmented media. When a nonfatal DASH/HLS fragment cannot be downloaded, the native fragment downloader logs that the fragment is skipped, continues assembling the remaining fragments, returns success for the non-empty output, and the normal `process_info` path can set `__write_download_archive = True`. The app also normally emits `--skip-unavailable-fragments` when its Skip Unavailable Fragments setting is enabled, which is the stored default.

**Impact:** A protected download can permanently record the source extractor+ID after producing media with missing fragments. Later protected runs can then skip that source as already downloaded, preventing an automatic retry that could produce the complete artifact.

**Required acceptance:**

1. Reject custom `--skip-unavailable-fragments` and alias `--no-abort-on-unavailable-fragments`, including current accepted long abbreviations, while Download History is enabled.
2. Preserve `--abort-on-unavailable-fragments` and `--no-skip-unavailable-fragments` as compatible fail-closed controls.
3. Append a final app-owned `--abort-on-unavailable-fragments` to the protected suffix so it overrides yt-dlp's default and the app's earlier standard/extended fragment-skip setting.
4. Preserve the existing app setting and disabled-history behavior outside protected runs; do not globally change normal downloader defaults.
5. Add regression coverage for custom aliases/abbreviations and for both standard and extended argument builders with fragment skipping enabled, proving the final protected abort directive occurs after the earlier skip directive.
6. Rerun the complete guarded Windows Debug/Release/full-regression gates.

**Baseline proof:** Test commit `d79e4856a455e5288b093538eb58389dbd0c20ea` adds `DOWNLOAD_HISTORY.ForcesCompleteFragmentDownloads`. Windows Audit build `35509561195` succeeds, while verification run `35509561205` fails on exactly that Download History regression: expected protected rejection, actual acceptance.

**Closure:** Guarded run `35509731703` landed `6eed8f61e1c606e48d8081f3e5d05d2d73240951` (`fix: require complete fragmented media under history`) and cleanup `b008e8abf59e3b7e92c3fc821e3e19a7dcdd3646`. The accepted repair rejects the two fragment-skip spellings/abbreviations and appends a final `--abort-on-unavailable-fragments`, so both standard and extended builders remain fail-closed even when their earlier setting requests skipping. Evidence artifact `10605135313` has SHA-256 `b4b685a5995ce07a05b2107bbd4ec50732d5d582f25ebfbdee744e5cf175ccad`.

### DH-A052 — Retained-format component proof is too narrow and can be bypassed by generic derivative recovery

**Priority / state:** High rebuild correctness/integrity risk / VERIFIED against the accepted A049 repair and current yt-dlp format-ID construction.

**Finding:** A049 recognizes retained merge/source components with a hard-coded `[A-Za-z0-9_-]+` suffix. Current yt-dlp can construct `format_id` directly from HLS group/name strings, and retained component filenames insert the resulting `f<format_id>` without that alphanumeric restriction; Windows-valid IDs can therefore contain spaces, dots, or parentheses and remain unrecoverable after total archive loss. Conversely, A049 also treats any alphanumeric `.f...` filename as a generic same-scan derivative. If a canonical parent has already recovered the source identity, an unselected `.f999` file containing that source ID can then be promoted by filename match even when adjacent metadata explicitly says only another format was selected.

A second proof boundary exists when `requested_formats` is present: it is the explicit selected-format list. Falling back to a conflicting combined top-level `format_id` after `requested_formats` does not contain the candidate can promote a component that authoritative selection metadata excludes.

**Impact:** Rebuild can either fail on a legitimate retained component or, more seriously, accept a media-like component that the authoritative owner metadata does not prove belongs to the completed download.

**Required acceptance:**

1. Retained `.f<format_id>` files must never use generic same-scan filename deferral; they recover only from adjacent authoritative owner metadata that proves the exact format ID.
2. Support the full filename-valid format ID string produced by yt-dlp rather than limiting it to alphanumeric/underscore/hyphen.
3. If multiple `.f` split points could identify different adjacent owner metadata, accept only an unambiguous native identity.
4. When `requested_formats` exists, treat its exact `format_id` values as authoritative; do not fall back to a conflicting combined top-level `format_id`.
5. Preserve split-chapter same-scan recovery, ordinary parent metadata recovery, unknown-media fail-closed behavior, and the one-filesystem-walk performance property.
6. Keep every media/sidecar byte and path unchanged.
7. Add regression coverage for punctuation-bearing valid format IDs, an unselected component beside a valid canonical parent, and inconsistent `requested_formats` versus top-level `format_id`.
8. Rerun the complete guarded Windows Debug/Release/full-regression gates.

**Baseline proof:** Test commit `ebf652c122833e3bdbfaa52a4de9bcd0e488791d` adds `DOWNLOAD_HISTORY.ValidatesRetainedFormatComponents` and makes the earlier retained-component fixture carry realistic selected-format metadata. Windows Audit build `35509901729` succeeds, while verification run `35509901740` fails only the new retained-component regression (`Healthy` expected, `Unsafe` actual on a valid punctuation-bearing component).

**Rejected first repair attempt:** Guarded run `35510062534` applied the proposed A052 source change in its temporary workspace and made `DOWNLOAD_HISTORY.ValidatesRetainedFormatComponents` pass, but then failed the previously closed A049 regression. The failure was traced to the older A049 test fixture rather than to a valid yt-dlp output family: it modeled a retained `.f137` component with a different basename from its owner `.info.json` and only one selected format. No production repair commit from that failed batch was pushed; request commit `8345d48d3edd523c768a643256c904f2fcbee340` was explicitly discarded by `f233b9e5382569b0d196aebe3f2bddfd48c68bad`.

Test-only commit `773460b5aefdbc38590cb77631d244e5c0803c37` corrects that fixture to yt-dlp's actual merge-component shape: the retained component shares the owner's basename and the metadata identifies multiple selected formats. Windows Audit build `35514621819` passes, and verification run `35514621818` passes A049 while failing only A052. This is the accepted retry baseline.

**Closure:** Corrected guarded run `35514741640` landed `9352b7e54a6b3d3b3230cae264d49b681d5b32d5` (`fix: require authoritative retained format proof`) and cleanup `950ba2eccc6ca44f57437a528b36cc17504f985b`. The guard shows `DOWNLOAD_HISTORY.ValidatesRetainedFormatComponents` failing at baseline and passing after repair while A049 and the complete regression suite remain green. Evidence artifact `10605499666` has SHA-256 `9e9bb717e1fecc2ffb5d00b7107edba7bcc49f3e5832874d5e57651dd759b20b`.






### DH-A053 — Local extractor page substitution can poison protected source identity

**Priority / state:** High source-identity and archive-integrity risk / VERIFIED against current protected filtering and current yt-dlp extractor request loading.

**Finding:** yt-dlp's hidden `--load-pages` option changes extractor request handling: before a live request is made, the extractor derives a deterministic dump filename and, when that local file exists, reads its bytes and parses them as the request response. Protected mode currently permits this option. This is materially different from `--write-pages` / `--dump-pages`, which only emit debugging copies of responses.

**Impact:** A protected run can derive formats, metadata, redirects, or source identity from locally supplied debug response content rather than the requested live source, then publish the resulting native extractor+ID to the application-owned archive. That defeats the feature's source-authority assumption in the same class as `--load-info-json`.

**Required acceptance:**

1. Reject `--load-pages` and the current unambiguous `--load-p` abbreviation while Download History protection is enabled.
2. Preserve `--write-pages` and `--dump-pages`; they do not substitute extractor input.
3. Preserve disabled-history behavior.
4. Keep the existing source/extractor, config/plugin, output, and execution-boundary protections intact.
5. Add regression coverage and rerun the complete guarded Windows Debug/Release/full-regression gates.

**Baseline proof:** Test commit `d197326167f7a0dd41194470090ee3f6727cc281` adds `DOWNLOAD_HISTORY.RejectsLocalExtractorPageSubstitution`. Windows Audit build `35514974374` succeeds. Verification run `35514974398` fails on exactly that Download History regression (`False` expected for protected `--load-pages`, actual `True`).

**Closure:** Guarded run `35515082750` landed `e88cc40c301be14cf15a971a2991373d20cd20cc` (`fix: reject local extractor page substitution`) and cleanup `60775289c91a325b89eb57f1c5f4fcc899699fc3`. The targeted regression passes after repair and the complete guarded Windows Debug/Release/full-regression gates are green. Evidence artifact `10606891305` has SHA-256 `22b88fff72b4f417c4a6e3b3cec618f28cd4bc2aeb8f2f28a5c14fe18ea6e5bd`.


### DH-A054 — Clean merged format IDs are ambiguous when selected IDs contain plus signs

**Priority / state:** High rebuild correctness/integrity risk / VERIFIED against current A052 recovery logic and current yt-dlp clean-info serialization.

**Finding:** Protected mode relies on clean per-media info JSON. yt-dlp's clean-info serialization removes `requested_formats` but preserves the top-level selected `format_id` and the `formats` catalogue. For merged downloads, yt-dlp builds the top-level value by joining selected format IDs with `+`. Individual format IDs are not restricted from containing `+`; source-derived HLS group/name values can legitimately include it, and retained component filenames prepend the exact individual ID as `.f<format_id>`.

A052 currently falls back to `combined.Split('+')` when `requested_formats` is absent. Therefore a selected ID such as `audio+main` cannot be recovered from `video+audio+main`; conversely, an unselected available ID such as `main` can be falsely treated as selected solely because it appears after a separator inside another selected ID.

**Impact:** After total archive loss, legitimate retained components can make an otherwise recoverable library fail, or an unproven component can be promoted into the recovered native identity family. The latter weakens the authoritative-metadata boundary established by A052.

**Required acceptance:**

1. Keep `requested_formats` authoritative when it is present.
2. For clean info JSON without `requested_formats`, require the preserved `formats` catalogue and parse the top-level combined `format_id` only as complete catalogue IDs separated by `+`.
3. A candidate format is proven selected only when at least one valid complete decomposition exists and **every** valid decomposition contains that exact candidate.
4. If the combined string has multiple valid decompositions that disagree about the candidate, fail closed.
5. If no persisted catalogue can prove a retained `.f<format_id>` component, fail closed rather than blindly splitting.
6. Preserve A049/A052 split-chapter/component behavior, one-pass filesystem inventory, and byte-for-byte media/sidecar immutability.
7. Add end-to-end regressions for a legitimate selected `audio+main` component, an ambiguous/unselected `main` component, and missing-catalogue fail-closed behavior.
8. Rerun the complete guarded Windows Debug/Release/full-regression gates.

**Baseline proof:** Test commit `731838bf173254b7a33cf09e061b2d3811856dfa` adds `DOWNLOAD_HISTORY.DisambiguatesCleanMergedFormatIds` and makes the earlier clean component-only fixtures carry a realistic preserved `formats` catalogue. Windows Audit build `35515302319` succeeds. Verification run `35515302359` fails only the new Download History regression (`Healthy` expected, `Unsafe` actual).

**Closure:** Guarded run `35515426328` landed `d4f506edab22a6a7e7c8a152207b58925ac7ea58` (`fix: prove clean merged retained formats`) and cleanup `d478c15beee8b232b9c7c9d301de818c5fb9ba4a`. The targeted regression passes after repair and the complete guarded Windows gates remain green. Evidence artifact `10606777382` has SHA-256 `9003b28a451440eee92997fbbbf31bd0b11f78e9900682d1895b6abf561af9a4`.


### DH-A055 — Extractor debug page writes can clobber protected state

**Priority / state:** High protected-ledger integrity risk / VERIFIED against current protected filtering and current yt-dlp extractor debug-write path.

**Finding:** yt-dlp's `--write-pages` is not a console-only debugging option. For every extractor response it derives a deterministic `.dump` filename from video ID/request data/URL, resolves it relative to the provider working directory, and opens that file with `wb`. Download History permits a custom native archive path in the same filesystem namespace and currently permits `--write-pages`.

**Impact:** A custom protected archive can collide with one of those deterministic debug filenames. The provider can then truncate the already validated native ledger during extraction, outside the application's pre-start integrity check. This is an archive-state mutation problem distinct from A053's `--load-pages` source substitution.

**Required acceptance:**

1. Reject `--write-pages` and current unambiguous abbreviation `--write-pa` while Download History protection is enabled.
2. Preserve console-only `--dump-pages`.
3. Preserve disabled-history behavior.
4. Keep A053 `--load-pages` rejection and all prior write-target protections intact.
5. Add regression coverage and rerun the complete guarded Windows Debug/Release/full-regression gates.

**Baseline proof:** Test commit `6fcc1fec41336cece66641830c6ae84f08167846` separates filesystem page writes from console page dumping and adds `DOWNLOAD_HISTORY.RejectsExtractorPageDumpWrites`. Windows Audit build `35515580616` succeeds. Verification run `35515580648` fails only that Download History regression (`False` expected for protected `--write-pages`, actual `True`).

**Closure:** Guarded run `35515725724` landed `2235a51efa5222b4a072a19141048570bc57fd6b` (`fix: block extractor page dump state writes`) and cleanup `ab0e9def271dd0e3671473ad2e8761abafbff373`. The targeted regression passes after repair and the complete guarded Windows gates remain green. Evidence artifact `10606129867` has SHA-256 `fae23969bc75dbe5f22b89c4ce651ed26a4cc123e9ddcafc579cc07efa03a1f8`.

**A053 clarification:** The original A053 acceptance text treated `--write-pages` as a safe neighboring control. That characterization was incorrect because upstream opens deterministic dump files for write/truncate. A053's landed production repair remains correct and scoped to local response substitution; A055 separately corrects the write-side boundary without rewriting history.

### DH-A056 — Playlist metafiles can masquerade as completed media or poison rebuild

**Priority / state:** High rebuild correctness risk / VERIFIED against current scanner behavior and current yt-dlp playlist-metafile flow.

**Finding:** yt-dlp can write playlist/multi-video `.info.json`, descriptions, and thumbnails when playlist metafiles are enabled. Those playlist objects are containers; normal native download-archive completion is recorded for media entries rather than the playlist thumbnail/metafile family. The current scanner's `TryRecoverFromInfoJson` does not distinguish `_type:"playlist"` / `"multi_video"` from video metadata. A same-stem GIF thumbnail therefore enters the completed-media allowlist and is reported unresolved when its metadata says GIF is a thumbnail. Worse, if the candidate extension does **not** match the playlist thumbnail metadata, the same playlist `extractor_key + id` can be promoted as if it were a completed media identity.

**Impact:** Explicit rebuild can reject a valid library because of a playlist cover thumbnail, or can synthesize an archive record for a playlist container from an unrelated same-stem media-like file. Neither outcome reflects completed-media identity.

**Required acceptance:**

1. Never use authoritative metadata whose explicit `_type` is `playlist` or `multi_video` as a completed-media archive identity.
2. Ignore a same-stem media-like playlist thumbnail only when playlist/multi-video metadata explicitly lists the candidate extension among its thumbnails.
3. A same-stem candidate whose extension is not proven as a playlist thumbnail remains unresolved; do not hide arbitrary media behind playlist metadata.
4. Preserve indexed-thumbnail handling, ordinary video metadata recovery, total-loss derivative recovery, and existing fail-closed behavior.
5. Do not rename, move, rewrite, or delete playlist metadata, thumbnails, or any media.
6. Keep inventory streaming; do not add a whole-library pre-index or second filesystem walk.
7. Add/retain regression coverage for a proven GIF playlist thumbnail plus a mismatched GIF control.
8. Rerun the complete guarded Windows Debug/Release/full-regression gates.

**Baseline proof:** Test commit `496eeeec2867564ca9736776f92f671f303a48ab` adds `DOWNLOAD_HISTORY.IgnoresPlaylistMediaLikeThumbnailSidecars`. Windows Audit build `35515896158` succeeds. Verification run `35515896136` fails on that Download History regression (`Healthy` expected for the proven playlist GIF thumbnail; actual `Unsafe`). The method also contains a mismatched-extension fail-closed control that prevents fixing this by blindly ignoring same-stem playlist media-like files.

**Closure:** Guarded run `35713472420` landed `8db13b79b56fc4e1061f6f0754938a650729f64a` (`fix: distinguish playlist thumbnail metafiles from media`) and cleanup `47cdac7d443ff6a16df9ae08262c4facce4675f7`. The targeted regression passes after repair and the complete guarded Windows Debug/Release/full-regression gates are green. Retained evidence artifact `10688101407` has SHA-256 `9c0bd87d6004336d40522cbbd307552758277250f2dfa902e0867dcc5acc9506`.

### DH-A057 — In-tree custom archive paths can collide with future provider outputs

**Priority / state:** High protected-ledger integrity and destructive-write risk / VERIFIED against current app path policy and current yt-dlp write order.

**Finding:** Download History allows a custom native archive anywhere under the active download root and forces `--write-info-json`. Current yt-dlp writes descriptions, subtitles, thumbnails, info JSON, and link files before the media download completes and before the native archive record is appended. The app owns the media output template but cannot know future metadata values while validating the archive path. A custom archive such as `<active-root>\Future-<id>.info.json` can therefore be initialized and validated as the ledger, then be replaced by that source's forced info JSON during a later protected run.

The protected suffix already keeps yt-dlp's safe-extension guard enabled (`--compat-options -allow-unsafe-ext`), blocks custom output/path replacement and arbitrary write hooks, and owns the output template. Under those constraints, an in-tree `.txt` archive does not alias any supported provider-generated media/sidecar extension. The default archive is already `yt-dlp-archive.txt`.

**Impact:** A protected run can destroy the ledger it validated at startup using an ordinary, app-enabled yt-dlp sidecar write. The provider may then append a native archive line to JSON/media bytes, leaving corrupt state and defeating duplicate prevention.

**Required acceptance:**

1. If the resolved native archive is inside the active download root, require a `.txt` archive path so it cannot alias the protected provider output namespace.
2. Preserve the default `yt-dlp-archive.txt` and custom in-tree `.txt` archive behavior.
3. Preserve custom archive extensions outside the active download root; configured scan-only roots are not provider output destinations and do not require this restriction.
4. Enforce the rule in common path resolution so an active-root change also revalidates a previously configured archive.
5. Do not rename, move, rewrite, or delete any existing media/sidecar file while reporting an invalid candidate.
6. Preserve all existing archive ownership, relocation, backup/lease, output-template, and scan-root semantics.
7. Regression coverage must prove an in-tree `.info.json` collision is rejected, an in-tree `.txt` archive is accepted, and an out-of-tree `.info.json` archive remains accepted.
8. Rerun the complete guarded Windows Debug/Release/full-regression gates.

**Baseline proof:** Test commit `0d4b931ad3dbcf669a1f156d03085f098c6bc654` adds `DOWNLOAD_HISTORY.RejectsArchiveOutputCollisions`; commit `bfaca397c24712e70538e45ff47d5525c303b2c5` adds unrelated forced-archive abbreviation coverage that already passes. Windows Audit build `35714195862` succeeds. Verification run `35714195885` fails only `DOWNLOAD_HISTORY.RejectsArchiveOutputCollisions` (`Invalid` expected for the dangerous in-tree `.info.json` archive; actual `Missing`) while `DOWNLOAD_HISTORY.RejectsCustomArgumentsThatBreakProtection` passes.

**Rejected first repair / regression-contract correction:** Guarded run `35714574674` applied the proposed in-tree `.txt` restriction and made A057 pass, but correctly rejected the batch because the older `DOWNLOAD_HISTORY.ArchiveFileIsNotInventoryMedia` regression still created a media-extension archive inside the active output root. No production repair commit from that failed batch was accepted. Request `863af643c429562fdfde608cf60c9431f1027a14` was explicitly discarded by `1fd0adec5bc7d3decabf4f1e3811a77a36d418d6`. Test commit `cf59989ad66cc9cc58eea44935555bc758403af8` preserves the older regression's actual A010 purpose by placing the media-extension archive in a separate scan-only root, where it must still be excluded from inventory without being a provider output target. Windows Audit build `35714896167` succeeds and verification run `35714896119` passes `ArchiveFileIsNotInventoryMedia` while failing only A057.

**Closure:** Guarded run `35715132259` landed `5d8608cee42152a432627997efa0a8ef5706fa3a` (`fix: isolate in-tree archive from provider outputs`) and cleanup `db562062201bf6b4f95b08d4eac4a849bf7072de`. The complete guarded Windows Debug/Release/full-regression gates pass. Retained evidence artifact `10688018733` has SHA-256 `00c2d001693aa2cfafe7d881840aa8d7ad117f5b7bb7d57b3710cb687656f21c`.

### DH-A058 — yt-dlp cache writeback can replace an out-of-tree archive

**Priority / state:** High protected-ledger integrity risk / VERIFIED against current yt-dlp cache implementation and protected custom-argument behavior.

**Finding:** A040 blocks recursive `--rm-cache-dir`, but ordinary filesystem caching remains intentionally available. Current yt-dlp resolves an effective cache root (default `${XDG_CACHE_HOME}/yt-dlp`, falling back to `~/.cache/yt-dlp`) and writes cache entries as `<root>/<section>/<quoted-key>.json`. `Cache.store()` calls `write_json_file`, which on Windows explicitly unlinks an existing destination before renaming the new JSON file into place.

Download History currently permits both default caching and custom `--cache-dir`. A custom archive outside the active output tree may therefore be a valid `.json` path such as `<cache-root>/soundcloud/client_id.json`. A protected SoundCloud extraction that refreshes the cached client ID can replace the native archive even though output-path, cookie, exec, plugin, and child-process protections all pass.

**Impact:** The provider can destroy a validated native ledger through a normal built-in cache write. This bypasses A057 because cache storage is not confined to the active media output tree.

**Required acceptance:**

1. Resolve the effective yt-dlp cache root using current provider semantics: default `XDG_CACHE_HOME`/home expansion plus ordered custom `--cache-dir` and `--no-cache-dir` overrides.
2. Reject protected execution when the prepared archive is structurally a possible current yt-dlp cache entry: exactly `<cache-root>/<valid-section>/<filename>.json`.
3. Preserve ordinary cache use when the archive cannot be a cache target.
4. Preserve a later `--no-cache-dir` override; if a later `--cache-dir` re-enables the colliding root, reject again.
5. Do not ban `--cache-dir` globally or change disabled-history behavior.
6. Reuse yt-dlp-compatible user/environment path expansion and Windows case-insensitive path comparison.
7. Do not mutate the archive or cache tree while validating the collision.
8. Rerun the complete guarded Windows Debug/Release/full-regression gates.

**Baseline proof:** Test commit `020ab1ad339c3ca54feb7e28200d34912e484714` adds `DOWNLOAD_HISTORY.RejectsCacheWritebackCollisions`, covering the default cache root, ordered custom cache enable/disable behavior, a non-colliding custom-cache control, and disabled-history behavior. Windows Audit build `35715431474` succeeds. Verification run `35715431332` fails only the new cache-collision regression (`False` expected from protected preparation; actual `True`) while A040 `RejectsDestructiveCacheRemoval` and A057 `RejectsArchiveOutputCollisions` remain green.

**Closure:** Guarded run `35715787783` landed `4581f4dd3635b9e91751dc47a64599e90e197995` (`fix: protect history from yt-dlp cache writeback`) and cleanup `fa97aba4301afa170dc5dbfc2ab108676469df78`. The accepted repair models the default cache root, ordered `--cache-dir` / `--no-cache-dir` overrides, current `<section>/<key>.json` cache layout, and inherited app/provider working directory without disabling non-colliding cache use. The complete guarded Windows Debug/Release/full-regression gates pass. Retained evidence artifact `10688134628` has SHA-256 `07d1368761d914f8219e445efb1e3168cf64adaedfea9ee240a6a3dd72ad43a0`.

### DH-A059 — Dangling cookie option can turn an app-owned token into a writeback target

**Priority / state:** High protected-ledger integrity risk / VERIFIED against current A047 argument bracketing, current cookie collision validation, and current yt-dlp option/cookie-save behavior.

**Finding:** A047 correctly establishes an app-owned isolation prefix before raw custom arguments and retains the full protected suffix afterward. That prevents an arbitrary dangling value-taking option from removing the only config/plugin isolation token. However, a trailing custom `--cookies` has a write side effect of its own. Python optparse consumes the next command-line token as the required cookie filename even when that token begins with `--`. In the standard protected builder the next token comes from the app-owned suffix; in the mostly-custom builder another app-owned token such as `-o` can be consumed first.

The current A038/A041 collision check calls `GetOptionValues` only on the **raw custom argument string**. A final `--cookies` therefore yields no value to validate. Once the complete command line is assembled, yt-dlp receives a non-null cookie filename sourced from an app-owned token. yt-dlp saves the cookie jar at shutdown and opens that filename for write/truncate. Because out-of-tree custom archive names are intentionally supported, the resulting relative filename can collide with a valid protected archive outside the active media tree.

**Impact:** A protected run can pass all pre-start archive/cookie checks, then truncate the live native ledger through cookie writeback to a filename that did not appear as a cookie path in the user-supplied string. This defeats the protected-state boundary without disabling the A047 isolation prefix.

**Required acceptance:**

1. While Download History is enabled, reject custom `--cookies` when its required filename is not explicitly present in the raw custom argument string.
2. Keep ordinary explicit `--cookies FILE` and `--cookies=FILE` available subject to the existing A038/A041 archive/backup/lock collision checks.
3. Do not globally reject unrelated dangling value-taking options merely because A047 brackets them; scope this repair to the cookie writeback path unless another concrete side effect is proven.
4. Preserve Download History disabled behavior.
5. Preserve authentication-config cookie validation and all existing config/plugin isolation ordering.
6. Add/retain a regression proving trailing `--cookies` is rejected under protection and remains untouched when protection is disabled.
7. Rerun the complete guarded Windows Debug/Release/full-regression gates.

**Baseline proof:** Test commit `41cc7020928f719ed498b04e92c6649b9d1aa376` adds `DOWNLOAD_HISTORY.RejectsDanglingCookieWritebackOption`. Windows Audit build `35949401155` succeeds. Verification run `35949401156` fails on exactly that Download History regression (protected rejection expected; actual acceptance); Debug/Release builds and packaging complete successfully.

**Closure:** Guarded run `35949682442` landed `753739e420a21f385b0ecd3648323c6d117ebd8e` (`fix: reject dangling protected cookie writeback`) and cleanup `9260e0df91cd31f15d7830d31f3e00936b24c4da`. The targeted regression passes after repair and the complete guarded Windows Debug/Release/full-regression gates are green. Retained evidence artifact `10787784052` has SHA-256 `87d2027699f018867c75a6e6ca881345c3eeb27d26cbdd11a75954462bb361e6`.

### DH-A060 — Mostly-custom dangling option can consume the protected output constraint

**Priority / state:** High protected-output containment risk / VERIFIED against current standard mostly-custom builder and current yt-dlp optparse behavior.

**Finding:** A047 moved the non-negotiable config/plugin isolation prefix before raw custom arguments and kept the full protected suffix after them. That correctly prevents a dangling one-value option from removing the only isolation token. The standard `DownloadInfo` mostly-custom branch, however, rebuilds its argument buffer as `protected-prefix → raw custom arguments → -o <app output template>`. Current yt-dlp `--proxy URL` consumes exactly one following token even when that token begins with `-`. A trailing custom `--proxy` therefore consumes the app-owned `-o` token as its proxy value. The quoted output-template token that follows is then parsed as a positional input instead of an output option.

The ordinary standard custom path and the extended downloader already add their app-owned output option before raw custom arguments; this gap is specific to the standard mostly-custom buffer replacement.

**Impact:** A protected mostly-custom invocation can lose the active-download-root output constraint without using a blocked `-o`/`--output` override. A real source already present in the custom string can then use yt-dlp's default output location/template, violating the feature requirement that new protected downloads stay inside the active destination and weakening later inventory/rebuild assumptions.

**Required acceptance:**

1. In the protected standard mostly-custom path, place the app-owned `-o <output template>` before raw custom arguments.
2. Keep the protected isolation prefix before both the output option and raw custom arguments, and keep the full protected suffix after raw custom arguments.
3. Preserve the existing unprotected/disabled-history mostly-custom argument order and behavior.
4. Do not globally reject unrelated dangling value-taking options; A047 bracketing remains the general parser-boundary defense.
5. Preserve ordinary standard custom and extended output ordering.
6. Add/retain regression coverage using a trailing one-value `--proxy` to prove `-o` precedes raw custom input under protection.
7. Rerun the complete guarded Windows Debug/Release/full-regression gates.

**Baseline proof:** Test commit `f50a6bd1b2e92919f7b237f0e2be03a8d41cd46f` adds `DOWNLOAD_HISTORY.ProtectsMostlyCustomOutputBeforeRawArguments`. Windows Audit build `35949936042` succeeds. Verification run `35949936059` fails on exactly that Download History regression because the protected `-o` appears after trailing `--proxy`.

**Closure:** Guarded run `35950145991` landed `a13db96a5fdf9a676fa35a5012894d2b329337da` (`fix: protect mostly-custom output before raw arguments`) and cleanup `85a8a697a967f12b37450bbb5651df4bbf20ad3d`. The protected branch now orders isolation prefix → app-owned output → raw custom arguments → full protected suffix, while the disabled-history branch keeps its previous ordering. The complete guarded Windows Debug/Release/full-regression gates pass. Retained evidence artifact `10788237739` has SHA-256 `43d4044e116a65030f8440e63003517bd166b5675a62a8cc0c6f70d9a4140435`.

### DH-A061 — Protected schemas can produce media with unrecoverable literal extensions

**Priority / state:** High rebuild correctness and protected-ledger integrity risk / VERIFIED against current filename validation, inventory allowlist, and current yt-dlp output-template preparation.

**Finding:** Download History requires an active `%(id)s` in the final filename but does not require the final media filename to end in the actual `%(ext)s`. Current yt-dlp evaluates the default media output template as written; it does not replace an arbitrary literal extension merely because the selected format has a different `ext`. A protected schema such as `%(title)s-%(id)s.txt` can therefore produce media whose physical extension is `.txt`.

The recovery scanner intentionally treats `.txt` as a sidecar/non-media extension and inventories only the verified media-extension allowlist. After total archive loss, a literal-extension protected download can therefore disappear from physical inventory even though its filename contains the source ID and adjacent info JSON exists. The same gap invalidates A057's assumption that an in-tree `.txt` archive cannot alias provider media output: a user-controlled literal `.txt` media schema can target that namespace.

Escaped `%%(ext)s`, an `%(ext)s` token followed by another suffix, or an extension token not preceded by the final dot have the same rebuild problem because the actual filesystem extension is not the provider media extension.

**Impact:** Download History can accept and record downloads that cannot later reconstruct the native archive from the physical library. In the in-tree custom-archive case, a future media download can also overwrite the validated `.txt` ledger itself.

**Required acceptance:**

1. While Download History is enabled, require the final filename component to end exactly in an active `.%(ext)s` token in addition to the existing active `%(id)s` requirement.
2. Reject literal extensions, escaped `%%(ext)s`, missing extensions, and additional suffixes after `%(ext)s` under protection.
3. Preserve Download History disabled behavior.
4. Enforce the same invariant when committing enabled settings, not only when a download command is prepared.
5. Update the Download History enablement dialog to recommend a schema containing both the media ID and terminal real extension without silently changing an accepted user schema.
6. Update the parent Settings save gate/warning so enabled protection cannot persist an unrecoverable schema and disabled-history users are warned when removing a previously protected ID/extension form.
7. Preserve historical schema records for recovery; do not rename or rewrite existing media produced by older schemas.
8. Preserve extended-downloader behavior, which already appends `.%(ext)s` when needed.
9. Rerun the complete guarded Windows Debug/Release/full-regression gates.

**Baseline proof:** Test commits `d39338fef1c658c208880a3bd0b96f4c32e7dfe5` and `f6cce9ec2144a3caf0ec45bb29175eb5f0992a94` add and tighten `DOWNLOAD_HISTORY.RequiresRecoverableMediaExtensionTemplate`. Windows Audit build `35950633185` succeeds. Verification run `35950633304` fails on exactly that Download History regression (protected literal-extension rejection expected; actual acceptance); Debug/Release builds and packaging complete successfully.

**Rejected first repair attempt:** Guarded run `35950933352` applied the proposed A061 production repair and made its targeted regression pass, but correctly rejected the batch because the older `DOWNLOAD_HISTORY.SettingsRejectIdRemovalWhileEnabled` regression asserted the exact prior source spelling `DownloadHistory.Enabled && !DownloadHistory.HasRequiredIdTemplate`. The extension-aware Settings condition still enforced the original ID-removal requirement, so this was a stale test contract rather than a product regression. No production repair commit from the failed batch was accepted. Request `83902262503a666b556256e575e3872e69b3e566` was explicitly discarded by `3305af271514e988cbda275cb67c3cf9f74db080`; test-only commit `7564e30d2b4b843ff2890aca9017555bde9c98a4` restores that older regression to a semantic ID-safety assertion before retrying A061.

**Closure:** Guarded run `35960108749` landed `796738890e4898187c4db947dfe062601aa600b9` (`fix: require recoverable protected media extensions`) and cleanup `5e96506ce0cedd51c8bed4bb380b78dd4127cb83`. The targeted extension-template regression and the complete Download History/full AuditRegression suite pass after repair; Debug/Release builds and packaging are green. Retained evidence artifact `10791818806` has SHA-256 `8745463cbb822da48a34b06d467da131190b0280dec15ad82ab95b881ce3975c`. Current upstream yt-dlp's safe-extension guard does not permit `.txt` as a media/subtitle extension, so the terminal real-extension invariant restores A057's separation between in-tree `.txt` history and protected provider outputs.

**Reopened integration edge:** Regression commit `9da72816d501a376967f7beb909aafebb8721df1` exercises the acceptance requirement that the extended downloader preserve its historical behavior of appending terminal `.%(ext)s` when a per-item schema omits it. Verification run `35960748514` fails only `DOWNLOAD_HISTORY.RequiresRecoverableMediaExtensionTemplate`: `ExtendedMediaDetails.GenerateArguments` calls the common protected validator before its existing extension-normalization block. Standard protected downloads remain correctly strict. The repair must move the existing extended normalization ahead of protected validation rather than weaken the common invariant.

**Final closure:** Guarded run `35960942684` landed `6ae4bb02ca7d6274319f66f635f1dce3dd9ba23c` (`fix: normalize extended schema before protected validation`) and cleanup `fe9881e67ba36a8593b237812dc9067d88f0a443`. The extended normalization regression passes and the complete guarded Debug/Release/full-regression gates are green. Evidence artifact `10792048603` has SHA-256 `6c27f84c3d0391c0c0739444573b77957a5405e1f1c77332002d0aed2fc60d72`.

### DH-A062 — Recovery template keys are validated case-insensitively but yt-dlp keys are case-sensitive

**Priority / state:** High protected-output/rebuild correctness risk / VERIFIED against current app validation/recovery and current yt-dlp output-template evaluation.

**Finding:** The protected contract requires the exact yt-dlp metadata fields `%(id)s` and terminal `.%(ext)s`. Current yt-dlp traverses output-template field names directly against its metadata dictionary; the field names are case-sensitive and a missing field falls back to the configured NA placeholder. The app instead searches for `%(id)s`, accepts the terminal extension token, deduplicates remembered schemas, and recognizes ID tokens in historical schema regexes with case-insensitive comparisons.

Consequently a protected schema such as `%(title)s-%(ID)s.%(ext)s` can pass the mandatory-ID gate even though yt-dlp does not embed the native ID. Likewise `%(title)s-%(id)s.%(EXT)s` can pass the A061 extension gate while producing an NA-style literal extension that physical inventory does not recognize as media. An older invalid uppercase schema can also be treated as if it had embedded the native ID, and case-insensitive schema-history deduplication can suppress persistence of a later valid lowercase schema.

**Impact:** New protected files can lose the filename identity/real-extension guarantees used for collision resistance and archive-loss recovery. In the extension case the native ledger may record successful completion while a later total-loss rebuild cannot even inventory the resulting media extension. Historical recovery can also grant evidentiary meaning to a schema yt-dlp never interpreted as the required field.

**Required acceptance:**

1. Required protected template fields must match yt-dlp metadata-key and conversion spelling exactly: active `%(id)s` and terminal `.%(ext)s`.
2. Reject case-mismatched key/conversion variants while Download History protection is enabled; disabled-history behavior remains unchanged.
3. Historical schema recovery must recognize `%(id)s` only with the same case-exact semantics.
4. Remembered-schema deduplication must not let an invalid case variant suppress persistence/use of a valid case-exact schema.
5. Preserve A028 percent-escape semantics, A061 extended pre-validation normalization, and all safe lowercase schemas.
6. Keep filename/media handling non-destructive.
7. Add regression coverage for case-mismatched required keys, disabled-history behavior, invalid historical uppercase schema evidence, and valid-schema persistence beside an old invalid case variant.
8. Rerun the complete guarded Windows Debug/Release/full-regression gates.

**Baseline proof:** Test commit `cd6c6f1db117444137ad1625ac5df658fa17b2a5` adds `DOWNLOAD_HISTORY.RequiresCaseExactRecoveryTemplateKeys`. Windows Audit build `35961264205` succeeds. Verification run `35961264194` fails the new regression immediately (`False` expected from the case-mismatched required-field check; actual `True`). Test-only commit `8a9a6d58f7aa4cc97b7c3a7fe79ecffb5925a93d` additionally locks schema-history persistence so an old invalid uppercase variant cannot suppress a later valid lowercase schema.

**Closure:** Guarded run `35961632427` landed `6b35c4b4b6197a5e11b0c3c94d9f8d616153149c` (`fix: match yt-dlp recovery template keys exactly`) and cleanup `112c032127381c0db81e5ff5cffeb85d4c5a4ead`. The repair switches only output-template/schema-history semantics to ordinal case-exact comparison; Windows filesystem/path comparisons remain case-insensitive. The targeted regression and complete guarded Debug/Release/full-regression gates pass. Evidence artifact `10792273151` has SHA-256 `5a12ea04eebe835c129d1b0c9b2895d8a3f3c28d07d19fdeed426bb4b049e3f4`.

### DH-A063 — Current MHTML storyboard media is invisible to physical inventory

**Priority / state:** High archive-loss recovery correctness risk / VERIFIED against current inventory and current yt-dlp format/downloader definitions.

**Finding:** Current yt-dlp classifies `mhtml` as its storyboard media extension and exposes normal selectable storyboard formats with `ext: "mhtml"` / `protocol: "mhtml"` (including YouTube, Twitch, FranceTV, Panopto and common DASH image storyboards). `MhtmlFD` is a normal downloader, so a successful selected storyboard format reaches the same source-level native archive completion path as other formats.

Download History's physical-media allowlist covers current audio/video and additional safe direct-media extensions, but not `.mhtml`. Protected mode does not ban ordinary format selection. A protected storyboard download can therefore create authoritative media plus the forced adjacent info JSON and record its native identity, while an explicit rebuild after archive loss enumerates zero completed media.

**Impact:** A valid protected download can become unrecoverable solely because the selected current yt-dlp media class is a storyboard. If the primary/backup history is lost, Rebuild cannot reconstruct the identity even though both the MHTML media and authoritative info JSON remain intact.

**Required acceptance:**

1. Treat current yt-dlp `.mhtml` storyboard outputs as completed media during physical inventory.
2. Preserve thumbnail/subtitle/manifest exclusions; do not generically admit image or manifest extensions.
3. Recover storyboard identity through the same authoritative adjacent info JSON rules as other completed media.
4. Preserve byte-for-byte MHTML media and sidecar immutability during analyze/rebuild.
5. Preserve all A025/A029/A031/A035 media-vs-sidecar ambiguity behavior.
6. Add a regression proving Analyze sees the MHTML media and explicit Rebuild restores the native archive without modifying media/metadata.
7. Rerun the complete guarded Windows Debug/Release/full-regression gates.

**Baseline proof:** Test commit `43d053357ee12f6f18230902d6cc170139279656` adds `DOWNLOAD_HISTORY.InventoriesStoryboardMedia`. Windows Audit build `35961878290` succeeds. Verification run `35961878023` fails only the new Download History regression (`CompletedMedia=1` expected; actual `0`).

**Closure:** Guarded run `35962070685` landed `10b191b94726a71c09b6ad3a297a77e571ed2968` (`fix: inventory current MHTML storyboard media`) and cleanup `eeba54ef2e45ba8088884a699de8d713dd18cb2a`. The targeted storyboard regression and complete guarded Debug/Release/full-regression gates pass. Evidence artifact `10793106244` has SHA-256 `b212a5f31837ae7645f2ab2a066f19dada4fff7346ccf263ed46fa484181513a`.

### DH-A064 — Split-chapter derivatives are not explicitly contained in the active library

**Priority / state:** High protected-output containment and rebuild-integrity risk / VERIFIED against current protected arguments and current yt-dlp chapter output semantics.

**Finding:** Protected mode blocks user `-P/--paths` and `-o/--output` overrides and owns the default media output template, but it permits the built-in `--split-chapters` postprocessor. Current yt-dlp gives split chapters an independent `chapter` output-template/path namespace; `FFmpegSplitChaptersPP` calls `prepare_filename(..., 'chapter')`, and both `--paths chapter:...` and `--output chapter:...` can control that derivative family.

The protected suffix currently contains no app-owned `chapter:` path. That leaves split-chapter placement dependent on yt-dlp's generic path resolution instead of explicitly proving that every derivative remains beneath the active Download History library. The existing chapter filename itself is suitable for recovery because current yt-dlp's default chapter template includes the parent source `[%(id)s].%(ext)s`; the gap is containment, not identity.

**Impact:** Protected execution can create a media derivative whose path is not established by the same active-root invariant used for normal media. A derivative outside configured inventory roots can disappear from explicit rebuild evidence, while an unexpected write outside the active library also violates the protected-output boundary.

**Required acceptance:**

1. Protected arguments must append an app-owned `chapter:` path rooted in the resolved active download library.
2. The app-owned chapter path must occur after raw custom arguments so the final protected value wins.
3. Continue rejecting user `-P/--paths` and `-o/--output` overrides while protection is enabled.
4. Preserve yt-dlp's current chapter filename template; do not rename or rewrite split outputs merely to satisfy containment.
5. Escape literal `%` / `$` in the resolved path consistently with other app-owned yt-dlp path arguments.
6. Preserve disabled-history behavior and ordinary `--split-chapters` availability.
7. Preserve existing split-chapter ID recovery and total-loss derivative rebuild behavior.
8. Add regression coverage proving the protected suffix contains a rooted `chapter:` path and follows raw `--split-chapters` arguments.
9. Rerun the complete guarded Windows Debug/Release/full-regression gates.

**Baseline proof:** Test commit `9f66919774ade22566ab613f4722a64fcfb8691a` adds `DOWNLOAD_HISTORY.ContainsSplitChapterOutputs`. Windows Audit build run `35962305315` succeeds. Verification run `35962305261` fails only the new containment assertion ("Protected split chapters do not have an app-owned chapter output path") while the pre-existing `DOWNLOAD_HISTORY.RecognizesSplitChapterIds` regression remains green.

**Closure:** Guarded run `36017332073` landed `9f2b423bb23b233818723c4ac7f34ecab4da5924` (`fix: contain protected split chapter outputs`) and cleanup `e286fe8dc0e1461ca8d2669ef0f364b4457abf29`. The repair appends an escaped app-owned `chapter:` path rooted at the resolved active library after raw custom arguments without changing yt-dlp's chapter filename template. The targeted regression and complete guarded Debug/Release/full-regression gates pass. Evidence artifact `10814743726` has SHA-256 `3c1b3dbad85ad5f120d21745f6ff4a33db02da01dd63ce9918a0c746990958f5`.

### DH-A065 — Prepared execution can cross into an incompatible provider before launch

**Priority / state:** High protected-execution integrity risk / VERIFIED by runtime regression.

**Finding:** Protected command preparation rejects providers outside yt-dlp / yt-dlp-nightly and builds yt-dlp-specific archive, plugin-isolation, output-containment, and compatibility arguments. The returned `DownloadHistoryExecution` is then revalidated immediately before the provider process starts, but `ValidatePreparedExecution` currently rechecks only enabled/archive/backup state. It does not recheck the selected provider family.

The downloader resolves `Verification.YoutubeDlPath` separately from the prepared execution context. If `Downloads.YtdlType` changes after argument generation but before `AcquireValidatedLease`, an old yt-dlp-protected command can therefore be launched through a youtube-dl provider without forcing regeneration.

**Impact:** The process-start guard can approve an execution whose provider no longer implements the semantics that were audited when the command was prepared. Even if a particular incompatible provider rejects some flags, protection must fail before launch rather than depending on downstream option errors.

**Required acceptance:**

1. At every prepared-execution validation point, require the current provider to remain yt-dlp or yt-dlp-nightly.
2. A switch between stable yt-dlp and yt-dlp-nightly remains compatible; do not require exact provider identity.
3. Reject a switch to youtube-dl/youtube-dl-nightly before process start with an actionable regenerate-command failure.
4. Preserve existing enabled/archive/backup/snapshot/floor validation and locking order.
5. Do not make active media-root or filename-schema edits retroactively invalidate a command whose concrete output arguments were already generated.
6. Preserve disabled-history behavior.
7. Extend the existing execution-context regression rather than creating redundant runtime coverage.
8. Rerun the complete guarded Windows Debug/Release/full-regression gates.

**Baseline proof:** Test-only commit `a491fb4f8bf0773f0cce186117597dfb606b96dd` extends `DOWNLOAD_HISTORY.ExecutionContextRejectsSettingChanges` with a yt-dlp → youtube-dl provider flip before lease acquisition. Audit build run `36018496834` succeeds. Verification run `36018496741` fails only the two registrations of that same legacy/current execution-context method (`ExecutionContextRejectsSettingChanges` and `ExecutionContextSurvivesDisableButNotReset`), each because the expected `InvalidOperationException` is not thrown.

**Closure:** Guarded run `36018887072` landed `d6fcff4a96e91225173910177e15d0c7d519f076` (`fix: revalidate provider family before protected launch`) and cleanup `f5e41292fdf90c2e33aed4d70f43c26a2fb4e146`. The start-time guard accepts either yt-dlp stable or nightly but rejects transition to the youtube-dl family before process start. Both execution-context registrations and the complete guarded Debug/Release/full-regression gates pass. Evidence artifact `10816245764` has SHA-256 `93daac4427fc61e81a708f5733b46e33679d90c4890977056ed82cd3b8c9e81f`.

### DH-A066 — Validate Archive reports only the ID requirement when extension recovery is missing

**Priority / state:** Low management-UI correctness risk / VERIFIED by regression.

**Finding:** A061 correctly requires protected output filenames to contain an active `%(id)s` and end in an active `.%(ext)s`. The Download History enablement prompt and parent Settings gate describe both requirements. The separate `Validate Archive` path calls the same candidate validator but, when validation fails, still constructs the pre-A061 status message saying only that `%(id)s` is missing.

**Impact:** A user with a valid ID token but a literal/missing media extension receives a misleading remediation message even though the protection logic correctly rejects the schema.

**Required acceptance:**

1. The validation status must explain both required recovery fields: active `%(id)s` and terminal `.%(ext)s`.
2. Do not weaken or otherwise change the A061 schema-validation rules.
3. Keep enable/save/rebuild behavior unchanged.
4. Add a regression scoped to the validation-message wiring.
5. Rerun the complete guarded Windows Debug/Release/full-regression gates.

**Baseline proof:** Test-only commit `df0f8e345ff9ceea570e4f2a90964d1c819a4d27` adds `DOWNLOAD_HISTORY.ValidationExplainsMissingExtensionTemplate`. Audit build run `36019431041` succeeds. Verification run `36019430396` fails only that regression because the `ValidateArchive` method does not mention `%(ext)s` in its rejected-candidate message.

**Closure:** Guarded run `36019876157` landed `8c0c5dd1b5fe8a690052647dc49aa2fc4e7a95bb` (`fix: explain both protected filename requirements`) and cleanup `7caeee278ba5de8d545c2568ae22b15efdcc6071`. The targeted regression and complete guarded Debug/Release/full-regression gates pass. Evidence artifact `10815744648` has SHA-256 `84c35971c87a4f29020428a9ef44e3d8c30b89cba6b679137bd0ae7c218b1e9b`.

### DH-A067 — Archive management UI resolves provider paths with .NET-only environment semantics

**Priority / state:** Medium management correctness and operator-safety risk / VERIFIED against current dialog source and the previously established yt-dlp direct-path expansion contract.

**Finding:** Runtime Download History archive resolution uses `DownloadHistory.ResolveYtDlpDirectPath`, which mirrors yt-dlp direct-path expansion for `~`, `%VAR%`, `$VAR`, and `${VAR}`. The Download History dialog does not consistently use that resolver for management actions. `BrowseArchive`, `OpenLocation`, and the candidate-vs-saved comparison inside `ResetHistory` still call `Path.GetFullPath(Environment.ExpandEnvironmentVariables(...))`, which only applies .NET/Windows-style `%VAR%` expansion and does not match yt-dlp for dollar variables or user-home expansion.

**Impact:** A saved custom archive path such as `$YTDL_HISTORY_ROOT\\archive.txt`, `${YTDL_HISTORY_ROOT}\\archive.txt`, or `~\\archive.txt` can point to one physical file during protected execution while Browse/Open/Reset reason about a different literal path. This can open the wrong directory, produce a misleading browse starting point, or incorrectly claim that the Reset History field contains an unsaved path change.

**Required acceptance:**

1. Browse Archive must resolve its initial directory through `DownloadHistory.ResolveYtDlpDirectPath`.
2. Open Location must resolve the displayed/configured path through the same helper.
3. Reset History candidate-vs-saved path comparison must resolve configured custom paths through the same helper before comparing.
4. Preserve the existing fallback to `DownloadHistory.EffectiveArchivePath` for an empty/default field.
5. Do not alter scan-only inventory-root semantics; those remain ordinary app filesystem paths.
6. Preserve malformed-path exception handling and existing UI behavior outside the path-expansion correction.
7. Regression coverage must inspect all three management paths and reject direct `Environment.ExpandEnvironmentVariables` use there.
8. Rerun the complete guarded Windows Debug/Release/full-regression gates.

**Baseline:** test commits `254d67a683a03995324c2d49e27b0e422239031d` and `ea77ea4610d0dedcb3c6e50b11ccf85040fd94c6` add and repair the source-inspection regression `DOWNLOAD_HISTORY.ManagementUsesYtDlpArchivePathExpansion`. The first test revision failed before its semantic assertion because it assumed parameterless event-handler signatures; the corrected revision uses stable method-name prefixes. Current source still contains .NET-only expansion in all three target management paths.

**Rejected first repair attempt:** Guarded run `36072358563` applied the proposed three-line dialog repair in its temporary workspace, but the regression still failed before evaluating product behavior because its Reset History source slice searched for a nonexistent later `BuildUi()` method. No production repair commit from that failed batch was accepted. Request commit `a3df345a81c3bc2ef80f139b05d2c19b1fb1bff6` was explicitly discarded by `64d586e4fcd5b7339889eaa01c94b767e2f15e5f`; test-only commit `26d63b7642a61ef897495d56a80c442839b5ee29` now terminates the Reset History slice at the actual following `SaveAndClose` handler.

