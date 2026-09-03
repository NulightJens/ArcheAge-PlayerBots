# T-122 handoff

Verdict: `READY-source-only`. Autonomous NPC/signpost quest intake and the
initial native monster-hunt lifecycle are implemented, fail closed, and pass
the focused proof described below. No registered host, runtime, client,
database, lease, or global control ledger was changed.

## Source identity

- Assigned source base: `0bff81c350a216b7018099749fd64096fbffc098`.
- Dispatch/preparation HEAD: `8a0acd874fc7a2df94d50ed9dcf3f43782afc27c`.
- Branch: `codex/T-122-autonomous-signpost-kill-quest`.
- The commit containing this handoff is the T-122 candidate; resolve its exact
  identity with `git rev-parse HEAD` after commit creation.
- `git diff --check` passed before candidate creation. The final candidate is
  a descendant of the assigned source base.

## Changed behavior and files

Quest intake now ranks exact eligible NPC and doodad quest starts together,
preserves main-story priority, revalidates the current giver/function in
interaction range, and calls only AAEmu's native acceptance lifecycle. The
new opt-in lifecycle parses one authoritative monster-hunt objective, selects
only matching living hostile templates, delegates movement and combat to the
existing controllers, waits for authoritative credit and Ready state, and
reports through the native NPC, doodad, or journal endpoint. It chooses only a
deterministic valid offered reward, never grants credit or fabricates quest
state, clears only controller-owned state, and resumes nearby discovery after
authoritative completion. Unsupported/ambiguous shapes, stale objects, world
or ownership conflicts, death, unreachable targets, and bounded timeouts
suspend safely with stable debug state and bounded logging.

The candidate changes exactly:

- `src/AAEmu.Game/Bots/Questing/BotQuestAuthority.cs`
- `src/AAEmu.Game/Bots/Questing/BotQuestIntakeController.cs`
- `src/AAEmu.Game/Bots/Questing/BotQuestLifecycleController.cs`
- `src/AAEmu.Game/Bots/Host/BotRuntime.cs`
- `src/AAEmu.Game/Bots/Host/BotHostTask.cs`
- `src/AAEmu.Game/Models/Game/Bots/BotConfig.cs`
- `src/AAEmu.Game/Scripts/Commands/BotDebugCommand.cs`
- `tests/AAEmu.UnitTests/Bots/Questing/BotQuestAuthorityTests.cs`
- `tests/AAEmu.UnitTests/Bots/Questing/BotQuestDoodadIntakeTests.cs`
- `tests/AAEmu.UnitTests/Bots/Questing/BotQuestLifecycleControllerTests.cs`
- `tests/AAEmu.UnitTests/Bots/Host/BotHostBehaviorTests.cs`
- `tests/AAEmu.UnitTests/Game/Core/Managers/BotManagers/BotConfigTests.cs`
- `tests/AAEmu.UnitTests/Game/Scripts/Commands/BotQuestAutonomyTests.cs`
- `compatibility/aaemu-1.2-r208022-doodad-quest-adapter.patch`
- `docs/QUEST-AUTONOMY.md`
- `ops/tasks/T-122/HANDOFF.md`

## Isolated build and test proof

- Fresh retained proof tree:
  `D:\Codex-Labs\t122-aaemu12-source-build-v2`.
- Host base: clean AAEmu 1.2 r208022 commit
  `62e3eb1d87da01194802ac886cd500134facad28` before patch assembly.
- Released base compatibility patch SHA-256:
  `baa0979a200eb7f4403985d7166092f937033c31650aa11336fba31543d96606`.
- New doodad/objective/report adapter SHA-256:
  `f2db46554f97a48fc71b068960d8ad3f2f98b2a496d4db0338b4eef253ea890e`.
- Both compatibility patches applied cleanly after `git apply --check`; strict
  reverse apply-checks pass in the retained assembled host. The host has 34
  expected overlay/module status entries.
- Exact final command `dotnet build AAEmu.slnx --configuration Debug` passed:
  zero errors, 80 warnings, 25.84 seconds. Warnings are pre-existing pinned
  dependency advisories and existing compiler/analyzer families; the final
  implementation introduced no build error.
- Final Game assembly SHA-256:
  `1a330e3d7a16bc6f7c4cc7010092ce8e5759ef9fb7e2de583f7b4436fb5ce7fb`.
- Final UnitTests assembly SHA-256:
  `fd515c48664893fa9dec7dad6251383b3f6721473b8ea379df4ad81cd81`.
- Complete questing namespace: 38 passed, zero failed, including the retained
  17 NPC intake tests plus doodad, authority, lifecycle, no-credit,
  endpoint/reward, chaining, duplicate, timeout, and fail-closed cases.
- `BotQuestAutonomyTests`: 4 passed, zero failed.
- Exact lifecycle and intake host-precedence checks: 1 passed each, zero
  failed.
- Exact config default, full-document, golden-output, and lifecycle-bound
  checks: 1 passed each, zero failed.
- Total focused executions: 48 passed, zero failed. The full suite was not run,
  in accordance with the source-task integration-wave boundary.
- Registered reference checkout remains clean at the exact host commit above.

## Runtime and acceptance boundary

The behavior defaults off. This task did not install into or start a registered
AAEmu host, acquire or edit the runtime lease, alter runtime configuration,
connect to the database, launch/control the client, or mutate any retained live
session. The Desireen Signpost is documented only as the named acceptance
fixture; no object ID, template ID, coordinates, or quest ID are embedded in
the implementation, compatibility adapter, tests, or autonomy documentation.

Physical acceptance remains an Integrator/runtime responsibility: assemble the
candidate on a fresh AAEmu 1.2 host, apply the released base patch followed by
the new doodad adapter, enable quest intake and completion in validated bot
configuration, then observe an eligible bot natively discover, accept, fight,
receive authoritative credit, report/reward, and rescan without a per-quest
command. Preserve normal runtime/client/database/lease controls while doing so.
