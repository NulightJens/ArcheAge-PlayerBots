# T-044 handoff

## Source identity

- Task: `T-044` — deterministic combat/stealth qualification harness.
- Branch: `task/T-044-combat-stealth-harness`.
- Worktree: `C:\Users\jensh\.codex\worktrees\a9a5\PB-W00-control`.
- Starting commit: `b564ea55dbc21ade081b8ae15b22e5f38f0abd14`.
- Wave-2 base ancestor: `e0350b3ee24352a1d96d81a60f34475fd8e56a45`.
- Delivered source commit: this handoff's commit; use the immutable OID reported with the worker result.

## Changed files

- Added `scripts/combat/CombatQualification.psm1` and the plan, evidence-template,
  evidence-validator, offline-test, input-template, and operator README files in
  `scripts/combat/`.
- Added exact lost-target retention and bounded exact-NPC reacquisition behavior
  in `BotCombatState`, `BotCombatTask`, `BotCombatManager`, and
  `BotAttackObjectCommand`.
- Added `/botbuffnpc <botId> <npcObjId> <buffId|-buffId> [abLevel]` through the
  existing bot-buff command seam so T-036 can apply and remove a verified stealth
  buff from the exact retained NPC target.
- Added deterministic manager, combat-task, and command tests, including a
  fake-time 51-second release case with no sleep.

## Proof

Offline qualification-harness proof, separate from physical acceptance:

- `pwsh -NoProfile -File .\scripts\combat\Test-CombatQualificationHarness.ps1`
  — PASS, 11 verdict scenarios, no sleeps.
- PowerShell parser validation of every `.ps1`/`.psm1` in `scripts/combat/` —
  PASS.
- JSON parsing of `scripts/combat/qualification-input.template.json` — PASS.
- Scan for `Start-Sleep`, `Start-Process`, `Stop-Process`, and `Remove-Item` in
  `scripts/combat/` — PASS (none present).
- `git diff --check` — PASS.

Focused AAEmu 1.2 overlay proof used the registered integration checkout while
binding `PlayerBotsModuleRoot` to this task worktree:

- `dotnet build AAEmu.UnitTests\AAEmu.UnitTests.csproj --no-restore --property:PlayerBotsModuleRoot="C:\Users\jensh\.codex\worktrees\a9a5\PB-W00-control\"`
  — PASS, 0 errors, 40 warnings.
- Direct TUnit class filters: `BotCombatManagerTests` 13/13,
  `BotCombatStateTests` 38/38, `BotCombatTaskTests` 15/15,
  `BotCommandsTests` 43/43, and `SpawnPassiveNpcCommandTests` 8/8 — total
  117/117 passed, 0 failed, 0 skipped.

The offline harness proves deterministic generation and fail-closed analysis.
It does not prove a physical AAEmu runtime result.

## Retained failures

- The first full-solution overlay build failed after product compilation because
  the new test lacked `IBuffs` and `FakeTimeProvider` imports. The imports were
  corrected; the final focused overlay build is green.
- The first exact-NPC reacquisition test reached the required transitions but
  entered native `BasicCombat` without a model-table fixture. The supported
  injected combat handler was carried through reacquisition; production behavior
  is unchanged when no handler is supplied. The exact test and its containing
  class are green.
- The final build retains 40 pre-existing/compiler warnings and no errors.

## Runtime and workspace state

- No server was started, stopped, deployed, or controlled. No runtime lease was
  acquired or changed.
- No database, client fixture, retained physical evidence, or global operations
  ledger was written.
- The registered read-only AAEmu 1.2 reference is clean at
  `62e3eb1d87da01194802ac886cd500134facad28`.
- The installed module in the registered integration checkout is clean at
  `60f574d70c35dce418dc8a9ca53a99bd775bf099`.
- The integration host remains at `62e3eb1d87da01194802ac886cd500134facad28`
  with the receipted module-install changes declared by `ops/WORKSPACES.yaml`.
  T-044 used only an MSBuild source overlay and did not edit that host source.

## Unproven boundary

Physical qualification remains **INCOMPLETE** until T-036 supplies and retains:

- exactly 100 real bot IDs and distinct real mortal target identities;
- a verified stealth buff and exact stealth target identities;
- isolated versioned test-database provenance;
- all 1/5/10/25/50/100 combat and matched-Idle observations;
- authoritative transition, cast, kill-credit, stealth loss/reacquisition/release,
  resource, health, byte-range log, cleanup, graceful-shutdown, and clean-restart
  evidence bound to one immutable plan fingerprint.

The exact operator commands and evidence fields are in
`scripts/combat/README.md`. The physical flow is:

```powershell
$module = 'D:\Codex-Labs\aaemu-1.2-r208022-integration-v1\modules\archeage-playerbots'
$run = 'D:\Codex-Labs\evidence\T-036\REPLACE-RUN-ID'
pwsh -NoProfile -File "$module\scripts\combat\New-CombatQualificationPlan.ps1" -InputPath "$run\qualification-input.json" -OutputPath "$run\plan.json"
pwsh -NoProfile -File "$module\scripts\combat\New-CombatQualificationEvidenceTemplate.ps1" -PlanPath "$run\plan.json" -OutputPath "$run\evidence.json"
# Under the T-036 runtime lease, execute every generated stimulus in order and fill evidence.json.
pwsh -NoProfile -File "$module\scripts\combat\Test-CombatQualificationEvidence.ps1" -PlanPath "$run\plan.json" -EvidencePath "$run\evidence.json" -OutputPath "$run\result.json"
```

Exit 0 is PASS, exit 1 is a complete measured FAIL, and exit 2 is INCOMPLETE.

## Exact integration action

On `integration/aaemu12-world`, the Integrator should cherry-pick the immutable
T-044 commit reported with this handoff. Then install that integrated module into
the registered AAEmu 1.2 integration checkout, run the focused overlay gate, and
retain the full-suite run for the integration wave. Do not claim physical combat
or stealth acceptance until T-036 completes the README workflow under the runtime
lease.
