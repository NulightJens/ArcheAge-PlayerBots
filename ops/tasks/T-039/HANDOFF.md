# T-039 handoff

## Source identity

- Codex worktree: `C:\Users\jensh\.codex\worktrees\6e30\PB-W00-control`
- Starting module HEAD: `4db079de50304dc63885111c6f05bcf205ce5a9b`
- Declared worker base (ancestor): `7b38aa0b38058bed4ddb9ce3ea2d5bce8d01212f`
- Focused host: AAEmu 1.2 checkout `C:\AAEmu` at `72fd2cc70bba3887e093e8a3800f13fed9b3e111`, descended from pinned host base `62e3eb1d87da01194802ac886cd500134facad28`
- Task commit: the single T-039 commit containing this handoff; its immutable hash is reported with the task verdict.

## Changed files

- `src/AAEmu.Game/Bots/Life/BotBehaviorProfile.cs`
- `src/AAEmu.Game/Bots/Life/BotLifeStateMachine.cs`
- `tests/AAEmu.UnitTests/Bots/Life/BotBehaviorProfileTests.cs`
- `tests/AAEmu.UnitTests/Bots/Life/BotLifeStateMachineTests.cs`
- `ops/tasks/T-039/HANDOFF.md`

## Focused proof

- Injected only the new task files into the clean AAEmu 1.2 host at MSBuild evaluation time through an ignored task-local verification target; the host source tree remained clean.
- `dotnet build C:\AAEmu\AAEmu.UnitTests\AAEmu.UnitTests.csproj --no-incremental`: passed with zero errors. The 65 warnings were retained host warnings; none referenced a T-039 file.
- `AAEmu.UnitTests.exe --treenode-filter '/*/AAEmu.UnitTests.Bots.Life/*/*' --minimum-expected-tests 8`: 8 passed, 0 failed, 0 skipped in 802 ms.
- The focused tests exercise all 96 state/event pairs, every required life state, the idempotence law for 48 lifecycle state/event inputs, supplied-time boundaries, invalid chronology, profile validation, and deterministic replay. No test sleeps.
- `git diff --check`: passed. The AAEmu host's tracked source status remained clean after verification.

## Retained failures

- A direct Roslyn syntax-check attempt targeting the Windows `NUL` device failed with `CS2021`; it produced no file or source change. The subsequent AAEmu build compiled the same sources successfully.
- Two initial focused-run attempts used guessed `--filter-uid` values and selected zero tests, tripping the one-test minimum. Discovery remained intact; the documented TUnit tree filter then selected and passed all eight task tests.
- Restore/build retained the host's existing `NU1903` advisory for `SQLitePCLRaw.lib.e_sqlite3` 2.1.11. T-039 neither introduces nor changes that dependency.

## Runtime state

- No game or login runtime was started, stopped, deployed, or controlled.
- No runtime lease was required or acquired.
- No deployed AAEmu source tree was modified.

## Unproven boundaries

- Character spawning, persistence, population-density decisions, and integration with existing managers/configuration remain outside T-039.
- The full AAEmu 1.2 suite remains an integration-wave gate.
- AAEmu 3.0 remains frozen and was not tested.
- Live runtime behavior was intentionally not exercised.

## Exact integration action

On `integration/aaemu12-world`, cherry-pick the single T-039 commit hash reported with this handoff, then include it in the next unchanged-fingerprint AAEmu 1.2 integration-wave suite. No compatibility patch, manifest, host hook, migration, or runtime deployment step is required.
