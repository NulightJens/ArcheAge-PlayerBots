# T-055 handoff

T-055 is source-complete. Every fresh AAEmu 1.2 loopback `@system` actor now
captures one active `MainWorld`, applies that world's spawn transform, and
retains the same world as `ParentWorld`. Authorization, connectionless and
non-persistent identity, player-registration behavior, the headless fallback,
and `spawnpassive`'s fail-closed world guard remain unchanged.

## Source identity and changed files

- Task/branch: `T-055` / `codex/t055-system-actor-world`.
- Assignment base: `3bdeedadbe8818c29079bb495b93fcd8fe7582f8`.
- Delivery identity: the commit containing this handoff; report its immutable
  OID when closing the task.
- AAEmu 1.2 reference/base:
  `D:\Codex-Labs\aaemu-1.2-r208022-reference-v1` at
  `62e3eb1d87da01194802ac886cd500134facad28`.
- Regenerated 1.2 patch SHA-256:
  `0285c8c21133ec00abbd8f5de925c56dc2d87f7d8595fee89a6063b3ff02ca3e`.
- Changed exactly the declared `SystemActor.cs`, two deterministic test files,
  active 1.2 patch, compatibility README, module manifest, changelog, and this
  handoff. No global ledger, runtime lease, host source, database, client, or
  AAEmu 3.0 artifact changed.

## Proof

Before the later Control Tower read boundary, the registered AAEmu 1.2
integration checkout was used only for a `PlayerBotsModuleRoot` overlay build
and focused test execution; no host source was edited or deployed. After that
boundary it was not accessed again.

- `dotnet build AAEmu.UnitTests\AAEmu.UnitTests.csproj --no-restore
  --property:PlayerBotsModuleRoot='C:\Users\jensh\.codex\worktrees\8fd0\PB-W00-control\'`
  — PASS, 0 errors and 33 retained warnings.
- `AAEmu.UnitTests.exe --treenode-filter
  '/*/AAEmu.UnitTests.Services.WebApi/CommandControllerTests/*'
  --minimum-expected-tests 3 --no-progress --no-ansi --output Detailed`
  — 3 passed, 0 failed, 0 skipped.
- `AAEmu.UnitTests.exe --treenode-filter
  '/*/AAEmu.UnitTests.Game.Scripts.Commands/SpawnPassiveNpcCommandTests/*'
  --minimum-expected-tests 10 --no-progress --no-ansi --output Detailed`
  — 10 passed, 0 failed, 0 skipped.
- `AAEmu.UnitTests.exe --treenode-filter
  '/*/AAEmu.UnitTests.Game.Core.Managers.UnitManagers/CharacterManagerTests/*'
  --minimum-expected-tests 20 --no-progress --no-ansi --output Detailed`
  — 33 passed, 0 failed, 0 skipped. Focused total: 46/46.

The controller regression captures the actual fresh loopback actor and proves
exact name `@system`, access 100, account 0, null connection, the same registered
world and full spawn transform, and zero player registrations. The existing
worldless `addbot` request remains green. The command regression proves the
active-world actor advances to ordinary NPC-template validation, while a
worldless character still receives T-054's exact missing-world rejection.

Remaining task-worktree/reference proof:

- `git -C D:\Codex-Labs\aaemu-1.2-r208022-reference-v1 apply --check
  --whitespace=error-all <absolute-active-patch>` — PASS against the complete
  patch; reference head and clean state were identical before and after.
- `git patch-id --stable` — old and regenerated patch IDs both
  `7891d6bb88fd8b76f66067c15f064b611323d642`. Regeneration changed only two
  `GameService` hunk boundaries; host content and behavior are identical.
- `playerbots.module.json` parsed with `ConvertFrom-Json`; five declared hashes
  reproduced: both active 1.2 fields, frozen 3.0 patch, optional security patch,
  and migration. Frozen 3.0 identity remains
  `9c877d395b7f6dc7dc47dbf1bd4927af3a5d08392434c758cea364385a6ba15e`.
- `git diff --quiet HEAD --
  src/AAEmu.Game/Scripts/Commands/SpawnPassiveNpcCommand.cs` — PASS; the world
  guard was not weakened.
- `git diff --check` — PASS.

The first compile probes retained two missing `using` directives; the first
test probes retained a malformed tree filter, an empty controller argument,
and test worlds that were not registered in `WorldManager`. Those fixture and
operator issues were corrected. The final build and all final focused classes
above are green; there is no retained product failure.

## Runtime state, unproven boundary, and integration action

No Login, GameServer, MySQL, or client process was started, stopped, or
controlled. No runtime lease was claimed or edited, no database was accessed,
and no physical fixture or acceptance result was created. T-054 remains
`INCOMPLETE`; physical passive-NPC creation, combat, stealth, cleanup, and
restart remain unproven until a new immutable runtime task runs after
integration.

Integrator: cherry-pick the immutable T-055 delivery commit onto
`integration/aaemu12-world`, update the installed module through the normal
lease-controlled integration workflow, and run the integration-wave full 1.2
suite. The regenerated patch has the same stable patch ID as the installed
patch, so do not reapply it to an already-patched host merely because its
canonical byte hash changed. After source integration and deployment, PB-000
may dispatch a fresh versioned physical attempt; never reuse T-054's
`public-alpha-v3` evidence.
