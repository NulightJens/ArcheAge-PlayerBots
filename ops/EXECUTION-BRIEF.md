# Execution brief

Goal: G-001 — one level-one Nuian completes five starter quests.

Loop:

```text
inspect -> focused test -> build -> deploy -> server + client test
        -> first blocker -> fix -> repeat
```

Done means the same fingerprinted build is deployed, native server progress is
real, the client visibly confirms behavior, five quests complete without
per-action commands, and state survives a graceful restart.

Report only:

```text
State:
Build:
Server marker:
Client marker:
Blocker:
Next action:
```
