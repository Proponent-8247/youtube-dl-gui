# Final remediation status — `audit-fixes`

Date: 2026-09-13 (America/Boise)

## Purpose and authority

This document is the current remediation-status record for the completed end-to-end audit of `Proponent-8247/youtube-dl-gui` on `audit-fixes`. Historical audit/checklist files are preserved as discovery-time evidence and intentionally retain their original unchecked boxes and wording; they must not be read as the current repair state.

The substantive source-review matrix was closed at **105/105 reviewed files**. The remediation work then corrected the in-scope confirmed defects and implemented the product/security decisions supplied by the repository owner. Failed or incomplete guarded attempts remain in history as evidence and are not counted as successful repairs.

## Current production-source checkpoint

The final source-changing repair checkpoint before this documentation-only closeout is:

- `3389b72180503214e8566388dbb5017cfdae33d5` — `fix: allow bounded updater IPC reentrant reply`
- guarded repair run `34774007540` — **SUCCESS**
- guarded batch closeout `ee69c7fc2563dac0c101dd606a9333106ce08513`

The guarded run began with exactly the three expected V011 live-updater failures and required all three to become green without introducing any new regression before publishing the repair.

## In-scope remediation status

All confirmed in-scope code findings discovered by the completed audit/review are repaired at the source checkpoint above and covered by the repository regression/build machinery where executable acceptance was practical.

Important late findings include:

- **O043** — caller-configured `ExtendedLinkLabel` colors survive hover/state transitions; dedicated regression is green.
- **O044** — independent batch runs receive unique readable batch identities; `E2E_O044.BatchIdsAreUniqueAndReadable` is green.
- **O045** — the bounded updater request previously used `SMTO_BLOCK`, preventing the updater UI thread from processing the synchronous nested `WM_COPYDATA` reply required by the existing updater protocol. The repair retains a bounded timeout and `SMTO_ABORTIFHUNG`, but uses a reentrant bounded send only for the updater request path. Other bounded sends retain their blocking behavior.

The final V011 live two-process validation now uses the actual compiled Debug application and updater executables and verifies:

- the application launches the real updater and binds the session to the expected process/window;
- malformed update IPC is rejected before the valid packet is accepted;
- Unicode application filename, version, SHA-256 and process identities survive the real wire exchange;
- native pointer and packet-layout sizes are compared between the actual application and actual updater processes rather than against the architecture of the separate test host;
- synchronous acknowledgement completes;
- successful replacement writes the expected payload;
- cancellation leaves the application untouched and cleans temporary state;
- injected replacement failure rolls back to the original application and cleans temporary state.

## Owner policy decisions implemented

The remediation preserves the owner decisions supplied during the patching pass:

- **O006:** owner closure cancels owned GIF/FFmpeg/ImageMagick conversion work and cleanup is contained.
- **O017:** ordinary diagnostics redact credentials/tokens; explicitly raw provider diagnostics remain raw with a warning that they may contain secrets.
- **O022:** application-owned tool installation/update survives Settings-form closure and remains tracked by the application.
- **O024:** batch-from-file work is tracked as application work and participates in orderly shutdown rather than being silently killed.
- **O033:** link-file import is all-or-nothing; a read/validation error prevents partial queue mutation and reports the failure context.
- **P001:** application update metadata/releases target `Proponent-8247/youtube-dl-gui`; downloader-provider repositories remain their appropriate provider upstreams.
- **P002:** complete per-session log/console/raw-provider/queue history is retained in a disk-backed session archive before UI/line truncation, ordinary URL-bearing queue records are redacted, Clear is UI-only, and the session history can be explicitly exported.
- **P003:** downloaded executables require authoritative file hashes at minimum; available Authenticode publisher/signature information is checked and invalid signatures are rejected; provider self-update is not treated as a trusted replacement path.
- **P005:** hostile remote thumbnail bytes are decoded/normalized out of process with bounds/cancellation/cleanup before normalized image data is handed to the GUI decoder.

## Explicitly deferred / outside current remediation scope

The following are deliberately **not** represented as repaired requirements because the owner explicitly deferred or excluded them:

- **O015 product-feature completion:** do not complete/expand the Release-hidden experimental Extended Converter feature as part of this bug-remediation train. Confirmed defects in existing paths were repaired, but unfinished feature design remains outside scope.
- **P004:** policy for failure to attach a Windows Job Object remains deferred until the owner has enough product/operational context to select fail-closed versus best-effort behavior.
- **P006 / download-history feature:** unfinished Debug/experimental feature completion and the separate persistent download-history project remain outside this remediation scope.

These exclusions are scope decisions, not undisclosed failed tests.

## Verification rules and evidence

The repository's guarded repair workflow pins the request parent, verifies the live branch has not moved, builds before and after each repair, executes the compiled regression inventory, rejects unexpected new failures, requires declared failures to be resolved, and only then publishes the source repair. Failed attempts therefore do not silently become production commits.

The final documentation commit containing this file is intentionally source-neutral. Operational signoff requires **Audit build** and the full **Audit verification** workflow to succeed at that exact documentation SHA. The exact final SHA and run IDs are recorded outside this file after those workflows complete, avoiding an infinite self-referential commit/verification loop.

A green build/regression suite is not a formal security certification or proof against every possible race/provider/OS combination. Existing finite-test limits, external-provider behavior, legacy nullable warnings, and long-duration real-world soak coverage remain ordinary residual validation risk rather than known unpatched in-scope defects.

## Tool-health notes

During the larger audit/remediation session, the GitHub connector remained usable well beyond the originally suspected approximately 60-minute window. Isolated transient transport/unsupported-URL errors recovered immediately and were not sustained outages. Direct container DNS access to `github.com` was separately unavailable during portions of the work, so repository operations used the GitHub connector. In-progress Actions log blobs occasionally returned temporary 404s until a job completed; normal connector reads/writes remained healthy.
