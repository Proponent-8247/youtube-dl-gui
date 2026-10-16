# WS-10 — Cross-cutting concurrency, lifetime, cancellation, shutdown audit

Scope: **entire repository** at the pinned baseline.

Perform an independent whole-repository pass for concurrency and lifecycle defects: UI thread affinity, STA/MTA requirements, async/await and async-void, fire-and-forget tasks, process/output races, cancellation propagation, object/image/stream/process disposal, form ownership, application shutdown, queue mutation, retry overlap, file replacement races, event unsubscription, locks, and deadlock/hang potential.

Record concrete source evidence and caller chains. Separate confirmed defects from validation leads.

## Findings

