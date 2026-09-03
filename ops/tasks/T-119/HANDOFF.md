# T-119 handoff

Verdict: `READY-source-only`. Autonomous local quest intake is implemented and
focused-proof green. It has not been installed into the lease-controlled host,
enabled in runtime configuration, or physically accepted in the ArcheAge
client because the user's T-118 live session remains active and outside this
task.

## Source identity

- Preparation/binding commits: `aa9d2957fa348b545615faa167f9c2f15d888e6d` /
  `d42236e77cfc8030b78eb9750db9f78a3df8d35d`.
- Implementation commit/parent/tree:
  `2d1b8d7103d35c46ec1bc9f13df36e40f4ba2b15` /
  `d42236e77cfc8030b78eb9750db9f78a3df8d35d` /
  `1276326b17cabfb3dbe10010ddbcf65dea729417`.
- Stable patch ID: `d525d7bc14e72ac0c5042bc6443044762e253084`.
- Branch: `codex/T-119-autonomous-quest-intake`; implementation replay was
  clean at the exact commit above and `git diff --check` passed.

## Changed behavior and files

Each `BotRuntime` now owns an opt-in `BotQuestIntakeController`. Before the
one-kill lifecycle can select grind, an eligible idle bot reads the cached
nearby-NPC blackboard value, filters invalid/world-incompatible/unreachable
targets, ranks story quests first, walks normally to one quest giver, and calls
AAEmu's non-forced `AddQuestFromNpc` authority for every eligible start on that
NPC. Rejections use per-candidate backoff and do not fabricate success or starve
other starts. Controller-owned motion is revalidated and stopped on disable,
death/despawn, world change, or competing combat state. `/botdebug` exposes one
stable read-only status line. The feature defaults off.

The implementation commit changes exactly:

- `src/AAEmu.Game/Bots/Questing/BotQuestIntakeController.cs`
- `src/AAEmu.Game/Bots/Host/BotRuntime.cs`
- `src/AAEmu.Game/Bots/Host/BotHostTask.cs`
- `src/AAEmu.Game/Models/Game/Bots/BotConfig.cs`
- `src/AAEmu.Game/Scripts/Commands/BotDebugCommand.cs`
- `tests/AAEmu.UnitTests/Bots/Questing/BotQuestIntakeControllerTests.cs`
- `tests/AAEmu.UnitTests/Bots/Host/BotHostBehaviorTests.cs`
- `tests/AAEmu.UnitTests/Game/Core/Managers/BotManagers/BotConfigTests.cs`
- `tests/AAEmu.UnitTests/Game/Scripts/Commands/BotCommandsTests.cs`
- `docs/CONFIGURATION.md`
- `README.md`

## Proof

- Complete new controller suite: 17 passed, zero failed.
- Complete `BotHostBehaviorTests`: 35 passed, zero failed, including host
  precedence that leaves the one-kill grind selector idle.
- Complete `BotCommandsTests`: 46 passed, zero failed.
- `BotConfigTests`: all 28 non-destructive test methods passed. The sole
  excluded method, `Load_MissingFile_WritesDefaultFileAndKeepsDefaults`, deletes
  its temporary directory in cleanup and was not invoked under the machine-wide
  no-destruction policy. All new defaults, full JSON load, round-trip, golden
  serialization, finite fallback, and bound checks passed.
- Fresh retained proof host:
  `D:\Codex-Labs\t119-aaemu12-source-build-v2`.
- Fresh AAEmu.Game no-incremental build: zero errors, 31 warnings in 8.49s.
  Warning families are `NU1903`, `CS0169`, `CS8632`, `CS9113`, `CA1859`, and
  `CA2000`; none originates in the new controller.
- Compatibility patch SHA-256 remains
  `baa0979a200eb7f4403985d7166092f937033c31650aa11336fba31543d96606`.
  Installer check-only reported `ready/supported`, installation into the
  isolated proof host succeeded, strict reverse apply-check passed, and the
  proof host contains exactly the expected 30 patch/module/migration status
  entries. Its module is clean at the exact implementation commit.
- Proof Game assembly SHA-256:
  `c4c0c510095ea86984c4923049f4a71075ef413a46a3e498e745654c2ce8ccab`.
- Registered AAEmu 1.2 reference remains clean and detached at
  `62e3eb1d87da01194802ac886cd500134facad28`.

## Runtime state and unproven boundaries

No registered Login, Game, client, launcher, deployed module, runtime config,
database, or observer was changed or controlled. Read-only final process
sampling found the original T-118 processes still running: Login PID `103064`,
Game PID `23160`, and launcher PID `62516`.

Physical acceptance is still required. This source does not create characters,
reset quest state, execute/report objectives, chain completions, or provide
arbitrary long-range routing. It therefore proves the requested autonomous
find/walk/accept intake behavior, not the first-five-quests completion claim.

## Exact next action

After the user explicitly finishes the T-118 live session, create an independent
Integrator/runtime task with the AAEmu 1.2 lease. Cherry-pick implementation
commit `2d1b8d7103d35c46ec1bc9f13df36e40f4ba2b15` onto the saved integration
branch, install and rebuild the registered AAEmu 1.2 host, set
`QuestIntakeEnabled` to `true`, and gracefully restart. Then use an existing
eligible level-one Nuian bot identity beside the user and accept no quests by GM
command: verify it chooses the local story giver, walks into range, accepts the
story and eligible nearby side quests, and that `/botdebug <characterId>` plus
server logs match the client-visible journal state.
