# T-123 handoff: transfer-road world navigation graph

## Verdict

READY — source-only candidate. The transfer-road snapshot seam, deterministic world graph, distance-weighted cross-zone routing, local-navigation handoff, fail-closed replanning, debug export, documentation, and focused tests are complete. Physical runtime acceptance remains pending by contract.

## Source identity

- Branch: `codex/T-123-transfer-road-navigation`
- Contracted module source base: `0bff81c22766f37e34f14cb2cf47760824a04ba5`
- Dispatch/preparation parent: `8a0acd874fc7a2df94d50ed9dcf3f43782afc27c`
- Implementation commit: `9340ceb8f58c0be8aa00e83e530bce442bfa5273`
- Implementation tree: `69b415cac214956f6c9df98207d1eece0f006348`
- Stable implementation patch ID: `4f2fafe8e229bb0702518eabc96e81adaf407e1a`
- Pinned AAEmu 1.2 host base: `62e3eb1d87da01194802ac886cd500134facad28`

## Changed files

- `compatibility/aaemu-1.2-r208022-transfer-road-adapter.patch`
- `docs/WORLD-NAVIGATION.md`
- `src/AAEmu.Game/Bots/Navigation/TransferRoadSnapshot.cs`
- `src/AAEmu.Game/Bots/Navigation/WorldNavigationDebugExporter.cs`
- `src/AAEmu.Game/Bots/Navigation/WorldRoadGraph.cs`
- `src/AAEmu.Game/Bots/Navigation/WorldRoadGraphBuilder.cs`
- `src/AAEmu.Game/Bots/Navigation/WorldRoadNavigator.cs`
- `src/AAEmu.Game/Bots/Navigation/WorldRoadRoutePlanner.cs`
- `tests/AAEmu.UnitTests/Bots/Navigation/WorldRoadGraphTests.cs`
- `tests/AAEmu.UnitTests/Bots/Navigation/WorldRoadNavigatorTests.cs`
- `tests/AAEmu.UnitTests/Bots/Navigation/WorldRoadRoutePlannerTests.cs`
- `ops/tasks/T-123/HANDOFF.md` (this handoff-only follow-up)

## Proof

- Focused navigation tests: `AAEmu.UnitTests.exe --treenode-filter '/*/AAEmu.UnitTests.Bots.Navigation/*/*' --minimum-expected-tests 30 --no-progress --no-ansi --output Detailed` — 30 passed, 0 failed, 0 skipped, 412 ms.
- Unit-test project build: `dotnet build AAEmu.UnitTests/AAEmu.UnitTests.csproj --nologo --no-restore --verbosity quiet` — 0 errors, 43 warnings.
- Standalone game build: `dotnet build AAEmu.Game/AAEmu.Game.csproj --nologo --no-restore --no-incremental --verbosity quiet` — 0 errors, 40 warnings, 12.79 s.
- Built `AAEmu.Game.dll` SHA-256: `F3265829EFD5A24611318B1CE0B02CC4456FC623112974EFFB4D64087155BE74`.
- Adapter patch SHA-256: `C81E3CE9F52EA04FE1692EF470863D1CF547AD34380A842CCB948831A1EF23B1`.
- The adapter patch passes `git apply --check` against the clean, detached registered reference at the pinned host commit and passes `git apply -R --check` in the isolated proof checkout.
- The registered `aaemu12_reference` remained clean at `62e3eb1d87da01194802ac886cd500134facad28`.
- `git diff --cached --check` passed before the source commit, and the source commit contains only the declared source/test/compatibility/documentation scope.

The isolated retained build checkout is `D:\Codex-Labs\aaemu-1.2-r208022-t123-build-v1`. It contains the adapter, the existing AAEmu 1.2 module compatibility patch, and a junction to this task worktree; it is not a registered, deployed, or runtime host.

## Retained failures and warnings

- A first `--no-restore` build attempt in the fresh isolated clone failed because `obj/project.assets.json` did not yet exist. A normal restore/build populated assets; every final build and focused-test gate above is green.
- Initial development-only focused assertions exposed an overly strict float assertion and same-road endpoint self-snapping; both were corrected before the candidate commit and are covered by the final 30-test run.
- Final builds retain upstream and analyzer warnings, including the host package advisory and CA1859 suggestions; there are no compile errors or focused-test failures.

## Runtime state

- No registered runtime, deployed AAEmu host, client fixture, database, or lease file was started, stopped, changed, or sampled.
- T-120 and its runtime state were untouched.
- The AAEmu 1.2 reference checkout was read-only and remains clean.

## Unproven boundaries

- No physical client/server runtime acceptance was attempted.
- Production `transfer_path.xml` content was not sampled through a running host; graph behavior is proven with synthetic immutable snapshots and the compiling host adapter seam.
- The full AAEmu 1.2 suite was not run; that gate belongs to the integration wave.
- AAEmu 3.0 remains frozen and was not built or tested.

## Exact integration action

From the designated integration worktree, cherry-pick implementation commit `9340ceb8f58c0be8aa00e83e530bce442bfa5273`. On a fresh or lease-controlled AAEmu 1.2 checkout at `62e3eb1d87da01194802ac886cd500134facad28`, apply the normal module patch and then `compatibility/aaemu-1.2-r208022-transfer-road-adapter.patch`, install the module, rebuild the game and unit-test projects, rerun the focused navigation filter, and run the full 1.2 suite once for the integration wave. Runtime/client proof requires a later task holding the runtime lease.
