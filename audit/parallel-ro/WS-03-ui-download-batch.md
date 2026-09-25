# WS-03 — Main/download/batch/archive UI and queue lifecycle

Primary scope:
- `youtube-dl-gui/Forms/frmMain*`
- `youtube-dl-gui/Forms/frmDownloader*`
- `youtube-dl-gui/Forms/frmExtendedDownloader*`
- `youtube-dl-gui/Forms/frmBatchDownloader*`
- `youtube-dl-gui/Forms/frmArchiveDownloader*`
- `youtube-dl-gui/Classes/BatchHelper.cs`
- `youtube-dl-gui/Classes/QueueList.cs`

Audit threading/apartment state, queue mutation, form ownership/lifetime, cancellation, progress/state restoration, batch-file import, shutdown coordination, cross-thread UI access, resource leaks, and user-visible correctness.

## Findings

