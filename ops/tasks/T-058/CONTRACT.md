# T-058 contract

## Outcome

After a retained bot is active, every fresh loopback `@system` actor receives a
deterministic qualified non-zero-zone transform and matching `ParentWorld` from
an active bot, so world-positioned fixture commands operate near a reachable
bot instead of `MainWorld.Template.SpawnPosition` at ZoneId `0`. Before any bot
exists, worldless headless commands remain usable and world-positioned commands
fail closed rather than spawning from an invalid origin.

## Pass

- Reproduce T-057's exact source/runtime boundary deterministically: the
  current synthetic actor uses `MainWorld.Template.SpawnPosition`, whose
  physical fixture origin was `(0,0,0)` / ZoneId `0`, and five commands emitted
  `GetWorldByZone(): No world template defined for ZoneId 0` before producing
  zero casts and kills.
- Resolve a qualified anchor only from active bots that have a non-null
  `ParentWorld`, usable transform, and non-zero ZoneId. Selection must be stable
  and explicit (lowest character ID), never dependent on concurrent-dictionary
  enumeration order. Capture the anchor world and transform consistently.
- A fresh `@system` actor created after a qualified bot exists must copy that
  bot's world, instance, non-zero zone, coordinates, and rotation without
  registering as a player, holding a connection/account, mutating the bot, or
  persisting across requests.
- If no qualified active bot exists, preserve access level 100/account 0/null
  connection and existing worldless commands such as `addbot`, but do not give
  a world-positioned command a usable `ParentWorld` paired with ZoneId `0`.
  Preserve `spawnpassive`'s existing fail-closed guard.
- Add deterministic coverage for valid anchoring, stable lowest-ID selection,
  invalid/ZoneId-0 bot exclusion, no-bot fallback, controller freshness and
  zero player registration, worldless addbot behavior, and `spawnpassive`
  advancing past the world guard only with a qualified anchor.
- Keep the active AAEmu 1.2 compatibility patch and all manifest hashes byte-
  identical; this is module-source behavior, not a host-patch change. Prove the
  existing patch still applies cleanly to `aaemu12_reference` with
  `git apply --check` only and AAEmu 3.0 identities remain unchanged.
- Run the focused controller, system-actor, `spawnpassive`, access, and relevant
  manager tests, build with the module overlay against the registered host only
  if permitted by the task boundary, and `git diff --check`. Record exact
  commands/counts and any unproven physical boundary.
- Commit only declared source/tests/docs and a concise handoff with the exact
  integration action.

## Non-goals

Deploying to a host; starting Login, GameServer, MySQL, or an ArcheAge client;
claiming a runtime lease; accessing databases; changing the passive-NPC world
guard, target AI, combat rotation, navigation, compatibility patch, manifests,
or AAEmu 3.0; rerunning live combat/stealth, scale, Population Director, soak,
or release work.
