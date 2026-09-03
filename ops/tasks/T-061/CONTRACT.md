# T-061 contract

## Outcome

One persistent AAEmu 1.2 bot completes at least three autonomous gameplay
iterations from managed login through self-selected activity, fail-closed
travel, authoritative mortal combat and kill credit, loot or another native
progression result, recovery, normal logout, and clean restart. Commands may
stage fixtures and retain observations; they may not replace the bot's choices
or per-action gameplay.

## Runtime authority and immutable inputs

- Do not start or control Login, GameServer, MySQL, or the ArcheAge client
  until the committed `ops/RUNTIME-LEASE.yaml` in registered workspace
  `playerbots_control` assigns `aaemu12` to T-061 and this exact thread.
- Use only `aaemu12_integration`, database identity
  `aaemu12_database_public_alpha_v1`, and evidence workspace
  `aaemu12_t061_evidence_v1`. The ArcheAge client is prohibited.
- The installed module is immutable for this attempt: source
  `cf57b11474b9e7f3e9ece588dc3aea0a56c02ef9`, tree
  `1e15fa38ef2f91c62f9a5a72709703d3acd1505a`, host base
  `62e3eb1d87da01194802ac886cd500134facad28`.
- Reuse the accepted T-060 cleanup only as preflight evidence. T-060 remains
  `INCOMPLETE`; its cohort-25 scale-fixture blocker is not a one-bot PASS.
- Do not edit or deploy source, mutate or query databases directly, control a
  client, reinterpret predecessor evidence, or alter global ledgers/leases.

## Pass

- Prove the exact lease assignment, source/build/config/database fingerprint,
  clean registered workspaces, zero conflicting processes/clients/listeners,
  empty active log paths, and initial zero bot/runtime counts.
- Start only through the retained guarded AAEmu 1.2 runner. Observe existing
  MySQL read-only; never start, stop, or control it.
- Use exactly one retained persistent bot identity. Managed add/login and one
  explicit transition into autonomous/free operation are permitted staging.
  After that transition, no command may select the activity, destination,
  target, attack, loot/progression action, recovery action, or logout.
- A staged one-zone fixture may bound the opportunity set, but the bot must
  independently choose an activity and target, travel through the navigation
  boundary, correct range/facing, damage and kill a mortal target with exact
  server kill credit, and produce native loot or another authoritative
  progression delta.
- Retain per iteration the bot identity/roster, selected activity and decision
  reason, origin/destination and fail-closed navigation evidence, casts,
  health/death/kill credit, progression before/after, recovery, resource
  samples, exact log offsets/hash, and normal logout with zero unintended bots.
- Complete at least three iterations. A synthetic command may create or remove
  an inert fixture only at a declared boundary after the bot has logged out;
  it may not make a failed autonomous iteration pass.
- After the third iteration, prove normal logout, zero bot/runtime counts,
  graceful Game-then-Login shutdown, zero processes/clients/listeners, then a
  clean restart with distinct PIDs and preserved identity, roster, and relevant
  progress. Gracefully stop both again and leave the runtime clean.
- Any absent self-selected activity, invalid travel, unchanged mortal health,
  zero exact kill credit, absent progression, operator-directed gameplay,
  failed cleanup, or missing persistence is a functional failure. Stop at the
  first smallest verified blocker and retain `FAIL`/`INCOMPLETE`; do not loop
  around it or activate a larger cohort.
- Commit only `ops/evidence/aaemu12-t061-one-bot-autonomy-v1.yaml` and a concise
  handoff. Raw runtime material stays in the registered external evidence path.

## Non-goals

Source implementation or deployment; multi-bot scale; the one-zone Activity
Director; groups, dungeons, PvP, economy, governance, AAEmu 3.0, direct database
work, or ArcheAge client acceptance.
