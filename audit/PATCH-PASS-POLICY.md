# Patch-pass policy decisions — 2026-09-09

This checkpoint records the human product/security decisions supplied for the post-audit remediation pass. These decisions are binding for patches on `audit-fixes` unless explicitly revised later.

- **O006:** Closing the GIF conversion owner cancels the owned FFmpeg/ImageMagick conversion and performs safe cleanup.
- **O015:** Do not patch or complete the Release-hidden experimental Extended Converter feature in this pass.
- **O017:** Normal diagnostics redact URL credentials/tokens. Explicit raw/provider diagnostics remain raw and must warn that they may contain secrets.
- **O022:** Application-owned tool installation/update continues if Settings closes. Settings detaches safely; completion must use an application-owned surface, never disposed Settings controls.
- **O024:** Batch-from-file is tracked as active application work and participates in coordinated shutdown rather than being silently killed.
- **O033:** Link-file import is all-or-nothing. Read and validate first; on failure warn with how/where the import failed, then leave the queue unchanged.
- **P001:** Application updates come from `Proponent-8247/youtube-dl-gui` fork releases, not upstream `murrty/youtube-dl-gui` releases.
- **P002:** Retain console/output/queue history for the session rather than truncating it; make old session content optionally exportable.
- **P003:** File hashes are mandatory minimum integrity checks. Publisher/signature authenticity is also required when an authenticity mechanism is available.
- **P004:** Deferred. No policy change in this pass because the failure-mode expectations for Windows Job Object attachment are not yet defined.
- **P005:** Implement native image decoder isolation/sandboxing rather than accepting in-process decoder exposure as the final security boundary.
- **P006:** Experimental tool completion and download-history are outside current remediation scope. Download-history will be addressed after main bugs are resolved.

Patch discipline remains unchanged: preserve intended functionality and compatibility, avoid cosmetic mass-refactors, prefer one conceptual fix per commit, add focused regression coverage where practical, re-audit changed code/callers, and require exact-head build/test evidence before closing a finding.
