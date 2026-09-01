# T-083 handoff

## Source identity and outcome

- Writer dispatch and exact candidate parent:
  `dd09b2d6b655f92cd5afa36cd70e7d08b9efd877`.
- Rejected source baseline:
  `370a2ee0cff17ea7b56c9a06dc069ad789245de0`; all 13 T-081
  blobs were replayed before correction. The final candidate keeps 11 of those
  blobs byte-identical and changes only the compatibility patch plus its two
  manifest hash declarations. Within the 28-section patch, only
  `AAEmu.Game/GameService.cs` and
  `AAEmu.UnitTests/Services/GameServiceTests.cs` differ from T-081.
- Candidate: this single commit, detached and limited to the T-083 write scope.
- Isolated proof host:
  `D:\Codex-Labs\t083-aaemu12-source-build-v1`, detached at pinned AAEmu 1.2
  commit `62e3eb1d87da01194802ac886cd500134facad28`.
- Outcome: `GameService` now owns one lifecycle lock/state machine covering
  Director admission, construction/publication, `TryStart`, recurring schedule
  result/publication, cancellation, Director stop, state clearing, and the
  once-only transition into `BotManager.Stop()`. Lock order is service
  lifecycle, scheduler, then Director execution/state; Director ticks never
  acquire the service lifecycle lock.

## Changed paths

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
- `ops/tasks/T-083/HANDOFF.md`

The last 11 non-patch/non-manifest T-081 product paths above match their
rejected-candidate blob IDs exactly. T-083 adds no unrelated product behavior.

## Deterministic concurrency proof

All tests use `FakeTimeProvider`, bounded events/countdowns, and no sleeps.
The 15-test `GameServiceTests` selection passed 15/15 and includes these six
required lifecycle proofs:

- Stop entered while construction was blocked before instance publication:
  factory 1, schedule 1, cancel 1, Director stop 1, BotManager stop 1, active
  recurring entries 0.
- Stop entered while scheduling was blocked before success publication:
  schedule 1, cancel 1, Director stop 1, BotManager stop 1, active entries 0.
- Scheduler rejection concurrent with stop: schedule attempts 1, schedule
  successes 0, cancel 0, Director stop 1, BotManager stop 1, active entries 0.
- Six concurrent starts, all admitted to the test gate before construction was
  released: factory 1 and schedule 1; shutdown then produced cancel 1,
  Director stop 1, BotManager stop 1, and active entries 0.
- Six concurrent stops, all admitted while the first stop was held inside the
  lifecycle lock: cancel 1, Director stop 1, BotManager stop 1, active entries
  0; later repeated stops remained harmless.
- Normal and repeated sequential stop ordering was exactly
  `director-schedule,director-cancel,director-stop,bot-stop`, once each.

Every successful-schedule interleaving asserted that the exact scheduled task
was cancelled and marked cancelled; every stopped service rejected a later
start without another schedule.

## Build, focused tests, and patch proof

- Fresh-clone restore completed with the retained `NU1903` warning for
  `SQLitePCLRaw.lib.e_sqlite3` 2.1.11.
- Final clean build:
  `dotnet build AAEmu.UnitTests\AAEmu.UnitTests.csproj --no-incremental --no-restore -p:PlayerBotsModuleRoot='D:\Codex-Labs\t083-aaemu12-source-build-v1\modules\archeage-playerbots\'`
  succeeded with 72 warnings and 0 errors.
- Final service concurrency selection: 15 passed, 0 skipped, 0 failed.
- Final retained/expanded T-081 focused selection: 154 unique tests passed,
  0 skipped, 0 failed. With the dedicated service rerun, final passing
  executions were 169.
- Full suite invocations: 0; the integration-wave gate remains unconsumed.
- Final compatibility patch SHA-256:
  `395a83ab5bf6a4f4f1c0d56289590d6a36ad36b3ab5f87e1701d6aa17ffbefcd`.
  Both AAEmu 1.2 declarations in `playerbots.module.json` match.
- `git apply --check --whitespace=error-all` passed against the registered
  pristine AAEmu 1.2 reference, which remained clean at the pinned commit.
  Repository and isolated-host `git diff --check` both passed.

Retained final failures: none. Retained warnings are the pinned NuGet warning
and existing compiler/analyzer/TUnit warnings. One superseded setup invocation
attempted `--no-restore` before the fresh clone had `project.assets.json` and
failed with `NETSDK1004`; restore was then run and all final gates above passed.

## Runtime state, boundaries, and exact next action

No runtime, MySQL/database, client, registered integration host/module,
integration branch, global ledger/lease, retained evidence, AAEmu 3.0, scale,
soak, or packaging state was written or controlled. Physical one-zone refill,
the full AAEmu 1.2 suite, and runtime acceptance remain unproven.

A fresh dedicated Integrator must verify that this replacement is one clean
child of the dispatch commit and must not merge or replay rejected T-081. On
the current accepted integration head, under a newly committed build-only
lease, cherry-pick this replacement commit, install that exact source into the
registered AAEmu 1.2 host/module, recheck the patch hash, run a clean build,
the focused selection, and exactly one full AAEmu 1.2 suite. Only if every gate
is green may it commit the integration receipt/advance the integration branch;
T-041 runtime proof remains blocked until then.
