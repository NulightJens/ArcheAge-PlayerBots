# T-065 handoff

T-065 is **INCOMPLETE** at the first missing evidence boundary. Counted
iteration one proved autonomous activity selection, exact-target combat, a
server-authoritative kill, and normal self-logout, but it did not expose a
before/after experience or acquired-loot delta. The fail-fast contract therefore
forbade staging iterations two and three or running restart persistence.

## Source identity and changed files

- Lease/thread: `04912db77beaac540a45d455fe852ebd6f4284de` /
  `01a05c3c-9169-7eb3-b0a6-3ecaf41ecf16`.
- Host/reference: `62e3eb1d87da01194802ac886cd500134facad28`.
- Installed module: `401dd353ef37aa858763b41dc2e5c1663446ff57`,
  clean tree `6803d1d7e1d287798390e5aed0b835b9c8f12aef`.
- Changed files: `ops/evidence/aaemu12-t065-one-bot-autonomy-v2.yaml`
  and `ops/tasks/T-065/HANDOFF.md` only.

## Proof and first blocker

Preflight proved the exact lease, pinned clean reference/module identities,
accepted binary/config hashes, isolated schemas, zero Login/Game/client
processes, five free loopback ports, empty active logs, and observed MySQL PIDs
6308 and 8076. The guarded runner started Login PID 111236 and Game PID 111268
with the exact five loopback listeners and initial bot/runtime counts of zero.

One excluded operator probe serialized an empty JSON array through PowerShell's
reserved `$args` variable and was rejected before command dispatch; metrics and
debug proved it created no bot/runtime or fixture. The corrected serializer
then began the sole counted iteration with only `addbot 20001` and
`spawnpassive 10004 12`.

Bot 20001 (`ScaleBot000`, ObjId 37950) independently emitted
`activity=grind reason=nearby_mortal`, selected exact staged NPC 44966 at 12 m,
cast once successfully, killed its 148 HP mortal instance, received exact
server kill credit, returned to Idle, and completed normal persisted logout.
Metrics ended at zero bots/runtimes with one spawn, one despawn, one observed
and credited kill, zero cast failures, and zero tick errors.

The first blocker is absent authoritative progression evidence. Level was 51
before and after logout, the only retained bag count was four before combat,
and the server's native kill/loot-container log does not state an XP or acquired
loot delta. Direct database access was prohibited. The bot also began at
7168/9516 HP and immediate logout left no post-combat live sample proving a
debt-free recovery boundary. No successful full iteration is therefore
claimed.

## Cleanup and exact integration action

Game then Login stopped through graceful Ctrl+C. The shutdown marker reports
zero remaining bots/runtimes; final checks found zero Login/Game/client
processes, zero required listeners, and empty active log paths. MySQL PIDs 6308
and 8076 were unchanged and never controlled or queried directly. Forced
termination was not used.

Raw evidence is sealed at
`D:\Codex-Labs\evidence\T-065\one-bot-autonomy-v2`; its 25-payload manifest
SHA-256 is
`0114c06ccef9a45ef0a09955d57e06e5761c97505eed517665bcb0b6e86caa91`.

Integrator: fast-forward/cherry-pick this commit as a truthful T-065
`INCOMPLETE` receipt only. PB-000 may then release the `aaemu12` lease using
this receipt as cleanup evidence and dispatch only the smallest read-only
progression/resource-observability follow-up. Keep T-041 and T-037 blocked.
