# PlayerBots capability roadmap

This file records capability dependencies. `BOARD.yaml` owns current dispatch; historical evidence does not.

## Active target

AAEmu 1.2 is the sole feature/runtime target until the one-zone Population Director is accepted. AAEmu 3.0 is preserved as a future port and release-boundary compatibility regression.

## Dependency graph

```text
Accepted 1.2 module baseline
├── T-038 navigation boundary ─────────────┐
├── T-039 life-state FSM and profiles ─────┤
├── T-040 persistent identity and roster ──┤
├── T-043 isolated database ──────┐         │
├── T-044 combat harness ─────────┼─> T-036 combat/stealth closure ─┐
│                                 │                                 │
│                                 └─────────────────────────────────┘
│                                                                   │
│                                                                   └─> T-037 scale/recovery
└── T-007 evidence translator                │
                                             ▼
                              T-041 one-zone Population Director
                                             │
                              30-minute soak and clean recovery
                                             │
                                  multi-zone population control
                                             │
                          residency, ownership, groups, governance
                                             │
                              economy, questing, and zone conflict
```

## Shareable milestone

The first public-ready milestone requires:

- Reproducible installation against the pinned AAEmu 1.2 base.
- Configurable one-zone population density and resource ceiling.
- Persistent bot identities across logout and clean restart.
- Deterministic lifecycle, rest, death, and recovery behavior.
- Fail-closed navigation boundary with obstacle diagnostics.
- Qualified combat behavior and normal logout recovery.
- Operator documentation, known limitations, and versioned artifact.

## Parallelism boundary

T-038, T-039, and T-040 are independent source lanes. T-036 and T-037 share the live runtime and execute serially. T-041 is the first integration point. Runtime control, global ledger changes, release metadata, and integration branches always have a single writer.
