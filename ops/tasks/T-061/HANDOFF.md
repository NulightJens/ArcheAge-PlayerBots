# T-061 handoff

T-061 is **FAIL** at the first functional boundary. The exact leased AAEmu 1.2
runtime started cleanly, but one free persistent bot never selected or activated
an activity, so the required three autonomous gameplay iterations did not begin.

## Source identity and changed files

- Lease/thread: `739a48f44e0ce1b2344082b582008daf74b607b8` /
  `01a05bf9-cfbf-7942-853c-310035ae673a`.
- Host/reference: `62e3eb1d87da01194802ac886cd500134facad28`.
- Installed module: `cf57b11474b9e7f3e9ece588dc3aea0a56c02ef9`,
  clean tree `1e15fa38ef2f91c62f9a5a72709703d3acd1505a`.
- Changed files: `ops/evidence/aaemu12-t061-one-bot-autonomy-v1.yaml`
  and `ops/tasks/T-061/HANDOFF.md` only.

## Proof and first blocker

Preflight proved the exact lease, pinned and clean reference/module identities,
matching binaries/configs and isolated schemas, zero Login/Game/client
processes, five free loopback ports, empty live logs, and untouched observed
MySQL PIDs 6308 and 8076. The guarded runner started Login PID 117784 and Game
PID 122728 with all five exact listeners and initial bot/runtime counts of zero.

The attempt managed-login bot 20001 (`ScaleBot000`), staged passive mortal NPC
44960 at 12 m, and issued only the contract-permitted `botstate 20001 free`
transition. Across seven observations over 35 seconds, the bot stayed `Idle`,
`Forced: None (auto)`, combat `IsActive: False`, targetless, and without a
movement destination. Metrics retained 17 brain steps, 16 idle brain steps,
zero combat steps, path requests, casts, kills, recoveries, and tick errors.
No command chose an activity, destination, target, attack, loot, recovery, or
acceptance logout.

The smallest blocker is missing live single-bot lifecycle/activity wiring. A
fresh `BotCombatState` starts inactive; Idle only enters an activity when it is
already active; `free` only clears the forced override; and the deterministic
life FSM has no production caller. A bounded source follow-up must make a free
Idle bot select and activate its own activity with a decision reason, then
request its own normal persisted logout. T-061 did not authorize that source or
deployment change.

## Runtime state and retained boundaries

`/removebot 20001` was used only for cleanup and is not acceptance evidence.
Final bot/runtime counts were zero. Game then Login stopped through graceful
Ctrl+C, the shutdown marker reports zero remaining bots/runtimes, and final
checks found zero Login/Game/client processes, zero required listeners, and
empty active log directories. MySQL PIDs 6308 and 8076 were unchanged and were
never controlled or queried directly. One disposed-PhysicsManager entry is
shutdown-only retained host noise; forced termination was not used.

No successful iteration, travel, mortal combat, kill credit, progression,
recovery, autonomous logout, restart persistence, or PASS is claimed. Raw
evidence is sealed at
`D:\Codex-Labs\evidence\T-061\one-bot-autonomy-v1`; its 34-payload manifest
SHA-256 is
`8df04259caca8621b3a9e6ce54cab7cee164e47e00b394163fc65cb4d0255305`.

## Exact integration and control actions

1. Integrator: integrate this commit as a truthful T-061 `FAIL` receipt only;
   do not mark the single-bot gate passed.
2. PB-000: after accepting the cleanup proof, commit `ops/RUNTIME-LEASE.yaml`
   with `aaemu12` status `released-after-T-061-fail-cleanup`, `owner_task: null`,
   `owner_thread_id: null`, `released_at: 2026-09-01`, and cleanup receipt
   `ops/evidence/aaemu12-t061-one-bot-autonomy-v1.yaml`.
3. PB-000: dispatch the bounded single-bot lifecycle/activity activation and
   autonomous logout wiring task; keep T-041 and T-037 blocked.
