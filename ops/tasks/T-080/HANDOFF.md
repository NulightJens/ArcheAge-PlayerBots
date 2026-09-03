# T-080 handoff

T-080 is **PASS**. Candidate `ad8c0cf2aa9eed46c936c835931ce344341f7d09`
was independently verified as the truthful, exact two-file T-079 runtime receipt
and replayed without changing its verdict or evidence boundary.

## Source identity and changed files

- Reviewed candidate: `ad8c0cf2aa9eed46c936c835931ce344341f7d09`;
  exact parent: `f12d8b16c14c4eab4ac7afe2e1422e04e1953ec6`.
- Contractual pre-dispatch integration identity:
  `5b3f37e5f324eb84dd17d180b002ff48f312e603`.
- Integration parent: `1be78151bc71f5129d807a361692bb82755ede9f`;
  Control Tower descendants `6e53e2a3a92e416b4f6b55aafb6a3b6afd67886a`
  and `1be78151bc71f5129d807a361692bb82755ede9f` are preserved.
- Replayed byte-identical candidate blobs:
  `ops/evidence/aaemu12-t079-one-bot-autonomy-v6.yaml`
  (`89ecd039e6d0b1f8d1073ea8d1a2b8996a492f68`) and
  `ops/tasks/T-079/HANDOFF.md`
  (`515dc545b6d76c43fbed0a67c0607192dbfc8b04`).
- The only new integrator-authored file is `ops/tasks/T-080/HANDOFF.md`.

## Independent proof

The candidate has the exact declared parent and adds exactly the receipt and
T-079 handoff. Manifest
`fe00f692aec54da1760ef6e99645f1d21a13bc902f7fc3c7cc81b6d3ae4c5a4e`
was recomputed read-only. All 6,490 payloads and all 51,584,593 bytes matched
their declared paths, lengths, and SHA-256 hashes, with zero missing,
duplicate, unsafe, unlisted, mismatched, or reparse-point entries.

A 49-assertion semantic replay passed in full. The sealed evidence agrees on
the exact lease/thread, host/module/observer fingerprints; three serial
observer-armed iterations; a fresh safe online boundary before each inert
fixture; one autonomous kill, +14 XP, fixed pending-window brain/mover
counters, debt-free full recovery, completion, logout, and zero boundary per
iteration; +42 XP total; distinct-PID restart; persistent 104-entry level-51
identity/roster; an uncounted no-fixture re-add; unchanged MySQL PIDs; graceful
Game-then-Login shutdown; and zero final residue. The command inventory contains
only the allowed four `addbot`, three `spawnpassive`, one cleanup
`removebot`, observer, metrics, roster, and online queries.

## Runtime state, retained boundary, and action

No runtime, MySQL, database, client, source, tooling, host, external evidence,
global ledger, lease, or AAEmu 3.0 state was touched. The runtime remains
stopped and clean with the lease released. No receipt-integrity failure is
retained. The known post-readiness PhysicsThread disposed-provider warning and
the explicitly superseded immutable artifacts remain preserved.

PASS remains limited to exactly three counted one-kill iterations. It does not
claim a fourth iteration, scale, Activity Director behavior, or unqueried exact
post-restart XP; restart persistence is limited to the exact roster hash,
identity, and retained level.

Integration action: fast-forward this handoff commit onto
`integration/aaemu12-world`. PB-000 may then mark the single-bot milestone
satisfied and unblock T-041 through a separate Control Tower ledger commit.
