# T-052 contract

## Outcome

The two retained AAEmu 1.2 Game startup error families from T-050 are traced to
specific source paths and classified as blocking, non-blocking, or ambiguous for
the next immutable physical attempt.

## Pass

- Verify hashes and inspect only the retained T-050 Game server/error logs and
  registered source identities. Do not start a runtime or query a database.
- Establish exact timestamps, multiplicity, surrounding lifecycle markers, and
  whether each error preceded or followed Game/Web API readiness and graceful
  cleanup.
- Trace each error to concrete pinned-host or installed-module code and explain
  the likely trigger using direct source evidence. Distinguish evidence from
  inference.
- Determine whether either error invalidates runtime readiness or the planned
  combat/stealth gate. If the evidence cannot decide, say so and prescribe the
  smallest additional observation for v3.
- If correction is required, define a bounded follow-up task with exact file
  scope and regression proof; do not implement it here.
- Commit only a concise `HANDOFF.md` containing source identity, evidence hashes,
  findings, retained uncertainty, runtime state, and the exact PB-000 action.

## Non-goals

- Source fixes, host deployment, runtime/client/database control, changing any
  evidence, combat/stealth execution, scale, Population Director, soak, release,
  or AAEmu 3.0.
