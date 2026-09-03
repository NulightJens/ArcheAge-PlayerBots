# T-083 contract

## Outcome

One replacement candidate replays T-081's bounded Activity Director behavior
and removes T-082's release-blocking AAEmu 1.2 `GameService` race. After any
concurrent start/stop interleaving, either no recurring Director was scheduled,
or the exact scheduled Director was cancelled and stopped before
`BotManager.Stop()` returns. No late publication, live task, or uncancelled
recurring scheduler entry is possible.

## Required correction

- Begin from the exact T-083 dispatch commit. Replay the 13 T-081 candidate
  blobs as the source baseline, then change only the compatibility seam and its
  directly related host tests as needed to fix the defect recorded in
  `ops/tasks/T-082/HANDOFF.md`. Do not merge the rejected writer branch or
  introduce unrelated semantic changes.
- Serialize the entire service lifecycle decision: start admission, Director
  construction/publication, `TryStart`, recurring scheduler result/publication,
  cancellation, Director stop, state clearing, and the transition into normal
  `BotManager.Stop()`. A private service-owned lock/state machine is preferred
  over independent publication flags. Define and preserve one lock order; do
  not hold a lock that a Director tick must acquire before it can finish.
- Sequential repeated start is idempotent. Disabled/invalid configuration and
  scheduler rejection remain fail-closed. Sequential repeated stop cancels at
  most once, stops at most once, and remains harmless. A start that loses to or
  overlaps shutdown may not publish or schedule work after shutdown begins.
- Preserve byte-identically every T-081 product blob not required for the
  bounded race correction, including zone-qualified lifecycle eligibility,
  no-shrink policy, one-at-a-time refill, wrong-boundary cleanup, metrics,
  configuration, and source tests.

## Deterministic proof

- Add controllable concurrency tests for at least: stop while construction is
  blocked before instance publication; stop while recurring scheduling is
  blocked before its success is published; scheduler rejection concurrent
  with stop; repeated concurrent starts; repeated concurrent stops; and normal
  Director-before-`BotManager.Stop()` ordering. Use bounded barriers/events and
  deterministic time; no sleeps as correctness gates.
- For every interleaving, assert exact schedule/cancel/Director-stop/BotManager-
  stop counts and ordering, no post-stop scheduled activity, and no leaked
  recurring entry. Retain all T-081 focused suites and add the concurrency
  selection; run them in a new versioned isolated AAEmu 1.2 source/build copy.
- Regenerate the full compatibility patch against the pristine pinned host,
  strict-apply-check it, recompute SHA-256, and update both manifest
  declarations. Run a clean build and focused tests only; the full suite is
  reserved for the later Integrator.
- Commit only the declared write scope and a concise handoff with exact parent,
  changed paths, build/test counts, patch hash, retained warnings/failures, and
  the exact fresh-integrator action. Leave the writer worktree clean.

## Non-goals

Runtime/host install; full suite; product redesign; new Director policy or
gameplay behavior; database/client/roster work; scale; soak; packaging; global
ledger/lease edits; retained evidence; integration-branch mutation; or AAEmu
3.0.
