# T-049 contract

## Outcome

The exact T-048 startup-proof patch is independently reviewed, installed on the
registered AAEmu 1.2 host, compiled, and fully unit-qualified before the
integration branch advances.

## Pass

- Start from the newest dispatched `integration/aaemu12-world` ancestor and
  replay exact source commit `dc9340fc61d1090e61eda99e55f0cd993b2af898`.
  Verify its stable patch identity and handoff; do not modify its worktree.
- Review the new Login log hunk, strict predicate module, start-script use,
  deterministic failure cases, documentation, and manifest hashes. Correct only
  a verified defect inside T-049's declared union scope before the full suite.
- Confirm `aaemu12_reference` remains clean at `62e3eb1d` and the registered
  integration host/module match their previous receipt before changing them.
- On `aaemu12_integration`, run read-only `git apply --check
  --whitespace=error-all --include=AAEmu.Login/LoginService.cs` against the
  integrated active patch. Only if that passes, apply the same exact include
  without `--check`. Do not reapply existing Game/UnitTests hunks or edit the
  clean reference.
- Update the installed module worktree cleanly to the reconstructed integration
  candidate, run the installer check/idempotent install, and prove the complete
  updated compatibility patch reverse-checks as installed.
- Run the 13-assertion no-runtime startup-guard test, changed PowerShell parser
  checks, manifest/hash reproduction, credential scan, and a zero-error
  single-worker/node-reuse-disabled AAEmu solution build.
- Run appropriate focused AAEmu 1.2 checks for host/startup compatibility, then
  invoke the complete AAEmu 1.2 unit suite exactly once after all prior gates are
  green.
- Do not start any runtime process or database-backed operation. If host state is
  mismatched or the incremental hunk cannot be applied exactly, retain it and
  write a blocked handoff; do not clean/reset/reverse/discard host state.
- On green, write `ops/evidence/aaemu12-startup-schema-integration-v1.yaml`,
  commit the final handoff, and fast-forward `integration/aaemu12-world` only.
  Include exact PB-000 actions to register the new installed module/patch
  fingerprint and dispatch a fresh T-036 runtime attempt.

## Non-goals

- Runtime/client control, database access, physical combat/stealth acceptance,
  scale qualification, Population Director work, or AAEmu 3.0.
- Global-ledger edits or destructive recovery of any checkout/process/artifact.
