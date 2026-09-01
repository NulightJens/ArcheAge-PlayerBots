# T-060 contract

## Outcome

The integrated qualified active-bot anchor enables a complete fresh live
headless AAEmu 1.2 receipt for matched Idle controls, mortal combat, stealth
loss/reacquisition/release, zero-bot cleanup, graceful shutdown, and clean
restart. This is a physical server/database-backed bot test, not a physical
ArcheAge client test. No ArcheAge client may be launched or controlled.

## Runtime authority and immutable predecessors

- T-036 v1, T-050 v2, T-054 v3, and T-057 v4 remain immutable and
  `INCOMPLETE`. Use only new external directory
  `D:\Codex-Labs\evidence\T-036\public-alpha-v5`; never overwrite,
  reinterpret, or complete a predecessor.
- Do not start or control Login/Game until the latest committed
  `ops/RUNTIME-LEASE.yaml` assigns `aaemu12` to T-060 and this task's final
  thread identity. This is the sole task authorized to own the live headless
  services and loopback API. Starting or controlling the ArcheAge client is
  prohibited. A conflicting owner, process, or required-port listener blocks
  startup rather than permitting takeover.
- Use only registered runtime `aaemu12_integration` and database identity
  `aaemu12_database_public_alpha_v1`. Schemas receive normal AAEmu runtime
  writes only. Direct inspection, mutation, reset, drop, truncate, import,
  reseed, or manual row changes are prohibited.
- Never force-stop. Stop Game then Login through their graceful console or
  Ctrl+C path. A graceful-stop timeout leaves the process running, retains the
  lease, and makes the gate `INCOMPLETE`.

## Pass

- Synchronize the final committed lease claim. Prove the exact assignment
  head, integrated tested/installed module `cf57b114...` / tree `1e15fa38...`,
  patch SHA-256 `0285c8c...`, host base `62e3eb1d...`, clean reference/module,
  zero Login/Game/client processes, and free ports 1234/1237/1239/1250/1280.
  Observe existing MySQL read-only and do not start, stop, or control it.
- Reuse retained task-local Data/config inputs only after proving their exact
  schema, loopback, and predecessor hashes. Do not recreate, edit, or overwrite
  them. The v5 evidence path must be absent before creation and all active
  runtime log paths must be empty.
- Start only through `Start-ScaleGateRuntime.ps1 -SafetyAcknowledged`. Carry
  forward the corrected v4 runner behavior: in-process startup, typed control
  objects, bounded line-by-line marker scans, and string-valued grade handling.
  Every successful fixture command response must be externalized immediately
  at a new literal v5 path before any parsing or validation.
- PASS requires a written startup receipt with exact emitted Login/Game
  schemas, required loopback/start markers, PIDs/start times/hashes/listeners,
  continuous zero-client observation, and initial zero-bot metrics before
  fixture work.
- Use exactly retained bot IDs 20001-20100. Create the first active bot before
  world-positioned `@system` fixture work, then prove its qualified non-zero-zone
  world/instance/finite transform is the system-actor anchor. A worldless,
  ZoneId 0, mismatched-instance, stale, or ambiguous anchor is material and
  makes the gate `INCOMPLETE`.
- Through the loopback API and `@system`, select one mortal passive-NPC target
  template and an AAEmu 1.2 buff whose response says `stealth=True`. Treat grade
  as its typed string label, not as a numeric field. Prove each fixture response
  and spawned object has the intended template, a non-zero zone, the qualified
  bot's world and instance, finite transform, and mortal alive status before
  stimuli. Spawn six distinct combat targets and distinct reacquire/release
  targets; never reuse a dead target.
- Create new qualification input, plan, and evidence artifacts in v5 and execute
  every generated stimulus in order for matched Idle/combat cohorts 1, 5, 10,
  25, 50, and 100 plus both stealth phases. Clean to zero bots and fixtures at
  every declared boundary so the deterministic lowest-ID anchor is explicit.
- Retain per phase exact log byte offsets/segment hash, fixture responses,
  complete metrics boundaries, at least two whole-process resource samples,
  per-bot health/debug evidence, target status, and zero-population cleanup.
  Idle controls require zero casts, kills, searches, and recoveries.
- Mortal combat requires verified casts and target death for every declared
  combat phase. Reacquisition must prove loss, `Combat -> Searching`, bounded
  radius, exact target-found, and Combat re-entry. Release must hold stealth
  beyond the 55-second timeout, prove give-up and `Searching -> Idle`, then
  remove the buff.
- Retain and classify every BaseBaiLoader or missing-world recurrence against
  ordered readiness, fixture request, cleanup, shutdown, process exit, and port
  release. Any pre-shutdown missing-world recurrence is material after T-059.
- After final zero-bot and fixture cleanup, gracefully stop Game then Login,
  preserve exact logs at new literal v5 paths, and prove process/listener
  release. Restart the same build with distinct PIDs, retain exact-schema
  startup and zero-population metrics, then gracefully stop both again and
  preserve final logs. No Login/Game/client process or required listener may
  remain.
- Run `Test-CombatQualificationEvidence.ps1`; exit 0 alone is PASS. Exit 1 is
  FAIL; exit 2 or missing/ambiguous material is INCOMPLETE. Never reinterpret.
- Commit only `ops/evidence/aaemu12-t060-combat-stealth-v5.yaml` and a concise
  handoff. Raw logs, configs, commands, database material, and credentials stay
  external. State the exact PB-000 lease-release and T-037 activation actions.

## Non-goals

Launching or controlling a physical ArcheAge client; client-visible
acceptance; scale budgeting; Population Director; soak; release packaging;
source, integration, global-ledger, retained-input, client-fixture, or AAEmu 3.0
edits.
