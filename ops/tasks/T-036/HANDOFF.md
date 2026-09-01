# T-036 handoff

T-036 is **INCOMPLETE**. The claimed AAEmu 1.2 runtime failed closed in the
integrated startup guard before Game startup, so no combat or stealth acceptance
is claimed.

## Source identity

- Task branch: `codex/T-036-combat-stealth`
- Synchronized lease ancestor: `fa3804120cbe988319f25856b4485573e8098121`
- Host/reference: `62e3eb1d87da01194802ac886cd500134facad28`
- Installed module: `9938495eb7f1904ac4575bc0db42fc28a7f35851`
- Database identity: `aaemu12_database_public_alpha_v1`
- Runtime owner: T-036 / `01a05a64-9193-7c02-8f34-10219e4043a4`

## Changed files

- `ops/evidence/aaemu12-t036-combat-stealth-v1.yaml`
- `ops/tasks/T-036/HANDOFF.md`

## Proof and retained failure

Preflight proved clean reference/module/donor identities, exact executable and
config hashes, no Game/Login process, and free loopback ports 1234, 1237, 1239,
1250, and 1280. The target Data root predated dispatch; after lease
synchronization, only `compact.sqlite3` and the three-file `Chronicle` subtree
were absent. Those four literal donor paths were copied with matching hashes and
zero overwrites. The two generated configs name the exact isolated schemas and
bind every listener to `127.0.0.1`; credentials remain external.

`Start-ScaleGateRuntime.ps1 -SafetyAcknowledged` started Login as PID 118304,
but its required exact-schema log predicate never appeared. The pinned build
logs a connection without a database name and logs the hard-coded updater
prefix `aaemu_login`. The guard timed out before Game startup and gracefully
stopped Login through Ctrl+C. The preserved Login log proves listener shutdown
and has SHA-256
`2e758a6d5aba39f1114ae81e87349a6eaed6da601dc775e46c3efe3f2d3d7bbd`.

The physical analyzer was run fail-closed against retained blocked material and
returned `INCOMPLETE` with native exit code 2. No retained ID, bot, passive NPC,
target, buff, cohort, command stimulus, or restart was used. Raw evidence is at
`D:\Codex-Labs\evidence\T-036\public-alpha-v1`; the sanitized receipt contains
its hashes.

## Final runtime state

Game was never started. Login exited gracefully. No AAEmu Game/Login process,
required listener, or server log remains in the runtime paths. The configs,
copied Data files, isolated databases, and raw evidence are retained. No direct
database access, destructive cleanup, client control, AAEmu 3.0 control, source
correction, or global-ledger edit occurred.

## Exact integration and control actions

1. Integrator: review and fast-forward this task commit as an INCOMPLETE runtime
   receipt only; do not mark T-036 done and do not activate T-037.
2. PB-000: after the committed receipt is visible on
   `integration/aaemu12-world`, release `aaemu12` from T-036 using the recorded
   zero-process/free-listener cleanup evidence.
3. PB-000: keep T-037 blocked. Dispatch a bounded startup-gate correction or a
   versioned replacement host/tooling task that can emit and validate the exact
   selected isolated schema without exposing credentials; then re-dispatch
   T-036 against the retained runtime inputs and evidence as a new immutable
   attempt.
