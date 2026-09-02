# PlayerBots capability roadmap

> **Legacy dependency view (2026-09-02):** `ops/GOALS.yaml` now owns executable
> product order and state. This T-number dependency graph is retained for
> history only. A task marked done is not a product acceptance claim.

This file records capability dependencies. `BOARD.yaml` owns current dispatch;
historical evidence does not. The upstream
[`mod-playerbots`](https://github.com/mod-playerbots/mod-playerbots) project is
the capability and sequencing guide: translate player-like autonomy into
ArcheAge-native behavior, but do not copy World of Warcraft-specific code or
content literally.

## Active target

AAEmu 1.2 r208022 is the sole feature/runtime target through the one-zone
public alpha. AAEmu 3.0 is a frozen compatibility checkpoint and receives only
its release-boundary install, build, and adapter regression.

## Product order

1. Prove a reliable single-bot autonomous gameplay loop.
2. Maintain a persistent autonomous population in one zone.
3. Add varied ArcheAge-native activities and player-like decisions.
4. Scale only behavior that remains functionally accepted.
5. Add group, dungeon, PvP, economy, and wider-world behavior.
6. Build governance, residency, ownership, and territorial simulation on that
   proven population.

Test harnesses, receipts, orchestration, and metrics support this order; they
are not product milestones. Favorable throughput cannot compensate for a bot
that does not damage, kill, progress, recover, or persist.

## Dependency graph

```text
Accepted AAEmu 1.2 foundations
├── T-038 navigation boundary ──────────────────────────────┐
├── T-039 life-state FSM and profiles ──────────────────────┤
├── T-040 persistent identity and roster ───────────────────┤
├── T-043 isolated database ────────────────────────────┐    │
├── T-044 combat harness ───────────────────────────────┼────┤
└── T-007 evidence translator ──────────────────────────┘    │
                                                            │
T-036/T-050/T-054/T-057 retained physical attempts          │
                         │                                  │
                         └──> T-060 bounded v5 diagnostic ──┤
                                                            ▼
                                  T-061 single-bot autonomous loop
                                                            │
                                                            ▼
                                  T-041 one-zone Autonomous Activity Director
                                                            │
                                                            ▼
                                  T-037 functional scale and recovery
                                                            │
                                                            ▼
                                  30-minute activity soak and clean restart
                                                            │
                                                            ▼
                                  one-zone public-alpha artifact
                                                            │
                                                            ▼
                           groups, dungeons, PvP, economy, and multi-zone play
                                                            │
                                                            ▼
                         residency, ownership, governance, and zone conflict
```

## Gate 1: bounded T-060 diagnostic

T-060 may finish only its already-authorized headless diagnostic under its
exclusive runtime lease. Its evidence must distinguish command acceptance and
scheduler performance from authoritative target damage and kill credit. Zero
mortal kills or unchanged mortal target health is a functional failure. T-060
must retain evidence, shut down gracefully, and release the lease; it does not
unlock scale directly.

## Gate 2: T-061 single-bot autonomous loop

Before any larger cohort, one persistent bot must complete at least three
iterations of the following loop without per-action operator commands:

- managed spawn or login and autonomous activity selection;
- fail-closed travel to a valid activity;
- target acquisition, range/facing correction, damage, and mortal kill credit;
- loot or another authoritative progression result;
- rest, death/revival, and failure recovery where applicable;
- normal logout with zero unintended retained bots; and
- clean restart with identity, roster, and relevant progress preserved.

Synthetic commands may stage or observe a fixture, but they may not stand in
for the bot's activity decisions. A failure creates the smallest bounded
diagnostic or correction task; it never authorizes a larger cohort.

## Gate 3: T-041 one-zone Autonomous Activity Director

The director maintains configured population bounds while bots independently
rotate through qualified one-zone activities such as wandering, grinding,
bounded quest participation, recovery, rest, and social grouping. Operator
diagnostics expose identities, current activities, failures, throttling, and
cleanup. Density without completed activity is not acceptance.

## Gate 4: T-037 functional scale and recovery

Qualify cohorts incrementally at 0, 1, 5, 10, 25, 50, 100, and the highest
safe level. Stop at the first functional, cleanup, stability, or resource
failure and diagnose that smallest failed cohort before continuing. Record
whole-server CPU, memory, tick behavior, active/inactive populations, activity
completion, mortal combat, normal logout, graceful shutdown, and restart.
Derive the resource ceiling only from cohorts whose autonomous behavior passes.

## Shareable one-zone milestone

The public alpha requires committed, fingerprinted evidence for:

- reproducible installation against pinned AAEmu 1.2;
- the repeated single-bot autonomous loop;
- configured one-zone population and activity bounds;
- persistent identity, roster, and relevant progress;
- qualified lifecycle, navigation, combat, death, revival, and recovery;
- incremental functional scale and an explicit highest-safe ceiling;
- a populated 30-minute activity soak followed by normal logout, graceful
  shutdown, zero unintended retained bots, and clean restart;
- focused tests and one full AAEmu 1.2 suite per integration wave;
- operator, installation, limitation, troubleshooting, recovery, and
  upstream-to-ArcheAge mapping documentation; and
- a clean-installed versioned artifact plus the frozen AAEmu 3.0
  release-boundary compatibility check.

## Parallelism boundary

PB-000 remains the non-feature Control Tower. One outcome has one task, branch,
chat, worktree, write scope, proof contract, and handoff. Independent source,
test, and documentation work may run in parallel, but integration, global
ledgers, database/runtime/client control, physical gates, and release metadata
are serialized. Exactly one task may hold the runtime lease. A tooling-only
wave requires a demonstrated blocker and should extend existing evidence
mechanisms rather than introduce a parallel framework.
