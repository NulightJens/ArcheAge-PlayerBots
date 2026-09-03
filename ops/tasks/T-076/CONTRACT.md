# T-076 contract

## Outcome

The exact T-075 runtime commit is independently reviewed and fast-forwarded
onto `integration/aaemu12-world` as a truthful `INCOMPLETE` receipt. No runtime
behavior, evidence content, or retained verdict changes.

## Pass

- Verify integration head `4bf760b9f7d70ffc7941758c29abad2298a5ccb6`,
  candidate `f24f0f246adfdcf6ce2c692fc43eccd5b0c1d834`, exact parent
  `ac1d04a1add6e89bddfa67ed63e71f326541f6f0`, clean worktrees, and exact
  two-file candidate diff.
- Verify receipt/handoff agree on lease/thread/source/tree, guarded startup,
  the original 165-byte HTTP 200 offline `botdebug 20001` response, absent
  optional named `obj` capture, failure before `observer-armed.json`, zero
  addbot/fixture/gameplay actions, no iteration/restart/retry, graceful Game
  then Login shutdown, zero final state, unchanged MySQL PIDs, and manifest
  SHA-256
  `9dd8bad3d52a67ccc3fc49161576f5bd227364ea3c2d23d7ea321510c893c69d`.
- Independently verify all 34 manifest payload lengths and hashes read-only.
  Replay the receipt and T-075 handoff byte-identically; never change the
  `INCOMPLETE` verdict.
- Add only a concise T-076 handoff directing PB-000 to correct and qualify the
  reusable observer parser before another fresh runtime proof.
- Fast-forward the saved branch without rewriting history and prove clean
  task/source/integration states.

## Non-goals

Runtime/build/tests/database/client/source/host mutation; ledger/lease edits;
another gameplay iteration; parser implementation; Activity Director; scale;
soak; packaging; or AAEmu 3.0.
