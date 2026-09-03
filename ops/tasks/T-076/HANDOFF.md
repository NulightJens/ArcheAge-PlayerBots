# T-076 handoff

T-076 passed receipt-integrity review and replays T-075 as the same truthful
`INCOMPLETE` result. The verdict, external evidence, and cleanup boundary are
unchanged.

## Source identity and changed files

- Pre-dispatch integration identity: `4bf760b9f7d70ffc7941758c29abad2298a5ccb6`.
- Preserved Control Tower dispatch/binding history:
  `4bf760b9f7d70ffc7941758c29abad2298a5ccb6` →
  `91d4c5d2376771a8c3a170b1176d6d47396fc324` →
  `c0963989648d550397ec9f7d8711575a88e6e207`.
- Reviewed candidate: `f24f0f246adfdcf6ce2c692fc43eccd5b0c1d834`,
  with exact parent `ac1d04a1add6e89bddfa67ed63e71f326541f6f0`.
- Replayed candidate files byte-identically:
  `ops/evidence/aaemu12-t075-one-bot-autonomy-v5.yaml` and
  `ops/tasks/T-075/HANDOFF.md`. T-076 adds only this handoff.

## Proof and retained boundary

The candidate has exactly the declared two-file diff, and the task, source,
and saved integration worktrees were clean at review. Receipt, handoff, and
sealed evidence agree on lease/thread/source/tree identity; guarded startup;
the original 165-byte HTTP 200 offline `botdebug 20001` response; the absent
optional `obj` capture and failure before `observer-armed.json`; zero addbot,
fixture, gameplay, iteration, restart, or retry actions; graceful Game-then-
Login shutdown; zero final bots, runtimes, processes, clients, listeners, and
active logs; and unchanged MySQL PIDs 6308/8076.

Manifest SHA-256 is
`9dd8bad3d52a67ccc3fc49161576f5bd227364ea3c2d23d7ea321510c893c69d`.
All 34 declared payloads independently matched length and SHA-256, with zero
duplicates, unsafe paths, missing files, failures, or unlisted payloads.

T-075 remains `INCOMPLETE`: no autonomous product behavior was exercised.
T-076 did not start runtime, build, test, query MySQL, access a database or
client, change source/host/lease/ledgers/external evidence, or touch AAEmu 3.0.

## Integration and PB-000 action

Fast-forward `integration/aaemu12-world` from the preserved
`c0963989648d550397ec9f7d8711575a88e6e207` history to this handoff commit.
PB-000 must correct and qualify the reusable offline observer parser before
dispatching another fresh runtime proof, then use a new immutable evidence
root and prove observer arming before addbot and fixture staging. Keep T-041
and T-037 blocked.
