# RECOVERY_JOURNAL.md — youtube-dl-gui whole-repository audit

**Status (last reconciled 2026-11-08 UTC): READ-ONLY AUDIT AND 17→4 BRANCH CONSOLIDATION COMPLETE; 18 OF 130 FINDINGS ACCEPTED/CLOSED, 112 REMAIN; #97/#98/#99/#103/#134 NEWLY ACCEPTED; #124 HARDENED BUT RACE ACCEPTANCE OPEN; CRITICAL #93 AND TRUST-ROOTS #94/#116 REMAIN OPEN.**

Repository: https://github.com/Proponent-8247/youtube-dl-gui
Integration line: `feature/download-history-archive-3.3.0-2`
Audited immutable source SHA: `7e369eb4afa6477e81f3e185ce9754bfd91982a6`
Base release SHA: `49a290d07b220e4a9cd98d44c6db7f45a5bbbc98` (`release/3.3.0-2`)
First recovery-journal-only commit: `76a4c725bfda62656cf4ff2ac25f61d303ea3555`
Authoritative source tree at audit pin: 394 tracked paths; recursive API tree `truncated=false`
**Audit source pin is unchanged.** Since the read-only audit, authorized branch consolidation and separate remediation changes have occurred. The feature branch now contains archived audit scaffolds and a README edit; production remediation remains isolated on `remediation/whole-repo-20261016`. Resolve actual live SHAs on every resumption.

## User-approved phase boundaries (historical read-only phase; superseded by later remediation and consolidation authorization)

During the original read-only phase, the user instruction was: audit the **entire** repository, record supportable findings **immediately** in a durable repo TODO/GitHub Issues ledger, and **perform repairs later after the audit**. Do not treat “continue until all work complete” as authorization to start production fixes during the audit-only phase.

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

## User-authorized branch consolidation — 2026-10-16

Goal: reduce 17 live branches to no more than 5 (prefer 4), preserving unique useful commits and avoiding release of unaccepted fixes. Read #13 latest comment (2026-10-16) for complete 17-ref inventory, exact retiree SHAs, compare evidence, and pre-delete safety criteria.

- PR #150 successfully **MERGED** archive/coordinator `audit/parallel-ro-20260925` into feature at `d7ac486387900e5257bd10ea61f6af5818f198f3`: 12 original commits and 11 Markdown audit-scaffold records are now in feature DAG; all ten identical WS scaffold refs are ancestors.
- Unique `master` README attribution edits were forward-ported into feature in `0a111fa5e4df13e3052192bb474cdea30fb32b72`, preserving the master wording.
- PR #151 successfully **MERGED** feature's historical audit/README changes into remediation in `bb3daa18674aaa4ff53191e5f8e8c653f2145289`. Remediation remains isolated, **not CI-green** and not landed into master/release/feature.
- Retirees: ten `audit/ro-ws*` refs at `852d53c6e4e6afff21c3ba1d53c7b72ebfdc2be9`, `audit/parallel-ro-20260925` at `9ef93bb4663f29ef9ae4c1dd05480dfaf0661ff1`, `audit-fixes` at `7283444431e1243d83b86bf54838d22e3048cae9`, and `noop-check` at `695cb0942307c460f71e8207160989902b68375d`.
- GitHub compare confirmed `ahead_by=0` for all retiree commit groups versus feature (after merge), and the sole open PR #1 uses retained `release/3.3.0-2`. Candidate refs were unprotected at inventory.
- Keep 4: `master`, `release/3.3.0-2`, `feature/download-history-archive-3.3.0-2`, and `remediation/whole-repo-20261016`.
- State at this journal's start: **17 refs still present; retiree deletion NOT YET VERIFIED**. Connector exposes ref creation/update/compare but no deletion; if using a one-time GitHub Actions cleanup, require exact SHA, ancestry and PR checks and verify GitHub branch list afterward. Do not claim cleanup complete without read-back.
- Source remediation / 130-issue acceptance and Windows regression remain unresolved. This branch consolidation does not represent a release acceptance.
- Working branch SHA immediately before this journal update: `bb3daa18674aaa4ff53191e5f8e8c653f2145289`.

## 2026-10-24 UTC — BRANCH COUNT CONSOLIDATION COMPLETED AND VERIFIED

**User goal:** reduce 17 remote branches to no more than 5 without losing unique useful work. **Result: 4 branches remain**, independently verified via `GET /repos/Proponent-8247/youtube-dl-gui/branches?per_page=100` after deletion and after removal of the one-time cleanup workflow.

### Evidence / exact retained refs (post-cleanup)

| Kept branch | Verified SHA | Role |
| --- | --- | --- |
| `master` | `6e2dd1df40cfcde0fd6a264b3708b12999d70d7b` | Default; one-time cleanup workflow removed |
| `release/3.3.0-2` | `49a290d07b220e4a9cd98d44c6db7f45a5bbbc98` | Release baseline; open PR #1 head |
| `feature/download-history-archive-3.3.0-2` | `0a111fa5e4df13e3052192bb474cdea30fb32b72` | Integrated source and archived audit-history line |
| `remediation/whole-repo-20261016` | `5b6b9143750efe05951f07c4666cf37ea766cf7e` | Unaccepted security remediation work (SHA before this journal commit) |

**Execution:** GitHub Actions [run #40063459127](https://github.com/Proponent-8247/youtube-dl-gui/actions/runs/40063459127), workflow_dispatch, completed SUCCESS 2026-10-24 04:44:23 UTC. Job `prune` / job ID `120781680656`, step `Verify all branch identities, ancestry and PR safety, then retire redundant refs` SUCCESS. Log explicitly states `Observed 17 live branch refs`, `All preflight gates passed`, prints **13 distinct `Retired ... at <SHA>` lines**, and lists the four branches above afterward. API independent read-back also reports exactly those four refs.

**Retired 13 SHA-checked refs:**
- `audit/parallel-ro-20260925` @ `9ef93bb4663f29ef9ae4c1dd05480dfaf0661ff1`
- Ten `audit/ro-ws01-startup-config` through `audit/ro-ws10-concurrency` @ identical `852d53c6e4e6afff21c3ba1d53c7b72ebfdc2be9`
- `audit-fixes` @ `7283444431e1243d83b86bf54838d22e3048cae9`
- `noop-check` @ `695cb0942307c460f71e8207160989902b68375d`

**History preservation / disposition:** PR [#150](https://github.com/Proponent-8247/youtube-dl-gui/pull/150) merged the coordinator/scaffold history into feature, preserving the ten shard-head ancestries. Unique README changes from master were forward-ported to feature in `0a111fa5e4df13e3052192bb474cdea30fb32b72`. PR [#151](https://github.com/Proponent-8247/youtube-dl-gui/pull/151) merged the feature's archived historical documentation into remediation. The other obsolete refs were already ancestors. No useful unique production changes were lost, and no published branch was force-rewritten.

**Temporary privilege removal:** Removed `.github/workflows/one-time-branch-consolidation.yml` from master in `6e2dd1df40cfcde0fd6a264b3708b12999d70d7b` after confirmed success. There is no standing branch-deletion automation left in master.

**Status of separate gates:** This closes ONLY the branch-count/redundant-ref-cleanup milestone. Issue [#13](https://github.com/Proponent-8247/youtube-dl-gui/issues/13) remains open for final build/regression/security audit/release consolidation gates. Successful branch-prune workflow IS NOT evidence that Windows compilation or regression testing passed. The existing DH-A070/#142 regression and 130-finding remediation queue [#149](https://github.com/Proponent-8247/youtube-dl-gui/issues/149) remain outstanding; Critical #93 and related guard findings have unaccepted security changes isolated on remediation.

**Next resumption:** Read this section and #13; verify the four branch refs before other Git operations. Continue acceptance tests and remediation on `remediation/whole-repo-20261016`; keep feature/release/master isolated until the tests and final release gate pass. Do not repeat the obsolete branch-deletion operation.

## 2026-11-06 UTC — REMEDIATION RESUMED AFTER MEMORY/INSTRUCTIONS REVIEW

**Authoritative base:** `remediation/whole-repo-20261016` from `a36e90fcbfed1c812e2a318bd4a4065fd47c7100` at start of this session. The entire 4-branch inventory was reverified; no branches were recreated and no source was merged to feature/release/master. Pinned audit source: `7e369eb4afa6477e81f3e185ce9754bfd91982a6`.

**Recovered user workflow requirements:** Keep complete A–H interaction/decision skeleton (adaptive), versioned `RECOVERY_JOURNAL.md` plus issue-specific durable GitHub checkpoints and exact next cursor, conceptual commit per independent change, before/after build and adversarial execution-level gates. Do not close issues based only on source review, test-name inventory, or compiling; distinguish threat coverage and acceptance. No force/rewrite of published history; preserve release and historical audit snapshots. No secrets or private identifiers sent to unapproved external recipients.

### Completed and accepted: DH-A070 / #142 (HIGH)
- `1f8ce2d74ca6e8f690a36ac08200975e79f2b426`: independently scoped regression-contract correction: a legacy temp-companion test now establishes real archive ownership before Reset instead of bypassing the bound-state contract via private fields. Original temp-file non-destruction assertions preserved.
- `8ec58efc6f48779d672c71c68263906c39e92b0a`: Reset under `Sync`, fail closed if never enabled or no durable bound archive, reset only `Path.GetFullPath(BoundArchivePath)` (not mutable `EffectiveArchivePath`). Reject disabled-mode physical rebinding from `CommitSettings` and `ArchivePath` setter while preserving equivalent representations/implicit bound path and never-enabled future candidates.
- Windows Audit Build [#42306198166](https://github.com/Proponent-8247/youtube-dl-gui/actions/runs/42306198166): **SUCCESS**. Windows Audit Verification [#42306198170](https://github.com/Proponent-8247/youtube-dl-gui/actions/runs/42306198170): **SUCCESS** at exact production SHA, including compiled-application, full Release/packaging, and x86 regressions. Log explicitly shows `PASS DOWNLOAD_HISTORY.ResetRequiresOwnedBoundArchive` and `PASS DOWNLOAD_HISTORY.ResetPreservesUnownedTempCompanions` in all three passes. No FAIL regression lines.
- GitHub [issue #142](https://github.com/Proponent-8247/youtube-dl-gui/issues/142) CLOSED with issue-specific evidence and acceptance. [Queue #149](https://github.com/Proponent-8247/youtube-dl-gui/issues/149) now checks DH-A070 and records **129 outstanding issues**. Related #143/#145/#102 (reparse and hard-link ownership) remain open, not conflated with #142.

### Implemented, NOT YET ACCEPTED: release-security #101 and #100 (HIGH)
- `1736152f39eeef58d8cba1785f9eacede58dda40`: add `SECURITY.ReleaseCandidateDispatchVersionIsData` regression protecting the exact workflow env-to-PowerShell data boundary and rejecting direct expressions in the PowerShell run body.
- `d3f191eafff2014ed0497799043228b5fae94afe`: #101, move manual `expected_version` from inline expression in PowerShell source into step-level `EXPECTED_VERSION_INPUT` environment variable. Preserve branch-derived fallback. Adversarial `workflow_dispatch` with quotes/semicolons/newlines and forged artifact refusal still needs live execution-level proof.
- `f2e8218622c1da8bd9f82d5200516cc1fb982afb`: #100, require true immutable reviewed SHA `7e369eb4afa6477e81f3e185ce9754bfd91982a6` in release-candidate ancestry check. Keep `audited_base` manifest field. Live GitHub compare proves current `release/3.3.0-2` is 545 commits behind the reviewed baseline and cannot pass; remediation is a descendant. Negative/positive candidate acceptance and independent verifier trust-root #116 still pending.
- At this checkpoint: Audit Build [#42306784006](https://github.com/Proponent-8247/youtube-dl-gui/actions/runs/42306784006) **SUCCESS** at `f2e82186`; Audit Verification [#42306783959](https://github.com/Proponent-8247/youtube-dl-gui/actions/runs/42306783959) was **IN PROGRESS** after passing compiled regression and Release build, while the full-Release/x86 rerun continued. Do not infer final acceptance from this interim state. Earlier test-only CI was canceled automatically by later push; the final workflow run is the relevant code SHA.
- Both findings remain OPEN. Issue-specific implementation comments are persisted in #100 and #101.

### Next exact cursor / risks
1. Re-query run `42306783959`, inspect exact head, step conclusions and hostile-input regression. If unsuccessful, remediate focused cause first. If successful, note that end-to-end `workflow_dispatch` exploit probes and release verifier independent trust (#116) are not yet proven by ordinary regression.
2. Prioritize **Critical #93**: earlier verifier/publisher privilege split is implemented but independent artifact attestation, malicious `.csproj` write attempt and standalone read-only verification runner have not passed. Related #94/#95/#96/#105/#124 trusted guard/parser/test oracles remain open.
3. Continue highest severity Download History archive/reparse and unsafe argument issues (#143-#148, #59, #66, #102, #147), and provider/updater provenance. Work on remediation branch, conceptual commits, issue-specific negative and full Windows test gates; update [queue #149](https://github.com/Proponent-8247/youtube-dl-gui/issues/149) only for accepted closures.
4. Do not change the four-branch inventory, merge unfinished fixes into release/master or claim release-readiness. Four retained branches: `master`, `release/3.3.0-2`, `feature/download-history-archive-3.3.0-2`, `remediation/whole-repo-20261016`.

**Exact source branch head before this journal-only commit:** `f2e8218622c1da8bd9f82d5200516cc1fb982afb`. Journal write uses `[skip ci]` to avoid interrupting the in-flight exact-source verification run. Resolve updated head and journal blob by read-back after writing.

### 2026-11-06 final CI read-back for release-security changes

- Exact tested source `f2e8218622c1da8bd9f82d5200516cc1fb982afb`.
- [Windows Audit Build #42306784006](https://github.com/Proponent-8247/youtube-dl-gui/actions/runs/42306784006): **SUCCESS**.
- [Windows Audit Verification #42306783959](https://github.com/Proponent-8247/youtube-dl-gui/actions/runs/42306783959): **SUCCESS**. Debug build, updater and application Release baseline, compiled regression harness, full Release packaging, and full-Release/x86 re-runs all green. `SECURITY.ReleaseCandidateDispatchVersionIsData` explicitly PASS three times; `DOWNLOAD_HISTORY.ResetRequiresOwnedBoundArchive` remains PASS in all three passes.
- Artifacts: `audit-verification-f2e8218622c1da8bd9f82d5200516cc1fb982afb` (ID `13546493626`, SHA256 `0ed90310e970551f3a1df7d115cc1d8776de79ff47ef256b672c030f7efb3169`) and `audit-source-f2e8218622c1da8bd9f82d5200516cc1fb982afb` (ID `13546478250`). Both stored by GitHub Actions, no local artifact substitution.
- #100 and #101 are **NOT CLOSED**: actual negative `workflow_dispatch` hostile-version probes, legacy-head rejection and independent release verifier enforcement still require issue-specific proof; #116 remains open. This is a positive full-regression gate, not all security acceptance gates.
- Read-only audit, 4-branch consolidation, and #142 closure remain complete. **129 findings remain open** in canonical queue #149.
- Exact current working branch head before this journal-only update: `a199e997bd1ac2f4771b28b3697f9b5b7fe2587c`.
- Resumption cursor: prioritize Critical #93 credential separation verification and independent artifact trust; test #94/#95/#96/#105/#124 with adversarial job fixtures. Then full hostile release input/ancestry acceptance for #100/#101 and archive path/reparse/hardlink ownership regressions #143/#145/#102. No unverified releases or merges.

## 2026-11-06 UTC — REPAIR SECURITY AND REPARSE-STATE CHECKPOINT

**Source head before journal:** 1638dacaeae1426eae2eb8df270917685beb2c54. Four branch limit maintained; no release/feature/master integration.

### Critical #93 - automatic privileged publication DISABLED; trusted promotion still pending
- 67d8e4f21210f18b6f3c0f6ad3760dff16f0803d adds read-only publisher-absence regression.
- d3ee0c7b7fd632ed437be20eb7b274e6a8dc4afd removes the privileged artifact-consuming publish job entirely. Guarded repairs now generate read-only review artifacts, not automated repository writes.
- 4df4858addba11eaddbb9d9ce04508663e746546 adds hostile MSBuild fixture with dry-run git push; 592bac9914748bebe29506844fcd69fe4c78319d fixes XML comment, 7155107991d7b305b537d96897a7aae2b4c2b43f wires workflow probe, f61e0c78022bf6a26002dc007e13a4f565b166fe accepts observed Git Credential Manager denial.
- Windows Audit Build #42308255974 SUCCESS; Audit Verification #42308255968 SUCCESS at source f61e0c7. Dry-run push failed for lack of authentication. SECURITY.GuardedRepairHasNoAutomaticWritePublisher PASS 3/3.
- Critical #93 remains OPEN: no separately trusted review/promote path implemented. #94/#124 are independent unresolved trust-oracle issues.

### High #95 and #96 CLOSED — canonical count 127 outstanding
- #95: tests 42205bbaa07815e900a390aef25a29794b856131; parser fail-closed commit ce7c9824c745e58416d8f846db93c0a59aef1ee1; runner pre-build rejection f9c86ab63a6696eb74e39c32becf08c9f46b37bd. Both metadata and preflight reject request-controlled flaky exemptions.
- #96: tests aa7feedd4279bf6771d6db74169e4852120d0f2c and prior source parser restriction 186f1c06323d0452c9766489c616d0d5860e714b. Actual positive allowed-edit and negative tests-edit parser fixtures.
- All tests PASS in three Windows regressions at f61e0c7, including full Release and x86; the two findings were closed and canonical queue #149 updated to 127 remaining.

### High #145 IN PROGRESS — exact archive/backup/lock reparse-point protection
- e16458ad92a2db5ba4710cbf801def8ee9068a3f adds real Windows mklink symlink fixtures covering unrelated archive, bound-archive .bak and .lock; assert target bytes intact.
- 1638dacaeae1426eae2eb8df270917685beb2c54 adds exact entry reparse-point rejection before read, lease, reset, write/backup and with rechecks. Configured root junctions remain allowed.
- At checkpoint Audit Build #42308795862 and Audit Verification #42308795884 were running on 1638daca. Do not close #145 until all three Windows passes and adversarial fixture behavior verified.
- File-system TOCTOU races and hard-link/DOS alias identity (#102) remain separate unaccepted risks.

### Next cursor
1. Inspect GH runs 42308795862 / 42308795884; diagnose any failure before accepting #145.
2. Add runtime hostile version-input and release-ancestry negative tests for #100/#101; #116 release verifier independence remains high severity.
3. Implement Windows file identity/hard-link checks for #102 and trusted promotion for Critical #93/#94; preserve four branches and do not merge unverified source.

## 2026-11-06 UTC — DH-A073 ACCEPTED, EMERGENCY FEATURE WORKFLOW HARDENING, HARD-LINK WORK

Source head before this journal: fc5aa1cc7286ed56265b095b7ccb54da89abe5cb. Four retained branches; no release/master integration.

### #145 / DH-A073 CLOSED — source SHA 1638dacaeae1426eae2eb8df270917685beb2c54
- Real Windows file-symlink archive/backup/lock fixture e16458ad92a2db5ba4710cbf801def8ee9068a3f; production exact state-entry reparse refusal 1638dacaeae1426eae2eb8df270917685beb2c54.
- Windows Audit Build run #42308795862 SUCCESS. Windows Audit Verification #42308795884 attempt 1 failed unrelated V011 rollback IPC startup timeout waiting for updater-pid in x86; the new reparse test passed all 3 passes. Exact same code SHA attempt 2 of #42308795884 SUCCESS, including packaging, x86 and 3/3 DOWNLOAD_HISTORY.RejectsReparseArchiveAndCompanions PASS, no FAIL lines.
- #145 closed with acceptance details. Queue #149 reconciled to **126 original findings remaining** (accepted #142, #95, #96, #145). #102 physical aliasing and #143 descendant reparse paths remain open.

### Critical #93 feature-branch deployment hotfix, separate from unaccepted production fixes
- The feature branch still had the original guarded-repair workflow granting contents: write and default persisted checkout credentials, even though remediation branch had safe read-only workflow. The unsafe active feature CI needed immediate privilege reduction.
- Feature-only CI workflow commit 5dac86e799b84609af2face5cf5031f26d5faf2f: .github/workflows/audit-repair.yml contents: read and persist-credentials: false. No product source changed, no remediation code merged, old runner may fail when attempting a push; that fail-closed behavior is intentional until trusted promotion exists.
- GitHub read-back verified feature head and no-write/no-persistent-credentials; feature Audit Build #42309420222 SUCCESS. The feature baseline audit verification has unrelated Download History regressions and is not security acceptance. #93 remains open for trusted promoter, with #94/#124 separate.

### #102 / PAR-09-001 HIGH — Win32 file identity work, pending CI acceptance
- 7552c3b07edacf9f866246a53ed19e61d6e0792d: actual Windows CreateHardLinkW fixture against unrelated native archive, bound .bak and .lock companions, cookie hard-link alias, alternate directory junction alias and distinct identical-content positive control.
- 2335128f36aeee9080ea96bf9daace157445691a: use read-only GetFileInformationByHandle BY_HANDLE_FILE_INFORMATION and Windows volume/file IDs; reject state with LinkCount !=1, compare cookie file identity against archive/backup/lock to catch hard-link/DOS/alternate junction names, without following state redirects or altering selected root semantics.
- fc5aa1cc7286ed56265b095b7ccb54da89abe5cb: negative test expects fail-closed exception from lease state identity guard rather than a permissive report.
- Windows Build #42309902049 SUCCESS at fc5aa; full Windows Audit Verification #42309902054 in progress at this checkpoint, NOT YET ACCEPTED. Recheck exact run and fix test/source failures before closing #102. This is preflight identity enforcement and does not claim race-free native provider write.

### Next cursor
1. Read run #42309902054, verify both new hard-link/physical-cookie regression tests in all 3 passes and no other failures; fix if necessary. Reconcile #102 issue/queue only after acceptance.
2. Complete actual hostile PowerShell release input and reviewed ancestor positive/negative tests for #101/#100; #116 release verifier trust root independent and open.
3. Implement separate trusted guarded-repair promotion after Critical #93 mitigation, plus #94/#105/#124 integrity acceptance. Review persistent source and V011 intermittent startup timeout without accepting flaky exemptions.
4. Preserve exactly four branches and explicit GitHub journal; avoid released artifacts/branch integration until all release gates pass.

## 2026-11-06 UTC — HIGH #102 ACCEPTED, RELEASE SECURITY NEGATIVE TESTS STARTED

Source head before this journal: eb94cb728947eb03d76b464482765b2cff44b5fa; four branch limit maintained, no release/master/feature product merge.

### PAR-09-001/#102 CLOSED with Windows file-identity acceptance
- Test commit 7552c3b07edacf9f866246a53ed19e61d6e0792d creates NTFS hard-link primary/backup/lock and cookie junction alias fixtures; production 2335128f36aeee9080ea96bf9daace157445691a uses read-only GetFileInformationByHandle and link count/file identity; test expectation corrected fc5aa1cc7286ed56265b095b7ccb54da89abe5cb.
- Initial Windows test at fc5aa1c failed existing DOWNLOAD_HISTORY.ManagementFailsFastWhenBusy because link count verification attempted a second open of an exclusively held lock. Fixed in fdeba304d4fe5edce7ad43960e78605ae8b02048 by inspecting the acquired lock's SafeFileHandle, rather than reopening it; important regression/compatibility fix.
- Windows Audit Build #42310174106 SUCCESS and Audit Verification #42310174238 SUCCESS on exact fdeba304. Both new hardlink and cookie aliases, existing busy-lock and prior reparse-point tests all PASS 3/3 including x86 with no FAIL lines. #102 closed with evidence, canonical queue #149 now **125 original findings remain**.
- Remaining distinction: identity at preflight is not a race-free handle pinned to native yt-dlp open. #143 descendant reparse path and other TOCTOU risks still OPEN.

### PAR-07-016/#100 and PAR-07-017/#101 execution-level test implementation
- eb94cb728947eb03d76b464482765b2cff44b5fa: atomic two-file commit adds tests/AuditRegression.ReleaseSecurity.cs and registers SECURITY.ReleaseVersionInjectionCannotRunPowerShell and SECURITY.ReleaseCandidateRejectsOldReviewedHistory in RepairWave17.
- Release input test extracts real workflow run source, executes it under Windows PowerShell with quoted/semi-colon, CRLF and expression-like input through EXPECTED_VERSION_INPUT; uses mock verifier to prove literal data and no injected.marker side effect. Then invokes real release verifier with malicious version and requires version mismatch before build, not artifact creation.
- Release ancestry test uses actual git merge-base runtime checks: reviewed audit SHA is ancestor of current code; stale prerelease SHA descends from old policy but not required reviewed source. Verifies release script pins the real reviewed SHA and manifest records audited_base.
- Windows Audit Build #42310749163 and Audit Verification #42310749189 started at exact test SHA, no accepted result yet. Keep #100/#101 OPEN pending all three passes and any separate release workflow trust #116.

### Next cursor
1. Inspect runs 42310749163/42310749189, diagnose actual PowerShell process or git ancestry tests if red; close #100/#101 only when evidence complete and no unstated scope claims.
2. Continue Critical #93 trusted promotion/root #94 and remaining high Download History #143/#144/#146/#147/#148, high injection #59/#66/#24.
3. Maintain 4 branches and issue-centric recovery journal; no forced pushes/release publication.

## 2026-11-06 UTC — RELEASE SECURITY ACCEPTED; PROXY/SUBTITLE ARGUMENT BOUNDARY IN PROGRESS

Source head before checkpoint: 7052228474889d9636eb61a8d7fa29e64a1a622c. Preserve four branches, no release/master/feature application merge.

### PAR-07-016/#100 and PAR-07-017/#101 accepted/closed
- Source f2e8218622c1da8bd9f82d5200516cc1fb982afb pins required audited release SHA 7e369eb4afa6477e81f3e185ce9754bfd91982a6; source d3f191eafff2014ed0497799043228b5fae94afe sends expected_version through step env EXPECTED_VERSION_INPUT rather than interpolating shell source.
- Tests eb94cb728947eb03d76b464482765b2cff44b5fa: SECURITY.ReleaseVersionInjectionCannotRunPowerShell executes the ACTUAL release workflow step in PowerShell with hostile strings and a harmless mock, then proves real verifier version mismatch; SECURITY.ReleaseCandidateRejectsOldReviewedHistory tests actual git ancestry and rejects stale release while accepting reviewed head.
- Windows Audit Build #42310749163 SUCCESS and full Audit Verification #42310749189 SUCCESS at eb94cb7. Both tests PASS in all 3 runs, including x86. #100/#101 CLOSED with exact evidence. Caveat: no live GitHub workflow_dispatch event was performed, but Actions-equivalent actual PowerShell source/data-boundary execution was performed. #116 separately owned verifier trust root remains open.
- Canonical issue #149 reduced from 125 to **123 remaining original findings**.

### High #59 proxy input injection and #66 subtitle format injection implementation — pending acceptance
- 191bbd92d7c6b210efbdd5f39f914f54985b1345: atomic test commit of tests/AuditRegression.ProviderArgumentSecurity.cs and RepairWave17 registration, with positive valid settings and malformed host/port/format negatives under protected Download History for standard and extended generators.
- 11583a6da93a0d9c646e867b462f3c812dbf8267: #59, shared DownloadHelper.TryBuildProxyArgument validates protocol selection, host grammar, numeric 1-65535 port and URI, produces complete escaped proxy operand. Standard and extended argument builders refuse malformed settings and clear DownloadHistoryExecution before command publication. Previously KeyPress-only UI checks were bypassed by clipboard/INI settings.
- 7052228474889d9636eb61a8d7fa29e64a1a622c: #66, shared DownloadHelper.TryBuildSubtitleFormatArgument validates format-name/slash-selector grammar and emits one escaped --sub-format value; invalid persisted whitespace/--exec or option terminator patterns reject both generators before command publication.
- Windows Build #42311525829 and full Audit Verification #42311525724 running at exact 7052228; neither #59 nor #66 accepted or closed until CI read-back. Comments in each issue.

### Next cursor
1. Inspect runs #42311525829/#42311525724 for syntax/build failures and SECURITY.ProviderProxySettingCannotInjectArguments / SECURITY.ProviderSubtitleFormatCannotInjectArguments all 3 passes; remediate test/source if red.
2. Then high #147 EJS unverified remote script injection, #24 playlist/date injected settings, #26 Authenticode revocation and #27 signed-app authenticity; Critical #93 publisher trust root still open despite read-only mitigation.
3. Continue issue-specific checkpoints and queue only on accepted gates, no force updates or release promotion.

## 2026-11-06 UTC — PROVIDER-ARGUMENT SECURITY ACCEPTED; EJS & SELECTION REMEDIATION IN PROGRESS

**Current exact branch head before this journal commit:** `7db8a204e23f924514b303572a2306e6d2d92107`. All four GitHub branches remain, no release/master production integration.

### High #59 and #66 ACCEPTED/CLOSED
- Test commit `191bbd92d7c6b210efbdd5f39f914f54985b1345`: `SECURITY.ProviderProxySettingCannotInjectArguments` and `SECURITY.ProviderSubtitleFormatCannotInjectArguments` positive and negative settings tests for standard and extended builders under protected history.
- #59 production `11583a6da93a0d9c646e867b462f3c812dbf8267`: validate pasted/persisted host/port/protocol; emit complete proxy URL as a single escaped operand; reject malformed settings before protected command publication.
- #66 production `7052228474889d9636eb61a8d7fa29e64a1a622c`: validate persisted subtitle format grammar; emit one escaped `--sub-format` operand and reject malformed option-like values.
- [Audit Build #42311525829](https://github.com/Proponent-8247/youtube-dl-gui/actions/runs/42311525829) and [Audit Verification #42311525724](https://github.com/Proponent-8247/youtube-dl-gui/actions/runs/42311525724) **SUCCESS** at exact code `7052228474889d9636eb61a8d7fa29e64a1a622c`. Both new tests PASS in **all three** runs including x86; no regression FAIL lines. Issues #59 and #66 CLOSED with acceptance details. Canonical queue #149 now has **121 remaining** of original 130.

### High #147 / DH-A075 remote EJS loading, implementation pending full CI
- `1e70e184ef43838baed858268c215dd6cbda514b`: `DOWNLOAD_HISTORY.RejectsUntrustedRemoteEjsScripts` tests explicit/equal/quoted/abbreviated `--remote-components ejs:github`, `ejs:npm`, `--extractor-args youtube-ejs` developer keys (dev/repo/script_version), safe normal YouTube extractor tuning, and unchanged disabled-history behavior.
- `00d42b170b3a547fe27848e44e505dc4f1f2478f`: reject any custom remote component while Download History is protected, and reject custom `youtube-ejs:` developer extractor namespace without blocking unrelated safe `youtube:player_client` tuning. Block quoted/abbreviated/equal option forms through the existing tokenizer. No untrusted JavaScript is fetched or run by tests.
- Windows Audit Build #42311986580 **SUCCESS**. Windows Audit Verification #42311986563 at exact source `00d42b1` has passed compiled regression and full Release package and was running x86/full-Release reruns at this checkpoint. **DO NOT CLOSE #147** until complete green exact-SHA log read-back, 3/3 negative EJS test and source scope review.
- Downstream `7db8a204e23f924514b303572a2306e6d2d92107`: independent test-only [skip ci] commit for High #24 `SECURITY.ProviderSelectionFieldsCannotInjectOptions`: valid playlist range/date and invalid pasted `--exec`, option terminators/newlines/date; integer playlist start/end compatibility and disabled-history negative. #24 production implementation NOT YET COMMITTED. Do not mistake #147's run as testing #24.

### Exact recovery cursor
1. Inspect Windows run #42311986563 / #42311986580. If green, close #147 and queue mark after reviewing `DOWNLOAD_HISTORY.RejectsUntrustedRemoteEjsScripts` three PASS lines, no failures.
2. Implement High #24 shared strict selector grammar with early command-generation fail closed and UI feedback, preserving valid playlist/date and integer start/end, then test [skip ci] test `7db8a20` and full Windows CI.
3. Continue #143/#144/#146/#148 archive integrity, #26 Authenticode revocation, #27 signed-app updater trust, #70 PATH discovery writes, Critical #93 independently trusted publication.
4. Preserve 4 branches, conceptual commits, per-issue evidence and journal, no release merge without gates.

## 2026-11-08 UTC — REMEDIATION CONTINUATION, REQUEST/UPDATE/ORACLE INTEGRITY

**Authoritative branch:** `remediation/whole-repo-20261016`. Other three retained branches unchanged; no unaccepted fixes promoted to master, feature or release. Work branch before this journal-only checkpoint: `ba2aed019532b9d158627a452fbe947f9f1e5696`. Original immutable audit source `7e369eb4afa6477e81f3e185ce9754bfd91982a6`; reviewed release base `49a290d07b220e4a9cd98d44c6db7f45a5bbbc98`.

### ACCEPTED and CLOSED (2 new findings, 117 remain)
- **#70 / PAR-01-014 (HIGH)**: Regular provider/FFmpeg updating never adopts executables discovered on PATH or an existing external static path. Explicitly user-confirmed first-time installation can CREATE a properly named, missing custom destination only with no existing output/companions. Public updater/extractor API signatures preserved after original reflection compatibility regressions caught accidental breaking optional parameters. Negative executable-entry-point tests prove no modification of external sentinels, positive C3 first-run and V011 updater IPC pass. Latest exact source `a2fc6694454b2e0263740de8ec9cf0157e80d484`, Windows [build #42509868654](https://github.com/Proponent-8247/youtube-dl-gui/actions/runs/42509868654) and [verification #42509868662](https://github.com/Proponent-8247/youtube-dl-gui/actions/runs/42509868662) **SUCCESS**; relevant tests PASS 3/3.
- **#105 / PAR-07-019 (HIGH)**: Real Git request fixture tests accept exactly allowed wrapper + chunked part files and reject smuggled unrelated source changes BEFORE baseline build; request-commit patch plus SHA256 receipt preserved. PS5 JSON nested-array and missing Get-FileHash in isolated child process repaired using flattened strings and .NET SHA256 (`2579b7f`, `7d19ee5`, `adb457a`). `SECURITY.GuardedRepairRejectsSmuggledRequestCommit` PASS 3/3 on same Windows CI. #105 CLOSED.
- [Canonical queue #149](https://github.com/Proponent-8247/youtube-dl-gui/issues/149) checkboxes and accepted ledger updated for #70/#105; 13/130 accepted, 117 outstanding.

### IMPLEMENTED — acceptance pending
- **#124/PAR-09-012 (HIGH):** `tools/RepairInputIntegrity.ps1` added, loaded into trusted in-memory PowerShell before candidate builds, recording SHA-256 input snapshots of all test/tool/workflow files (tracked + untracked) and the embedded updater EXE; runner `Assert-ImmutableOracle` enforces the snapshot after each MSBuild stage, before compiling regressions, and after tests. Execution regression `SECURITY.GuardedRepairDetectsBuildTimeOracleMutation` exercises tracked test changes, new undeclared test, mutable guard, workflow, embedded updater and permitted bin/Debug output. Additional `tests/AuditOracleTamperProbe.proj` exercises a real malicious MSBuild target to modify a test, with the in-memory guard required to reject. Relevant commits `ae5cc36`, `11e9416`, `d33704e`, `73ae450`, `39efed5`, `1e99860`, `d6ad3e3`, `60e22de`. [Windows verification #42510582383](https://github.com/Proponent-8247/youtube-dl-gui/actions/runs/42510582383) SUCCESS at `1e99860`, initial oracle mutation test PASS 3/3; new explicit MSBuild target test is not included at that older source SHA. Do not close #124 yet.
- **#98/PAR-07-014 (MEDIUM):** immutable reviewed release-base evidence added to audit verification workflow `bc8d8cb`: exact `49a290d...` ancestor check and reviewed-base source ZIP, history, diff stat/files/source and SHA256, while master comparison remains supplemental. Regression `SECURITY.AuditVerificationUsesImmutableReleaseBase` added in `9f3dbf6` and registered `10a9e6c`. Need inspect actual GitHub Actions artifact and test acceptance.
- **#97/PAR-07-013 (MEDIUM):** requests must name distinct reviewed failing baselines and every conceptual repair must name at least one unique reviewed failure; parser rejects vacuous and non-baseline claims `d15e0a4`. Runner enforces the test actually failed immediately before the repair and passed immediately after `ba2aed0`. Existing security fixtures updated to valid review-failure plans `db87695`/`2042da2`; new runtime regression `SECURITY.GuardedRepairRequiresReviewedFailureResolution` `9382f78`. Await full Windows gates.

### Exact CI and next cursor
- Current combined Windows [build #42511135911](https://github.com/Proponent-8247/youtube-dl-gui/actions/runs/42511135911) and [verification #42511135925](https://github.com/Proponent-8247/youtube-dl-gui/actions/runs/42511135925) started at source `ba2aed019532b9d158627a452fbe947f9f1e5696` (prior to this journal-only commit); final conclusions **NOT YET VERIFIED** at journal write.
- Re-query exact job logs for `SECURITY.GuardedRepairRequiresReviewedFailureResolution`, `SECURITY.GuardedRepairDetectsBuildTimeOracleMutation` (real MSBuild fixture), `SECURITY.AuditVerificationUsesImmutableReleaseBase`, and release/x86 gates. Repair any real failures; do not lower tests.
- After green, independently inspect recorded reviewed-base evidence and decide #124/#98/#97 scope-specific acceptance; close issues and update #149 only after evidence passes.
- Still open and high-priority: Critical #93 (read-only guarded verification established, trusted standalone publication not yet accepted), #94 mutable guard trust root, #116 release candidate self-verification, Download History #143/#144/#146/#148. #103 request-parts cleanup and #134 self-cancelation also require separate disposition; no automatic writer currently exists.


## 2026-11-08 UTC — 18 ACCEPTED / 112 OPEN: TRUSTED REGRESSION INVENTORY AND REQUEST CLEANUP

- **#97 (MEDIUM), #98 (MEDIUM)** closed and accepted at exact `ba2aed019532b9d158627a452fbe947f9f1e5696`. Windows [Build #42511135911](https://github.com/Proponent-8247/youtube-dl-gui/actions/runs/42511135911) and [Verification #42511135925](https://github.com/Proponent-8247/youtube-dl-gui/actions/runs/42511135925) SUCCESS, each relevant positive/negative regression PASS 3/3. #97 no vacuous fixes; #98 GitHub source artifact `13629037644` downloaded and audited against immutable reviewed release SHA `49a290d07b220e4a9cd98d44c6db7f45a5bbbc98`; nested archive SHA256 matches `ae487019cf80e771ed3a46506f466160ab3578f18e5121c1b65c37dda11939a4`.
- **#103 (LOW) accepted**: `tools/RepairRequestLifecycle.ps1` deletes/stages only parser-validated exact wrapper and referenced part files; trigger `.github/workflows/audit-repair.yml` now covers `.audit-repairs.parts/**`. Real temporary Git+PowerShell regression `SECURITY.GuardedRepairCleansOnlyValidatedChunkedPayloads` rejects unapproved path and preserves unrelated tracked chunk; PASS 3/3.
- **#99 (MEDIUM) accepted**: 312-name immutable reviewed regression inventory, `tests/REVIEWED-REGRESSION-NAMES.txt` at pinned commit `ec7ee09e5658e8371ef2ac422f805f74411c61c4`. `tools/Assert-ReviewedRegressions.ps1` loads the manifest from `git show <trusted-commit>:...`, NOT candidate-local content; both audit-verify.yml and Build-ReleaseCandidate.ps1 enforce complete approved coverage for three passes and emit immutable manifest SHA in evidence. Runtime negative/positive `SECURITY.ReviewedRegressionInventoryCannotShrink` validates all 312, missing, extra, and duplicate names; PASS 3/3.
- **#134 (MEDIUM) accepted by structural retirement**: original auto publisher and remote cleanup push removed; `SECURITY.GuardedRepairHasNoAutomaticWritePublisher` PASS 3/3. Ordinary superseded request cancellation remains. #93 independent trusted publisher STILL OPEN.
- **Exact combined acceptance source:** `593ee657d64656a08dcb626e0c861c449668e60c`. Windows [Build #42511942865](https://github.com/Proponent-8247/youtube-dl-gui/actions/runs/42511942865) SUCCESS, [Verification #42511942922](https://github.com/Proponent-8247/youtube-dl-gui/actions/runs/42511942922) SUCCESS. GitHub artifact `13630017177` downloaded/independently inspected: initial, full Release and x86 each have **314 unique cases, zero failures**, 312 required reviewed tests present; `verification-summary.json` explicitly records reviewed manifest commit `ec7ee09...`, minimum 312, `passes=3`, release base `49a290d...`, candidate `593ee657...`. CI log states 312/314 coverage three times, tests #103/#99/#124 PASS all three runs.
- Initial #103 verification run `42511606942` at `802fbce2` failed due to **separate intermittent V011 IPC ack timeout**, not chunk cleanup; rerun was canceled by newer source push. Follow-on exact-source `593ee657...` passed V011 and all other cases 3/3. Do not self-whitelist this IPC regression.
- **#124 still OPEN:** build mutation SHA256 snapshot and hostile MSBuild target regression PASS 3/3 at `593ee657...`, including embedded updater resource, but a background process can race between source hashing and test compilation; stronger isolated oracle/candidate workspace acceptance remains necessary.
- **Remaining original queue:** 18 accepted / **112 open**. Canonical [#149](https://github.com/Proponent-8247/youtube-dl-gui/issues/149) reconciled checkboxes, user-specific issue evidence on #97/#98/#99/#103/#134; 4 remote branches preserved. Critical #93, High #94 and #116 pending trusted policy roots. No code merged to release/master/feature.
- Latest branch source HEAD prior to this journal update: `593ee657d64656a08dcb626e0c861c449668e60c`. Next cursor: continue High issues (#26 revocation, #27 signer identity, #36 crash backup recovery, #67 provider initialization cycle, #143/#144/#146/#148 archive correctness); retain focused negative/positive and full Windows regression gates. If any new core CI change, wait for green source SHA before closing.

