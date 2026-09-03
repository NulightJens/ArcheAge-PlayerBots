# T-093 handoff

T-093 is **PASS-receipt-integrity** for a retained T-092 **FAIL**. Candidate
`885a6858e97ae073f86eed1aeef1c45bee3bb2ac`, with exact parent
`dea1c660a076b2db1fbd5586214cfa2b9cde1d7b`, was independently verified and
replayed byte-for-byte as commit
`e978a41b62f00493b22c96f436073eb4f8c42ba7` onto binding commit
`9fbf5db146d29b9c99b6eea3bf86a7656c53b484`. The candidate and replay stable
patch ID is `67e1adb88f3d55c74106b0aa497ffb7cc2f3127e`. The replay contains only the
T-092 receipt and handoff; their identical candidate/replay blobs are
`80d1da542ca2e8db0e4ac861b50b8c6882c1debf` and
`e7eba7d756f93a4284806d13149f9e4ce1c70673`.

The immutable root
`D:\Codex-Labs\evidence\T-092\one-zone-autonomy-v3` was read only. Manifest
SHA-256 `39f6c85e69faa78d87824632f0db168bcbc87db4fd26b8d350f7bea70370809c`
covers exactly 1,523 payloads and 22,604,049 bytes. Every safe relative path,
byte length, and SHA-256 was recomputed; the filesystem inventory found zero
missing, mismatched, duplicate, unsafe, unlisted, or reparse-point payloads.
The root creation timestamp corroborates the sealed fresh-root claim, and the
precreated `run-2` layout contains zero payloads.

The continuous Windows PowerShell client observer independently replays green:
PID `123292`, one launch, no relaunch, 866 raw snapshots, 866 ledger rows, zero
client-process samples, zero errors, maximum adjacent gap `653.19 ms`, first
sample `2026-09-01T17:57:40.3443432Z`, last sample
`2026-09-01T18:05:43.6525726Z`, and terminal chain
`d65dcd4204332615de276dc8f0ea67f117f1b2012e732ef946c46db32439c664`.
The first sample precedes runtime-start evidence and the last follows cleanup;
the observer exited cooperatively through the zero-byte sentinel.

All 92 semantic assertions passed. Exact raw Director lines admit
`[20001,20002,20003]` at `18:02:07.7555867Z`, `18:02:22.7661669Z`, and
`18:02:37.7786292Z`. The first autonomy samples arrived near `18:02:32Z`:
20001 and 20002 were already online with object IDs `7797` and `37952`, while
only 20003 produced two offline samples and the `armed.json`/`live.json`
boundaries before admission. This is the first material invalid gate and must
remain **FAIL**.

No fixture, directed gameplay, metrics, or roster command file exists. No
wave, lifecycle credit, progression, recovery, normal logout, refill, retry,
or restart was attempted. Game then Login stopped gracefully with zero exit
codes and no forced termination. Raw logs prove one Director start, three
admissions, one Director stop, three despawns, and one cleanup marker with zero
remaining bots and runtimes. Final evidence retains zero relevant processes,
listeners, clients, and live logs; unchanged OS-observed MySQL PIDs
`[6308,8076]` without integrator database access; exact config restoration;
clean reference/module identities; and the unchanged 30-entry host overlay.
The native `PhysicsManager` disposed-provider warning remains sealed under
SHA-256 `12be8531665da26d1be331f76a34366b28ebfdc13fc35aeafd6292990ac84a92`.

Two read-only validator probes are retained. First, PowerShell JSON timestamp
coercion discarded fractional seconds and falsely calculated a 1000 ms gap;
parsing the exact JSON strings corrected it to 653.19 ms. Second, the semantic
validator initially treated the existence of precreated boundary directories
as a failure; file-level validation proved zero boundary files for 20001/20002
and exactly `armed.json` plus `live.json` for 20003. Neither probe changed the
evidence, Git records, runtime, host, config, database, client, or any external
state.

Changed files are exactly the byte-identical T-092 receipt and handoff plus
`ops/evidence/aaemu12-t092-fail-integration-v1.yaml` and this handoff. T-093 did
not start or control a process, use the registered host, access the database or
client, mutate external evidence, edit global ledgers or the runtime lease,
change source/tooling/config, or consume tests/full suite.

Integration action: saved branch `integration/aaemu12-world` is fast-forwarded
only from `9fbf5db146d29b9c99b6eea3bf86a7656c53b484` through the commit containing
this receipt and handoff, without rewriting history. PB-000 may accept the
retained T-092 FAIL and bind T-094 under a new exact lease and fresh v4 root.
T-037 remains blocked; the sealed v3 root must never be reused or overwritten.
