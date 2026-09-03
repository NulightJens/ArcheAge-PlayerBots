# T-057 contract

## Outcome

The integrated `@system` world-context correction enables a complete new live
headless AAEmu 1.2 receipt for matched Idle controls, mortal combat, stealth
loss/reacquisition/release, zero-bot cleanup, graceful shutdown, and clean
restart. No ArcheAge client is launched in this task.

## Runtime authority and immutable predecessors

- T-036 v1, T-050 v2, and T-054 v3 remain immutable and `INCOMPLETE`. Use only
  new external directory `D:\Codex-Labs\evidence\T-036\public-alpha-v4`;
  never overwrite or reinterpret a predecessor.
- Do not start or control Login/Game until the latest committed
  `ops/RUNTIME-LEASE.yaml` assigns `aaemu12` to T-057 and this task's final
  thread identity. This is the only task authorized to own the live headless
  services and loopback API. Starting or controlling the ArcheAge client is
  prohibited.
- Use only registered runtime `aaemu12_integration` and database identity
  `aaemu12_database_public_alpha_v1`. Schemas receive normal AAEmu runtime
  writes only. Direct inspection, mutation, reset, drop, truncate, import,
  reseed, or manual row changes are prohibited.
- Never force-stop. A graceful-stop timeout leaves the process running and the
  gate `INCOMPLETE` with the lease retained.

## Pass

- Synchronize the final lease commit. Prove integration `d262117...`, tested and
  installed module `1d08632...` / tree `1f3befa...`, patch SHA-256
  `0285c8c...`, host base `62e3eb1d...`, clean reference/module, no
  Login/Game/client process, and free ports 1234/1237/1239/1250/1280.
- Reuse retained task-local Data/config inputs only after proving exact schema,
  loopback, and predecessor hashes. Do not recreate, edit, or overwrite them.
  The v4 evidence path must be absent before creation and all active runtime log
  paths must be empty.
- Start only through corrected `Start-ScaleGateRuntime.ps1
  -SafetyAcknowledged`. Use the retained v3 runner lessons: in-process startup,
  typed control-object output, bounded line-by-line marker scans, and immutable
  step receipts. Do not overwrite v3 scripts or artifacts.
- PASS requires a written startup receipt with exact emitted Login/Game schemas,
  required loopback/start markers, retained PIDs/start times/hashes/listeners,
  and initial zero-bot metrics before fixture work.
- Use exactly retained IDs 20001-20100. Through loopback API and `@system`,
  verify one mortal passive-NPC template and an AAEmu 1.2 buff whose response
  says `stealth=True`; spawn six distinct combat targets and distinct
  reacquire/release targets, never reusing a dead target.
- Create new qualification input, plan, and evidence artifacts in v4 and execute
  every generated stimulus in order for matched Idle/combat cohorts 1, 5, 10,
  25, 50, and 100 plus both stealth phases.
- Retain per phase exact log byte offsets/segment hash, command responses,
  complete metrics boundaries, at least two whole-process resource samples,
  per-bot health/debug evidence, target status, and zero-population cleanup.
  Idle controls require zero casts, kills, searches, and recoveries.
- Reacquisition must prove loss, `Combat -> Searching`, bounded radius, exact
  target-found, and Combat re-entry. Release must hold stealth beyond the
  55-second timeout, prove give-up and `Searching -> Idle`, then remove the buff.
- Retain and classify any BaseBaiLoader or missing-world recurrence against
  ordered readiness, fixture request, shutdown, cleanup, process exit, and port
  release. Any pre-shutdown missing-world recurrence is material after T-056.
- After final zero-bot cleanup, gracefully stop Game then Login, preserve exact
  logs at new literal v4 paths, and prove process/listener release. Restart the
  same build with distinct PIDs, retain exact-schema startup and zero-population
  metrics, then gracefully stop both again and preserve final logs. No
  Login/Game/client process or required listener may remain.
- Run `Test-CombatQualificationEvidence.ps1`; exit 0 alone is PASS. Exit 1 is
  FAIL; exit 2 or missing/ambiguous material is INCOMPLETE. Never reinterpret.
- Commit only `ops/evidence/aaemu12-t057-combat-stealth-v4.yaml` and a concise
  handoff. Raw logs/configs/commands/database material/credentials stay
  external. State the exact PB-000 lease-release and T-037 activation actions.

## Non-goals

Launching or controlling a physical ArcheAge client; client-visible acceptance;
scale budgeting; Population Director; soak; release packaging; source,
integration, global-ledger, retained-input, client-fixture, or AAEmu 3.0 edits.
