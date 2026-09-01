# T-074 contract

## Outcome

The exact T-073 runtime commit is independently reviewed and fast-forwarded
onto `integration/aaemu12-world` as a truthful `INCOMPLETE` receipt. No runtime
behavior, evidence content, or retained verdict changes.

## Pass

- Verify integration head `7261daa3a715213781c2c6a4e163ece69abddda8`,
  candidate `3000b8b92fdb7e88a6196dd966c4420891c81953`, exact parent,
  clean worktrees, and exact two-file diff.
- Verify receipt/handoff agree on lease/thread/source/tree, autonomous kill,
  `+14` XP, ordered recovery pending at 6921/6966 then completed at 6966/6966,
  debt-free progression, self-logout, absent direct recovery-window brain/mover
  counters, stopped iterations 2/3/restart, cleanup, unchanged MySQL PIDs, and
  manifest SHA-256
  `9f3b32de05e43ed64dff7dfd2b32219d3114aba7364f59674926a8c1a2278a14`.
- Independently verify all 382 manifest payload lengths/hashes read-only. Replay
  receipt and T-073 handoff byte-identically; never change INCOMPLETE.
- Add only concise T-074 handoff directing PB-000 to release the lease and
  dispatch a fresh proof with pre-armed read-only counters and a new root.
- Fast-forward the saved branch without rewriting history; prove clean states.

## Non-goals

Runtime/build/tests/database/client/source/host mutation; ledger/lease edits;
another gameplay iteration; Activity Director; scale; soak; packaging; or
AAEmu 3.0.
