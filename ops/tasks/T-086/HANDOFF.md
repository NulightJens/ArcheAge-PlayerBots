# T-086 handoff

## Source identity and outcome

- PB-000 binding commit: `2ef7c8ce5778156785a074157b09db013e7d0a13`.
- Exact detached dispatch parent: `0da7166627f53118184687736132a88cdb668e1e`.
- Writer worktree: `C:\Users\jensh\.codex\worktrees\e2ee\PB-W00-control`.
- Candidate: the single detached commit containing this handoff, with the exact
  dispatch parent above and only the five T-086 write-scope paths below.
- Outcome: `spawnpassive <npcTemplateId> <distance> <anchorBotId>` now resolves
  only that active bot, captures a detached qualified transform, preserves its
  world/zone/instance boundary for terrain placement, revalidates the same bot
  immediately before NPC creation, and adds `anchorBotId`, `anchorZone`, and
  `anchorInstance` to anchored success output. It never selects a target,
  changes bot state/activity, or invokes gameplay control.
- Existing aliases, one/two-argument parsing, invariant culture, default and
  bounds, help bytes, command-character anchor behavior, failure order,
  passive AI, spawn response, displacement suppression, and no-respawn behavior
  remain unchanged. The legacy path never evaluates `BotManager`.

## Retained T-041 diagnosis

T-041 remains a product FAIL, not an Activity Director PASS. All three
`spawnpassive 10004 12` calls placed their passive NPCs at
`(13607.9, 13301.7, 28.5)`. One native skill created by bot `20001` credited
kills for all three stacked objects. That is one native area effect over a
stacked operator fixture, not three independent bot decisions. T-086 corrects
only the fixture-isolation boundary and does not reinterpret or overwrite the
immutable T-041 evidence.

## Changed paths

- `compatibility/aaemu-1.2-r208022-v3.patch`
- `playerbots.module.json`
- `src/AAEmu.Game/Scripts/Commands/SpawnPassiveNpcCommand.cs`
- `tests/AAEmu.UnitTests/Game/Scripts/Commands/SpawnPassiveNpcCommandTests.cs`
- `ops/tasks/T-086/HANDOFF.md`

The compatibility patch was regenerated from the isolated pinned host. Its 28
hunk payloads are unchanged; only every patch `index` abbreviation normalized
from nine to eight hexadecimal characters. Both AAEmu 1.2 manifest declarations
were refreshed to the resulting hash. The frozen AAEmu 3.0 identity is unchanged.

## Focused tests and isolated build

- Isolated proof host:
  `D:\Codex-Labs\t086-aaemu12-source-build-v1`, detached at pinned AAEmu 1.2
  commit `62e3eb1d87da01194802ac886cd500134facad28`.
- The complete compatibility patch applied with
  `git apply --check --whitespace=error-all` before application.
- Final no-incremental build:
  `dotnet build AAEmu.UnitTests\AAEmu.UnitTests.csproj --no-incremental --no-restore -p:PlayerBotsModuleRoot='D:\Codex-Labs\t086-aaemu12-source-build-v1\modules\archeage-playerbots\'`
  passed with 0 errors and 72 retained warnings. No warning references the
  T-086 command or tests. The retained warnings include the pinned
  `SQLitePCLRaw.lib.e_sqlite3` 2.1.11 `NU1903` advisory and existing host/module
  compiler, analyzer, and TUnit diagnostics.
- Final focused selection:
  `AAEmu.UnitTests.exe --treenode-filter '/*/AAEmu.UnitTests.Game.Scripts.Commands/SpawnPassiveNpcCommandTests/*' --minimum-expected-tests 28 --no-progress --no-ansi --output Detailed`
  passed 28, failed 0, skipped 0.
- The 28 cases cover legacy syntax/default/culture/bounds/help/aliases and
  BotManager independence; exact three-argument parsing; invalid/missing/zero
  IDs and arity; absent, mismatched, worldless, zone-zero, nonfinite,
  boundary-inconsistent, and stale bots; fail-before-creation behavior;
  qualified detached snapshots; terrain/world/zone/instance preservation;
  audit output; and target/combat/transform non-mutation.
- Full suite invocations: 0. The integration-wave gate remains unconsumed.

## Patch and source integrity

- Final AAEmu 1.2 patch SHA-256:
  `baa0979a200eb7f4403985d7166092f937033c31650aa11336fba31543d96606`.
- Both `playerbots.module.json` AAEmu 1.2 declarations match that hash.
- The patch is byte-identical to `git diff --binary --no-ext-diff` from the
  isolated pinned host, applies cleanly with whitespace errors enabled to the
  registered pristine AAEmu 1.2 reference, and reverse-checks against the
  isolated applied host.
- The registered reference remained clean and detached at
  `62e3eb1d87da01194802ac886cd500134facad28`.
- The four pre-handoff scoped files in the writer worktree and isolated module
  copy have matching SHA-256 values. Repository and isolated-host
  `git diff --check` both passed.

## Retained failures and boundaries

- One superseded focused run passed 27 cases and hit a test-fixture setup error
  when an added assertion wrote `DisabledSetPosition`, whose setter resolves an
  unrelated unregistered `SusManager`. No product assertion failed. The invalid
  fixture action was removed; target, combat-state, detached-transform, and
  activity-control non-mutation coverage remains, and the final run passed
  28/28.
- No source, test, build, patch, or final focused-test failure remains.
- No runtime, MySQL/database, client, registered integration host/module,
  integration branch, global ledger/lease, retained evidence, autonomous
  Director/combat/lifecycle behavior, scale, soak, packaging, or AAEmu 3.0 state
  was written or controlled.
- Physical per-bot fixture isolation, a fresh three-bot Director proof, the
  complete AAEmu 1.2 suite, runtime acceptance, scale, soak, and packaging
  remain unproven.

## Exact integration action

PB-000 must dispatch an independent Integrator. The Integrator must verify this
candidate is one clean child of dispatch parent
`0da7166627f53118184687736132a88cdb668e1e`, cherry-pick only this candidate onto
the accepted integration head, acquire a fresh build-only lease before touching
the registered AAEmu 1.2 integration host/module, verify the new patch hash,
install the exact candidate, run the 28 focused command cases, a clean complete
build, and exactly one full AAEmu 1.2 suite, then record a new integration
receipt. Only after that gate passes may PB-000 dispatch a fresh versioned T-041
runtime proof with one spatially isolated passive opportunity anchored to each
configured bot. Never reuse or overwrite the retained T-041 evidence root.
