# T-039 contract

## Outcome

A deterministic, host-independent state machine models offline, spawning, idle, active, resting, dead, recovering, and despawning behavior using explicit events and data-driven profiles.

## Pass

- Every state/event pair is accepted or rejected explicitly.
- Death, failed spawn, logout, recovery, and restart transitions are idempotent.
- Time-dependent behavior uses supplied timestamps or clocks; tests do not sleep.
- Profiles validate activity/rest limits and reject impossible or negative values.
- Replay of the same event sequence produces the same final state and transition trace.

## Non-goals

- AAEmu character spawning.
- Database persistence.
- Population density decisions.
- Editing existing shared manager/configuration files.
