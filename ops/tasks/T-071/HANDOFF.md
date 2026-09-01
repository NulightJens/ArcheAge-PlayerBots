# T-071 handoff

Verdict: `PASS-source-focused`.

## Source identity

- Writer envelope: `a1c15016e3e7ccc121c0bad048dc7bb9d5c7aa8f`.
- Declared integrated parent: `2f0cedd6d2400c3f435f2d01415df27b357a13eb`.
- AAEmu 1.2 host base: `62e3eb1d87da01194802ac886cd500134facad28`.
- Candidate: the single T-071 commit containing this handoff.

## Changed files

- `src/AAEmu.Game/Bots/Life/BotLifeController.cs`
- `src/AAEmu.Game/Scripts/Commands/BotDebugCommand.cs`
- `tests/AAEmu.UnitTests/Bots/Life/BotLifeControllerTests.cs`
- `tests/AAEmu.UnitTests/Bots/Host/BotHostBehaviorTests.cs`
- `tests/AAEmu.UnitTests/Game/Scripts/Commands/BotCommandsTests.cs`
- `ops/tasks/T-071/HANDOFF.md`

The lifecycle now enters an observable controller-owned recovery wait after the
one-kill targetless/noncombat/movement-clear boundary when HP or MP is not full.
The host's existing suspension seam pauses mover/brain work while native world
regeneration remains untouched. Invalid resource observations fail closed.
Only a full valid HP/MP observation captures the immutable completion snapshot,
logs the original progression delta, and enters the unchanged one-shot logout
callback path. Recovery status and invariant resource values are exposed in the
controller view, structured logs, and `/botdebug`.

## Proof

Validation used retained isolated host
`C:\Users\jensh\.codex\build-evidence\T-071-v1\host`, cloned from the pinned
AAEmu 1.2 base and installed only inside that isolated copy. The build selected
this writer worktree through `PlayerBotsModuleRoot`.

- `dotnet build AAEmu.UnitTests\AAEmu.UnitTests.csproj --no-incremental --no-restore -p:PlayerBotsModuleRoot='C:\Users\jensh\.codex\worktrees\b798\PB-W00-control\'`
  - `0` errors, `40` retained warnings.
- TUnit tree selections:
  - `BotLifeControllerTests`: `10` passed.
  - `BotHostBehaviorTests`: `32` passed.
  - `BotCommandsTests`: `44` passed.
  - `BotLifeStateMachineTests`: `6` passed.
  - `BotBehaviorProfileTests`: `2` passed.
  - `BotKillCreditTests`: `6` passed.
  - Total: `100` passed, `0` skipped, `0` failed.
- `git diff --check`: passed.

Retained non-blocking build warnings include the existing `NU1903`
`SQLitePCLRaw.lib.e_sqlite3` advisory plus existing compiler, TUnit, and code
analysis warnings. No test failure is retained. An initial focused host run
correctly exposed an invalid generic fixture maximum-MP observation; the bounded
lifecycle fixture now supplies explicit fixed maxima, and the final selection is
green.

## Environment and runtime boundary

Before a Control Tower scope clarification arrived, one output-only
`dotnet build` completed in the registered `aaemu12_integration` host using the same
`PlayerBotsModuleRoot` override. It refreshed ignored `bin`/`obj` outputs only.
The registered host's tracked source/configuration status remained the declared
receipted install state, and its installed module stayed clean at
`4287ec53e998ecff2e5eb9670908166957a79662`. Those outputs were retained; the
registered host was not used again. All final proof came from the isolated path
above.

No AAEmu Game/Login process, MySQL instance, or client was started, controlled,
or stopped; no runtime lease was claimed. Native recovery timing, physical
recovery logs and `/botdebug`, normal live logout, later autonomous iterations,
and clean restart remain unproven and require a fresh immutable runtime task.

## Integration action

Cherry-pick the single T-071 candidate commit containing this handoff onto
`integration/aaemu12-world`. Then, under the AAEmu 1.2 lease, install/build the
integrated source and require the integration gate to remain green before
dispatching a new versioned one-bot autonomy proof with a fresh evidence root.
