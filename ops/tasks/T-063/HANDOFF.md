# T-063 handoff

T-063 is complete as a bounded source change. The production host now uses the
shared life state machine to activate exactly one valid free Idle runtime for a
single nearby-mortal grind, records the deterministic decision, and requests
the normal persisted logout path exactly once after authoritative kill credit
and a safe targetless/noncombat boundary.

## Source identity and changed files

- Task worktree started at dispatch commit
  `f22ae76d8a401ad374f74df55a6ef6a675c3ad1f`, whose dispatch base is
  `c658ebbb9f4ba5ec15285416ff8bd1c7f016520b`.
- AAEmu 1.2 reference identity is
  `62e3eb1d87da01194802ac886cd500134facad28`.
- Validation used the existing approved
  `compatibility/aaemu-1.2-r208022-v3.patch`, SHA-256
  `0285C8C21133EC00ABBD8F5DE925C56DC2D87F7D8595FEE89A6063B3FF02CA3E`.
- The implementation commit is the exact T-063 task head reported with this
  handoff.
- Changed files are `src/AAEmu.Game/Bots/Life/BotLifeController.cs`,
  `src/AAEmu.Game/Bots/Host/BotRuntime.cs`,
  `src/AAEmu.Game/Bots/Host/BotHost.cs`,
  `src/AAEmu.Game/Bots/Host/BotHostTask.cs`,
  `src/AAEmu.Game/Scripts/Commands/BotDebugCommand.cs`,
  `tests/AAEmu.UnitTests/Bots/Life/BotLifeControllerTests.cs`,
  `tests/AAEmu.UnitTests/Bots/Host/BotHostBehaviorTests.cs`,
  `tests/AAEmu.UnitTests/Game/Scripts/Commands/BotCommandsTests.cs`, and this
  handoff.

## Behavior and proof

The new per-runtime controller resets a re-added persistent identity to a fresh
post-spawn Idle life state. It fails closed unless the snapshot contains exactly
one healthy, unforced, inactive, targetless and destinationless runtime with a
live nearby hostile NPC already supplied by the existing blackboard seam. An
accepted `ActivityRequested` transition configures a one-kill grind without
choosing a target or destination and exposes/logs
`activity=grind reason=nearby_mortal` with UTC timestamps.

After the existing authoritative kill-credit subscription records the kill, the
controller waits for Idle noncombat state, refuses a living retained target,
clears only a defeated retained target, accepts `LogoutRequested` once, and
queues the host callback. `BotHostTask` invokes `BotManager.DespawnBot` only
after runtime iteration, outside the runtime lock, and never retries the
callback. Success completes the life state to Offline; failure remains visible
and keeps the runtime inert. `/botdebug` reports the deterministic life,
transition, activity, reason, and logout timestamps/outcome.

An isolated exact-reference AAEmu 1.2 build with the approved compatibility
patch completed with 0 errors and 40 retained warnings. The final affected test
selection passed 141, failed 0, skipped 0. It covers the controller fail-closed
matrix, one-kill activation and logout, callback concurrency and exception
behavior, clean re-registration, life FSM/profile, host behavior, kill credit,
combat task, manager, and command output. The full AAEmu 1.2 suite was not run
because repository policy reserves it for the integration wave.

The initial pristine-reference build was retained at
`C:\Users\jensh\.codex\build-evidence\T-063-v1`; it stopped at the known
missing `OnTeamChangedArgs` compatibility seam and is superseded by the green
exact-reference patched validation at
`C:\Users\jensh\.codex\build-evidence\T-063-v2`. No production or focused test
failure remains.

## Runtime state and unproven boundaries

No AAEmu runtime, client, database, listener, runtime lease, deployed host, or
AAEmu 3.0 checkout was started, controlled, or changed. The registered AAEmu
1.2 reference remained clean at its pinned commit.

Physical one-bot activity, kill, normal persisted logout, restart persistence,
and a new immutable T-061 acceptance receipt remain unproven until integration.
This task does not claim the T-061 gate passed.

## Exact integration action

Integrator: cherry-pick the exact T-063 task head reported by the worker onto
`integration/aaemu12-world`, install/build it against the AAEmu 1.2 v3
compatibility seam, and run the full AAEmu 1.2 suite once for the integration
wave. Only after that is green, dispatch a new immutable T-061 runtime proof
version for one-bot activity, kill, persisted logout, and restart persistence.
Do not change the disposition of unrelated tasks as part of this integration.
