# T-048 contract

## Outcome

AAEmu 1.2 Login emits a credential-free line naming the actual selected database,
and the runtime start guard accepts only that exact isolated schema line before
starting Game.

## Pass

- Treat the committed T-036 INCOMPLETE receipt as the regression fixture. Do not
  reinterpret it or modify its retained external evidence.
- Extend the active AAEmu 1.2 compatibility patch so `LoginService` logs the
  resolved `dbConnectionsConfig.Value.MySQLProvider.Database` after configuration
  precedence is complete and before database update/startup advances. The line
  must contain the database name only—never host, port, user, password, connection
  string, or another credential field.
- Update `Start-ScaleGateRuntime.ps1` to require an exact escaped match for the new
  selected-schema line plus its existing loopback startup predicates. A generic
  connection line, hard-coded updater prefix, donor schema, substring collision,
  missing line, or wrong schema must fail closed before Game start.
- Put deterministic log-predicate logic in `ScaleRuntimeStartupGuard.psm1` and add
  a no-runtime `Test-ScaleRuntimeStartupGuard.ps1` covering exact pass and every
  required failure case, including names that contain regex metacharacters.
- Update scale documentation with the exact proof contract and T-036 retained
  failure/retry boundary.
- Regenerate every AAEmu 1.2 compatibility-patch SHA-256 field in
  `playerbots.module.json`; do not change the frozen AAEmu 3.0 hash.
- Prove the complete updated patch applies cleanly to registered clean reference
  `aaemu12_reference` using read-only `git apply --check`. Do not apply it there.
- Run the deterministic startup-guard test, PowerShell parser checks for every
  changed script/module, manifest JSON parsing/hash reproduction, prohibited
  credential/log-field scan, and `git diff --check`.
- Commit a concise handoff with source identity, changed files, exact proof,
  retained failures, runtime state, and the Integrator action needed to apply the
  one new host hunk to the registered integration host.

## Non-goals

- Editing or deploying to any AAEmu host checkout.
- Starting Login/Game/client/MySQL, querying or mutating databases, claiming a
  runtime lease, or rerunning T-036.
- Combat, stealth, scale, Population Director, release, or AAEmu 3.0 changes.
