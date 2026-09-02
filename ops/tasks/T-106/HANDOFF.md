# T-106 handoff

## Source identity and outcome

- Exact binding and candidate parent:
  `8b7a10101910c704b5f2d08129d7468cf36b8796` (tree
  `5960ffe01a1ca9f38df9f4b67a1f67df71483021`).
- Dispatch base: `41f2d35a9e20bd66fff80a4ae8394321785e172b`.
- Accepted integration source:
  `fd3c70e5b24a6952c24be2b882de26ce918ce0ca`.
- The candidate is the single detached child containing this handoff. Its exact
  commit, tree, stable patch ID, and four path blobs are reported to PB-000
  after commit creation because a commit cannot contain its own identity.
- Outcome: PASS. `spawnpassive` now supports the exact grammar
  `spawnpassive <npcTemplateId> [distance] [anchorBotId] [yawOffsetDegrees]`.
  The optional fourth value defaults to `0`, parses with invariant
  `NumberStyles.Float`, must be finite, and is inclusive from `-180` through
  `180`. Every invalid angle uses the exact error
  `Yaw offset degrees must be a finite invariant number from -180 through 180.`

## Changed paths

- `src/AAEmu.Game/Scripts/Commands/SpawnPassiveNpcCommand.cs`
- `tests/AAEmu.UnitTests/Game/Scripts/Commands/SpawnPassiveNpcCommandTests.cs`
- `ops/evidence/aaemu12-passive-anchor-yaw-offset-v1.yaml`
- `ops/tasks/T-106/HANDOFF.md`

No path outside the four-path T-106 write scope changed.

## Implementation and proof

- Both legacy `TryParse` overloads remain source compatible and retain their
  prior maximum arities. The new Execute-only overload returns the fourth
  `float` output. One-, two-, and three-argument results and errors are
  unchanged; excess arity continues to use the help path.
- Geometry starts with `sourceTransform.CloneDetached()`. For nonzero offsets
  only, the clone performs
  `Rotate(0f, 0f, yawOffsetDegrees.DegToRad())`, where the pinned conversion is
  `MathF.PI / 180f * yawOffsetDegrees`, then performs the unchanged
  `AddDistanceToFront(distance)`. This yields the pinned single-precision
  formulas `x - distance * MathF.Sin(sourceYaw + offsetRadians)` and
  `y + distance * MathF.Cos(sourceYaw + offsetRadians)`.
- The explicit zero path skips rotation, so omitted and explicit zero offsets
  produced identical X/Y/Z/roll/pitch/yaw bits. Existing terrain resolution,
  default world-template ID `0`, world/zone/instance propagation, facing back
  toward the anchor, and post-geometry `IsAnchorStillCurrent` ordering remain
  unchanged.
- Complete focused `SpawnPassiveNpcCommandTests`: 46 passed, 0 failed, 0
  skipped in 1.528 seconds. Coverage includes invariant `de-DE` parsing for
  `0`, `45`, `-45`, `180`, and `-180`; all malformed, localized, non-finite,
  out-of-range, and excess-arity cases; bit-exact zero geometry; bit-exact
  signed 45-degree geometry; terrain inputs; and retained fail-closed anchor
  cases.
- Invalid angles caused zero bot-resolver and terrain-resolver calls. Source
  position/rotation, live and detached anchor transforms, target, battle state,
  parent world, audit scalars, and all existing staleness gates remained
  unchanged. No target, combat, movement, AI, world, or source transform
  mutation was added before the existing spawn boundary.
- Fresh proof workspace:
  `D:\Codex-Labs\t106-aaemu12-source-build-v1`, detached at pinned AAEmu 1.2
  `62e3eb1d87da01194802ac886cd500134facad28`, with exactly the 28 compatibility
  files and a proof-only junction to this writer tree. The first and only
  complete no-incremental build passed with 72 retained warnings and 0 errors
  in 31.80 seconds.
- Compatibility patch SHA-256 remained
  `baa0979a200eb7f4403985d7166092f937033c31650aa11336fba31543d96606`.
  Strict whitespace-error apply-check passed against the registered clean
  reference; reverse-check passed in the isolated applied checkout; the
  reference remained clean and detached at the pinned commit.
- Writer/proof blobs matched for command source and tests. Full-suite
  invocations: 0.

## Retained failures and runtime state

No product, focused-test, build, or patch-gate failure was retained. The 72
build warnings are the existing pinned package advisory and host/module/analyzer
warning boundary.

No runtime, process, listener, database, client, deployed configuration,
registered integration host/module, lease, global ledger, external runtime
evidence, AAEmu 3.0, scale, soak, or packaging state was read, written,
started, stopped, or controlled. T-104 remains a retained FAIL. Installation,
the full AAEmu 1.2 suite, live angular fixture placement, lifecycle/refill,
distinct-PID restart/rebootstrap, and dwell acceptance remain unproven.

## Exact integration action

PB-000 must dispatch T-107 to verify the reported candidate is one clean child
of `8b7a10101910c704b5f2d08129d7468cf36b8796`, replay only that candidate onto
the current integration head, acquire any required build-only lease before
writing the registered integration host, verify the exact patch and path
blobs, install the exact replay, rerun all 46 focused cases, perform one clean
AAEmu 1.2 build and exactly one permitted full suite, and record a new
integration receipt. Only after that PASS may PB-000 dispatch fresh T-108
runtime evidence using a new immutable v7 root.
