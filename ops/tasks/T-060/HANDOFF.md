# T-060 handoff

T-060 is **INCOMPLETE**. The qualified active-bot anchor enabled a fresh live
AAEmu 1.2 headless gate, and matched Idle/combat cohorts 1, 5, and 10 passed.
The terminal attempt stopped fail-closed at cohort 25 before stealth, restart,
or analyzer acceptance.

## Source identity and changed files

- Lease/thread: `fadd161adf7ec7a5be97491a06eff4672542f98d` /
  `01a05b47-b0b7-71b3-90df-be5f07c6e81f`.
- Host/reference: `62e3eb1d87da01194802ac886cd500134facad28`.
- Installed module: `cf57b11474b9e7f3e9ece588dc3aea0a56c02ef9`,
  clean tree `1e15fa38ef2f91c62f9a5a72709703d3acd1505a`.
- Compatibility patch SHA-256:
  `0285c8c21133ec00abbd8f5de925c56dc2d87f7d8595fee89a6063b3ff02ca3e`.
- Changed files: `ops/evidence/aaemu12-t060-combat-stealth-v5.yaml` and
  `ops/tasks/T-060/HANDOFF.md` only.

## Proof and retained blocker

The terminal preflight proved the exact lease/build/config/database identities,
clean reference and module, zero Login/Game/client processes, free required
ports, empty live logs, and untouched observed MySQL PIDs 6308/8076. Startup
through `Start-ScaleGateRuntime.ps1 -SafetyAcknowledged` retained exact schemas,
all five loopback listeners, Login PID 84136, Game PID 97748, and initial zero
population. The qualified nonzero-zone anchor, typed `grade=Weak`, eight
distinct targets, and live `stealth=True` buff 896 response were retained.

Idle controls 1, 5, 10, and 25 completed. Mortal combat cohorts 1, 5, and 10
completed with exact target deaths. At cohort 25, bots 20001-20003 accepted the
shared 148-HP target 44425, and bot 20002 killed it. The server retains exact
kill credit at line 219200 and Dead transition at line 219202; metrics retain
one cast attempt, one cast success, and one credited kill. Because the target
had already despawned, generated attack commands for bots 20004-20025 reported
the living object absent, and the status observer could not prove the corpse as
dead. Cohort 25 is therefore incomplete rather than passed.

The smallest verified blocker is the combination of a low-HP shared fixture
that cannot survive the serialized 25/50/100 fan-out and no exact death
tombstone addressable after despawn. A follow-up must either qualify a durable
mortal target or retain exact post-despawn death observability. T-060 did not
authorize source or deployment changes, and Control Tower ended live iteration
at this boundary.

No complete evidence document existed for cohorts 25/50/100, stealth, and
restart, so `Test-CombatQualificationEvidence.ps1` was not invoked and no exit
code or PASS was fabricated. Attempts and corrections remain immutable under
`D:\Codex-Labs\evidence\T-036\public-alpha-v5`; the 3,995-entry final manifest
SHA-256 is
`464582cc9168566d4ee27dcc7470c3c23e8122ff28cfa807ab8c0339cd36bc5d`.

## Final runtime state and retained failures

The runner removed the full reserved bot range, proved final bot/runtime counts
of zero, stopped Game before Login through graceful Ctrl+C, preserved logs, and
left zero Login/Game/client processes, zero required listeners, and empty live
log directories. Continuous client monitoring retained 740 samples and zero
violations. Existing MySQL PIDs 6308 and 8076 were not controlled. No force
termination, direct database access, destructive cleanup, client/AAEmu 3.0
control, source/deployment edit, global-ledger edit, lease edit, or predecessor
overwrite occurred.

There were zero pre-shutdown missing-world or material BaseBai occurrences. One
disposed-service PhysicsManager error is shutdown-only host noise after graceful
stop and is retained rather than suppressed.

## Exact integration and control actions

1. Integrator: integrate this commit as an `INCOMPLETE` T-060 runtime receipt
   only. Do not mark the physical gate passed.
2. PB-000: after accepting the committed zero-bot, graceful Game-then-Login
   stop, zero-process, free-listener, empty-live-log, and zero-client proof,
   commit release of the `aaemu12` lease from T-060. Until that commit, leave
   `ops/RUNTIME-LEASE.yaml` unchanged.
3. PB-000: keep T-037 blocked. Dispatch a new bounded fixture/harness task for
   durable fan-out target qualification or exact post-despawn death
   observability; do not widen or reinterpret T-060.
