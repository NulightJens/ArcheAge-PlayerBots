# T-084 contract

## Outcome

The exact T-083 serialized Activity Director replacement is independently
reviewed, replayed onto the saved integration lineage, installed in the
registered AAEmu 1.2 integration checkout, and qualified by focused tests plus
exactly one complete unit-suite invocation. A truthful receipt binds the
tested/installed source and decides whether T-041 may receive runtime authority.

## Authority and source identity

- Do not write the registered integration host/module or build outputs until
  PB-000 commits `ops/RUNTIME-LEASE.yaml` assigning `aaemu12` to T-084's exact
  thread for install/build/test only. This never authorizes Login, GameServer,
  MySQL, database, client, or live runtime control.
- Verify replacement
  `b1ffc8006aaa4a4eb4da54a4993057eb8969df96`, exact parent
  `dd09b2d6b655f92cd5afa36cd70e7d08b9efd877`, clean writer worktree, and
  exactly 14 declared paths. Verify rejected T-081 is not an ancestor of the
  saved branch. Preserve all Control Tower dispatch/binding/lease descendants;
  do not merge either writer branch directly.
- Recompute patch SHA-256
  `395a83ab5bf6a4f4f1c0d56289590d6a36ad36b3ab5f87e1701d6aa17ffbefcd`,
  verify both manifest declarations, and strict-apply-check against the clean
  pinned reference at `62e3eb1d87da01194802ac886cd500134facad28`.

## Independent review and proof

- Re-review the full T-081 product surface: bounds, refill, retry, zone/default-
  world qualification, no shrink, wrong-boundary normal cleanup, unforced
  lifecycle, manual/wrong-zone isolation, visibility, disabled/invalid defaults,
  and graceful shutdown.
- Specifically close every T-082 defect. Confirm one service-owned lifecycle
  linearization covers construction/publication, `TryStart`, schedule result,
  cancellation, Director stop, state clearing, and once-only BotManager stop;
  no valid interleaving can leave late or uncancelled work. Review lock order
  and all deterministic barrier tests. Reject rather than repair any defect.
- Replay candidate blobs byte-identically onto the current integration lineage.
  Advance the installed module non-destructively, run installer check-only
  before and after one idempotent normal install, and prove the reference stays
  clean with no unreceipted tracked host changes outside the expected overlay.
- Run a clean complete AAEmu 1.2 solution build, the directly affected focused
  selection including concurrency, and the full AAEmu 1.2 unit suite exactly
  once. Zero build/test failures are required; report exact pass/intentional-
  skip/warning counts. Do not consume a second full-suite invocation.
- Verify zero Login/Game/client processes, free runtime ports, and leave MySQL
  wholly untouched. Do not claim runtime behavior from build/tests.
- Commit only byte-identical candidate blobs/handoffs, one sanitized integration
  receipt, and T-084 handoff. Fast-forward `integration/aaemu12-world` through
  the qualified commit without rewriting history. Leave worktrees clean.

## Receipt and pass boundary

Bind integration parent, replacement source/parent/tree, rejected source,
tested and installed module source/tree, host/reference identities, patch hash,
commands/counts, warnings/failures, no-runtime state, and unproven boundaries.
PASS may unblock T-041 only; it does not prove physical Director behavior,
scale, soak, packaging, or release readiness.

## Non-goals

Product correction; runtime/gameplay/evidence; MySQL/database/client access;
roster mutation; scale; soak; release artifacts; global ledger/lease edits;
retained evidence; or AAEmu 3.0.
