# WS-05 — Updater, trust, replacement/rollback, updater IPC

Primary scope:
- `youtube-dl-gui/Updater/**`
- `youtube-dl-gui-updater/**`
- embedded updater resource linkage
- update-related IPC and release metadata paths

Audit source-of-truth URLs, version selection, hash/signature/authenticity assumptions, download/retry, temporary paths, parent-process wait, cancellation, privilege boundaries, rollback/recovery, sender validation, path traversal/collision, and failure safety.

Respect existing policy: fork releases are intended update source; hashes are mandatory minimum integrity checks; authenticity verification is required when available.

## Findings

