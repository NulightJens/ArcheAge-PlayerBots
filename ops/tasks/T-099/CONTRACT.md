# T-099 contract: integrate retained T-098 fixture-anchor failure

Act only as the Integrator for the exact T-098 candidate
`e7361e10c8c1d722c3f18481af07e6173030fb30`, whose parent is binding commit
`5d5919e56cd980d0db2ad1907178ba2a59868eb7` and whose tree is
`b428927a3d428721d6a5e8ce0651b5013bee6903`. Require the candidate to change
exactly `ops/evidence/aaemu12-t098-one-zone-autonomy-v5.yaml` and
`ops/tasks/T-098/HANDOFF.md`. Replay it without rewriting onto the exact saved
lineage, then add only this task's integration receipt and handoff.

Independently validate the immutable root
`D:\Codex-Labs\evidence\T-098\one-zone-autonomy-v5`. Require manifest SHA-256
`1ab0074aad22af52abcef062982a10ad7c48146facabdde092e691c341127e42`,
length `2507986`, exactly `12277` payload files and `33980701` payload bytes,
and zero missing, mismatched, duplicate, unsafe, unlisted, or reparse payloads.
Recompute payload length/hash checks from the manifest. Revalidate all 1,752
client raw files and ledger-chain rows, terminal hash, zero client/error rows,
maximum gap `661.367 ms`, and first-before-Login/last-after-cleanup boundaries.
Revalidate every referenced raw sample for each of the three autonomy observers.

From authoritative retained logs and bytes, independently prove only what the
FAIL receipt claims: all observers pre-armed; Director delay `180055.392 ms`
inside `[180000,195000]`; admissions `[20001,20002,20003]` one per tick with
zero failures/wrong-zone/overlap; one exact request
`spawnpassive 10004 5 20001`; response `Active bot anchor 20001 has an
inconsistent world or instance boundary.`; no spawned fixture, retry, later
fixture, directed gameplay, lifecycle wave, or restart. Retain the `12.163 m`
20001/20002 anchor separation as a risk, not fixture-separation proof.

Independently validate graceful Game-then-Login shutdown, cooperative observer
stops, exact config restoration, zero final relevant processes/listeners/live
logs/bots/runtimes, unchanged observed MySQL PIDs without database access, and
exact host/source/overlay identities. Do not run builds/tests or touch runtime,
database, client, source, patches, global ledgers/lease, or external evidence.

Fast-forward the saved `integration/aaemu12-world` branch only after every gate
passes. Commit `ops/evidence/aaemu12-t098-fail-integration-v1.yaml` and
`ops/tasks/T-099/HANDOFF.md`, clearly recording T-098 as retained FAIL and
keeping T-037 blocked. Report replay commit/tree, final integration head,
manifest/client/observer validation, retained anomaly, and exact downstream
action: source correction must precede a fresh versioned runtime successor.
