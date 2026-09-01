# PlayerBots scale/resource truth gate

This is an advanced release and capacity-validation tool. It is not required to install PlayerBots or run a normal group of companions. Start with the [PlayerBots Guide](../../docs/README.md) unless you are measuring a dedicated server environment.

This directory contains the retained, live-server 0/10/50/100 PlayerBots
ladder. It does not start, stop, deploy to, or force-terminate AAEmu. The
operator must start an isolated runtime and later stop it with a real Ctrl+C so
AAEmu's `StopAsync` path runs. `Finalize-ScaleGate.ps1` then binds the cleanup
log line to the exact run by its retained byte offset. A run that crosses a log
rollover is proven across the unique archive containing that offset plus the
current log instead of discarding the rollover boundary.

## Safety preconditions

- Confirm that no other developer or service owns the runtime, ports, or
  selected schemas. The scripts require explicit `-SafetyAcknowledged`.
- Use an isolated runtime and a new versioned database named
  `aaemu_playerbots_*vN`. Legacy `aaemu_t021_*vN` evidence remains accepted for
  compatibility. The script refuses the normal `aaemu_game` database and
  checks that the deployed `Config.Local.json` explicitly selects the supplied
  name.
- Bind the command API to `127.0.0.1` or `localhost` in `Config.Local.json`.
- Supply 100 real, retained character IDs. The example IDs are placeholders,
  not evidence and must not be used unless those exact characters exist.
- Put the database password in `AAEMU_PLAYERBOTS_DB_PASSWORD` (or name another
  environment variable). The secret is passed through `MYSQL_PWD` only for the
  status query and is never written to artifacts.
- Never reset an existing database. If seed data must change, create a new
  `vN+1` schema and retain the prior schema.

`New-IsolatedScaleDatabase.ps1` clones donor game/login schemas into brand-new
versioned schemas and seeds 100 retained characters from an explicitly named
template. It refuses existing destinations, retains both donor dumps, scans the
dumps for destructive SQL before import, and never drops/resets a schema.
Generated container and item IDs are collision-checked and constrained to the
server's unsigned 32-bit ID-manager ranges. Item IDs also stay within the
manager's initial 100k bitset window so its signed expansion defect is not
triggered by test data.
`New-TaskRuntimeConfig.ps1` creates new task-local `Config.Local.json` files,
selects those schemas, and binds every listener to loopback without printing
the copied passwords. Both scripts refuse to overwrite prior output.

`Start-ScaleGateRuntime.ps1` starts the selected Login and Game binaries only
after explicit operator acknowledgement, loopback-port checks, isolated
database-name checks, and NLog's destructive-on-start log targets. It supplies the
database and listener settings as per-process environment variables because
AAEmu user secrets load after `Config.Local.json`. Startup succeeds only when
both logs identify the exact isolated schemas. Stop both processes with
`Stop-ScaleGateRuntime.ps1`; it sends Ctrl+C and never force-terminates.

## Exact selected-schema startup proof

The active AAEmu 1.2 patch makes `LoginService` emit
`Selected Login database schema: <database>` from the resolved
`Connections.MySQLProvider.Database` value after configuration precedence is
complete and before the Login database updater runs. Only the database name is
logged; host, port, user, password, and connection strings are excluded.

Before it can start Game, `Start-ScaleGateRuntime.ps1` requires that message to
end with the exact supplied Login schema name, with the name escaped as literal
text, and also requires `InternalNetwork started` plus the configured
`http://127.0.0.1:<port>` listener. A generic connection message, the updater's
hard-coded `aaemu_login` prefix, a donor or wrong schema, a substring collision,
or a missing selected-schema line is not proof and fails closed. Run the
deterministic no-runtime regression harness with:

```powershell
pwsh -NoProfile -File .\scripts\scale\Test-ScaleRuntimeStartupGuard.ps1
```

The committed T-036 attempt remains `INCOMPLETE`: the old Login binary emitted
only a generic connection line and a hard-coded updater prefix, so the guard
timed out and Game never started. Do not reinterpret or retry that attempt. An
Integrator must apply the new Login hunk and rebuild the registered AAEmu 1.2
integration host; only then may a newly dispatched physical attempt claim a
fresh runtime lease and reuse the retained external inputs.

`Analyze-ScaleGate.ps1` produces a non-overwriting analysis beside an immutable
final result. It recomputes normalized CPU from retained cumulative process CPU
ticks and timestamps, reports all required rates and distributions, and keeps
exact-measurement completeness separate from budget-qualified capacity.

## Two-pass budget workflow

The audit deliberately provides no invented limits. The first full ladder runs
without `-BudgetPolicyPath`; it retains raw measurements, writes
`budget-template.json`, returns exit code 2, and has verdict `INCOMPLETE`.
Review that no-bot baseline, choose the desired whole-server tick target, fill
every limit, record `approvedBy`/`approvedAtUtc`, and retain the policy.

Run the same ladder again with that approved policy. Every steady window must
retain the exact requested bot count. Missing DB/process/server metrics, a
partial ladder, an unset limit, or missing recovery evidence can never become
PASS. MySQL query rates are explicitly global-server rates because MySQL's
global status counters are not schema-scoped.

Example measurement command (PowerShell 7):

```powershell
$env:AAEMU_PLAYERBOTS_DB_PASSWORD = '<set outside shell history where possible>'
pwsh -NoProfile -File .\modules\archeage-playerbots\scripts\scale\Invoke-ScaleGate.ps1 `
  -RuntimeRoot 'C:\path\to\playerbots-runtime-v1' `
  -SourceRoot 'C:\path\to\AAEmu' `
  -BotIdsPath 'C:\path\to\retained-bot-ids.json' `
  -DatabaseName 'aaemu_playerbots_scale_v1' `
  -GameProcessId 12345 `
  -ServerLogPath 'C:\path\to\runtime-v1\AAEmu.Game\bin\Debug\net10.0\Logs\Server.log' `
  -SafetyAcknowledged
```

Send Ctrl+C with the included safe wrapper. On timeout it leaves the process
running and returns INCOMPLETE; it never calls `Stop-Process` or any force-kill
API. Stop the game first and the login process separately if the lab owns both:

```powershell
pwsh -NoProfile -File .\modules\archeage-playerbots\scripts\scale\Stop-ScaleGateRuntime.ps1 `
  -ProcessId 12345 -TimeoutSeconds 180
```

After the operator confirms the game process exited without force termination:

```powershell
pwsh -NoProfile -File .\modules\archeage-playerbots\scripts\scale\Finalize-ScaleGate.ps1 `
  -RunDirectory 'C:\path\to\runs\20260827T000000Z-12345678' `
  -ServerLogPath 'C:\path\to\runtime-v1\AAEmu.Game\bin\Debug\net10.0\Logs\Server.log'
```

The measure script always exits 2 because shutdown evidence cannot exist while
it is still querying the server. The finalizer is the only component allowed to
emit PASS: exit 0 is PASS, 1 is a complete measured FAIL, and 2 is INCOMPLETE.

## Evidence interpretation

`result.json` retains raw snapshots and one-second process samples. `report.md`
summarizes whole-server and host p50/p95/p99/max, CPU/memory/GC/allocation,
database rates, cadence, activity distribution, scans/path requests, decisions,
invalid targets, casts/kills, stuck recovery, and spawn/despawn costs. The
finalizer writes immutable follow-on files `result.final.json` and `final.md`;
it does not overwrite the raw result.

The highest stable population is the highest exact load that passes every
approved limit and recovery. It is never extrapolated. A simulator or a
bot-host-only timing result is not accepted as population evidence.
