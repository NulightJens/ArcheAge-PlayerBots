# T-121 handoff: server-owned arbitrary-level bot identities

## Candidate identity

- Branch: `codex/T-121-bot-identities`
- Dispatch parent: `8a0acd874fc7a2df94d50ed9dcf3f43782afc27c`
- Task source base: `0bff81c350a216b7018099749fd64096fbffc098`
- AAEmu 1.2 host target: `62e3eb1d87da01194802ac886cd500134facad28`
- Candidate commit: the single commit immediately following this handoff update; use the commit recorded in the task completion response.

## Outcome

The module now has a server-owned identity creation pipeline for arbitrary valid levels. `createbot` / `create_bot` accepts `<name> <race> <gender> <archetype> <level> [here|race-spawn]`, validates all input before mutation, asks a host authority to persist the character, records a level-aware archetype plan and roster entry, and admits the resulting persisted ID through the existing `BotManager.SpawnBot` path.

The factory is serialized and compensates failures in reverse order. Roster compensation is narrowly scoped and idempotent; the AAEmu 1.2 host patch retains a soft-deleted audit row after a post-persistence failure instead of hard-deleting it. A successful admission clears the host rollback guard.

The dedicated account must already exist and be offline. It is configured with `AAEMU_PLAYERBOTS_ACCOUNT_ID`; no account is created implicitly. `AAEMU_PLAYERBOTS_ROSTER_PATH` is optional and defaults to `Data/PlayerBots/bot-roster.json`.

Archetype plans are level-aware: level 1 receives only the starting ability tree, later thresholds unlock the next trees, and the final plan is present once all three are available. The host patch initializes native starter inventory/equipment/supplies, action slots, actabilities, skills, empty quests, exact character and ability experience, then uses the normal direct persistence path without a client connection or client packets.

`here` placement requires a finite transform in the default world instance and a valid zone because the AAEmu 1.2 character row does not retain arbitrary world-instance identity. `race-spawn` delegates placement to native race-template creation.

## Changed paths

- `src/AAEmu.Game/Bots/Population/Identity/BotIdentityCreation.cs`
- `src/AAEmu.Game/Bots/Population/Identity/BotIdentityFactory.cs`
- `src/AAEmu.Game/Bots/Population/Identity/BotIdentityFactoryOptions.cs`
- `src/AAEmu.Game/Bots/Population/Identity/BotRoster.cs`
- `src/AAEmu.Game/Bots/Population/Identity/JsonBotRosterStore.cs`
- `src/AAEmu.Game/Core/Managers/Bots/BotArchetypeManager.cs`
- `src/AAEmu.Game/Core/Managers/Bots/BotManager.cs`
- `src/AAEmu.Game/Scripts/Commands/CreateBotCommand.cs`
- `tests/AAEmu.UnitTests/Bots/Population/Identity/JsonBotRosterStoreTests.cs`
- `tests/AAEmu.UnitTests/Game/Core/Managers/BotManagers/BotIdentityFactoryTests.cs`
- `tests/AAEmu.UnitTests/Game/Scripts/Commands/CreateBotCommandTests.cs`
- `compatibility/aaemu-1.2-r208022-bot-identity-factory.patch`
- `docs/BOT-IDENTITIES.md`
- `ops/tasks/T-121/HANDOFF.md`

`IBotManager.cs` was evaluated but deliberately left content-identical so existing host and test implementations do not acquire a new mandatory member. Creation is exposed on the concrete `BotManager`, while the command accepts an injected creation factory.

## AAEmu 1.2 compatibility patch

Apply in this order to a clean checkout at the pinned host commit:

1. `compatibility/aaemu-1.2-r208022-v3.patch`
2. `compatibility/aaemu-1.2-r208022-bot-identity-factory.patch`

T-121 patch SHA-256:

`2461b25f571fbc9f11fd8d401e4ff7c1f5e2c07d5900896e608bf2ff18ffcf95`

The patch adds host access levels, non-creating account lookup, and the AAEmu 1.2 `CharacterManager` implementation of the identity-authority seam. It uses the host's character ID allocation, name registration, template validation, initialization, and direct database persistence paths. No database schema migration is included.

## Verification

Fresh verification checkout retained at `D:\Codex-Labs\aaemu-1.2-r208022-t121-verify-v1`:

- clean checkout of host commit `62e3eb1d87da01194802ac886cd500134facad28`
- module junction pointed at this T-121 worktree
- `git apply --check compatibility/aaemu-1.2-r208022-v3.patch`
- applied the v3 patch
- `git apply --check compatibility/aaemu-1.2-r208022-bot-identity-factory.patch`
- applied the T-121 patch
- reverse applicability check with `--whitespace=error-all` passed after application
- `dotnet build AAEmu.slnx --no-incremental --nologo`: passed, 81 warnings, 0 errors (18.92 seconds)

Final focused test command shape:

```powershell
$testApp = ".\AAEmu.UnitTests\bin\Debug\net10.0\AAEmu.UnitTests.exe"
& $testApp --treenode-filter "/*/*/<ClassName>/*" --minimum-expected-tests <Count> --no-progress --no-ansi --disable-logo
```

Final focused results:

- `BotIdentityFactoryTests`: 13 passed
- `CreateBotCommandTests`: 5 passed
- `JsonBotRosterStoreTests`: 8 passed
- `BotManagerTests`: 21 passed
- `BotAccessLevelsTests`: 3 passed
- Total: 50 passed, 0 failed, 0 skipped

The factory tests include a source-contract assertion over the standalone host patch for native inventory, skills, empty quests, exact-level experience, normal persistence, and absence of client packet/connection creation. `BotManagerTests` prove the existing persisted-ID load/spawn path remains intact.

## Corrected verification findings

- The first isolated solution build exposed two compile errors after `CreateBot` was added to `IBotManager`; unrelated test doubles would have required out-of-scope changes. The method was moved back to concrete `BotManager`, after which the build passed.
- One initial `here` command test touched the global world singleton. The fixture now supplies private finite world fields and the command suite passes.
- The first host build exposed two missing `WorldSpawnPosition` namespace errors. The host patch import was corrected and the fresh host build passes.
- The first access-level integrity run found the command because discovery expects class names beginning with `Bot`. The class was renamed to `BotCreateCommand`; all three integrity tests pass.
- Two later commands used incompatible test-runner syntax (`dotnet test ... --filter` and then `--filter-class`) and ran zero tests. They were invocation errors, not test failures. The final direct TUnit `--treenode-filter` run above is authoritative.

## Boundaries and unproven claims

- No T-120 host, runtime, client, database, deployed tree, reference checkout, evidence, lease, or ledger was read or modified for implementation or validation.
- No runtime process was started and no database query or write was performed.
- No AAEmu 3.0 implementation or validation was performed; the new 1.2-only integration is guarded from that target.
- The full test suite was not run. Full-suite invocation count for T-121 is zero.
- Real database creation, dedicated-account provisioning, restart persistence, client visibility, concurrency/scale, soak, and packaging remain unproven.
- The two isolated T-121 build directories were retained; nothing was deleted.

## Required independent integration action

An independent Integrator should cherry-pick the single T-121 candidate commit onto the accepted integration head. In a fresh build-only checkout at host commit `62e3eb1d87da01194802ac886cd500134facad28`, apply the v3 patch and then the T-121 patch, verify the hash above, run the 50 focused tests and a clean full solution build, then use the one authorized full-suite invocation. Only a separately authorized runtime task may provision/configure the dedicated account and prove database persistence, restart load, and client visibility. Do not reuse T-120's retained runtime or evidence as T-121 proof.
