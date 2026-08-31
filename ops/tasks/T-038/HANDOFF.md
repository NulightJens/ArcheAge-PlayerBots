# T-038 handoff

## Verdict

PASS. Every queued destination is authorized before `BotMovementTask` can publish movement. Rejections clear the destination, publish no move or jump, and retain a bounded status/reason diagnostic. Existing same-surface direct movement remains available only through the explicitly named compatibility policy.

## Source identity

- Codex worktree: `C:\Users\jensh\.codex\worktrees\46cf\PB-W00-control`
- Worktree state at dispatch: detached at `4db079de50304dc63885111c6f05bcf205ce5a9b`
- Planned task base: `7b38aa0b38058bed4ddb9ce3ea2d5bce8d01212f`
- The worktree parent differs from the planned base only by the Control Tower's `ops/CURRENT.yaml` dispatch update; T-038 did not edit that file.
- AAEmu 1.2 validation host base: `62e3eb1d87da01194802ac886cd500134facad28`
- Compatibility patch SHA-256: `afbb6aa2c5d379eb76ad339642c190f4185b4981815aa27c68d0680d28edd046`

## Changed files

- `src/AAEmu.Game/Bots/Navigation/NavigationDecisionBoundary.cs`
- `src/AAEmu.Game/Models/Game/Bots/BotMovementState.cs`
- `src/AAEmu.Game/Models/Tasks/Bots/BotMovementTask.cs`
- `tests/AAEmu.UnitTests/Bots/Navigation/NavigationDecisionBoundaryTests.cs`
- `tests/AAEmu.UnitTests/Game/Core/Managers/BotManagers/BotMovementTaskTests.cs`
- `ops/tasks/T-038/HANDOFF.md`

## Focused proof

- AAEmu 1.2 test project build, SDK `10.0.302`, with `PlayerBotsModuleRoot` overridden to this worktree: succeeded with 0 errors. Existing host/package warnings remain, including `NU1903` for `SQLitePCLRaw.lib.e_sqlite3` 2.1.11.
- Focused TUnit filter `/*/*/(NavigationDecisionBoundaryTests)|(BotMovementTaskTests)/*`, serialized with minimum expected count 36: 36 passed, 0 failed, 0 skipped.
- Covered non-finite geometry, invalid surfaces, destination height/surface disagreement, unavailable surface/reachability data, surface mismatch, explicit unreachable and reachable probes, fail-closed behavior, named same-surface compatibility acceptance, rejected-request no-movement behavior, and the immediate movement-task regression surface.

## Retained failures

- A read-only discovery command initially looked for module-local `.csproj` files that do not exist; the repository instead supplies MSBuild target imports for a host checkout. No files changed.
- An initial focused test attempt passed displayed test names to `--filter-uid`; those names were not node UIDs, so the minimum-test guard rejected a zero-test run.
- The first class-filtered run selected 35 tests and all 35 passed, but its provisional minimum-test guard was 40, so the run was retained as non-green. The corrected 35-test run passed; after adding the distinct reachability-confirmed case, the final 36-test run passed.
- A post-commit status subcommand initially inherited the validation host working directory and therefore reported the host checkout instead of the module worktree; the test portion passed, and the source/status check was immediately repeated with an explicit module path.
- No build failure or test assertion failure remains.

## Runtime state

- No live runtime was started, deployed, controlled, or stopped.
- The AAEmu validation host's tracked source state was unchanged by this task; compilation used a property override to consume the Codex worktree directly.

## Unproven boundaries

- No general wall, cave, water, ship, terrain, or pathfinding avoidance is claimed.
- The ground-height compatibility provider establishes only finite same-surface endpoints and bounded destination-height agreement; it does not inspect the route between them.
- No live navigation acceptance, full AAEmu 1.2 suite, AAEmu 3.0 gate, or Population Director integration was run; those remain integration/milestone responsibilities.

## Exact integration action

On `integration/aaemu12-world`, cherry-pick the single final T-038 commit hash reported by this writer. Resolve no scope outside the six files listed above, then include the result in the next integration-wave full AAEmu 1.2 suite.
