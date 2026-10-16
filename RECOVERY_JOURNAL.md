# RECOVERY_JOURNAL.md — youtube-dl-gui whole-repository audit

**Status (last reconciled 2026-10-16): FULL READ-ONLY AUDIT COVERAGE AND LEDGER RECONCILIATION COMPLETE; PRODUCTION REMEDIATION EXPLICITLY DEFERRED.**

Repository: https://github.com/Proponent-8247/youtube-dl-gui
Integration line: `feature/download-history-archive-3.3.0-2`
Audited immutable source SHA: `7e369eb4afa6477e81f3e185ce9754bfd91982a6`
Base release SHA: `49a290d07b220e4a9cd98d44c6db7f45a5bbbc98` (`release/3.3.0-2`)
First recovery-journal-only commit: `76a4c725bfda62656cf4ff2ac25f61d303ea3555`
Authoritative source tree at audit pin: 394 tracked paths; recursive API tree `truncated=false`
**Audit source pin is unchanged.** This journal's own later commits update only this journal; resolve the actual live head from GitHub on every resumption.

## User-approved phase boundaries

The latest user instruction was: audit the **entire** repository, record supportable findings **immediately** in a durable repo TODO/GitHub Issues ledger, and **perform repairs later after the audit**. Do not treat “continue until all work complete” as authorization to start production fixes during the audit-only phase.

Do not change production source, test implementation, CI workflows, released artifacts, or branch history as a purported audit remediation. No cherry-picks/merges/deletions/history rewrites without later authorization. The only feature-branch content changes during this resumed phase were creation and update of this **user-requested audit-management recovery journal**. GitHub Issues/comments were also updated.

Use a versioned journal, verify actual file and branch SHA after each write, reconcile all statements against GitHub refs, issues, test runs and artifacts. Do not rely on chat state. Check issue timestamps; another worker may have advanced the ledger.

## Audited workstreams — COMPLETE

Coordinator: [#12](https://github.com/Proponent-8247/youtube-dl-gui/issues/12). All source audits pinned to `7e369eb4afa6477e81f3e185ce9754bfd91982a6`.

| Workstream | Parent | Source-coverage state |
|---|---|---|
| WS-01 Startup/config/persistence/CLI/IPC | [#2](https://github.com/Proponent-8247/youtube-dl-gui/issues/2) | Complete, parent closed; shards A–D |
| WS-02 Download arguments/provider/process | [#3](https://github.com/Proponent-8247/youtube-dl-gui/issues/3) | Complete, parent closed; shards A–C |
| WS-03 Main/download/batch/archive UI | [#4](https://github.com/Proponent-8247/youtube-dl-gui/issues/4) | Complete, parent closed |
| WS-04 Conversion/media/ffprobe/thumbnails | [#5](https://github.com/Proponent-8247/youtube-dl-gui/issues/5) | Complete, parent closed |
| WS-05 Updater/trust/rollback | [#6](https://github.com/Proponent-8247/youtube-dl-gui/issues/6) | Complete, parent closed |
| WS-06 Controls/native/logging/auth/localization | [#7](https://github.com/Proponent-8247/youtube-dl-gui/issues/7) | Complete, parent closed |
| WS-07 Tests/CI/build/release | [#8](https://github.com/Proponent-8247/youtube-dl-gui/issues/8) | **Completed and closed 2026-10-16**: A/B/C previous shard closes, D solution/CSproj/SHProj/tools/release integration examined; pinned 187 C# paths vs 184 active include paths; 3 legacy uncompiled tracked copies; no missing compiled source |
| WS-08 Docs/add-on/resources/languages | [#9](https://github.com/Proponent-8247/youtube-dl-gui/issues/9) | Complete, parent closed |
| WS-09 Cross-cutting security | [#10](https://github.com/Proponent-8247/youtube-dl-gui/issues/10) | **Completed and closed 2026-10-16**: A–D closeouts, C/D independent re-reviews; PAR-09-001..015 |
| WS-10 Cross-cutting concurrency/lifetime | [#11](https://github.com/Proponent-8247/youtube-dl-gui/issues/11) | **Completed and closed 2026-10-16**: A–F closeouts, several independent re-reviews; PAR-10-001..013 |

The coordinator #12 now has all ten checkboxes checked. It **remains OPEN** because its original completion definition also includes later fixes/CI-green integration/branch consolidation that the user explicitly deferred.

## Canonical read-only finding queue — PERSISTED

**[#149 — canonical remediation queue](https://github.com/Proponent-8247/youtube-dl-gui/issues/149)** is the authoritative index. It contains 130 unique confirmed open findings with unchecked fix tasks, source SHA, severity/confidence and acceptance criteria through individual linked issues:

- 123 unique `PAR-XX-NNN` findings: 15 WS01, 13 WS02, 11 WS03, 6 WS04, 8 WS05, 14 WS06, 20 WS07, 8 WS08, 15 WS09, 13 WS10. No numbering gaps.
- 7 previously stranded verified Download History findings now separately filed as [#142–#148](https://github.com/Proponent-8247/youtube-dl-gui/issues): DH-A070–A075 and DH-A081. Other DH-A076–080/A082–083 are already represented by the PAR-07 issue set; do not multiply them.
- Severity distribution for all 130: **1 Critical, 25 High, 61 Medium, 43 Low**. Metadata integrity check: all 130 have pinned SHA, severity, confidence, unchecked TODO and `NOT IMPLEMENTED`.
- Five duplicate/superseded PAR issue records #76, #77, #80, #86 and #88 are closed and not counted. Distinct-but-similar findings (batch downloader vs converter; test oracle vs product behavior; temp postprocessing residue vs false-rebuild completion; raw session disk size vs secret retention) were reviewed and retained as separate issues; see #149 closeout comment.
- The older [F/O/V/P findings checklist](https://github.com/Proponent-8247/youtube-dl-gui/blob/7e369eb4afa6477e81f3e185ce9754bfd91982a6/audit/FINDINGS-CHECKLIST.md) remains historical evidence, **not** 155 additional confirmed defects: 126 historical F-items need finding-specific verification; 17 F-items have named regression evidence; eight O source/risk leads, ten V validation tasks, six P policy decisions and five completion checks remain.
- [Original Download History TODO](https://github.com/Proponent-8247/youtube-dl-gui/blob/7e369eb4afa6477e81f3e185ce9754bfd91982a6/audit/DOWNLOAD-HISTORY-TODO.md) is preserved as pinned prior evidence. No existing audit source file was edited during read-only reconciliation.

## Build / regression evidence

Exact source pin `7e369eb4...`:
- [Audit build 36176309827](https://github.com/Proponent-8247/youtube-dl-gui/actions/runs/36176309827): **success**.
- [Audit verification 36176309808](https://github.com/Proponent-8247/youtube-dl-gui/actions/runs/36176309808): **failure**, specifically `DOWNLOAD_HISTORY.ResetRequiresOwnedBoundArchive`, already tracked as **DH-A070/#142**. Debug/Release/updater compilation and Release packaging completed; subsequent full Release regression pass skipped because existing known regression failed. Do **not** call this CI-green or claim acceptance.

No new production CI run is required merely because `RECOVERY_JOURNAL.md` changed; the code/tree under review is still the pinned SHA. Full build/test/fault/soak and issue-specific acceptance are required **after remediation**.

## Branch reconciliation — PLAN RECORDED, MERGES DEFERRED

[#13 — branch inventory/consolidation tracker](https://github.com/Proponent-8247/youtube-dl-gui/issues/13) now contains the live 2026-10-16 inventory and a complete planning-only disposition for 16 branches.

- Release, audit-fixes, noop-check are ancestors of feature and have zero unique ahead commits.
- Master `4f9fd9e0...` has two unique `README.md` heading edits (no unique production code), requiring explicit accept/reject or forward-port decision when consolidation is authorized.
- `audit/parallel-ro-20260925` has 12 unique audit-scaffold commits; all ten `audit/ro-ws*` refs share scaffold SHA `852d53c6...` with 11 unique audit-only commits. The issue ledger supersedes the scaffolds; no deletion or merge was performed.
- Initial audit pin to live feature ref compared as one journal-only commit. This journal update will create another journal-only commit; on resumption **re-run compare** and verify its entire file list remains only `RECOVERY_JOURNAL.md`.

## Exact resumption plan — subsequent *authorized* remediation phase

1. Confirm feature ref, pinned source ref, journal blob and all 130 open items from GitHub; re-read queue #149 and coordinator #12, branch planner #13. Reconcile journal with reality; never assume a write succeeded without read-back.
2. Decide each finding's disposition with user: fix / accepted risk / validation required / duplicate / rejected with rationale. Prioritize Critical (#93) guard credential trust boundary, then High state ownership/false-completion/updater security, then Medium/Low correctness/UI/docs.
3. Plan small conceptual fixes; run baseline and changed Windows Debug/Release/full-regression suites, focused tests and external-runtime validations; flag uncertain behavior and preserve compatibility.
4. Audit each conceptual change, record evidence and exact SHA/CI links; no source/test/CI weakening to make red tests go away. No history rewrite.
5. Reconcile branch #13 unique work semantically; only after explicit approval, forward-port useful changes, test consolidated integration head and verify no unique work stranded.
6. Update this journal at each meaningful checkpoint; include objectives, starting state, decisions, commits, files, tests/results, blockers, precise next cursor, and recovery instructions.

**Audit-only phase status: complete. Production remediation / full end-to-end acceptance / branch integration: intentionally NOT STARTED in this phase.**

## Remediation checkpoint — 2026-10-16 (acceptance PENDING)

Authorization: user requested continuation through remediation after completed read-only audit. Original audited pin remains `7e369eb4afa6477e81f3e185ce9754bfd91982a6`; feature head at entry `959c8531258ffa45d1ad93e08d1b32b6478afbad`. GitHub compared pin to entry head: two commits changing only `RECOVERY_JOURNAL.md`.

**Separate working branch:** `remediation/whole-repo-20261016` forked from entry head. Do not treat this branch as accepted or CI-green.

Critical #93 / PAR-07-009 initial commits:
- `987b486b17575041ea8dac3224f945abf25bdacd`: workflow read-only verifier; checkout without persisted credential; separate publisher job with write permission, bundle and manifest validation, exact original-branch-head comparison, non-force push.
- `c9b982de861772c95bb806a49df50f8089ba9754`: verify runner creates commit bundle and digest manifest without pushing.
- `521fb3e40e814c9923655a2eb27578e0afa5ce0b`: privileged publisher checks proposed changed paths and refuses guard/tool/test modifications.
- Initial static checks confirmed read-only verifier, separated publisher and removed verifier push. No live Windows build, hostile build-injected credential-leak test, end-to-end publishing test, or independent artifact attestation has passed. **#93 is not fixed/closed.**
- Follow-up security issues #94/#95/#96/#105/#124 also remain open. The publisher's downloaded bundle/manifest currently trusts outputs of proposed-code execution: independently validate artifact provenance, guard-pinning and changed-file allowlists before acceptance. Potentially use a trusted, separate post-verification attest/signature boundary before privileged publication.

**Verification baseline:** Prior Windows build success run 36176309827; failing regression run 36176309808 (DH-A070, #142). No new Windows CI results have been obtained in this checkpoint.

**Exact recovery cursor:** inspect actual branch head and GitHub Actions syntax/results; harden #93 against untrusted artifact/job-output forgery and all unsafe Git credential paths; run adversarial fixture + verification/publishing acceptance; then address #94/#95/#96/#105/#124, then #142 and remaining queue. Keep one conceptual fix per commit and update finding evidence. Do not merge to feature/release or close issues before gates pass.

### Additional security preflight fixes — 2026-10-16
- `e48d45aa1b51c0110b11a248c00c95b8e3acacdf`: #105 request-only commit check uses validated --request-files wrapper/part inventory and rejects unrelated files before baseline build; #95 disallows all request-controlled flaky exemptions pending reviewed trusted policy.
- `186f1c06323d0452c9766489c616d0d5860e714b`: #96 removes tests as editable paths in ordinary exact repair parser. Legitimate test-contract changes require separate reviewed workflow.
- Follow-up: tests can still be modified as side effects of untrusted build/test code (#124), and other trust-root issues remain. Do not mark #95/#96/#105 accepted until negative tests verify the actual shell/Python behavior and Windows workflow executes successfully.
- Exact checked remote working head before this journal update: `186f1c06323d0452c9766489c616d0d5860e714b`.

### CI branch activation checkpoint — 2026-10-16
- `5845876ccf41fcf0cf184b644686876b072342af`: include remediation branch in audit-build.yml push trigger.
- `3646c2d22a2bcc1c1c31b0321eb6a75f6f285f71`: include remediation branch in audit-verify.yml push trigger.
- After these pushes, GitHub Actions branch-filter query returned **zero workflow runs at observation time**. A run must not be assumed to exist or pass. Check workflow permission/configuration and re-query on resumption; consider protected PR-based test if push trigger does not fire.
- Exact work-branch head before this journal commit: `3646c2d22a2bcc1c1c31b0321eb6a75f6f285f71`.
- Source fixes are still unaccepted; do not merge, close findings, or claim complete remediation.

### #124 build-time mutation containment — 2026-10-16
- `f8a6a8bfc275c91cdd071820dc3558cac03c1174`: compare git porcelain status before/after build/regressions; remove implicit staging of embedded updater resource.
- `dae37d07cbf87986b20d5cc6c241828afa3fa8bc`: also compare binary tracked diffs (status alone cannot detect in-place edits to already-dirty files).
- **Incomplete security acceptance:** untracked files that already exist can still be edited in place without porcelain differences; ignored test/tool source must be inventoried; attacker-controlled build could restore mutated oracle before snapshot; evidence and tests may be attacked indirectly. Must isolate/protect the test oracle in a trusted workspace/process, audit all generated inputs, and run adversarial fixtures before considering #124 closed.
- Current automated CI is unverified. Keep all related findings open.

### Branch cleanup assessment and handoff — 2026-10-16
- User authorized cleanup. GitHub branch inventory/merge-base and unique-file reconciliation validated **12 safe obsolete branch candidates** (10 duplicate audit workstream scaffolds, audit/parallel-ro-20260925, noop-check). All 10 shard refs @ `852d53c6e4e6afff21c3ba1d53c7b72ebfdc2be9`; parallel extra README-only mapping commit `9ef93bb4663f29ef9ae4c1dd05480dfaf0661ff1`; noop strict ancestor @ `695cb0942307c460f71e8207160989902b68375d`. Preserve other 5 branches.
- Exact candidate heads and dispositions are persistently documented in [#13 comment 6220936725](https://github.com/Proponent-8247/youtube-dl-gui/issues/13#issuecomment-6220936725).
- **Deletion not executed:** connector has no delete-ref tool and no authenticated local remote access exists. Tested local script `prune_obsolete_audit_branches.py` (available as conversation download): dry-run, branch SHA drift abort, exact-SHA force-with-lease, 12 simulated removals, retained-five protection, idempotence and actual Git local bare-remote deletion syntax. The real remote had **17** branches at last verification.
- Run script from authenticated checkout for read-only preview then `--apply`; verify live branch count 5 afterward. Preserve integration #13 open until later release consolidation and master README decision.
