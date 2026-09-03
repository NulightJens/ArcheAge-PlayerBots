# T-065 contract

## Outcome

One persistent AAEmu 1.2 bot completes at least three autonomous one-kill
gameplay iterations through self-selected activity, navigation, authoritative
mortal combat and kill credit, native progression, a safe recovery boundary,
normal self-logout, and restart persistence. This is a new immutable v2 proof;
the retained T-061 v1 `FAIL` receipt is never reused or modified.

## Runtime authority and immutable inputs

- Do not start or control Login, GameServer, MySQL, or the ArcheAge client
  until committed `ops/RUNTIME-LEASE.yaml` in registered workspace
  `playerbots_control` assigns `aaemu12` to T-065 and this exact thread.
- Use only `aaemu12_integration`, database identity
  `aaemu12_database_public_alpha_v1`, and evidence workspace
  `aaemu12_t065_evidence_v2`. The ArcheAge client is prohibited.
- The installed module is immutable for this attempt: source
  `401dd353ef37aa858763b41dc2e5c1663446ff57`, tree
  `6803d1d7e1d287798390e5aed0b835b9c8f12aef`, host base
  `62e3eb1d87da01194802ac886cd500134facad28`.
- Reuse T-064 only as the accepted build/install fingerprint. Retain T-061 v1
  as historical failure evidence. Do not edit/deploy source, query or mutate
  databases directly, control a client, reinterpret predecessor evidence, or
  alter global ledgers/leases.

## Pass

- Prove the exact lease assignment, source/build/config/database fingerprint,
  clean registered workspaces, zero conflicting processes/clients/listeners,
  empty active log paths, and initial zero bot/runtime counts.
- Start only through the retained guarded AAEmu 1.2 runner. Observe existing
  MySQL process identity read-only; never start, stop, or control MySQL.
- Use persistent bot ID `20001` for exactly three counted iterations. At each
  declared iteration boundary, managed `addbot 20001` and staging one inert
  mortal opportunity near that bot are permitted. Do not issue `botstate` or
  any command that selects activity, destination, target, attack, loot,
  progression, recovery, or logout.
- After opportunity staging, the bot must independently emit the deterministic
  `activity=grind reason=nearby_mortal` decision, enter the existing navigation
  and combat path, select the exact staged mortal target, correct range/facing,
  cast, damage and kill it with authoritative server kill credit, and produce
  native loot or another authoritative progression delta.
- Retain per iteration the persistent identity/roster, controller state and
  transition timestamps, origin/destination and fail-closed navigation,
  target/casts/health/kill credit, progression before/after, resource samples,
  safe targetless noncombat recovery boundary, normal self-logout, zero
  unintended bots, exact log offsets, and evidence hashes.
- A safe recovery boundary requires Idle/targetless/noncombat cleanup with no
  unresolved movement or resource debt before self-logout. If combat creates
  an HP/resource deficit, prove restoration or a truthful persisted/re-add
  recovery delta; if no deficit occurs, record that no recovery debt existed.
  Never manufacture recovery with an operator command.
- Count an iteration only after self-logout returns the host to zero bots and
  zero runtimes. Stage the next fixture only at that zero-population boundary.
  Stop at the first missing activity, invalid travel, unchanged target health,
  absent exact kill credit, absent progression, unsafe recovery boundary,
  operator-directed gameplay, or failed self-logout.
- After iteration three, gracefully stop Game then Login and prove zero
  processes/clients/listeners. Restart with distinct PIDs and verify preserved
  identity, roster, and relevant progress using read-only roster observation.
  If active re-add is required for observation, stage no opportunity, do not
  count it as gameplay, remove it only at the declared final cleanup boundary,
  then gracefully stop Game and Login again.
- Commit only `ops/evidence/aaemu12-t065-one-bot-autonomy-v2.yaml` and the
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
