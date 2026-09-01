# T-100 handoff

## Source identity and outcome

- Binding and candidate parent:
  `96b8b5004a89a0dec6522753f64d03fd1d732c5b`.
- Dispatch base: `c0d08ddd3c2db5e542f17ec8b25751814ae344f9`.
- Accepted source base: `1ab6c2e80a4c7e209149a804cdff6ae209142501`.
- Candidate: the single commit containing this handoff. Its exact commit and
  tree hashes are reported to PB-000 after commit creation because a commit
  cannot contain its own identity.
- Outcome: `SpawnPassiveNpcCommand` now rejects `world.Template == null` with
  the stable error `Active bot anchor 20001 does not have a world template.`,
  then uses `world.Template.Id` without treating valid ID `0` as missing.
  Parent-world, instance, live world-ID, zone, finite-transform, resolver
  identity, and post-snapshot checks remain fail-closed and exact.

## Changed paths

- `src/AAEmu.Game/Scripts/Commands/SpawnPassiveNpcCommand.cs`
- `tests/AAEmu.UnitTests/Game/Scripts/Commands/SpawnPassiveNpcCommandTests.cs`
- `ops/evidence/aaemu12-passive-anchor-default-world-zero-v1.yaml`
- `ops/tasks/T-100/HANDOFF.md`

No path outside the four-path T-100 write scope was changed.

## Proof

- Complete focused `SpawnPassiveNpcCommandTests`: 40 passed, 0 failed,
  0 skipped. The 28 retained cases and 12 new boundary cases cover valid
  default-world ID 0, detached/no-mutation placement, null template, instance,
  world ID, zone, finite transform, resolver identity, concurrent departure,
  parent-world change, transform swap, and live boundary changes.
- Fresh isolated proof workspace:
  `D:\Codex-Labs\t100-aaemu12-source-build-v1`, detached at pinned AAEmu 1.2
  `62e3eb1d87da01194802ac886cd500134facad28`.
- Complete no-incremental unit-test-project build: PASS, 72 retained warnings,
  0 errors. A final incremental test-assembly recompile after the fixture-only
  correction passed with 43 retained warnings and 0 errors; production source
  did not change after the complete build.
- Compatibility patch SHA-256 remained
  `baa0979a200eb7f4403985d7166092f937033c31650aa11336fba31543d96606`.
  Strict apply-check passed against the registered clean reference; reverse
  check passed in the isolated applied workspace. The reference remained clean
  and detached at the pinned commit.
- Writer/proof normalized blobs match for both source and tests, and all writer,
  isolated module, and isolated host diff checks passed.
- Full-suite invocations: 0.

## Retained failure and runtime state

The first focused run was 39 pass and one fixture error: the new parent-world
staleness test called pinned host `ParentWorld = null`, whose setter dereferences
the null value. The test was corrected to use the established backing-field
fixture seam; no product assertion failed, and the complete final run is green.

No runtime was started, stopped, inspected, or controlled. No registered
integration host/module, deployed configuration, external evidence, database,
client, global ledger, lease, AAEmu 3.0, scale, soak, or packaging state was
written or controlled. T-098 remains a retained FAIL. Installation, the full
AAEmu 1.2 suite, and physical default-world fixture placement remain unproven.

## Exact integration action

PB-000 must dispatch an independent Integrator to verify this candidate is one
clean child of `96b8b5004a89a0dec6522753f64d03fd1d732c5b`, cherry-pick only this
candidate onto the accepted integration head, acquire the required build-only
lease before writing the registered integration host/module, verify the patch
hash, install the exact candidate, rerun all 40 focused command cases, perform
one clean build and the next permitted full-suite wave gate, and record a new
integration receipt. Only after that PASS may PB-000 dispatch a fresh versioned
runtime successor; never reuse or overwrite the retained T-098 evidence root.
