# T-066 handoff

T-066 integrated exact T-065 source candidate
`630c6c9d24505dc074e0da31ed1d633f84014c0d` as a truthful `INCOMPLETE`
runtime receipt. The verdict, evidence, and cleanup boundary are unchanged.

## Source identity and changed files

- Candidate/parent: `630c6c9d24505dc074e0da31ed1d633f84014c0d` /
  `04912db77beaac540a45d455fe852ebd6f4284de`.
- Immutable module source/tree: `401dd353ef37aa858763b41dc2e5c1663446ff57`
  / `6803d1d7e1d287798390e5aed0b835b9c8f12aef`.
- Replayed byte-identically from the candidate:
  `ops/evidence/aaemu12-t065-one-bot-autonomy-v2.yaml` and
  `ops/tasks/T-065/HANDOFF.md`.
- T-066 added only this handoff. No global ledger or lease file was edited.

## Receipt-integrity proof

The candidate has the exact parent and exact two-file diff. Its receipt and
handoff agree on the lease/thread, immutable source/tree, one counted
iteration, autonomous decision/combat/kill/logout, absent authoritative
progression delta, the stop before iterations two and three and restart,
graceful cleanup, and unchanged MySQL PIDs 6308 and 8076. Both replayed Git
blobs match the candidate exactly.

The retained 25-payload external `manifest.json` independently hashes to
`0114c06ccef9a45ef0a09955d57e06e5761c97505eed517665bcb0b6e86caa91`.
No external evidence was modified.

## Retained boundary and runtime state

T-065 remains `INCOMPLETE`: no authoritative experience or acquired-loot
before/after delta was observed. Iterations two and three and restart
persistence remain unexecuted, and recovery remains unproven. T-061 v1 and
T-065 v2 remain immutable retained evidence.

T-066 started or controlled no runtime, MySQL process, or client; accessed no
database; and changed no source, deployed host, or AAEmu 3.0 artifact. It
accepts only T-065's recorded graceful Game-then-Login cleanup with zero final
bots, runtimes, Login/Game/client processes, required listeners, and active log
entries, plus unchanged MySQL PIDs.

## Exact integration action

The Integrator must fast-forward the saved `integration/aaemu12-world` branch
through this handoff commit without rewriting history. PB-000 must then release
the `aaemu12` lease using T-065 cleanup, retain T-061 v1 and T-065 v2, and
dispatch only the smallest read-only progression/resource-observability source
correction. Keep T-041 and T-037 blocked.
