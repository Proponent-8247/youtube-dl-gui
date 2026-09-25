# WS-06 — Controls, native interop, logging, authentication, localization runtime

Primary scope:
- `youtube-dl-gui/Controls/**`
- `youtube-dl-gui/Classes/NativeMethods.cs`
- `youtube-dl-gui/Logging/**`
- `youtube-dl-gui/Forms/frmAuthentication*`
- `youtube-dl-gui/Forms/frmLanguage*`
- `youtube-dl-gui/Language.cs`
- authentication data classes and runtime localization bindings

Audit Win32 ownership/message handling, handle/COM/thread requirements, control parsing, password/token handling, diagnostic redaction, exception paths, logging gates, localization reload behavior, and disposal/lifetime correctness.

## Findings

