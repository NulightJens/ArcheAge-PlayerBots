# T-081 contract

## Outcome

The existing AAEmu 1.2 `GameService` startup/shutdown integration owns a
periodic one-zone Activity Director. It reconciles an explicit ordered set of
persistent AAEmu character identities toward configured population bounds,
spawns them free of forced gameplay state, lets each independently run the
accepted one-kill lifecycle, and refills normal self-logouts without exceeding
the configured maximum.

## Required behavior

- Replace the one-shot `BotAutoSpawnTask` production path with one Director
  instance scheduled only after `Server started!` is logged. Graceful service
  shutdown must cancel the Director before the existing `BotManager.Stop()`
  cleanup. Start/stop and overlapping ticks are idempotent and race-safe.
- Add opt-in configuration for enabled state, one nonzero zone ID, an ordered
  unique list of persistent character IDs, minimum/target/maximum population,
  initial delay, reconciliation interval, and bounded retry backoff. Defaults
  are disabled and preserve current empty-auto-spawn behavior. Invalid zone,
  empty/duplicate/zero IDs, or `min <= target <= max <= eligible identities`
  violations fail closed with operator-visible reasons.
- On each serialized tick, count only configured identities that are live in
  the configured nonzero zone and the default world instance. When below
  target, attempt at most one deterministic eligible identity, never an
  already-live or in-flight identity, and never a spawn that could exceed
  maximum. Retry failures only after the configured backoff.
- A newly spawned identity is accepted only if its actual world, default
  instance, and zone match the configured boundary. A wrong-zone/world spawn
  is immediately returned through the normal persisted `DespawnBot` path,
  recorded, and cooled down. Never reposition, teleport, target, stage an
  opportunity for, or otherwise gameplay-command a bot.
- Never shrink or force-despawn pre-existing activity to satisfy a lowered
  maximum. At or above maximum, refuse further spawns and report over-capacity.
  Director cleanup may only undo its own just-created invalid spawn; service
  shutdown remains the existing normal all-bot cleanup.
- Director identities spawn with free/unforced combat state. Extend the
  production lifecycle eligibility guard so configured Director identities
  can independently select `grind reason=nearby_mortal`, recover, and request
  normal persisted logout while multiple runtimes exist. Unconfigured/manual
  bots retain the accepted fail-closed behavior. Preserve all one-bot T-079
  lifecycle semantics and direct-target prohibitions.
- Expose a deterministic immutable snapshot and structured logs containing at
  least enabled/valid, zone, min/target/max, eligible/live-qualified,
  live-wrong-zone, in-flight/cooldown, attempt/success/failure/refill counts,
  last identity/result/reason, and start/stop/tick timestamps. Surface the
  snapshot through the existing operator metrics command without adding a
  gameplay mutation command.
- Keep AAEmu-version-specific service/DI/task scheduling edits in the existing
  compatibility patch. Recompute its SHA-256 and update every manifest field
  that declares it. Do not move host calls into shared policy classes.

## Deterministic proof

- Test disabled and every invalid-configuration fail-closed case; production
  startup ordering; graceful cancellation; overlapping/idempotent ticks;
  bootstrap from zero; deterministic selection; one-at-a-time refill;
  min/target/max caps; duplicate/in-flight exclusion; failure backoff;
  wrong-zone/default-instance rejection and normal cleanup; no forced state or
  direct gameplay commands; self-logout refill; multi-runtime lifecycle
  eligibility only for configured Director IDs; manual-bot isolation;
  immutable snapshot/log fields; and operator command visibility.
- Run directly affected AAEmu 1.2 tests in an isolated source/build copy. Do
  not install to or mutate the registered AAEmu host. Record exact commands,
  counts, source identity, retained warnings/failures, and the exact T-082
  integration action in a concise handoff.
- Commit only the declared write scope. Leave the task worktree clean.

## Non-goals

Runtime evidence; cohort scale; 30-minute soak; packaging; database/roster
mutation; bot creation; zone relocation; new combat, navigation, recovery,
loot, quest, social, or activity-selection behavior; client work; global
ledger/lease edits; retained-evidence changes; AAEmu 3.0; or integration-branch
mutation.
