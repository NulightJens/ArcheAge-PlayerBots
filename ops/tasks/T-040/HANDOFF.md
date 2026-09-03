# T-040 handoff

Verdict: **PASS** — the binary contract is satisfied by a stable AAEmu-character identity model and a versioned, deterministic JSON roster boundary.

## Source identity

- Task: `T-040` (`AAEmu12`)
- Codex worktree: `C:\Users\jensh\.codex\worktrees\035a\PB-W00-control`
- Starting module commit: `4db079de50304dc63885111c6f05bcf205ce5a9b`
- Planned worker base (ancestor): `7b38aa0b38058bed4ddb9ce3ea2d5bce8d01212f`
- Focused host: AAEmu 1.2 r208022 at `62e3eb1d87da01194802ac886cd500134facad28`

## Changed files

- `src/AAEmu.Game/Bots/Population/Identity/BotRoster.cs`
- `src/AAEmu.Game/Bots/Population/Identity/JsonBotRosterStore.cs`
- `tests/AAEmu.UnitTests/Bots/Population/Identity/JsonBotRosterStoreTests.cs`
- `ops/tasks/T-040/HANDOFF.md`

The identity is a non-zero authoritative AAEmu character ID. The roster preserves enabled state, profile, home zone, desired life state, and `playerbots.bot-roster.v1`; serializes and reads entries in character-ID order; rejects duplicate IDs; and consults an injected authoritative-character lookup on every read and write. No schema artifact was necessary, so no database was created, reset, or changed.

## Focused proof

- `dotnet build AAEmu.UnitTests\AAEmu.UnitTests.csproj --no-incremental -p:PlayerBotsModuleRoot=<T-040-worktree>\`
  - Passed with 0 errors. The host emitted 42 pre-existing warnings, including the retained `SQLitePCLRaw.lib.e_sqlite3` NU1903 advisory; no T-040 warning was emitted.
- `AAEmu.UnitTests.exe --treenode-filter '/*/AAEmu.UnitTests.Bots.Population.Identity/JsonBotRosterStoreTests/*' --minimum-expected-tests 7 --no-progress --no-ansi --output Detailed`
  - Passed: 7 total, 7 succeeded, 0 failed, 0 skipped.
  - Proves create/read/update through three store instances, deterministic ordering independent of dictionary iteration, all persisted fields and schema version, duplicate rejection in API and stored data, unknown-schema rejection, invalid authoritative-character rejection, and update-missing rejection.

## Retained failures

- The first focused invocation used seven displayed method names with `--filter-uid`; this TUnit runner selected 0 tests and exited 1.
- The second invocation put namespace and class in one tree-filter segment; it selected 0 tests, and `--minimum-expected-tests 7` correctly exited 1.
- Both selection failures and their complete outputs remain in the Codex task transcript. No product or test assertion failed.

## Runtime state

- No live runtime was started, deployed, controlled, or stopped.
- The isolated AAEmu 1.2 validation checkout was used only for compilation and focused unit tests.

## Unproven boundaries

- No BotManager, BotHost, BotConfig, spawn, density, or live-runtime integration is included.
- The injected character lookup is the authority seam; wiring it to AAEmu's character database belongs to a later integration task.
- The file store serializes callers within one store instance; multi-process writer coordination is not claimed.
- The full AAEmu 1.2 suite remains the integration-wave gate.

## Exact integration action

From the integration worktree on `integration/aaemu12-world`, run:

```powershell
git cherry-pick (git -C 'C:\Users\jensh\.codex\worktrees\035a\PB-W00-control' rev-parse HEAD)
```
