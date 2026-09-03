# T-088 contract: reprove one-zone autonomy with isolated anchored fixtures

## Outcome

Produce one fresh, immutable, live-headless AAEmu 1.2 receipt proving that the
integrated Activity Director admits exactly the configured persistent cohort in
qualified zone `221`; all three identities independently complete two native
one-kill, progression, recovery, normal-self-logout lifecycles; the Director
refills every normal departure; and a clean distinct-PID restart reboots and
holds the same target. This is a corrected successor proof to retained T-041,
not a reinterpretation of it and not a scale, soak, packaging, or client gate.

## Fixed identity and configuration

- Use registered workspaces only: `playerbots_control`, `aaemu12_reference`,
  `aaemu12_integration`, `aaemu12_database_public_alpha_v1`, and
  `aaemu12_t088_evidence_v2`. Never substitute a similar path.
- Require host base `62e3eb1d87da01194802ac886cd500134facad28`,
  installed module source `e284c739ae168fc95fb77a91bf0f23bd5dd2f6a4`,
  installed tree `4b3f96aaedb96ec40c1dc5eef4256efe02cd99f2`, patch
  SHA-256 `baa0979a200eb7f4403985d7166092f937033c31650aa11336fba31543d96606`,
  and Game assembly SHA-256
  `a4508402e8564a6c6807471c9cd6c9e9bc5950e279ea1a9e7841d35a61d35075`.
- Use persistent IDs `[20001, 20002, 20003]`, qualified zone `221`, minimum
  `2`, target `3`, and maximum `3`.
- In the deployed runtime `BotConfig.json`, change only the nine Director
  assignments used by T-041: enabled `true`; fixed zone, IDs, and bounds above;
  initial delay `60000` ms; reconciliation interval `15000` ms; retry backoff
  `30000` ms. Preserve its exact original bytes before editing, require the
  continuity SHA-256
  `7b57a52149f6059cbcd972c38b161c46781d6730dbb3c6b93890171ae8c43202`,
  and restore those exact bytes after final shutdown.
- The fresh evidence root is
  `D:\Codex-Labs\evidence\T-088\one-zone-autonomy-v2`. It must be absent at
  preflight. If it exists, stop; do not overwrite, remove, or rename it and do
  not choose a replacement. T-041's root is immutable and read-only.

## Authority and prohibitions

- Do not start or edit anything until `ops/RUNTIME-LEASE.yaml` names this exact
  task thread and `TASK.yaml` names the exact worktree at one committed Control
  Tower binding. Recheck that commit immediately before preflight.
- The runtime lease may authorize only loopback Login/Game start and graceful
  stop, existing startup/stop scripts, read-only observers and metrics/debug/
  roster queries, a continuous read-only client-process sampler, the six exact
  inert fixture commands below, normal runtime outputs, and the exact deployed
  config edit/restore.
- Never start, stop, query, or otherwise control MySQL or any database. Never
  start or control the ArcheAge client; observe its absence only. Record the
  existing MySQL PID set without connecting to it.
- Never issue `addbot`, `removebot`, `botstate`, activity, destination, target,
  travel, attack, loot, progression, recovery, logout, metrics reset/activity,
  or any command that selects or advances bot gameplay. The Director must
  bootstrap and refill from zero. Do not shrink or clean the cohort by command.
- Do not edit module/host source, tests, tools, scripts, manifests, patches,
  global ledgers/lease, predecessor receipts/evidence, client files, or AAEmu
  3.0. Never delete/reset/clean/uninstall/force-stop anything. Preserve any
  pre-existing runtime logs losslessly inside the fresh evidence root.

## Preflight and continuous observer boundary

Before first startup, retain exact binding/source/config/database-name/process/
port/log/MySQL-PID fingerprints and prove the reference and installed module
states match the accepted T-087 receipt. Create the fresh evidence root once.

Before Login starts, launch a dedicated read-only process sampler beneath the
fresh root. At intervals no greater than one second it must retain timestamped
ArcheAge-client process-count samples plus raw/derived hashes. Its first sample
must precede Login startup, its last sample must follow final cleanup, no
adjacent gap may exceed two seconds, and every count must be zero. Stop it by a
cooperative sentinel/normal exit and wait for completion; never force-stop it.

Start Login then Game through the accepted scale runtime script with the
isolated public-alpha schema names and safety acknowledgement. Once the API is
live but before the Director's 60-second delay expires, start one unchanged
autonomy observer for each fixed bot ID. Each must retain transport bytes, raw
responses, hashes, and derived samples in its own directory and prove two live
offline samples before that identity's first Director admission. Admission
before the observer boundary is non-PASS.

## Counted runtime proof

Retain frequent `botmetrics snapshot` responses including every Director
envelope, authoritative Game/Login logs, and an exact operator-command ledger.
Require once-only Director start, valid configured fields, zero-to-exact-three
admission in order `[20001, 20002, 20003]` one per reconciliation tick, zone
`221` with default world/instance, maximum `3`, and no admission failure,
wrong-zone runtime, overlap, or directed bot command.

At each fresh all-three targetless, noncombat, stationary, full-resource
boundary, issue exactly this three-command batch, once and in this order:

1. `spawnpassive 10004 5 20001`
2. `spawnpassive 10004 5 20002`
3. `spawnpassive 10004 55 20003`

These distances are fixed from retained T-041 bot positions and stay within the
configured 60-meter autonomous search radius. Preserve exact request/response
bytes. Every success response must report its expected `anchorBotId`, zone
`221`, common default instance, and finite position. Before counting gameplay,
derive all three pairwise horizontal fixture distances and require each to be
at least `15.0` meters. If any command fails, audit fields mismatch, or the
separation floor fails, stop at that first material gate; do not add a fixture,
retry, or claim a lifecycle.

The separated batch is shared inert world input, never a target assignment.
Require every configured identity to independently select `grind` for
`nearby_mortal`, navigate, cast/damage, receive exactly one native kill credit
for one distinct batch object, gain positive authoritative progression,
complete any ordered recovery with fixed brain/mover counters while pending,
record exactly one debt-free completion, and normally self-logout. No identity
may receive two credits in one wave; no dead/uncredited fixture counts. Prove
the Director refills all departed identities to the exact target.

At the next fresh all-three safe boundary repeat the same exact three-command
batch. Require the same one-credit lifecycle for all three and a second complete
refill. Across the counted run there must be exactly six fixture commands and
six distinct credited objects, distributed two credits per identity. Stop at
the first invalid wave; never retry or overwrite evidence.

Reconcile observer samples, Director envelopes, metrics, progression records,
fixture audit output, command transport, logs, and continuous client samples.
Require zero tick error, duplicate admission, maximum breach, cross-zone
activation, forced transition, uncredited completion, response/hash failure,
or client process.

## Restart and cleanup

After both accepted waves and the second refill, stop Game then Login through
the graceful Ctrl+C path. Prove zero Game/Login/client processes, free required
ports, once-only Director/BotManager cleanup, and zero retained bots/runtimes.
Move first-run logs losslessly into evidence so the second startup cannot
overwrite them.

Restart Login/Game with distinct PIDs under the same configured Director bytes.
During the 60-second initial delay, pre-arm three fresh observers with two live
offline samples each. Prove normalized persistent roster identity, a second
zero-to-exact-target bootstrap one admission per tick, zone/default-boundary
qualification, maximum `3`, and at least two continuous minutes at reconciled
target without fixtures or gameplay commands.

Stop Game then Login gracefully. If a graceful stop fails, retain the process
and evidence and report the blocker; never force termination. Prove zero bots,
runtimes, Game/Login/client/observer processes, listeners, and live logs;
unchanged observed MySQL PIDs; and exact reference/module/host/observer hashes.
Restore the original deployed config bytes and verify their SHA-256. Then stop
the client sampler cooperatively and prove its complete zero-count/gap matrix.

## Verdict and handoff

Seal the external root with a complete relative-path/length/SHA-256 manifest;
verify zero missing, mismatched, duplicate, unsafe, or unlisted payloads. Commit
only `ops/evidence/aaemu12-t088-one-zone-autonomy-v2.yaml` and
`ops/tasks/T-088/HANDOFF.md`.

The handoff must say `PASS`, `FAIL`, or `INCOMPLETE` and bind source, lease,
config, processes, commands, fixture coordinates/separations/audits, identities,
per-wave lifecycle/progression/refill, restart, continuous client samples,
manifest, cleanup, anomalies, and all unproven boundaries. Do not edit ledgers
or release/reassign the lease. PB-000 alone accepts the handoff and dispatches
independent evidence integration before T-037 may activate.
