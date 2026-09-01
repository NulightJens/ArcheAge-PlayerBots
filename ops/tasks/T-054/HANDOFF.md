# T-054 handoff

T-054 is **INCOMPLETE**. The fresh v3 attempt proved the corrected exact-schema
startup and initial zero-bot state, but the installed AAEmu 1.2 source cannot
create the mandatory passive-NPC fixture through the loopback API as `@system`.
No combat or stealth acceptance is claimed.

## Source identity

- Lease commit/thread: `3d923e661dbf9ead1cdb0ccd6565cf2173d95336` /
  `01a05abe-7ea9-70c1-94c4-236f31392392`.
- Integration input: `a177607645cded9b318a6298ec5c44b0df2cc429`.
- Host/reference: `62e3eb1d87da01194802ac886cd500134facad28`.
- Installed module: `c0ae8898806ff3cc3e4a20107247a2b11ab9dcfe`,
  clean tree `3db93b130aa80ff658a673a12ae36205d368cf8b`.
- Compatibility patch SHA-256:
  `b3ee8cfbe2aad1c7d4cf207f37c3ae4eb422bd21266e6d66320c5ca57ef01d91`.

## Changed files

- `ops/evidence/aaemu12-t054-combat-stealth-v3.yaml`
- `ops/tasks/T-054/HANDOFF.md`

## Proof and retained failure

Preflight proved the final T-054 lease identity, clean reference and installed
module, exact host/module/patch and executable/config identities, immutable v1
and v2 predecessors, exact isolated schemas on loopback, retained IDs
20001-20100, no AAEmu process, free ports 1234/1237/1239/1250/1280, empty
runtime log paths, and an absent v3 evidence directory before creation.

Attempt 4 started only through `Start-ScaleGateRuntime.ps1
-SafetyAcknowledged`. Its immutable receipt records Login PID 114720 and Game
PID 117764, both exact selected schemas, every required loopback listener and
startup marker, executable/config/module hashes, and initial metrics with zero
bots and zero runtimes.

The loopback API then spawned retained bot 20001 as `ScaleBot000`. The required
`@system` fixture call returned exactly: `The command character is not in a
world instance.` The runner despawned bot 20001 and created no passive NPC,
buff, qualification input, plan, evidence, target, cohort, or stealth phase.
The analyzer was not invoked because no truthful plan/evidence pair existed;
no analyzer verdict or exit code is claimed. Missing mandatory material makes
the task `INCOMPLETE` under the contract.

Read-only source inspection pins the blocker. `SystemActor.Create` applies a
spawn position but never assigns `ParentWorld`; `SpawnPassiveNpcCommand.Execute`
rejects a character with null `ParentWorld`; and the loopback controller creates
a fresh `SystemActor` per `@system` request. The Control Tower explicitly barred
a bot-name/client workaround and another runtime attempt. The retained hashes
for those source files and all raw proof are in the sanitized receipt and
`D:\Codex-Labs\evidence\T-036\public-alpha-v3`.

The Game error log retains the causally pre-shutdown missing-world-instance
entry and one shutdown-time disposed-physics entry. It contains no
BaseBaiLoader collection-modification error. Earlier runner probes 1-3 are
retained independently; none began fixture work and all used graceful cleanup.

## Final runtime state and boundaries

Game was stopped before Login through the graceful Ctrl+C helper. The Game log
records `BOT ev=shutdown_cleanup remaining_bots=0 remaining_runtimes=0`.
Game and Login are absent, all five required ports are free, and both runtime
log directories are empty after intact log preservation. No force termination,
direct database access, client or AAEmu 3.0 control, source/global-ledger edit,
destructive cleanup, retained-config/Data edit, schema reset, or predecessor
overwrite occurred. The isolated schemas remain retained.

## Exact integration and control actions

1. Integrator: integrate this task commit as an `INCOMPLETE` runtime receipt
   only. Do not mark T-054 passed and do not activate T-037.
2. PB-000: after consuming the committed zero-bot, graceful-stop, zero-process,
   and free-listener proof, release the `aaemu12` lease. Until then, retain it.
3. PB-000: dispatch a bounded source correction that gives the loopback
   `@system` actor a valid AAEmu 1.2 `ParentWorld`, then dispatch a new immutable
   physical attempt in a fresh versioned evidence directory. Never reuse or
   overwrite `public-alpha-v3`.
