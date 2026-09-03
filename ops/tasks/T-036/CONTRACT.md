# T-036 contract

## Outcome

The integrated AAEmu 1.2 candidate physically proves matched Idle controls,
mortal combat, stealth loss, bounded reacquisition/release, zero-bot cleanup,
graceful shutdown, and clean restart against the registered isolated databases.

## Runtime authority

- Do not start or control a runtime until `ops/RUNTIME-LEASE.yaml` names T-036
  and this task's client/thread identity as the active `aaemu12` owner.
- Resolve every path through `ops/WORKSPACES.yaml`. The runtime is
  `aaemu12_integration`, the read-only asset/config donor is
  `aaemu12_legacy_t022`, and the database identity is
  `aaemu12_database_public_alpha_v1`.
- The Game and Login schemas may receive only normal AAEmu runtime writes.
  Direct inspection is read-only; schema changes, reset, drop, truncate, import,
  reseed, and manual row changes are prohibited.
- Never force-stop a process. Use `Stop-ScaleGateRuntime.ps1` and leave any
  non-exiting process running with the gate INCOMPLETE.

## Pass

- Confirm integrated commit `30d2b909`, installed module `9938495e`, host base
  `62e3eb1d`, clean reference/module checkouts, no AAEmu Game/Login process, and
  free loopback ports 1234, 1237, 1239, 1250, and 1280 before startup.
- Create only the absent external evidence directory. Copy the missing Data
  directory from the registered donor into the registered integration host
  without modifying the donor or overwriting a target; retain source/target
  hashes and exact provenance.
- Create the two absent `Config.Local.json` files with
  `New-TaskRuntimeConfig.ps1`, using the registered donor configs, exact
  public-alpha Game/Login schema names, and loopback-only listeners. Never print
  or commit copied credentials.
- Start Login and Game only with `Start-ScaleGateRuntime.ps1
  -SafetyAcknowledged`. Retain exact PIDs/start times, executable/config hashes,
  loopback listeners, isolated schema startup lines, and initial zero-bot metrics.
- Use the 100 exact retained IDs `20001` through `20100`. Through the loopback
  command API and `@system`, verify an existing mortal passive-NPC template and
  a real AAEmu 1.2 buff template whose server response reports `stealth=True`.
  Spawn six distinct mortal combat targets plus distinct reacquire and release
  targets; never reuse a dead target.
- Fill a new immutable T-036 qualification input outside Git, generate the plan
  and evidence skeleton with the integrated combat scripts, and execute every
  generated stimulus in order for matched Idle/combat cohorts 1, 5, 10, 25, 50,
  and 100 plus both stealth phases.
- For every phase retain exact server-log byte offsets and segment SHA-256,
  command responses, complete metrics boundaries, at least two whole-process
  resource samples, per-bot health/debug evidence, target identity/status, and
  exact-population cleanup. Idle controls must have zero casts, kills, searches,
  and recoveries.
- Reacquisition must prove target loss, `Combat -> Searching`, bounded radius,
  exact target-found, and return to Combat after stealth removal. Release must
  retain the target stealthed past the 55-second timeout, prove give-up and
  `Searching -> Idle`, then remove the buff without retroactively satisfying
  reacquisition.
- After final zero-population cleanup, gracefully stop Game then Login. Preserve
  each exact log by moving it to a new literal evidence path before restart; do
  not overwrite or discard logs. Prove zero remaining bots/runtimes, process
  exit, and listener release.
- Restart the same build with distinct PIDs/start times, retain the new startup
  segment and a zero-population metrics snapshot, then gracefully stop Game and
  Login again and preserve their final logs. No AAEmu Game/Login process or task
  listener may remain when the handoff is committed.
- Run `Test-CombatQualificationEvidence.ps1`. Only exit 0 with complete
  fingerprinted material is PASS; exit 1 is FAIL and exit 2 or missing material
  is INCOMPLETE. Never reinterpret a result.
- Commit only a sanitized version-1 receipt and concise `HANDOFF.md`. Raw logs,
  configs, command captures, database material, and credentials remain outside
  Git. The handoff must give PB-000 exact lease-release and T-037 activation
  actions.

## Non-goals

- Client control or client-visible acceptance.
- Scale budgeting, Population Director work, or the 30-minute soak.
- Source/product fixes, integration-branch advancement, global-ledger edits, or
  AAEmu 3.0 work.
