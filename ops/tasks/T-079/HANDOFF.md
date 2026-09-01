# T-079 handoff

T-079 is **PASS**. Persistent bot 20001 (`ScaleBot000`) completed three serial,
autonomous one-kill iterations through self-selected `grind` /
`nearby_mortal` activity, native navigation, authoritative combat and kill
credit, positive native progression, directly observed recovery, one debt-free
completion record, and normal self-logout. A distinct-PID restart preserved the
same 104-entry roster hash and bot identity/level, and final cleanup is clean.

## Source identity and changed files

- Lease/thread: `5b3f37e5f324eb84dd17d180b002ff48f312e603` /
  `01a05cfc-1ebf-79a2-94d8-4d287c3990ab`.
- Host/reference: `62e3eb1d87da01194802ac886cd500134facad28`.
- Installed module: `68aaaa3334a408d1d6d21e44472a8984e78618c2`,
  clean tree `9fa74d8057df8b5eb276ad223a10ed9b12791f88`.
- Observer: unchanged from integrated commit
  `a406a3598213745ac3bead7dc5ba7ce009cf50e3`.
- Changed Git files: `ops/evidence/aaemu12-t079-one-bot-autonomy-v6.yaml`
  and `ops/tasks/T-079/HANDOFF.md` only.

## Proof

All three counted iterations pre-armed fresh observers with two independently
hash-verified offline samples before `addbot`. Each reached a fresh targetless,
noncombat, stationary 9516/9516 HP and 6966/6966 MP boundary before exactly one
inert `spawnpassive 10004 12` fixture. The bot independently selected the exact
staged target and produced navigation, cast/damage, native kill, exact kill
credit, ordered pending-to-completed recovery, and one completion record.
Pending recovery retained 6, 11, and 8 samples respectively; brain/mover
counters remained fixed throughout every window. The first offline sample after
each normal self-logout was retained. Experience advanced exactly
`8082042 -> 8082056 -> 8082070 -> 8082084` (+42 total), while level 51, full
resources, inventory, and its inventory fingerprint remained debt-free.

After iteration three, Game then Login stopped gracefully. The guarded restart
used distinct Login/Game PIDs 81896/121812, reproduced the exact initial
104-entry roster hash, and retained bot 20001 as `ScaleBot000`, level 51. An
uncounted re-add proved the same identity at a fresh full-resource observer
sample without staging any fixture; it was removed only during final cleanup,
with the first offline sample retained. Direct database access was prohibited,
so the receipt records the last directly observed XP value before restart and
does not claim an unqueried post-restart XP value.

## Cleanup and integration action

Final Game-then-Login shutdown used graceful Ctrl+C. Verified cleanup has zero
bots, runtimes, Game/Login/observer/client processes, required-port listeners,
or active logs; all five ports are free, the shutdown cleanup marker occurs
exactly once, MySQL PIDs 6308/8076 are unchanged, and reference/module/host/
observer/artifact fingerprints remain exact. The known post-readiness
PhysicsThread disposed-service-provider warning from the first graceful Game
shutdown is retained and did not affect restart or cleanup. Immutable transport
and serialization missteps are retained and explicitly superseded by verified
records rather than overwritten.

Raw evidence is sealed at
`D:\Codex-Labs\evidence\T-079\one-bot-autonomy-v6`; all 6,490 manifest
payloads (51,584,593 bytes) match their declared lengths and SHA-256 hashes,
with zero unlisted files. Manifest SHA-256 is
`fe00f692aec54da1760ef6e99645f1d21a13bc902f7fc3c7cc81b6d3ae4c5a4e`.

Integrator: fast-forward/cherry-pick this commit as the T-079 `PASS` receipt.
PB-000 may release the `aaemu12` lease and advance dependent planning through
Control Tower ledger edits; this task intentionally does not edit global
ledgers, source, deployed host files, databases, clients, or predecessor
evidence.
