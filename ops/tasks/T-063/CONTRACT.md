# T-063 contract

## Outcome

The existing deterministic `BotLifeStateMachine` becomes a production
single-bot lifecycle controller. When and only when exactly one live runtime is
present, an unforced, alive, world-valid Idle bot with a nearby attackable
mortal opportunity autonomously selects bounded grinding, activates the
existing combat brain without a target command, records the decision reason,
and later requests its own normal persisted logout after one credited kill.

## Required behavior

- Reuse `BotLifeStateMachine` and `BotBehaviorProfile`; do not create a parallel
  lifecycle state model. A newly registered runtime begins at the truthful
  post-spawn Idle snapshot.
- Fail closed unless the host has exactly one non-retired runtime and that bot
  is alive, in a valid world/transform, unforced, Idle, combat-inactive, and has
  at least one nearby living attackable NPC opportunity through the existing
  blackboard/world seam.
- On an accepted `ActivityRequested` transition, choose only the minimal
  `grind` activity, set the existing combat state active with a one-kill
  boundary, and let the existing combat task independently scan, choose its
  target, travel, face, and fight. The controller must not assign a target,
  destination, cast, loot action, or recovery action.
- Retain one deterministic operator-visible decision such as
  `activity=grind reason=nearby_mortal`, plus life state, transition outcome,
  and timestamps in both structured logs and `/botdebug`.
- After one authoritative credited kill, wait for a safe targetless/non-combat
  boundary, submit `LogoutRequested` through the life FSM exactly once, and
  invoke the existing `BotManager.DespawnBot` normal save/logout path outside
  the runtime iteration/lock. Record success or failure; never silently retire
  a runtime or bypass persistence.
- Forced/manual bots, zero or multiple runtime populations, missing-world
  bots, dead/recovering bots, no-opportunity bots, and rejected/early life
  transitions must remain unchanged. The controller must not activate a cohort
  or become an Activity Director.
- Re-adding the same persistent identity creates a fresh Idle controller and
  permits a new deterministic iteration without leaking prior kill/logout
  state. Duplicate ticks may not duplicate activation or logout.
- Add focused deterministic tests with a controllable clock and injected
  logout seam for activation, fail-closed guards, no direct target selection,
  single credited-kill logout, normal callback ordering, callback failure, and
  clean re-add/restart state. Extend `/botdebug` tests for decision visibility.
- Run directly affected AAEmu 1.2 tests. Commit only the declared write scope
  and a concise handoff with exact test proof and integration action.

## Non-goals

Runtime/deployment proof; loot or quest implementation; activity rotation;
population bounds; multi-bot behavior; roaming/social/group activities;
Activity Director; scale, soak, release packaging, databases, clients, global
ledgers, or AAEmu 3.0.
