# Audit review index

Repository: `Proponent-8247/youtube-dl-gui`. Work branch: **audit-fixes**. No merge to master, release publication, history rewrite or download-history feature work is implied.

## Authoritative checklist set

1. **[Current whole-repository review checklist](CURRENT-REVIEW-CHECKLIST.md)**: the fresh review of source snapshot `e5858408008e17a34241779f18e7dfa1fed6a057`; reassesses O001-O008, adds O009-O036 and V011, and provides source locations, impact, evidence level and acceptance criteria. All current defect/risk items remain unchecked because this was a review/catalog task, not remediation.
2. **[Historical findings and existing validation/policy checklist](FINDINGS-CHECKLIST.md)**: preserves all F001-F143, O001-O008, V001-V010, P001-P006, source-commit mappings, evidence corrections and withdrawn records. The current review supersedes only the evidence status of the repeated O001-O008 entries; IDs are not duplicated in the combined count. See the additional current F121 path evidence below.
3. **[Review scope and verification](REVIEW-SCOPE.md)**: repository inventory, actual methods, positive evidence and limits.

**Combined inventory: 196 identified tracking items** = 143 historical F-items + 36 current O-items + 17 validation/policy V/P-items. Of the historical items, 17 retain their named, limited regression evidence and 126 still require broader finding-specific acceptance. There are also four withdrawn change records and unnumbered completion gates. None of these counts is an independent-vulnerability count.

## Current evidence supplement for an existing item

- [ ] **F121 — The main window's file-batch path still hosts download dialogs without setting STA.**
  - **Medium; source-confirmed apartment mismatch, specific COM/UI failure sequence pending.** The earlier correction `dbaa20e4f86141be2aa6f3753d7da85d5c41bc1c` adds SetApartmentState(STA) in frmBatchDownloader. The separate main-window route constructs a new Thread, creates frmDownloader and calls ShowDialog inside it, then starts the thread without setting its apartment. This is the same historical thread-affinity finding on an uncovered parallel entry point, not an additional O-item.
  - **Source:** [frmMain.cs:930-1025](https://github.com/Proponent-8247/youtube-dl-gui/blob/e5858408008e17a34241779f18e7dfa1fed6a057/youtube-dl-gui/Forms/frmMain.cs#L930-L1025); [the dedicated-window correction](https://github.com/Proponent-8247/youtube-dl-gui/commit/dbaa20e4f86141be2aa6f3753d7da85d5c41bc1c).
  - **Contract:** [Microsoft Thread.SetApartmentState documentation](https://learn.microsoft.com/en-us/dotnet/api/system.threading.thread.setapartmentstate?view=netframework-4.8.1) states that new threads default to MTA unless configured before Start. This source observation is not a claim that every WinForms action immediately fails on that thread.
  - **Acceptance:** Exercise both batch entry points, assert the apartment on the actual dialog thread, and test COM-dependent UI operations plus completion/cancellation. Marshal forms to an existing STA or establish an owned STA loop; do not remove worker/process cleanup. F121 stays unchecked until both paths are verified.

## Runtime evidence is not remediation

[Windows characterization run 34315957693](https://github.com/Proponent-8247/youtube-dl-gui/actions/runs/34315957693) reproduced all 12 expected observations on unchanged production code. Its green state means **bugs were observed**. [The evidence summary](evidence/current-observations.json) and [probe](repro/ReviewObservations.cs) identify the precise scope. The [userscript probe](repro/userscript-characterization.js) uses the actual repository script and a local DOM-shaped fixture, not a live browser.

The normal 80-test suite continues to have value, but it is not complete end-to-end coverage. It did not detect the newly reproduced batch, schema, duration, persistence, logging-gate and parser/control failures. Read the open V/P acceptance work before release sign-off.

The older [AUDIT-CLOSEOUT.md](AUDIT-CLOSEOUT.md) and [RECOVERED-FIX-HISTORY.md](RECOVERED-FIX-HISTORY.md) remain historical records. Their blanket completion language and incomplete commit index do not override the current checklists.
