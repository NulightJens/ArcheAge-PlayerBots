# T-049 handoff

T-049 is source-, install-, build-, focused-, and full-unit-green. The exact
T-048 payload was replayed from the dispatched integration ancestor, the one
new Login hunk was applied to the registered AAEmu 1.2 integration host, and
the installed module now points at the reconstructed source candidate.

## Source identity and review

- Dispatched ancestor: `3fc60d88dec7854a1b5a48f03fd84fed273bb067`
- Exact source commit: `dc9340fc61d1090e61eda99e55f0cd993b2af898`
- Source and replay stable patch ID:
  `38a61c6b11018b01cac8ea9577696dd090062a13`
- Reconstructed module commit:
  `78e75fc8995d871de56f761cc45fe4fd71b2ae3e`
- Reconstructed module tree:
  `92d100838dd4b963edc25245729ea29759deb05a`
- Control Tower setup commit retained in final ancestry:
  `0c4c98bb9c72562b560bd0e1f808eb8d61685fc4`
- Independent review found no verified defect requiring a T-049 correction.
  The Login hunk logs only the resolved `MySQLProvider.Database` value before
  the updater, while the shared predicate requires that exact literal schema
  line plus the internal-network and configured loopback-listener lines.

## Changed files

The replayed T-048 payload changes:

- `compatibility/aaemu-1.2-r208022-v3.patch`
- `compatibility/README.md`
- `playerbots.module.json`
- `scripts/scale/Start-ScaleGateRuntime.ps1`
- `scripts/scale/ScaleRuntimeStartupGuard.psm1`
- `scripts/scale/Test-ScaleRuntimeStartupGuard.ps1`
- `scripts/scale/README.md`
- `ops/tasks/T-048/HANDOFF.md`

T-049 adds:

- `ops/evidence/aaemu12-startup-schema-integration-v1.yaml`
- `ops/tasks/T-049/HANDOFF.md`

The integration host gained only the new tracked
`AAEmu.Login/LoginService.cs` modification beyond its previously receipted
module installation state. The complete active patch SHA-256 is
`5b44353fef730367b6dfe29baaa0c5141374163f6616549b6724cead91d82ac3`.

## Proof

- Prior host/module receipt match: PASS. The old complete patch reverse-checked,
  the module was clean at `9938495`, and the clean reference remained at
  `62e3eb1d87da01194802ac886cd500134facad28`.
- Exact Login include check and apply with `--whitespace=error-all`: PASS; one
  new tracked host path.
- Installed module: clean at `78e75fc`; installer check reported `installed`,
  the installer was idempotent, and the complete updated patch reverse-checks.
- No-runtime startup guard: 13/13 assertions passed.
- PowerShell parser: 3/3 changed script/module/test files passed.
- Manifest/hash reproduction: both host tracks and all three install hash
  fields passed; credential scan found only `MySQLProvider.Database` in the
  added Login source lines.
- Retained deterministic gates: 53 evidence-receipt assertions and 13 combat
  qualification scenarios passed.
- Single-worker, node-reuse-disabled solution build: 0 errors, 75 retained
  warnings.
- Focused AAEmu/PlayerBots tests: 120 passed, 0 skipped, 0 failed.
- Complete AAEmu 1.2 unit suite, invoked exactly once: 1,793 total; 1,789
  passed; 4 intentional skips; 0 failed.
- Receipt: `ops/evidence/aaemu12-startup-schema-integration-v1.yaml`.

## Retained failures, runtime state, and unproven boundaries

There are no product or gate failures. Two read-only PowerShell fingerprint
wrappers had operator syntax issues, and optional YAML-parser probes found no
parser package in the local Python/Node runtimes; the corrected fingerprint and
structural checks passed, and none changed repository state. The 75 existing
build warnings include the retained SQLitePCLRaw.lib.e_sqlite3 and SSH.NET
advisories.

No runtime lease was claimed. No Login, Game, client, or MySQL process was
started, stopped, or controlled, and no database was accessed. The clean
reference, client fixture, global ledgers, workspace registry, frozen AAEmu 3.0
host, and retained worker worktrees were not modified. Actual emitted-schema
startup proof and all physical combat/stealth acceptance remain pending a new
immutable T-036 attempt.

## Exact integration and PB-000 action

Fast-forward `integration/aaemu12-world` to the committed T-049 branch head
only; do not merge or replay any other branch. PB-000 should then register:

- integrated module source `78e75fc8995d871de56f761cc45fe4fd71b2ae3e`;
- installed module tree `92d100838dd4b963edc25245729ea29759deb05a`;
- active patch SHA-256
  `5b44353fef730367b6dfe29baaa0c5141374163f6616549b6724cead91d82ac3`;
- evidence receipt `ops/evidence/aaemu12-startup-schema-integration-v1.yaml`.

After accepting that receipt, PB-000 should dispatch a fresh immutable T-036
runtime attempt. That new task must claim a fresh `aaemu12` lease and prove the
exact selected Login schema before Game startup; it must not reuse or reinterpret
the retained incomplete T-036 attempt.
