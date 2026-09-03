# T-062 contract

## Outcome

The deterministic combat plan arms every bot in a cohort against one shared
mortal target through the existing atomic `botattackobject all <objId>` command
instead of serial per-bot attack commands. This closes T-060's verified
low-health target disappearance during cohort-25 command fan-out without
changing gameplay behavior or inventing a second evidence framework.

## Required behavior

- Preserve the exact cohort sizes 1, 5, 10, 25, 50, and 100, distinct supplied
  target identities, per-bot add/Idle/debug evidence, metrics boundaries,
  cleanup stimuli, stealth phases, and fail-closed analyzer semantics.
- After every cohort bot is added and placed in Idle and the starting metrics
  snapshot is retained, emit exactly one combat stimulus with command
  `botattackobject` and arguments `all <cohortTargetObjId>`.
- Emit no per-bot `botattackobject` stimulus for cohort combat. The existing
  server command performs the synchronous ordered fan-out before bot ticks can
  kill and despawn the shared target.
- Extend deterministic tests to prove attack-command count, selector, target
  identity, ordering after setup, and continued per-ID debug/cleanup coverage
  for all six cohorts. Retain overwrite refusal and byte-stable plan behavior.
- Update only the existing combat-harness documentation needed to explain the
  atomic shared-target boundary.
- Run `pwsh -NoProfile -File scripts/combat/Test-CombatQualificationHarness.ps1`
  and any narrower directly affected tests. Record exact commands and results.
- Commit only the declared write scope and a concise handoff with the exact
  integration action. Do not claim a live T-060/T-061/T-037 gate from offline
  fixtures.

## Non-goals

Runtime execution, target-template/health changes, dead-object tombstones,
production bot decision changes, autonomous activity selection, deployment,
database/client work, scale acceptance, or global-ledger edits.
