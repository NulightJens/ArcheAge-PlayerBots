# T-070 contract

## Outcome

The exact T-069 runtime commit is independently reviewed and fast-forwarded
onto `integration/aaemu12-world` as a truthful `FAIL` receipt. No runtime
behavior, evidence content, or retained verdict is changed.

## Pass

- Start from exact integration head
  `88182ede5f32399d0ffd8684391fb8a5260c7b0e`. Verify candidate
  `1f26c4309868b379ab0fb64cad59ae48a6a75648`, exact parent
  `bbd6d8912be17d7084c439f2bbb42871b248fa16`, clean source/integration
  worktrees, and an exact two-file candidate diff.
- Verify receipt and handoff agree on exact lease/thread, immutable source/tree,
  natural pre-activity recovery to full HP/MP, autonomous decision/combat/kill,
  `+14` XP, one progression record, exact `-45` MP completion delta, normal
  self-logout, stopped iterations 2/3 and restart, graceful cleanup, unchanged
  MySQL PIDs, and manifest SHA-256
  `0397ae08227fdc8c2952ef99208da592e8315471173efba068a5c4523f33b558`.
- Independently verify all 26 external manifest payload lengths and hashes
  read-only. Replay only the receipt and T-069 handoff byte-identically; do not
  edit either file or change `FAIL` to PASS/INCOMPLETE.
- Add only a concise T-070 handoff stating the integrated commit and exact
  Control Tower action: release the lease using T-069 cleanup, retain T-065 v2
  and T-069 v3, and dispatch only a bounded lifecycle recovery correction.
- Fast-forward the saved integration branch through the receipt/handoff commit
  without rewriting history. Preserve any ledger-only descendant and prove
  both worktrees clean.

## Non-goals

Runtime, build, tests, databases, clients, source correction, deployed host
mutation, global ledger/lease edits, another gameplay iteration, Activity
Director, scale, soak, release packaging, or AAEmu 3.0.
