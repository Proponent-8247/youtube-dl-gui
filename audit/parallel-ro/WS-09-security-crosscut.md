# WS-09 — Cross-cutting security/threat-boundary audit

Scope: **entire repository** at the pinned baseline.

Perform an independent security-focused pass across all production code, tests, workflows, tools, add-ons, and packaging. Trace trust boundaries end-to-end: URLs/CLI/IPC/clipboard/files/network/release metadata/media/image inputs -> parsing -> argument/process construction -> filesystem/registry/network/native calls -> logs/UI/output.

Focus on injection, quoting, path traversal, unsafe temp/replacement paths, credential/token exposure, updater authenticity, insecure transport, deserialization/parsing assumptions, untrusted image/media handling, process privilege/ownership, IPC sender validation, workflow/release supply chain, and denial-of-service/resource abuse.

Do not duplicate blindly. When a finding overlaps another workstream or historical F/O/V/P item, link it and state whether this pass confirms, narrows, or extends it.

## Findings

