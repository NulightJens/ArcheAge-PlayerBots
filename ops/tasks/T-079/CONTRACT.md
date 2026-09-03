# T-079 contract

## Outcome

One persistent AAEmu 1.2 bot completes at least three autonomous one-kill
gameplay iterations through self-selected activity, navigation, authoritative
combat/kill credit, native progression, directly observed natural recovery to a
debt-free completion boundary, normal self-logout, and restart persistence.
This is a new immutable v6 proof; all predecessor evidence remains untouched.

## Runtime authority and immutable inputs

- Do not start or control Login, GameServer, MySQL, or the ArcheAge client
  until committed `ops/RUNTIME-LEASE.yaml` assigns `aaemu12` to T-079 and this
  exact thread. The client is prohibited and MySQL is observation-only.
- Use only `aaemu12_integration`, database identity
  `aaemu12_database_public_alpha_v1`, and `aaemu12_t079_evidence_v6`. The fresh
  evidence root must be absent before first run and immutable once created.
- Immutable installed source/tree:
  `68aaaa3334a408d1d6d21e44472a8984e78618c2` /
  `9fa74d8057df8b5eb276ad223a10ed9b12791f88`; host base
  `62e3eb1d87da01194802ac886cd500134facad28`.
- Use `scripts/autonomy/Observe-AutonomyBot.ps1` exactly as integrated at
  `a406a3598213745ac3bead7dc5ba7ce009cf50e3`. Do not copy, patch, wrap with a
  replacement parser, or hand-roll another observer.

## Required observation boundaries

- For each iteration create a fresh observer subdirectory and start the
  integrated observer for bot `20001` before `addbot`. Wait for immutable
  `boundaries/armed.json` and `boundaries/live.json`; independently verify they
  came from two valid offline samples with correct raw hashes. No bot or fixture
  action is allowed before both files exist.
- Keep the same observer alive through addbot, safe-boundary wait, fixture
  staging, activity, kill, recovery, completion, and self-logout. Immediately
  before fixture staging require a fresh derived online sample for bot 20001
  and exact host-metric brain/mover counters.
- During `life_recovery state=pending`, retain multiple timestamped derived
  samples spanning the full window. Bot/runtime must stay registered and the
  directly parsed host `brain_steps` and `mover_steps` values must not advance.
  Preserve the first offline sample after self-logout, then end the observer
  only through its graceful console-cancellation or normal bounded-exit path.

## Pass

- Prove exact lease, fingerprints, clean workspaces, zero conflicting
  processes/clients/listeners, empty active logs, absent evidence root, and
  initial zero bot/runtime counts. Start only via the retained guarded runner;
  observe but never start, stop, query, or control MySQL.
- Use persistent bot ID `20001` for exactly three counted iterations. At each
  zero-population boundary, managed `addbot 20001` is permitted. Before fixture
  staging, let native recovery reach targetless/noncombat/stationary full HP/MP.
- Stage exactly one inert mortal opportunity near the bot only after that safe
  boundary and the fresh online observer sample. Never issue `botstate` or any
  activity, destination, target, attack, loot/progression, recovery, or logout
  command.
- The bot must independently emit `activity=grind reason=nearby_mortal`, select
  the exact staged target, navigate/correct range/facing, cast/damage/kill with
  exact server credit, and produce positive native experience or acquired-item
  progression.
- Require ordered pending-to-completed natural recovery at full HP/MP, direct
  counter suspension for the entire pending window, then exactly one completion
  progression record and normal self-logout to zero bots/runtimes.
- Retain exact baseline/completion level/XP, HP/MP maxima and deltas, inventory,
  progression fingerprint, target/cast/damage/kill credit, navigation, lifecycle
  UTCs, every raw/transport/derived observer sample, counter window, metrics,
  roster originals and normalized arrays, log offsets, and evidence hashes.
- Count an iteration only after valid recovery/completion/logout. Stage the next
  only at zero population. Stop at the first invalid/missing activity, travel,
  kill credit, progression, recovery, counter suspension, debt-free completion,
  duplicate record, operator-directed gameplay, evidence, or logout gate.
- After iteration three, gracefully stop Game then Login; restart with distinct
  PIDs and verify preserved identity/roster/relevant progress read-only. If an
  active re-add is required, stage no opportunity, do not count it, remove it
  only at final cleanup, then gracefully stop Game/Login again.
- Commit only `ops/evidence/aaemu12-t079-one-bot-autonomy-v6.yaml` and the
  concise handoff. Raw evidence remains in the fresh external root.

## Verdict and cleanup

PASS requires three complete iterations plus clean restart persistence and
final cleanup. A complete behavioral violation is `FAIL`; absent or ambiguous
required material is `INCOMPLETE`. On every verdict preserve evidence,
gracefully stop every observer, then Game and Login, prove zero bots/runtimes/
processes/clients/listeners and free ports, and record unchanged observed MySQL
PIDs. Never force stop, repeat the evidence attempt, or activate a larger cohort
after failure.

## Non-goals

Source/tooling/deployment/database/client work; operator recovery; fourth
iteration; multi-bot scale; Activity Director; groups/dungeons/PvP/economy/
governance; soak; release packaging; AAEmu 3.0; or client acceptance.
