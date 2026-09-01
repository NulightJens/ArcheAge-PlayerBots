# T-058 handoff

T-058 is source-complete. Each fresh AAEmu 1.2 loopback `@system` actor now
snapshots the lowest-character-ID qualified active bot's matching
`ParentWorld`, instance, non-zero zone, coordinates, and rotation. Worldless,
mismatched-instance, ZoneId-0, and non-finite candidates are excluded. With no
qualified bot, the actor remains worldless: `addbot` still works and
`spawnpassive` stops at its unchanged world guard.

## Source identity and changed files

- Branch/base: `codex/T-058-qualified-system-actor-anchor` at
  `fef939a460f262269f2586efd2e794fdcbe6f704`.
- Delivery identity: the commit containing this handoff; resolve its immutable
  OID before integration.
- AAEmu 1.2 reference/base:
  `D:\Codex-Labs\aaemu-1.2-r208022-reference-v1` at
  `62e3eb1d87da01194802ac886cd500134facad28`.
- Changed exactly `src/AAEmu.Game/Bots/Ops/SystemActor.cs`,
  `tests/AAEmu.UnitTests/Services/WebApi/CommandControllerTests.cs`,
  `tests/AAEmu.UnitTests/Game/Scripts/Commands/SpawnPassiveNpcCommandTests.cs`,
  `docs/COMMANDS.md`, `CHANGELOG.md`, and this handoff.

The actor captures each candidate through a detached transform snapshot,
validates that the captured instance still matches the same `ParentWorld`, and
sorts the active-bot snapshot explicitly by character ID. It does not mutate
the bot, create an account or connection, register a player, or persist an
actor between requests. The AAEmu 3.0 conditional path is unchanged.

## Proof

PB-000 explicitly permitted normal ignored build/test outputs on the registered
integration host while prohibiting tracked host edits or deployment.

- `dotnet build AAEmu.UnitTests\AAEmu.UnitTests.csproj --no-restore
  --property:PlayerBotsModuleRoot='C:\Users\jensh\.codex\worktrees\4dee\PB-W00-control\'`
  — PASS, 0 errors and 33 retained warnings.
- `AAEmu.UnitTests.exe --treenode-filter
  '/*/AAEmu.UnitTests.Services.WebApi/CommandControllerTests/*'
  --minimum-expected-tests 5 ...` — 5 passed, 0 failed, 0 skipped.
- `AAEmu.UnitTests.exe --treenode-filter
  '/*/AAEmu.UnitTests.Game.Scripts.Commands/SpawnPassiveNpcCommandTests/*'
  --minimum-expected-tests 11 ...` — 11 passed, 0 failed, 0 skipped.
- `AAEmu.UnitTests.exe --treenode-filter
  '/*/AAEmu.UnitTests.Game.Core.Managers.UnitManagers/CharacterManagerTests/*'
  --minimum-expected-tests 33 ...` — 33 passed, 0 failed, 0 skipped.
- `AAEmu.UnitTests.exe --treenode-filter
  '/*/AAEmu.UnitTests.Game.Core.Managers.BotManagers/BotManagerTests/*'
  --minimum-expected-tests 21 ...` — 21 passed, 0 failed, 0 skipped.
- Focused total: 70 passed, 0 failed, 0 skipped.

The controller cases reproduce T-057's `(0,0,0)` / ZoneId `0` main-world
template boundary, prove it is ignored without a qualified bot, verify full
transform copying from the stable lowest-ID candidate, exclude null-world,
mismatched-instance, ZoneId-0, and non-finite candidates, prove request
freshness and zero world player registrations, and keep worldless `addbot`
green. The command cases prove `spawnpassive` reaches ordinary NPC-template
validation only with a qualified anchor and otherwise retains the exact world
guard.

- Complete `aaemu-1.2-r208022-v3.patch` `git apply --check
  --whitespace=error-all` — PASS against the clean registered reference; its
  head/status remained pinned/clean.
- Manifest parse and SHA-256 reproduction — 5/5 fields matched. The active 1.2
  patch remains
  `0285c8c21133ec00abbd8f5de925c56dc2d87f7d8595fee89a6063b3ff02ca3e`;
  frozen 3.0 remains
  `9c877d395b7f6dc7dc47dbf1bd4927af3a5d08392434c758cea364385a6ba15e`.
- `SpawnPassiveNpcCommand.cs`, all compatibility files, and
  `playerbots.module.json` are unchanged; `git diff --check` passed.
- The integration host retained the same 30 tracked status entries before and
  after. Its installed module remains clean at
  `1d08632d303b35e7c2d5655de5b5e2b704896cc8`, tree
  `1f3befa4dc3c01222b057c28279e82d25fcc176e`.

Early focused probes exposed missing test singleton setup and an unsafe
null-world assignment when instance 0 was registered. Later probes exposed the
host transform clone's non-preserved `WorldId` and the need to keep failed
candidate out-values local. Those fixture and source defects were corrected;
the final build and all final focused groups above are green.

## Runtime state, unproven boundary, and integration action

No Login, GameServer, ArcheAge client, or AAEmu runtime process was started,
stopped, or controlled. Final process count for those names was zero. Two
pre-existing `mysqld` processes were observed read-only and left untouched; no
database command or connection was made. No source was deployed, no runtime
lease was claimed or edited, and no retained v1-v4 evidence or AAEmu 3.0 input
was touched.

T-057 remains `INCOMPLETE`. Live passive-NPC placement, combat, stealth,
cleanup, and restart behavior with the qualified anchor remain physically
unproven.

Integrator: resolve and cherry-pick the immutable T-058 delivery commit onto
`integration/aaemu12-world`, update the installed module through the normal
lease-controlled integration workflow, and run the focused 70-test set plus
the integration-wave full AAEmu 1.2 suite. Do not reapply or regenerate the
unchanged compatibility patch. After a green integration receipt, PB-000 may
dispatch a fresh immutable physical attempt with a new evidence version; do
not reuse or edit T-057's `public-alpha-v4` evidence.
