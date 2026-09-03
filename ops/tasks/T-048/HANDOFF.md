# T-048 handoff

T-048 is source-complete. The active AAEmu 1.2 patch now makes `LoginService`
log its resolved selected schema, and the scale runtime guard requires that
exact credential-free line plus both existing loopback startup predicates
before Game can start.

## Source identity

- Task branch: `codex/T-048-selected-schema-startup-proof`
- Dispatched ancestor: `58f86b47b7f62f3861b597de268b8a798a5c763a`
- AAEmu 1.2 host base: `62e3eb1d87da01194802ac886cd500134facad28`
- Active patch SHA-256: `5b44353fef730367b6dfe29baaa0c5141374163f6616549b6724cead91d82ac3`
- Frozen AAEmu 3.0 patch SHA-256 remains
  `9c877d395b7f6dc7dc47dbf1bd4927af3a5d08392434c758cea364385a6ba15e`.

## Changed files

- `compatibility/aaemu-1.2-r208022-v3.patch`
- `compatibility/README.md`
- `playerbots.module.json`
- `scripts/scale/Start-ScaleGateRuntime.ps1`
- `scripts/scale/ScaleRuntimeStartupGuard.psm1`
- `scripts/scale/Test-ScaleRuntimeStartupGuard.ps1`
- `scripts/scale/README.md`
- `ops/tasks/T-048/HANDOFF.md`

## Proof

- `Test-ScaleRuntimeStartupGuard.ps1`: PASS, 13 deterministic no-runtime
  assertions. Exact schema and regex-metacharacter names pass; generic
  connection, hard-coded updater prefix, donor, substring, missing, wrong
  schema, wrong logger, missing internal network, non-loopback listener, and
  wrong-port fixtures fail closed.
- PowerShell parser: PASS for the changed script, module, and test harness.
- Manifest parsing and SHA-256 reproduction: PASS for both AAEmu tracks, both
  AAEmu 1.2 patch fields, the optional security patch, and the migration.
- Credential scan: PASS. The new Login hunk logs only
  `dbConnectionsConfig.Value.MySQLProvider.Database`; it contains no host,
  port, user, password, data-source, or connection-string field.
- Registered `aaemu12_reference`: exact clean head confirmed before proof; the
  complete patch and the single Login include both pass read-only
  `git apply --check --whitespace=error-all`; the reference remained clean.
- Retained T-036 receipt and handoff: byte-unchanged by this task.
- `git diff --check`: PASS.

## Retained failure, runtime state, and unproven boundaries

The committed T-036 attempt remains `INCOMPLETE`; its old Login executable did
not emit the exact selected schema, the guard timed out, and Game never
started. This task did not reinterpret or alter that receipt or its external
evidence. No runtime lease was claimed, no Login/Game/client/MySQL process was
started or controlled, no database was accessed, and no AAEmu host or frozen
AAEmu 3.0 file was edited. Patched-host compilation, actual log emission, and a
new physical T-036 run remain unproven until independent integration.

## Exact Integrator action

1. Integrate the T-048 task commit into `integration/aaemu12-world` and update
   the installed module in registered workspace `aaemu12_integration` through
   the normal lease-controlled install/build workflow.
2. From registered workspace `aaemu12_integration`, first run a read-only
   `git apply --check --whitespace=error-all
   --include=AAEmu.Login/LoginService.cs` against the integrated
   `compatibility/aaemu-1.2-r208022-v3.patch`. If it passes, run the same
   command without `--check`. This applies only the one new Login hunk; do not
   reapply the already installed Game and UnitTests hunks.

   ```powershell
   $hostRoot = 'D:\Codex-Labs\aaemu-1.2-r208022-integration-v1'
   $patchPath = 'C:\Users\jensh\Documents\Codex\playerbots-worktrees\PB-W00-control\compatibility\aaemu-1.2-r208022-v3.patch'
   git -C $hostRoot apply --check --whitespace=error-all --include='AAEmu.Login/LoginService.cs' $patchPath
   git -C $hostRoot apply --whitespace=error-all --include='AAEmu.Login/LoginService.cs' $patchPath
   ```

3. Rebuild `AAEmu.Login`, run the focused AAEmu 1.2 integration checks, and
   record a new integration receipt binding the module commit, host state, and
   patch SHA-256 above. Do not start a runtime in the integration task.
4. After PB-000 accepts that integration receipt, dispatch a fresh immutable
   T-036 physical attempt. That new task—not this one—must claim a fresh
   `aaemu12` lease and prove the emitted exact schema before Game startup.
