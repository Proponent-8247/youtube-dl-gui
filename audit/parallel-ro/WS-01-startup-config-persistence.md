# WS-01 — Startup, configuration, persistence, registry, CLI/IPC

Primary scope:
- `youtube-dl-gui/Program.cs`
- `youtube-dl-gui/Arguments.cs`
- `youtube-dl-gui/Config/**`
- `youtube-dl-gui/Classes/SystemRegistry.cs`
- `youtube-dl-gui/Classes/Verification.cs`
- startup/single-instance/protocol/IPC paths reached from those files

Audit for correctness, security, malformed input handling, persistence corruption/loss, startup/shutdown behavior, compatibility, race conditions, and error handling. Follow all relevant callers/callees outside the primary scope.

## Findings

