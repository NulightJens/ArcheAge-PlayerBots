# T-066 contract

## Outcome

The exact T-065 runtime commit is independently reviewed and fast-forwarded
onto `integration/aaemu12-world` as a truthful `INCOMPLETE` receipt. No runtime
behavior, evidence content, or retained verdict is changed.

## Pass

- Start from exact integration head
  `04912db77beaac540a45d455fe852ebd6f4284de`. Verify candidate
  `630c6c9d24505dc074e0da31ed1d633f84014c0d`, its exact parent, clean source
  and integration worktrees, and an exact two-file candidate diff.
- Verify the committed receipt and handoff agree on the immutable source/tree,
  lease/thread, one counted iteration, autonomous decision/combat/kill/logout,
  absent progression delta, stopped iterations 2/3 and restart, graceful
  cleanup, unchanged MySQL PIDs, and manifest SHA-256
  `0114c06ccef9a45ef0a09955d57e06e5761c97505eed517665bcb0b6e86caa91`.
- Replay only the receipt and T-065 handoff, byte-identical to the candidate.
  Do not edit either file or change `INCOMPLETE` to PASS/FAIL.
- Add only a concise T-066 handoff stating the integrated commit and exact
  Control Tower action: release the lease using T-065 cleanup, retain T-061 v1
  and T-065 v2, and dispatch only read-only progression/resource observability.
- Fast-forward the saved integration branch through the receipt/handoff commit
  without rewriting history. Prove both worktrees clean.

## Non-goals

Runtime, build, tests, databases, clients, source correction, deployed host
mutation, global ledger/lease edits, another gameplay iteration, Activity
Director, scale, soak, release packaging, or AAEmu 3.0.
