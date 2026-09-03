# T-099 handoff

T-099 is **PASS-receipt-integrity** for a retained T-098 **FAIL**. Candidate
`e7361e10c8c1d722c3f18481af07e6173030fb30`, with exact parent
`5d5919e56cd980d0db2ad1907178ba2a59868eb7` and tree
`b428927a3d428721d6a5e8ce0651b5013bee6903`, was independently verified and
replayed byte-for-byte as commit
`84fbbb15cb9befe4dd78b26a57ce3cebd2d6d2ca` onto binding commit
`201e515e47a51b3b6798cddcf5cc5d122456eec6`. Replay tree is
`c6386496258ad4a4be08d636125f9ee3250a4093`; candidate and replay stable patch
ID `9b340788631fe6df8fce618af179a41ad2d424da` matches. Exact replayed blobs are
`c9ecebedc81f2fa6a542a3ce656c0d0a4c8b338b` for the T-098 receipt and
`7907eb1b6024a934c6601f658086c04cf1a11ef2` for its handoff.

The immutable root
`D:\Codex-Labs\evidence\T-098\one-zone-autonomy-v5` remained read-only.
Manifest SHA-256
`1ab0074aad22af52abcef062982a10ad7c48146facabdde092e691c341127e42`
has exact length 2,507,986 and covers 12,277 payloads totaling 33,980,701
bytes. Every safe relative path, length, and SHA-256 was recomputed; the
independent inventory found zero missing, mismatched, duplicate, unsafe,
unlisted, or reparse-point payloads. Wave-two and run-two layouts contain zero
payloads, and exactly one runtime-start record exists.

The continuous Windows PowerShell client observer independently replays green:
PID `114064`, one launch, 1,752 raw snapshots, 1,752 chained ledger rows, zero
client-process samples, zero errors, maximum adjacent gap `661.367 ms`, first
sample `2026-09-01T20:06:16.6146797Z`, last sample
`2026-09-01T20:22:32.8002491Z`, and terminal chain
`b215a39a0c778d7b66ae11687112066512b5cbb023b4f5d77cbdaff593eaf449`.
The first sample precedes Login and runtime-start evidence; the last follows
cleanup. Every raw length/hash, snapshot, sequential ledger row, previous hash,
and current hash recomputed exactly, and the zero-byte sentinel plus summary
prove cooperative exit.

All three autonomy observers are independently cross-linked to every referenced
raw response. Bots 20001/20002/20003 respectively have 1,166/1,167/1,168
derived and transport samples, 1,157 raw files each, and 9/10/11 expected
post-shutdown transport errors. There are no missing, duplicate, unreferenced,
length-mismatched, hash-mismatched, malformed, or semantically inconsistent raw
references. Each observer's first two samples are raw-verified `offline` with
`online=false`, null object ID, and exact armed/live boundaries. Latest second
offline sample `2026-09-01T20:10:18.0225757Z` precedes first admission by
80,364.812 ms.

Authoritative Game log replay has exactly 37 Director lines: one start, three
spawns, 32 steady ticks, and one graceful stop. Start
`2026-09-01T20:08:38.3319957Z` to first admission
`2026-09-01T20:11:38.3873878Z` independently derives `180055.392 ms`, inside
the `[180000,195000] ms` window. Admissions are exactly
`[20001,20002,20003]` with 15,022.037 and 15,043.321 ms gaps, one per tick,
three attempts/successes, zero failures, zero wrong-zone observations, zero
overlap, and no refill.

The fresh safe-boundary samples independently prove all three bots full-resource,
targetless, noncombat, stationary, and idle. Bot-anchor coordinates recompute
the retained 20001/20002 horizontal distance as `12.163 m`. This is only a
retained risk: no fixture spawned, so no fixture coordinates or pairwise
fixture separation are proven.

The complete command inventory contains one request, one response, and the safe
boundary only. Request bytes are exactly
`{"character":"@system","arguments":"10004 5 20001"}` under SHA-256
`ec7c00e3a8ee0a3ff638bfdae5e4a5a1852d27a843b56fa0041bd7038a7e739b`.
The 218-byte response has exact command line `spawnpassive 10004 5 20001` and
message `Active bot anchor 20001 has an inconsistent world or instance
boundary.` under SHA-256
`ba59f0550a863551cace87e28c5086398c5c84eb299871944cedcc6e7a20192a`.
No fixture spawned; no retry, later fixture, directed gameplay, lifecycle wave,
wave two, or restart was attempted. T-098 therefore remains **FAIL**.

Game then Login stopped gracefully, 15 seconds apart. Raw logs prove one
Director stop, exact despawns `[20001,20002,20003]`, and one cleanup marker
with zero bots and runtimes. Each autonomy observer's terminal transport errors
begin after Game shutdown, validation records cooperative absence, and stdout/
stderr are empty. Final and current read-only checks establish zero named AAEmu
or ArcheAge processes, zero required listeners, and zero live logs. MySQL PIDs
`[6308,8076]` remain `mysqld` under OS-only observation without database access.
Historical Game PID `116784` is now reused by unrelated `svchost`; exact-name,
listener, log, and sealed final-state gates remain green.

Identity checks are exact: reference and host head
`62e3eb1d87da01194802ac886cd500134facad28`; clean installed module head
`761ffa1e0bd76d06532688f34b45e192a493b239` and tree
`c36b1255cccb3a782e44e87a56bc9e867a946048`; expected 30-entry host overlay;
AAEmu 1.2 v3 patch SHA-256
`baa0979a200eb7f4403985d7166092f937033c31650aa11336fba31543d96606`
with green reverse-check; Game assembly SHA-256
`c1b3bd922c900c31c76a5748359b8363ea7c56ba34d27daed9c90fd1ddbd51ef`;
and observer blobs `0f8b5e2b3a811877648020b715e4737777d4947a` and
`94b82fb18a39e2356d43796b54e3eeea5ad7ee12`. Original config SHA-256
`7b57a52149f6059cbcd972c38b161c46781d6730dbb3c6b93890171ae8c43202`
is restored byte-for-byte. All three retained logs match their sealed move
lengths and hashes. The native `PhysicsManager` disposed-provider warning is
retained without invalidating cleanup.

Read-only verifier anomalies are retained in the integration receipt: initial
PowerShell timestamp coercion falsely produced a 1,000 ms client gap before
exact-string replay corrected it; several first probes failed on reserved names
or token spacing before corrected read-only invocations; and the first patch
probe selected the frozen AAEmu 3.0 patch before exact SHA matching selected the
contracted AAEmu 1.2 patch. None mutated Git, evidence, runtime, host, source,
patches, config, database, client, ledgers, or lease.

Changed paths are exactly the byte-identical T-098 receipt and handoff plus
`ops/evidence/aaemu12-t098-fail-integration-v1.yaml` and this handoff. T-099 did
not start or control runtime, observers, database, or client; did not build or
test; did not edit source, tooling, patches, global ledgers, lease, host, config,
or external evidence; and committed no raw logs.

Integration action: fast-forward saved branch `integration/aaemu12-world` only
from `201e515e47a51b3b6798cddcf5cc5d122456eec6` through the commit containing
this receipt and handoff, without history rewriting. PB-000 may then dispatch a
bounded source correction for the active-bot anchor world/instance inconsistency.
Source correction must precede a fresh versioned runtime successor. Never reuse
or overwrite the v5 evidence root, never reinterpret T-098 as PASS, and keep
T-037 blocked until the successor passes and is independently integrated.
