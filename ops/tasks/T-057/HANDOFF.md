# T-057 handoff

T-057 is **INCOMPLETE**. Both live-headless attempts proved exact AAEmu 1.2
startup and clean graceful shutdown, and the corrected `@system` actor can now
create a passive NPC. The physical fixture origin remains ZoneId `0`, however,
and none of five ordered candidate templates produced a cast or mortal kill.
No combat cohort, stealth phase, restart, or analyzer verdict is claimed.

## Source identity

- Lease commit/thread: `75549b6e2bc38e1da5694dfcd884590a2a8bbb68` /
  `01a05b06-ee62-77e0-81fe-684c85116af2`.
- Integration input: `d26211711cb8597d00deab1be4ca7a731f428c71`.
- Host/reference: `62e3eb1d87da01194802ac886cd500134facad28`.
- Installed module: `1d08632d303b35e7c2d5655de5b5e2b704896cc8`,
  clean tree `1f3befa4dc3c01222b057c28279e82d25fcc176e`.
- Compatibility patch SHA-256:
  `0285c8c21133ec00abbd8f5de925c56dc2d87f7d8595fee89a6063b3ff02ca3e`.

## Changed files

- `ops/evidence/aaemu12-t057-combat-stealth-v4.yaml`
- `ops/tasks/T-057/HANDOFF.md`

## Proof and retained failure

The immutable preflight proved the exact T-057 lease, clean reference and
installed module, expected host/module/patch/build/config/Data identities,
exact public-alpha schemas on loopback, retained IDs 20001-20100, immutable
v1-v3 predecessors, zero Login/Game/client processes, free ports
1234/1237/1239/1250/1280, empty runtime log paths, and an absent v4 directory
before creation.

Attempt 1 started Login PID 114896 and Game PID 101860 through
`Start-ScaleGateRuntime.ps1 -SafetyAcknowledged`. Exact schemas, all required
startup markers/listeners, and initial zero population were retained. The
corrected `@system` path created template 10004 as object 44242, but the v3
wrapper expected a numeric grade while AAEmu returned `grade=Weak`. That
operator probe stopped before attack or plan work and was retained unchanged.

Attempt 2 used a new runner that accepts the typed grade label. Login PID 69312
and Game PID 116244 again passed exact-schema startup and initial zero metrics.
The runner tested templates 10004, 10000, 10001, 10002, and 11180 in order.
Each received a full 120-second death poll, an authoritative Idle-to-Combat
transition, an accepted direct-traversal decision, and a Combat-to-Idle release.
Across all five byte-exact log segments there were zero cast events, kill
credits, and tick errors. No mortal template was verified.

Each attempt-2 fixture request emitted `GetWorldByZone(): No world template
defined for ZoneId 0` before shutdown. Read-only source pins the boundary:
`SystemActor.Create` assigns `MainWorld` and `MainWorld.Template.SpawnPosition`,
and `SpawnPassiveNpcCommand` queries terrain using that position's ZoneId. The
physical synthetic-actor origin is therefore not a qualified non-zero zone.
`BasicCombat` moves while outside attack range and auto-attacks only in range;
the accepted destinations with no cast are consistent with never reaching a
usable melee position. The runner did not externalize the successful
attempt-2 spawn/status response objects before failing, so exact distinct
target object IDs and a stronger movement diagnosis are also missing material.

Because no mortal target and no exact distinct target identities existed, no
truthful qualification input, plan, or evidence pair was created.
`Test-CombatQualificationEvidence.ps1` was not invoked and no exit code was
fabricated. The six Idle/combat cohorts, stealth buff/reacquisition/release,
and distinct-PID restart remain unexecuted.

Lifecycle classification retained one attempt-1 ZoneId-0 entry before stop,
one attempt-1 missing-world entry in the fixture/failure/shutdown second, and
five attempt-2 ZoneId-0 entries before stop. These are material after T-056.
Neither attempt contains a BaseBaiLoader collection-modification error. One
disposed-physics entry per attempt is shutdown-scoped native noise and did not
affect cleanup.

## Final runtime state and boundaries

Both attempts removed retained bots, stopped Game before Login through the
graceful Ctrl+C helper, preserved the logs externally, and retained
`BOT ev=shutdown_cleanup remaining_bots=0 remaining_runtimes=0`. Final state is
zero AAEmu processes, zero ArcheAge client processes, all five ports free, and
both live log directories empty. No force termination, direct database access,
client/AAEmu 3.0 control, source/global-ledger edit, destructive cleanup,
retained-input edit, schema reset, or predecessor overwrite occurred. The
isolated schemas remain retained. Raw evidence and its 35-file manifest are at
`D:\Codex-Labs\evidence\T-036\public-alpha-v4`.

## Exact integration and control actions

1. Integrator: integrate this task commit as an `INCOMPLETE` runtime receipt
   only. Do not mark T-057 passed.
2. PB-000: after accepting the committed zero-bot, graceful Game-then-Login
   stop, zero-process, free-listener, and empty-live-log proof, commit release
   of the `aaemu12` lease. Until then, retain T-057 ownership.
3. PB-000: keep T-037 blocked; do not activate it from this receipt. Dispatch a
   bounded AAEmu 1.2 source correction that gives `@system` a qualified
   non-zero zone/spawn origin, followed by a new immutable physical attempt
   whose runner externalizes each successful spawn/status response.
