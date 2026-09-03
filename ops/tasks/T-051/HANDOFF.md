# T-051 handoff

T-051 is source-complete. The startup wrapper now evaluates only a single
scalar snapshot from a growing log, and runtime readiness requires exact Login
and Game selected-schema lines plus their declared start and loopback markers.

## Source identity

- Task/thread: `T-051` / `01a05a9f-d70b-7c70-b913-199fae63a0cd`.
- Authoritative assignment ancestor:
  `c7ea086e32351dc64702ce5539f375a6929c8c96`.
- Worktree: `C:\Users\jensh\.codex\worktrees\ee21\PB-W00-control`.
- AAEmu 1.2 host base:
  `62e3eb1d87da01194802ac886cd500134facad28`.
- Active AAEmu 1.2 patch SHA-256:
  `b3ee8cfbe2aad1c7d4cf207f37c3ae4eb422bd21266e6d66320c5ca57ef01d91`.
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
- `ops/tasks/T-051/HANDOFF.md`

## Proof

- Deterministic no-runtime guard: PASS, 32 assertions. The harness reproduces
  T-050's advanced-function array-to-string binding failure, accepts a 20 MiB
  scalar fixture, rejects accidental arrays, preserves all 13 T-048 Login
  cases, and covers exact and negative Game schema/start/loopback cases.
- PowerShell parser: PASS for all three changed scripts/modules.
- Manifest JSON and all declared hashes: PASS. Both AAEmu 1.2 hash fields
  reproduce; the AAEmu 3.0, optional security, and migration hashes remain
  unchanged and reproduce.
- Credential-field scan: PASS. `GameService` passes only resolved
  `AppConfiguration.Instance.Connections.MySQLProvider.Database` to a logger
  message containing the schema name; no host, port, user, password,
  connection-string, or data-source field is present.
- Registered clean `aaemu12_reference`: exact pinned head confirmed; the whole
  patch passes `git apply --check --whitespace=error-all`; the checkout remains
  clean.
- Isolated scratch AAEmu 1.2 proof: complete solution build PASS with 0 errors;
  after the test-target disposal correction, the final focused UnitTests build
  passed with 0 errors and 70 retained warnings. `GameServiceTests`: 9 passed,
  0 failed, 0 skipped, including exact schema emission.
- T-050 committed receipt/handoff and retained external Game log are unchanged.
  `git diff --check`: PASS.

The versioned scratch checkout was moved intact, not deleted, to quarantine
event `20260901T015553920Z-4edcb737`; its manifest is
`C:\Users\jensh\.codex\quarantine\20260901T015553920Z-4edcb737\manifest.json`.

## Retained failures, runtime state, and unproven boundaries

T-050 remains `INCOMPLETE`; it has no exact selected Game-schema proof and is
not reinterpreted. Its retained `BaseBaiLoader` and missing-world errors are
outside T-051 and remain assigned to T-052.

No registered or deployed host, runtime, client, database, global ledger,
runtime lease, T-050 evidence, or AAEmu 3.0 file was edited. No Login, Game,
client, or MySQL process was started or controlled. Actual live schema emission
and physical combat/stealth qualification remain pending.

## Exact integration action

1. Integrate the final T-051 source commit into
   `integration/aaemu12-world`; do not mark T-050 passed.
2. In a fresh versioned scratch checkout of the pinned AAEmu 1.2 base, apply
   the complete patch and verify the final blobs:
   `AAEmu.Game/GameService.cs` =
   `403332171f6b92a88a10082885b54dd302fd6026` and
   `AAEmu.UnitTests/Services/GameServiceTests.cs` =
   `843d861208dfad4f5be58e1d38b96ed0311ca869`.
3. After verifying the registered integration host still matches the accepted
   T-049/T-050 installed state, copy only those two reconstructed final files
   into it and update the installed module through the normal Integrator
   workflow. Do not reapply the already-installed combined patch hunks.
4. Run installer idempotence, build, the focused 9-test `GameServiceTests`
   class, the deterministic 32-assertion guard, and the integration-wave full
   AAEmu 1.2 suite. Record the T-053 receipt without starting a runtime.
5. Only after PB-000 accepts T-053 and T-052's classification may a fresh T-054
   claim the runtime lease and write immutable `public-alpha-v3` evidence.
