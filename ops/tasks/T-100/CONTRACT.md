# T-100 contract: valid default-world active-bot anchor

Start only from the exact committed binding based on saved head
`1ab6c2e80a4c7e209149a804cdff6ae209142501`. The independently integrated
T-098 FAIL proves a bot already qualified by the Activity Director was rejected
by `spawnpassive 10004 5 20001` as an inconsistent world/instance anchor.
Pinned AAEmu declares that `WorldManager.DefaultWorldTemplateId` is usually
`0`; therefore world-template ID `0` is valid and must not be used as a null or
missing-template sentinel.

Make the smallest source correction in `SpawnPassiveNpcCommand`: distinguish
`world.Template == null` from `world.Template.Id == 0`, accept a consistent
default-world anchor with template ID `0`, and preserve all real fail-closed
guards. Parent-world reference, instance ID, live transform world ID, zone,
finite transform, resolver identity, and post-snapshot staleness checks must
remain exact. Do not weaken validation to accept a null template or mismatched
world/instance/zone. Do not mutate bot transform, target, combat, or movement.

Add focused regression coverage that proves:

- a fully consistent default-world anchor with template ID `0` resolves;
- the anchor snapshot and `CreateSpawnPosition` preserve world ID `0`, zone,
  instance audit, finite coordinates, and detached/no-mutation semantics;
- a missing/null world template fails closed with a precise stable error;
- existing mismatched instance, world ID, zone-zero, non-finite, resolver-ID,
  concurrent-departure, transform-swap, and live-boundary tests remain green.

Run the complete focused `SpawnPassiveNpcCommandTests` set. Verify the AAEmu
1.2 compatibility patch remains byte-identical at SHA-256
`baa0979a200eb7f4403985d7166092f937033c31650aa11336fba31543d96606`
and still applies cleanly to the pinned clean reference. Perform one isolated
AAEmu 1.2 build with zero errors; retain warnings exactly. Do not install into
the registered integration host and do not run the full suite.

Commit only the two source/test paths, focused evidence receipt, and concise
T-100 handoff. Report candidate commit/parent/tree, exact code condition,
focused count, build result/warnings, patch identity/apply proof, retained
fail-closed coverage, and unproven physical runtime boundary. PB-000 will
dispatch a separate Integrator/install/full-suite task before any fresh runtime
successor.
