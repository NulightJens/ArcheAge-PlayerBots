# T-074 handoff

T-074 passed receipt-integrity review and replays T-073 as the same truthful
`INCOMPLETE` result. The verdict, external evidence, and cleanup boundary are
unchanged.

## Source identity and changed files

- Dispatch envelope: `7c17ba6dcdb4c669f50f6b6d3dc8c7b734c8ec45`.
- Preserved Control Tower binding descendant and integration pre-head:
  `1a8fcc2628811701e9e6658181cba5d95dce62b5`.
- Reviewed candidate: `3000b8b92fdb7e88a6196dd966c4420891c81953`,
  with exact parent `7261daa3a715213781c2c6a4e163ece69abddda8`.
- Replayed candidate files, byte-identically:
  `ops/evidence/aaemu12-t073-one-bot-autonomy-v4.yaml` and
  `ops/tasks/T-073/HANDOFF.md`. T-074 adds only this handoff.

## Proof and retained boundary

The candidate has exactly the declared two-file diff, and the task, source,
and saved integration worktrees were clean at review. Receipt and handoff agree
on identity, the autonomous kill, `+14` XP, ordered `6921/6966` pending then
`6966/6966` recovery, debt-free progression, self-logout, clean shutdown, and
unchanged MySQL PIDs. The external manifest SHA-256 is
`9f3b32de05e43ed64dff7dfd2b32219d3114aba7364f59674926a8c1a2278a14`;
all 382 declared payloads independently matched length and SHA-256, with zero
duplicates, missing files, failures, or unlisted payloads.

The direct pending-window brain/mover counter snapshots remain absent.
Iterations two and three and restart persistence remain unstaged. T-073 is not
a pass and no runtime, MySQL, database, client, source, host, lease, ledger, or
external-evidence action was taken by T-074.

## Integration and PB-000 action

Fast-forward `integration/aaemu12-world` from the preserved
`1a8fcc2628811701e9e6658181cba5d95dce62b5` history to this handoff commit.
PB-000 must then release the clean `aaemu12` lease and dispatch only a fresh
proof with a new immutable evidence root and read-only brain/mover counters
armed before fixture staging. Keep T-041 and T-037 blocked.
