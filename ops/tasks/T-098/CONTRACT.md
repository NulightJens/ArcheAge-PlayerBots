# T-098 contract: corrected one-zone autonomy proof v5

Use only the exact committed binding/lease and registered AAEmu 1.2 workspaces.
Require host `62e3eb1d87da01194802ac886cd500134facad28`, installed module
source/tree `761ffa1e0bd76d06532688f34b45e192a493b239` /
`c36b1255cccb3a782e44e87a56bc9e867a946048`, patch SHA-256
`baa0979a200eb7f4403985d7166092f937033c31650aa11336fba31543d96606`,
Game assembly SHA-256
`c1b3bd922c900c31c76a5748359b8363ea7c56ba34d27daed9c90fd1ddbd51ef`,
and integrated client observer blob
`0f8b5e2b3a811877648020b715e4737777d4947a`.

The fresh root is `D:\Codex-Labs\evidence\T-098\one-zone-autonomy-v5`.
Require it absent, create it once, and never overwrite/rename/replace it.
Predecessor roots are immutable. Preserve original deployed config bytes and
SHA-256. Change only the nine Director assignments: enabled; zone `221`; IDs
`[20001,20002,20003]`; population `2/3/3`; initial delay `180000` ms;
reconciliation `15000` ms; retry `30000` ms. Restore exact bytes after final
shutdown.

Launch the integrated Windows PowerShell client observer before Login with 500
ms samples. Independently validate durable raw/ledger row zero at zero clients;
keep the same PID alive through both runs and final cleanup; never relaunch.
Start loopback Login then Game through the accepted wrapper. Immediately after
API readiness, launch one unchanged autonomy observer per bot. Require first
samples within 120 seconds of runtime-start evidence and two raw-verified
offline samples plus armed/live boundaries for every bot before admission.

Require authoritative Director start and ordered admissions
`[20001,20002,20003]`, one per reconciliation tick, zone/default
world/default instance, maximum 3, and zero failures/wrong-zone/overlap. Derive
first-admission elapsed time from authoritative log timestamps. The deployed
180000 ms delay is honored only if first admission is no earlier than 180000 ms
and no later than 195000 ms after Director start; retain exact elapsed time.
Stop without retry if outside that scheduler-tolerant window.

At each fresh all-three targetless, noncombat, stationary, full-resource
boundary, issue exactly once and in order:

1. `spawnpassive 10004 5 20001`
2. `spawnpassive 10004 5 20002`
3. `spawnpassive 10004 55 20003`

Preserve request/response bytes. Require expected anchor, zone `221`, common
default instance, finite coordinates, and all three pairwise horizontal
distances at least 15 meters. In each of two waves, every identity must
independently select `grind/nearby_mortal`, navigate, cast/damage, receive one
native credit for one distinct batch object, gain positive authoritative
progression, complete ordered recovery with fixed brain/mover counters, record
one debt-free completion, and normally self-logout. Require exact refill to
target after each wave. Total fixture commands six, credited objects six, and
credits exactly two per identity. No directed gameplay commands are permitted.

After wave two/refill, stop Game then Login gracefully and prove zero runtime
state. Move run-one logs losslessly. Restart with distinct Login/Game PIDs under
the same config. Pre-arm three fresh observers before admission, prove a second
ordered zero-to-three bootstrap and at least two continuous minutes at target
without fixtures/gameplay commands.

Finally stop Game then Login gracefully; never force-stop. Prove zero bots,
runtimes, relevant processes, required listeners and live logs; unchanged
observed MySQL PIDs without database access; exact host/source/config; and
once-only cleanup. Stop the client observer by fresh sentinel, validate every
raw hash and row-chain link, maximum gap at most 2000 ms, zero client/error
samples, matching terminal hash, and first-before-Login/last-after-cleanup.

Seal a complete relative-path/length/SHA-256 manifest with zero missing,
mismatched, duplicate, unsafe, unlisted, or reparse payloads. Commit only the
T-098 receipt and handoff. Report PASS/FAIL/INCOMPLETE with exact timing,
fixtures/separations, per-wave lifecycle/refill, restart/dwell, observer,
manifest, cleanup, anomalies, and unproven boundaries. PB-000 alone releases
the lease and dispatches independent evidence integration.
