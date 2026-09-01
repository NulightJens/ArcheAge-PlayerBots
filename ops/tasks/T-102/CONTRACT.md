# T-102 contract: exact bot transform telemetry

Start only from the exact committed binding based on saved head
`909c6db03b196ff7a706e68eb3cc2de1f0bfcabd`. T-101 proved the default-world
anchor correction, but retained T-098 runtime samples expose only position.
`SpawnPassiveNpcCommand.CreateSpawnPosition` offsets a fixture with exact
`-distance * sin(yaw)` / `distance * cos(yaw)` horizontal geometry, so a
one-shot preflight also needs unrounded yaw from the same botdebug sample.

Make the smallest diagnostic-only change to `BotDebugCommand`. Preserve every
existing message and append exactly one stable transform message immediately
after the existing position message, formatted with invariant culture and
round-trip float precision:

`Transform: world=<uint>, instance=<uint>, zone=<uint>, x=<float:R>, y=<float:R>, z=<float:R>, yaw_rad=<float:R>`

Use the bot's current live transform values. World ID `0` is valid and must be
emitted as `0`. Do not round yaw, substitute degrees, localize decimal
separators, mutate the transform, select a target, change combat/movement/life
state, or change the existing position message. This is observability only.

Add focused tests proving the exact line under a non-English current culture,
including default world `0`, explicit instance and zone IDs, finite signed
coordinates, and a nontrivial yaw that round-trips bit-exactly when parsed as a
single-precision invariant value. Prove existing botdebug diagnostics remain
present and bot transform, target, combat state, and movement state are
unchanged. Retain all existing BotCommandsTests behavior.

Run the complete focused `BotCommandsTests` set and require zero failures or
skips. Verify compatibility patch SHA-256
`baa0979a200eb7f4403985d7166092f937033c31650aa11336fba31543d96606`
is byte-identical and applies strictly to the pinned clean reference. Perform
one isolated AAEmu 1.2 build with zero errors and retain exact warnings. Do not
install into the registered integration host and do not run the full suite.

Commit only the command source, focused tests, T-102 receipt, and handoff.
Report candidate commit/parent/tree, exact output grammar, culture/round-trip
proof, focused count, build/warnings, patch proof, no-mutation proof, and the
unproven physical runtime boundary. PB-000 will dispatch a separate
Integrator/install/full-suite task before the fresh runtime successor.
