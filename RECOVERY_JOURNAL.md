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
