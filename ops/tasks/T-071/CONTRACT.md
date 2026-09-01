# T-071 contract

## Outcome

After the existing one-kill activity reaches its targetless/noncombat boundary,
the lifecycle waits in an observable recovery phase while native world recovery
clears HP and MP debt. Only then does it capture the immutable completion
snapshot and enter the unchanged normal logout callback path.

## Pass

- Preserve all T-063/T-067/T-068 accepted behavior: exact-one-bot fail-closed
  activation, self-selected nearby mortal opportunity, one-kill bound, no
  target/destination choice by lifecycle, native progression snapshot fields,
  stable inventory fingerprint, one structured progression record, deferred
  callback, duplicate suppression, and persistent identity reset.
- Once one kill is credited and dead target references are cleared, require the
  bot to be targetless, noncombat, stationary/no unresolved movement, alive,
  loaded in a valid world, and free of combat-search/duel/rest/respawn state.
- If HP/maxHP or MP/maxMP shows debt, transition the existing lifecycle into an
  explicit observable rest/recovery wait. Suspend normal bot brain/mover work
  during that wait without modifying world-native HP/MP regeneration.
- On subsequent lifecycle ticks, do not capture completion, queue logout, or
  call the logout callback while either resource is below a positive maximum,
  a target/combat/movement condition reappears, or resource state is invalid.
  Observability failure is fail-closed and must not throw from the host tick.
- When native recovery reaches HP=maxHP and MP=maxMP at the targetless,
  noncombat, movement-clear boundary, capture completion once, calculate the
  original activation-to-completion deltas, emit one structured progression
  record, and request normal logout once. Do not restore resources directly.
- Expose recovery pending/start/resource state through the controller view,
  structured lifecycle logs, and `/botdebug` sufficiently for a runtime receipt
  to prove the wait occurred and completed. Preserve explicit pending/
  unavailable semantics and invariant formatting.
- Prove focused unit cases for immediate debt-free logout, MP-debt wait, HP-debt
  wait, natural completion, invalid/unavailable resource fail-closed behavior,
  target/combat/movement reappearance, duplicate ticks, callback failure,
  persistent re-registration, structured-log order/count, host suspension, and
  `/botdebug` output. Run the adjacent focused AAEmu 1.2 selection.
- Commit only declared source/test paths and `ops/tasks/T-071/HANDOFF.md`.
  Report source identity, exact tests, retained warnings/failures, runtime-not-
  started state, unproven physical boundary, and exact integration action.

## Non-goals

Runtime/client/database work; direct HP/MP restoration; consumables, food,
potions, buffs, resurrection, target/combat/loot/progression changes; new
activity selection; general resting AI; Activity Director; multiple bots;
scale; soak; packaging; AAEmu 3.0; or global ledger/lease edits.
