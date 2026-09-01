# T-075 contract

## Outcome

One persistent AAEmu 1.2 bot completes at least three autonomous one-kill
gameplay iterations through self-selected activity, navigation, authoritative
combat/kill credit, native progression, directly observed natural recovery to a
debt-free completion boundary, normal self-logout, and restart persistence.
This is a new immutable v5 proof; all predecessor evidence remains untouched.

## Runtime authority and immutable inputs

- Do not start or control Login, GameServer, MySQL, or the ArcheAge client
  until committed `ops/RUNTIME-LEASE.yaml` assigns `aaemu12` to T-075 and this
  exact thread. The client is prohibited and MySQL is observation-only.
- Use only `aaemu12_integration`, database identity
  `aaemu12_database_public_alpha_v1`, and `aaemu12_t075_evidence_v5`. The fresh
  evidence root must be absent before first run and immutable once created.
- Immutable installed source/tree:
  `68aaaa3334a408d1d6d21e44472a8984e78618c2` /
  `9fa74d8057df8b5eb276ad223a10ed9b12791f88`; host base
  `62e3eb1d87da01194802ac886cd500134facad28`.
- Reuse T-072 only as build/install fingerprint and T-073 only as retained
  incomplete evidence. Do not edit/deploy source, query/mutate databases,
  control a client, reinterpret predecessor evidence, or alter global ledgers.

## Evidence-protocol correction

- Arm the read-only bot-status/metric observer before `addbot` and before every
  fixture-staging boundary. Keep it running through activity, kill, recovery,
  completion, and logout so direct brain-step and mover-step samples bracket
  the entire `life_recovery state=pending` window.
- Poll at a bounded cadence that cannot create gameplay commands. Preserve the
  raw response shape, including the expected offline-bot response after normal
  self-logout; do not classify that response as a transport failure.
- Normalize roster responses into a JSON array only in derived evidence before
  deciding a zero-population boundary. Preserve the original bytes separately.
  Neither normalization nor observation may direct bot behavior.

## Pass

- Prove exact lease, fingerprints, clean workspaces, zero conflicting
  processes/clients/listeners, empty active logs, absent evidence root, and
  initial zero bot/runtime counts. Start only via the retained guarded runner;
  observe but never start, stop, query, or control MySQL.
- Use persistent bot ID `20001` for exactly three counted iterations. At each
  zero-population boundary, managed `addbot 20001` is permitted. Before fixture
  staging, let native recovery reach targetless/noncombat/stationary full HP/MP.
- Stage exactly one inert mortal opportunity near the bot only after that safe
  boundary. Never issue `botstate` or any activity, destination, target, attack,
  loot/progression, recovery, or logout command.
- The bot must independently emit `activity=grind reason=nearby_mortal`, select
  the exact staged target, navigate/correct range/facing, cast/damage/kill with
  exact server credit, and produce positive native experience or acquired-item
  progression.
- After the kill, require targetless/noncombat/movement-clear state and exact
  ordered lifecycle evidence: `life_recovery state=pending` while HP/MP debt
  exists; bot/runtime remain registered; directly sampled brain/mover counters
  do not advance for the full pending window; native resources rise without
  operator control; then `life_recovery state=completed` at full HP/MP. Only
  afterward may the single completion/progression record and logout appear.
- Retain baseline/completion UTCs and exact level/XP, HP/maxHP, MP/maxMP, bag
  slots/units, summary/fingerprint, signed deltas, inventory_changed, recovery
  start/completion/order, direct counter samples, navigation, target/casts/kill
  credit, transition UTCs, zero unintended bots, log offsets, metrics, original
  and normalized observation payloads, and evidence hashes.
- Count an iteration only after one progression record and normal self-logout
  return zero bots/runtimes. Stage the next iteration only at that boundary.
  Stop at the first missing/invalid activity, travel, target health, kill
  credit, positive progression, recovery ordering/completion, resource
  availability, brain/mover suspension, debt-free completion, duplicate record,
  operator-directed gameplay, or failed logout.
- After iteration three, gracefully stop Game then Login; restart with distinct
  PIDs and verify preserved identity/roster/relevant progress read-only. If an
  active re-add is required, stage no opportunity, do not count it, remove it
  only at final cleanup, then gracefully stop Game/Login again.
- Commit only `ops/evidence/aaemu12-t075-one-bot-autonomy-v5.yaml` and the
  concise handoff. Raw evidence remains in the fresh external root.

## Verdict and cleanup

PASS requires three complete iterations plus clean restart persistence and
final cleanup. A complete behavioral violation is `FAIL`; absent or ambiguous
required material is `INCOMPLETE`. On every verdict preserve evidence,
gracefully stop Game then Login, prove zero bots/runtimes/processes/clients/
listeners and free ports, and record unchanged observed MySQL PIDs. Never force
stop, repeat the evidence attempt, or activate a larger cohort after failure.

## Non-goals

Source/deployment/database/client work; operator recovery; fourth iteration;
multi-bot scale; Activity Director; groups/dungeons/PvP/economy/governance;
soak; release packaging; AAEmu 3.0; or client acceptance.
