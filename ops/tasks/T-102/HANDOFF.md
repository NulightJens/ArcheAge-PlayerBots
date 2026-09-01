# T-102 handoff

## Source identity and outcome

- Binding and candidate parent:
  `9a94b21fd4f9855346109cfe7b5b941df13e6b4b`.
- Dispatch base: `968720578b8e1e85e925e63ac584e410bcc71635`.
- Accepted source base: `909c6db03b196ff7a706e68eb3cc2de1f0bfcabd`.
- Candidate: the single detached commit containing this handoff. Its exact
  commit and tree hashes are reported to PB-000 after commit creation because
  a commit cannot contain its own identity.
- Outcome: `botdebug` preserves every existing diagnostic and appends exactly
  one invariant, single-precision round-trip transform message immediately
  after the existing position message:
  `Transform: world=<uint>, instance=<uint>, zone=<uint>, x=<float:R>, y=<float:R>, z=<float:R>, yaw_rad=<float:R>`.
  Position and yaw come from one captured live world-transform value; world,
  instance, and zone come from the same bot's live transform scalars.

## Changed paths

- `src/AAEmu.Game/Scripts/Commands/BotDebugCommand.cs`
- `tests/AAEmu.UnitTests/Game/Scripts/Commands/BotCommandsTests.cs`
- `ops/evidence/aaemu12-botdebug-transform-telemetry-v1.yaml`
- `ops/tasks/T-102/HANDOFF.md`

No path outside the four-path T-102 write scope was changed.

## Proof

- Complete focused `BotCommandsTests`: 46 passed, 0 failed, 0 skipped.
- The new `de-DE` case emitted the exact invariant sample
  `Transform: world=0, instance=43, zone=601, x=-123.45679, y=0.00012345678, z=-98765.43, yaw_rad=1.2345678`.
  Parsing `yaw_rad` invariantly reproduced the input single-precision bits
  exactly: `0x3F9E0651`.
- The same case proved the transform line immediately follows the unchanged
  position message, existing header/health/movement/combat/task/metric
  diagnostics remain present, and transform, selected target, battle/combat
  state, destination, running/moving/falling state, velocity, and follow target
  are unchanged. Retained focused cases also kept the existing life diagnostics
  green.
- Fresh isolated proof workspace:
  `D:\Codex-Labs\t102-aaemu12-source-build-v1`, detached at pinned AAEmu 1.2
  `62e3eb1d87da01194802ac886cd500134facad28`, with the 28-file compatibility
  seam and a proof-only junction to this exact writer tree.
- Complete no-incremental build: PASS, 72 retained warnings, 0 errors. A final
  test-only recompile after the fixture-only correction passed with 43 retained
  warnings and 0 errors; production source did not change after the complete
  build.
- Compatibility patch SHA-256 remained
  `baa0979a200eb7f4403985d7166092f937033c31650aa11336fba31543d96606`.
  Strict whitespace-error apply-check passed against the registered clean
  reference, reverse-check passed in the isolated applied checkout, and the
  reference remained clean and detached at the pinned commit.
- Writer/proof blobs match for command source and tests. Full-suite
  invocations: 0.

## Retained failures and runtime state

The first isolated build invocation retained 3 warnings and 23 errors because
the newly cloned host lacked its required relative `modules/archeage-playerbots`
import path, so no module namespace was compiled. Adding a proof-only junction
to the exact writer worktree corrected the validation environment; no product
source changed for that correction.

The first focused run was 45 pass and one fixture setup failure: assigning the
pinned host's `Transform.InstanceId` property requires a `WorldManager`, which
this focused class intentionally does not register. The test was corrected to
use the established private-field fixture seam for instance, world, zone, and
parent-world state. No product assertion failed, and the complete final run is
green.

No runtime was started, stopped, inspected, or controlled. No registered
integration host/module, deployed configuration, external runtime evidence,
database, client, global ledger, lease, AAEmu 3.0, scale, soak, or packaging
state was written or controlled. T-098 remains a retained FAIL. Installation,
the full AAEmu 1.2 suite, live botdebug sampling, and physical one-shot fixture
preflight/placement remain unproven.

## Exact integration action

PB-000 must dispatch an independent Integrator to verify this candidate is one
clean child of `9a94b21fd4f9855346109cfe7b5b941df13e6b4b`, cherry-pick only this candidate
onto the current accepted integration head, acquire the required build-only
lease before writing the registered integration host/module, verify the patch
hash, install the exact candidate, rerun all 46 focused command cases, perform
one clean build and the next permitted full-suite wave gate, and record a new
integration receipt. Only after that PASS may PB-000 dispatch a fresh versioned
runtime successor to capture raw transform telemetry, precompute every fixture
coordinate and pairwise separation before any one-shot mutation, and prove the
physical lifecycle/refill/restart boundary without reusing or overwriting the
retained T-098 evidence root.
