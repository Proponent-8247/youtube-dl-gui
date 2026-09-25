# WS-07 — Tests, CI, build/release tooling, packaging, project inclusion

Primary scope:
- `tests/**`
- `.github/workflows/**`
- `tools/**`
- `youtube-dl-gui.sln`
- both `*.csproj`
- build/config files and generated-artifact rules

Audit whether tests actually prove their named contracts, false-positive/characterization tests, untested production paths, architecture/configuration matrix, project inclusion/exclusion, generated source/hash/date behavior, artifact provenance, release packaging, workflow permissions, failure handling, and reproducibility.

Do not "fix" test gaps during RO audit; record them as TODO findings.

## Findings

