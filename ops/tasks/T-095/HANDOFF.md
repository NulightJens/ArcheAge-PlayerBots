# T-095 handoff

T-095 is **PASS-receipt-integrity** for a retained T-094 **FAIL**. Candidate
`453d0bd60cf1e72117b10579d40e549b0a24729f`, with exact parent
`cc4f9fc6889abc17a149e6dad9d4ff92491a8dc0`, was independently verified and
replayed byte-for-byte as commit
`65b6cec7e612f5d5d3945d8fee0a6cdfd463889e` onto binding commit
`1ebfd0ec47138ebf15268befde9ed1671aa05ef8`. Candidate and replay stable patch
ID `c5954c963346fea4460f7d708d072d846fb77a5e` matches. The exact replayed
blobs are `4edd50931241a4b962a75b031bf60cbcae7b12b4` for the T-094 receipt and
`c00750c28a585033fa3ed24e2bc5d70a187af446` for its handoff.

The immutable root
`D:\Codex-Labs\evidence\T-094\one-zone-autonomy-v4` remained read-only.
Manifest SHA-256
`46ce346de698964231a522b2e3646af7a7fc0b0fbd5a080959c9e78da7d57293`
covers exactly 3,244 payloads and 24,226,957 bytes. Every safe relative path,
length, and SHA-256 was recomputed; the independent filesystem inventory found
zero missing, mismatched, duplicate, unsafe, unlisted, or reparse-point
payloads. The root creation time corroborates the fresh-root record, and the
precreated run-2 layout contains zero payload files.

The continuous Windows PowerShell client observer independently replays green:
PID `22748`, one launch, no relaunch, 1,001 raw snapshots, 1,001 chained ledger
rows, zero client-process samples, zero errors, maximum adjacent gap
`650.484 ms`, first sample `2026-09-01T18:56:08.1843772Z`, last sample
`2026-09-01T19:05:20.3607648Z`, and terminal chain
`f84262166440f2fcd31d57984a0271d5bb53fc9815a5e165ab3f7c4fd9047b86`.
The first sample precedes runtime-start evidence, the last follows cleanup, and
the observer exited cooperatively through its zero-byte sentinel.

All three autonomy observers are raw-verified. Each has 247 derived and
transport samples, 243 raw responses, four expected post-shutdown transport
errors, two initial `offline` samples with `online=false` and null object ID,
and exact `armed.json` plus `live.json` boundaries before admission. The latest
second offline sample is `2026-09-01T18:58:30.7703112Z`; first admission is
`2026-09-01T18:59:11.1494980Z`. The widened pre-arm gate therefore passed.

The first material invalid gate remains the exact Director delay. Sealed config
SHA-256 `5177e67c818d04ebf9c6b0164afc833e431cf4f687e660cffe02d5ef202de47c`
contains `ActivityDirectorInitialDelayMs=180000`. Exact raw Director lines show
start `2026-09-01T18:58:11.1285011Z` and first admission
`2026-09-01T18:59:11.1494980Z`, independently deriving `60020.997 ms`. The
Director admitted `[20001,20002,20003]` with three successes and no reported
failures, but the delay mismatch is non-PASS and T-094 remains **FAIL**.

No fixture, directed gameplay, metrics, or roster command file exists. No
fixture/separation gate, lifecycle wave, credit, progression, recovery, normal
logout, refill, retry, or restart was attempted. Game then Login stopped
gracefully with zero exit codes and no forced termination. Raw logs prove one
Director start, three admissions, one stop, three despawns, and one cleanup
marker with zero bots and runtimes. Final evidence has zero relevant processes,
listeners, clients, and live logs; unchanged OS-observed MySQL PIDs
`[6308,8076]` without database access; exact config restoration; clean
reference/module identities; and the unchanged 30-entry host overlay. The
sealed native `PhysicsManager` disposed-provider warning remains under SHA-256
`c768beb56a7553c32553cf2bef722df7c717dba420c87c490cc8e21833489445`.

Two read-only verifier probes are retained. The first incorrectly normalized a
two-backslash token and falsely reported 3,243 nested files as unlisted; the
corrected character-based comparison proved zero. The second let PowerShell
coerce exact JSON timestamps and falsely calculated a 1,000 ms maximum gap;
preserving timestamp strings corrected it to `650.484 ms`. Neither changed
evidence, Git records, runtime, host, config, database, client, or external
state.

Changed paths are exactly the byte-identical T-094 receipt and handoff plus
`ops/evidence/aaemu12-t094-fail-integration-v1.yaml` and this handoff. T-095 did
not start or control a process, use the registered host, access the database or
client, mutate external evidence, edit global ledgers or the runtime lease,
change source/tooling/config, or consume tests/full suite.

Integration action: saved branch `integration/aaemu12-world` is restricted to a
fast-forward-only update from `1ebfd0ec47138ebf15268befde9ed1671aa05ef8`
through the commit containing this receipt and handoff, without history
rewriting. PB-000 may accept the retained T-094 FAIL and continue the separate
Director-delay range correction. T-037 remains blocked; the sealed v4 root must
never be reused or overwritten.
