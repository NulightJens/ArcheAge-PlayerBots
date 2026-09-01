# T-069 contract

## Outcome

One persistent AAEmu 1.2 bot completes at least three autonomous one-kill
gameplay iterations through self-selected activity, navigation, authoritative
mortal combat and kill credit, native progression, a safe natural recovery
boundary, normal self-logout, and restart persistence. This is a new immutable
v3 proof; retained T-061 v1 and T-065 v2 evidence is never reused or modified.

## Runtime authority and immutable inputs

- Do not start or control Login, GameServer, MySQL, or the ArcheAge client
  until committed `ops/RUNTIME-LEASE.yaml` in registered workspace
  `playerbots_control` assigns `aaemu12` to T-069 and this exact thread.
- Use only `aaemu12_integration`, database identity
  `aaemu12_database_public_alpha_v1`, and evidence workspace
  `aaemu12_t069_evidence_v3`. The evidence root must be absent before the first
  run and is immutable once created. The ArcheAge client is prohibited.
- The installed module is immutable for this attempt: source
  `4287ec53e998ecff2e5eb9670908166957a79662`, tree
  `1c6820f2f47ec79a3aab40174e1b448f894f67e4`, host base
  `62e3eb1d87da01194802ac886cd500134facad28`.
- Reuse T-068 only as the accepted build/install fingerprint. Retain T-065 v2
  as historical incomplete evidence. Do not edit/deploy source, query or
  mutate databases directly, control a client, reinterpret predecessor
  evidence, or alter global ledgers/leases.

## Pass

- Prove the exact committed lease assignment, source/build/config/database
  fingerprint, clean registered workspaces, zero conflicting processes/
  clients/listeners, empty active log paths, absent fresh evidence root, and
  initial zero bot/runtime counts.
- Start only through the retained guarded AAEmu 1.2 runner. Observe existing
  MySQL process identity read-only; never start, stop, query, or control MySQL.
- Use persistent bot ID `20001` for exactly three counted iterations. At each
  declared zero-population boundary, managed `addbot 20001` is permitted.
  Before staging a fixture, leave the bot targetless and noncombat and allow
  native recovery to resolve any pre-existing HP/MP debt. Read-only observation
  is permitted; no operator recovery or gameplay command is permitted.
- Stage one inert mortal opportunity near that bot only after the safe resource
  boundary is proven. Do not issue `botstate` or any command that selects
  activity, destination, target, attack, loot, progression, recovery, or logout.
- After opportunity staging, the bot must independently emit
  `activity=grind reason=nearby_mortal`, enter the existing navigation/combat
  path, select the exact staged target, correct range/facing, cast, damage and
  kill it with authoritative server kill credit, and produce a positive native
  experience delta or newly acquired bag-item delta.
- Retain each iteration's immutable activation baseline and pre-logout
  completion record: exact UTCs, level/experience, HP/maxHP, MP/maxMP,
  occupied bag slots, total units, stable summary/fingerprint, signed deltas,
  and `inventory_changed`. Retain identity/roster, controller transitions,
  navigation, target/casts/health/kill credit, safe targetless/noncombat
  boundary, normal self-logout, zero unintended bots, exact log offsets, and
  evidence hashes.
- A safe recovery boundary requires no unresolved movement or HP/MP debt at
  activity activation and no newly created resource debt at completion. If the
  environment cannot reach that boundary naturally, stop; never manufacture
  it with an operator command.
- Count an iteration only after its single structured progression record and
  self-logout return the host to zero bots and zero runtimes. Stage the next
  bot/fixture only at that boundary. Stop at the first missing activity,
  invalid travel, unchanged target health, absent exact kill credit, absent
  positive progression, unavailable/ambiguous snapshot, unsafe recovery,
  operator-directed gameplay, duplicate progression record, or failed logout.
- After iteration three, gracefully stop Game then Login and prove zero
  processes/clients/listeners. Restart with distinct PIDs and verify preserved
  identity, roster, and relevant progress using read-only roster observation.
  If active re-add is required, stage no opportunity, do not count it as
  gameplay, remove it only at final cleanup, then gracefully stop Game/Login.
- Commit only `ops/evidence/aaemu12-t069-one-bot-autonomy-v3.yaml` and the
  concise handoff. Raw evidence remains in the new registered external root.

## Verdict and cleanup

PASS requires three complete iterations plus clean restart persistence and
final cleanup. A complete observation that violates behavior is `FAIL`; absent
or ambiguous required material is `INCOMPLETE`. On every verdict, preserve raw
evidence, remove no retained material, gracefully stop Game then Login, prove
zero bots/runtimes/processes/clients/listeners and free required ports, and
record unchanged observed MySQL PIDs. Do not repeat the attempt or activate a
larger cohort after a failure.

## Non-goals

Source implementation/deployment; direct database work; a fourth gameplay
iteration; multi-bot scale; Activity Director; groups, dungeons, PvP, economy,
governance, soak, release packaging, AAEmu 3.0, or client acceptance.
