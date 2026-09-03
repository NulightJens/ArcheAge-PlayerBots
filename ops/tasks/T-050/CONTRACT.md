# T-050 contract

## Outcome

The corrected integrated AAEmu 1.2 runtime produces a complete new physical
receipt for matched Idle controls, mortal combat, stealth loss/reacquisition and
release, zero-bot cleanup, graceful shutdown, and clean restart.

## Runtime authority and retained predecessor

- T-036 attempt v1 remains immutable and INCOMPLETE. Use new external directory
  `D:\Codex-Labs\evidence\T-036\public-alpha-v2`; never overwrite or reinterpret
  v1 artifacts.
- Do not start or control Login/Game until the latest committed
  `ops/RUNTIME-LEASE.yaml` assigns `aaemu12` to T-050 and this task's resolved
  thread identity.
- Use only registered runtime `aaemu12_integration` and database identity
  `aaemu12_database_public_alpha_v1`. Schemas receive normal AAEmu runtime writes
  only. Direct inspection is read-only; reset/drop/truncate/import/reseed/manual
  row changes are prohibited.
- Never force-stop. A graceful-stop timeout leaves the process running and the
  gate INCOMPLETE with the lease retained.

## Pass

- Synchronize the final lease commit. Prove integration `da570e3`, installed
  module `78e75fc`, patch SHA-256 `5b44353f...`, host base `62e3eb1d`, clean
  reference/module, no Game/Login process, and free ports 1234/1237/1239/1250/1280.
- Reuse the retained task-local Data/config files only after proving exact schema,
  loopback, and v1 hashes (`d87acc71...` Game config and `2b18234b...` Login
  config). Do not recreate, edit, or overwrite them. The v2 evidence path must be
  absent before creation and all runtime log paths must be empty.
- Start only through the corrected `Start-ScaleGateRuntime.ps1
  -SafetyAcknowledged`. PASS requires the exact emitted selected Login schema,
  exact Game schema, both loopback services, retained PIDs/start times, hashes,
  listeners, and initial zero-bot metrics before any fixture work.
- Use exactly retained IDs 20001-20100. Through the loopback API and `@system`,
  verify one mortal passive-NPC template and an AAEmu 1.2 buff whose server
  response says `stealth=True`; spawn six distinct combat targets and distinct
  reacquire/release targets, never reusing a dead target.
- Create new qualification input/plan/evidence artifacts in v2 and execute every
  generated stimulus in order for matched Idle/combat cohorts 1, 5, 10, 25, 50,
  and 100 plus both stealth phases.
- Retain per phase exact log byte offsets/segment hash, command responses,
  complete metrics boundaries, two or more whole-process resource samples,
  per-bot health/debug evidence, target status, and zero-population cleanup.
  Idle controls require zero casts, kills, searches, and recoveries.
- Reacquisition must prove loss, `Combat -> Searching`, bounded radius, exact
  target-found, and Combat re-entry. Release must hold stealth beyond the
  55-second timeout, prove give-up and `Searching -> Idle`, then remove the buff.
- After final cleanup, gracefully stop Game then Login, preserve all exact logs
  at new literal v2 paths, and prove shutdown cleanup, process exit, and listener
  release. Restart the same build with distinct PIDs, retain exact selected-schema
  startup and zero-population metrics, then gracefully stop both again and
  preserve final logs. No Game/Login process or task listener may remain.
- Run `Test-CombatQualificationEvidence.ps1`; exit 0 alone is PASS. Exit 1 is
  FAIL; exit 2 or missing/ambiguous material is INCOMPLETE. Never reinterpret.
- Commit only `ops/evidence/aaemu12-t050-combat-stealth-v2.yaml` and a concise
  handoff. Raw logs/configs/command captures/database material/credentials remain
  external. State exact PB-000 lease-release and T-037 activation actions.

## Non-goals

- Client-visible acceptance, scale budgeting, Population Director work, soak,
  release packaging, source/integration/global-ledger edits, or AAEmu 3.0.
