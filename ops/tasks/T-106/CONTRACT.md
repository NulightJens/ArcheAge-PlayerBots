# T-106 contract: bounded relative-yaw passive fixture placement

Start only from the exact committed task binding based on saved head
`fd3c70e5b24a6952c24be2b882de26ce918ce0ca`. T-105 independently integrated
the retained T-104 FAIL: for the exact three live transforms, all 1,331
forward-distance tuples were finite and in range, 87 met 20 m pairwise
separation, but zero met the 3 m unique-ownership margin. No fixture command or
world mutation occurred. A read-only deterministic mirror using the exact
retained transform bits, distances `{5,10,...,55}`, and relative angles
`{0,45,90,135,180,-135,-90,-45}` found 2,430 eligible tuples among all
681,472 combinations, so the missing capability is bounded angular placement.

Make the smallest source correction in `SpawnPassiveNpcCommand`. Extend the
grammar to:

`spawnpassive <npcTemplateId> [distance] [anchorBotId] [yawOffsetDegrees]`

The fourth argument is optional and positional, uses invariant-culture float
parsing, must be finite, and is accepted only in the inclusive range
`[-180, 180]`. Give invalid angle input a stable precise error. Its default is
exactly `0`. Preserve the existing two-output `TryParse` overload and the
existing anchor-output overload as source-compatible delegates; add only the
new output needed by `Execute`. Update command help to show all supported
arguments. One-, two-, and three-argument parsing and errors must not change.

Apply the relative angle only to a detached transform used for geometry. Add
the invariant degree offset to the detached clone's yaw, then use pinned
`AddDistanceToFront` behavior. Do not rotate or otherwise mutate the command
character, active bot, live anchor transform, detached anchor snapshot, target,
combat, movement, AI, or world before the existing spawn boundary. Preserve
the exact current zero-offset result, including float bits where the pinned
operations permit bit comparison. Preserve terrain height resolution,
world-template ID `0`, world/instance/zone propagation, fixture facing toward
the source anchor, and the post-geometry `IsAnchorStillCurrent` check before
spawn. Do not weaken or reorder the null-template, instance, world, zone,
finite-transform, resolver-identity, concurrent-departure, transform-swap, or
staleness gates.

Add focused regression coverage proving:

- `0`, `45`, `-45`, `180`, and `-180` parse invariantly, including under a
  non-English current culture;
- non-finite, malformed, localized-decimal, below-`-180`, above-`180`, and
  excess-arity input fails without state mutation and with the specified angle
  error when applicable;
- omitted and explicit zero offsets produce the same geometry as the current
  forward-only implementation;
- positive and negative offsets match the pinned single-precision formula
  `x - distance * sin(sourceYaw + offsetRadians)` and
  `y + distance * cos(sourceYaw + offsetRadians)` on a detached anchor;
- source position/rotation, world/zone/instance audit, terrain request, target,
  battle state, and anchor staleness behavior remain unchanged;
- every existing `SpawnPassiveNpcCommandTests` case remains green.

Run the complete focused `SpawnPassiveNpcCommandTests` set. Verify the AAEmu
1.2 compatibility patch remains byte-identical at SHA-256
`baa0979a200eb7f4403985d7166092f937033c31650aa11336fba31543d96606`
and still applies cleanly to the pinned clean reference. Perform one isolated
AAEmu 1.2 build with zero errors and retain warnings exactly. Do not install
into the registered integration host and do not run the full suite.

Commit only the command source, focused tests, T-106 receipt, and concise
handoff. Report candidate commit/parent/tree, exact grammar and error, exact
float conversion/geometry operations, focused count, build warnings, patch
identity/apply proof, no-mutation and fail-closed proof, and unproven physical
runtime boundary. PB-000 will dispatch a separate T-107
Integrator/install/full-suite task before fresh T-108 runtime evidence.
