# T-081 handoff

## Source identity

- Task worktree: `C:\Users\jensh\.codex\worktrees\c891\PB-W00-control`
- Dispatch commit: `069960e3d977767a3738b729c8e9f83767a78e8e`
- Exact parent: `a76ca2476e85f134aec94081f50ca4ba3c5112da`
- Candidate: this commit, detached and limited to the declared T-081 write scope
- AAEmu 1.2 proof source: versioned isolated clone `D:\Codex-Labs\t081-aaemu12-source-build-v1`, host commit `62e3eb1d87da01194802ac886cd500134facad28`

## Outcome

- Added an opt-in, fail-closed one-zone `BotActivityDirectorTask` with deterministic ordered identity selection, serialized/idempotent ticks, one-at-a-time refill, bounded retry cooldown, maximum refusal without shrinking, wrong-boundary normal despawn, immutable snapshots, and structured operator logs.
- Replaced the legacy production one-shot auto-spawn seam in the AAEmu 1.2 compatibility patch with one recurring Director scheduled after `Server started!`; graceful shutdown cancels/stops it before normal `BotManager.Stop()` cleanup.
- Extended lifecycle eligibility to independently admit only configured Director identities that are actually in the same configured zone/default-world/default-instance boundary. Manual, unconfigured, and wrong-boundary runtimes remain fail-closed.
- Added disabled-by-default validated configuration and surfaced `T081_DIRECTOR` through the existing metrics snapshot command.
- No runtime, database, client, registered host, integration branch, global ledger, lease, retained evidence, release artifact, or AAEmu 3.0 state was mutated.

## Changed files

- `compatibility/aaemu-1.2-r208022-v3.patch`
- `playerbots.module.json`
- `src/AAEmu.Game/Bots/Ops/BotActivityDirectorTask.cs`
- `src/AAEmu.Game/Bots/Host/BotHostTask.cs`
- `src/AAEmu.Game/Bots/Life/BotLifeController.cs`
- `src/AAEmu.Game/Models/Game/Bots/BotConfig.cs`
- `src/AAEmu.Game/Configurations/BotConfig.json`
- `src/AAEmu.Game/Scripts/Commands/BotMetricsCommand.cs`
- `tests/AAEmu.UnitTests/Bots/Ops/BotActivityDirectorTaskTests.cs`
- `tests/AAEmu.UnitTests/Bots/Host/BotHostBehaviorTests.cs`
- `tests/AAEmu.UnitTests/Game/Core/Managers/BotManagers/BotConfigTests.cs`
- `tests/AAEmu.UnitTests/Game/Scripts/Commands/BotCommandsTests.cs`
- `ops/tasks/T-081/HANDOFF.md`

## Proof

Build command, run from the isolated clone:

```powershell
dotnet build 'AAEmu.UnitTests\AAEmu.UnitTests.csproj' --no-incremental --no-restore -p:PlayerBotsModuleRoot='D:\Codex-Labs\t081-aaemu12-source-build-v1\modules\archeage-playerbots\'
```

Final build: succeeded, 72 warnings, 0 errors. Focused tests used `AAEmu.UnitTests.exe --treenode-filter <filter> --minimum-expected-tests <count> --no-progress --no-ansi`; 131 unique tests passed, with 142 passing executions including the final 10-service-test and 1-metrics-test post-build reruns:

- `BotActivityDirectorTaskTests`: 8/8
- Director-related `BotConfigTests`: 10/10
- `BotHostBehaviorTests`: 33/33
- `BotCommandsTests`: 45/45
- `GameServiceTests`: 10/10
- `BotLifeControllerTests`: 10/10
- `BotKillCreditTests`: 6/6
- `BotLifeStateMachineTests`: 6/6
- `BotBehaviorProfileTests`: 2/2
- `BotAutoSpawnTaskTests`: 1/1

Patch proof:

- `compatibility/aaemu-1.2-r208022-v3.patch` SHA-256: `5e30b0fdcbe6e9defac6426917fce22d2dcb61f860d4fb982d0dc4cf2657e2a8`
- Both `playerbots.module.json` declarations match that hash.
- `git apply --check --whitespace=error-all` passed against the clean pinned AAEmu 1.2 reference checkout.
- `git diff --check` passed, and the regenerated patch contains no legacy production `BotAutoSpawnTask`/`AutoSpawn*` configuration references.

## Retained warnings and failures

- Retained failures: none.
- Retained build warnings: the pinned dependency warning `NU1903` for `SQLitePCLRaw.lib.e_sqlite3` 2.1.11 and existing compiler/analyzer/TUnit warnings; T-081 also exposes non-blocking analyzer suggestions in its new Director/test code. No warning prevented compilation or focused proof.
- Superseded setup/test-harness failures: the first isolated materialization omitted `Bots/Ops` because of a case-insensitive copy exclusion; an initial Director fixture invoked the host `ZoneManager`; and the first metrics assertion ignored the host command prefix. Each was corrected before the final build and green focused runs.

## Runtime state and unproven boundaries

- Runtime state: never started or controlled; no runtime lease acquired.
- The registered AAEmu 1.2 reference remained clean at the pinned commit. All host edits/build outputs exist only in the retained versioned isolated clone.
- Explicitly unproven here: physical runtime acceptance, MySQL/roster behavior, client observation, scale, soak, packaging/release, full AAEmu 1.2 integration suite, and AAEmu 3.0 compatibility. These belong to later integration/runtime gates.

## Exact T-082 integration action

Verify the reported T-081 candidate is a single child of `069960e3d977767a3738b729c8e9f83767a78e8e`, then cherry-pick that candidate onto the current accepted integration head in the lease-controlled integration worktree. Re-verify the compatibility-patch SHA declarations and run the integration-wave AAEmu 1.2 full suite there; do not install from or merge this detached writer worktree directly.
