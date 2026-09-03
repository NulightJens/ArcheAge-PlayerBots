# T-086 contract: per-bot anchors for passive runtime fixtures

## Outcome

Correct only the fixture-isolation boundary proven by the retained T-041 FAIL.
Add an optional active-bot anchor to `spawnpassive` so an operator can place one
inert opportunity relative to each configured bot's actual qualified transform.
The option must not assign a target, change any bot state, or alter autonomous
Director/combat/lifecycle behavior.

## Required diagnosis

Use the integrated T-041 receipts and the immutable external evidence read-only.
Retain the exact finding: all three `spawnpassive 10004 12` calls produced NPCs
at `(13607.9, 13301.7, 28.5)`, and one native skill created by bot `20001`
credited kills for all three stacked objects. Do not describe those native area
effects as three independent bot decisions or reinterpret T-041 as a product
Director PASS.

## Behavior

- Preserve existing one- and two-argument syntax and behavior exactly:
  `spawnpassive <npcTemplateId> [distance]` keeps using the command character's
  already-qualified transform. Existing aliases, defaults, bounds, passive AI,
  terrain placement, spawn response, and failure ordering remain compatible.
- Add one optional third unsigned argument:
  `spawnpassive <npcTemplateId> <distance> <anchorBotId>`. A supplied ID must
  resolve through `BotManager` to a currently active bot with a finite transform,
  nonzero zone, world, and instance-consistent qualified boundary. Resolve and
  snapshot that bot only as the spawn anchor; never mutate it.
- Fail closed with a precise command error before NPC creation if the ID is
  missing/zero/unparseable, not active, worldless, zone zero, nonfinite, stale,
  or boundary-inconsistent. Do not silently fall back to the command character
  or another bot.
- Clone/detach the selected anchor transform, apply the existing distance and
  terrain logic, and spawn the same mortal, non-retaliating, non-displacing,
  non-respawning NPC instance. Include the selected anchor bot ID, zone, and
  instance in the success response for auditable fixture placement.
- Keep the default command path independent of `BotManager` and byte-compatible
  in its existing success/error responses. Do not add an angle, target selector,
  bot command, or gameplay control surface.

## Tests and deliverable

Add deterministic tests for legacy parsing/defaults/culture/bounds; exact
three-argument parsing; invalid ID/arity; absent bot; worldless/zone-zero/
nonfinite/mismatched bot rejection without spawning; qualified bot selection;
detached transform use; terrain/world/instance preservation; auditable output;
and proof that neither command nor helper mutates bot state/target/activity.
Prefer an internal pure/testable resolver seam over singleton-heavy tests.

Update only the declared source/test files, the AAEmu 1.2 v3 compatibility
patch, both manifest hash declarations, and `ops/tasks/T-086/HANDOFF.md`. Run
focused AAEmu 1.2 command tests and an isolated complete build. Prove the final
patch applies strictly to pinned host commit
`62e3eb1d87da01194802ac886cd500134facad28` and its SHA-256 matches both
manifest declarations.

Do not write the registered integration host/module, run a runtime or full
suite, access MySQL/database/client, edit ledgers/lease/retained evidence, alter
bot gameplay behavior, perform scale/soak/packaging, or touch AAEmu 3.0. Commit
the source candidate and concise handoff; PB-000 will dispatch an independent
Integrator before any fresh runtime proof.
