# Parallel read-only audit — 2026-09-25

Pinned production baseline: `release/3.3.0-2` at `49a290d07b220e4a9cd98d44c6db7f45a5bbbc98`.

Purpose: split a complete code audit/review into resumable parallel work while preserving every finding in Git immediately.

## Hard rules

1. Product code is READ ONLY during this phase.
2. Do not modify source, tests, workflows, docs, existing audit files, branches, tags, releases, or published history.
3. The only allowed changes are additions/updates to your assigned `audit/parallel-ro/WS-XX-*.md` ledger on your assigned workstream branch.
4. Audit the exact pinned baseline above. If branch content later moves, continue to reason about the pinned baseline unless the coordinator explicitly rebases the audit.
5. Audit every file in your primary scope. Follow callers/callees across scope boundaries whenever necessary to determine correctness.
6. Record each supportable finding IMMEDIATELY; do not wait until the end of the chat.
7. One finding per commit where practical. Commit message: `audit(wsXX): record PAR-XX-NNN <short title>`.
8. Never implement fixes during the RO phase.
9. Do not treat style preferences as defects. Preserve intended behavior and compatibility requirements.
10. Existing `audit/` findings are prior evidence, not a substitute for fresh inspection. Reconcile duplicates explicitly.

## Finding format

- [ ] **PAR-XX-NNN — Title**
  - Severity: Critical | High | Medium | Low | Informational
  - Confidence: Confirmed | Probable | Validation lead
  - Category: correctness | security | reliability | concurrency | resource lifetime | compatibility | build | test | documentation
  - Source: exact file path + line/range or symbol
  - Behavior:
  - Impact:
  - Evidence / reasoning:
  - Relevant callers/callees:
  - Existing audit relation: F/O/V/P ID, duplicate/extension/new
  - Acceptance test for later remediation:
  - Fix status: **NOT IMPLEMENTED (RO audit)**

If a lead is disproved, append a `REJECTED LEAD` entry with the evidence.

## Completion record

Each workstream ends with:
- files/paths inspected
- cross-boundary files inspected
- findings posted
- rejected leads
- unresolved validation/runtime needs
- confirmation that product code was not changed

## Workstreams

- WS-01 Startup, configuration, persistence, registry, CLI/IPC
- WS-02 Download argument construction, provider selection, process execution
- WS-03 Main/download/batch/archive UI flows and queue lifecycle
- WS-04 Conversion/media metadata/ffprobe/thumbnails/merging
- WS-05 Updater, update trust, replacement/rollback, updater IPC
- WS-06 Controls, native interop, logging, authentication, localization
- WS-07 Tests, CI, build/release tooling, packaging, project inclusion
- WS-08 Documentation, add-on/userscript, resources, language/resx consistency
- WS-09 Cross-cutting security/threat-boundary pass across the entire repo
- WS-10 Cross-cutting concurrency/resource-lifetime/shutdown pass across the entire repo

After all workstreams finish, perform a coordinator reconciliation pass on a separate branch: deduplicate, map to historical F/O/V/P items, and produce the fix queue. Do not begin remediation before that pass.
