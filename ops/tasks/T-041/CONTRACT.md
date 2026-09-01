# T-041 contract: one-zone Autonomous Activity Director

## Outcome

Produce one fresh, immutable, live-headless AAEmu 1.2 receipt proving that the
integrated Activity Director admits exactly the configured persistent cohort in
qualified zone `221`, restores its population target after normal autonomous
self-logout, and preserves that behavior across a clean distinct-PID restart.
This is a three-bot one-zone proof, not a scale, soak, packaging, or client gate.

## Fixed identity and configuration

- Use registered workspaces only: `playerbots_control`, `aaemu12_reference`,
  `aaemu12_integration`, `aaemu12_database_public_alpha_v1`, and
  `aaemu12_t041_evidence_v1`. Never substitute a similar path.
- Require host base `62e3eb1d87da01194802ac886cd500134facad28`,
  installed module source `2243b53dcbda7a65ab66123c4bce4864d4c743dd`,
  installed tree `731314bf03169ffcc08a383a9652afd9281c75fa`, and
  compatibility patch SHA-256
  `395a83ab5bf6a4f4f1c0d56289590d6a36ad36b3ab5f87e1701d6aa17ffbefcd`.
- Use persistent character IDs `[20001, 20002, 20003]` in that order and
  qualified zone `221`. Configure minimum `2`, target `3`, and maximum `3`.
- In the deployed runtime `BotConfig.json`, change only the nine Director
  properties: enabled `true`, the fixed zone/IDs/bounds above, initial delay
  `60000` ms, reconciliation interval `15000` ms, and retry backoff `30000` ms.
  Preserve the exact original bytes and SHA-256 before editing. After the final
  shutdown, restore those exact bytes and prove the restored SHA-256.
- The fresh evidence root is
  `D:\Codex-Labs\evidence\T-041\one-zone-autonomy-v1`. It must be absent at
  preflight. If it exists, do not overwrite, remove, or choose a replacement;
  stop and report the exact blocker.

## Authority and prohibitions

- Do not start anything until `ops/RUNTIME-LEASE.yaml` names this exact task
  thread and the task file names the exact worktree at one committed Control
  Tower revision. Recheck the committed binding immediately before preflight.
- The committed lease may authorize only loopback Login/Game start and graceful
  stop, the existing startup/stop scripts, concurrent read-only observers,
  read-only roster/debug/metrics commands, bounded inert `spawnpassive 10004 12`
  fixtures, normal runtime outputs, and the exact deployed config edit/restore.
- Never start or control MySQL or the ArcheAge client. Never query or mutate a
  database. Observe existing MySQL PIDs only. Continuously prove zero ArcheAge
  client processes while the gate runs.
- Never issue `addbot`, `removebot`, `botstate`, activity, destination, target,
  travel, attack, loot, progression, recovery, logout, metrics reset/activity,
  or any other command that selects or advances bot gameplay. The Director must
  bootstrap and refill from zero. Do not shrink or clean the cohort by command.
- Do not edit module/host source, tests, scripts, manifests, compatibility
  patches, global ledgers, predecessor receipts/evidence, client files, or
  AAEmu 3.0. Do not delete/reset/clean/uninstall/force-stop anything. Preserve
  literal pre-existing runtime logs by moving them into the fresh evidence root
  before startup; never discard them.

## Preflight and observer boundary

Before the first runtime start, prove and retain:

1. exact lease/task/thread/worktree binding and a clean dispatch head;
2. clean pinned reference plus exact installed module/patch/host fingerprints;
3. selected isolated game/login schema names from local configs, without a
   database connection or query;
4. zero Game/Login/client processes, zero listeners on
   `1234/1237/1239/1250/1280`, zero live runtime-log files after lossless
   preservation, and the observed untouched MySQL PID set;
5. the original and configured deployed `BotConfig.json` bytes and hashes; and
6. the fresh evidence-root absence check before its one-time creation.

Start Login then Game through `scripts/scale/Start-ScaleGateRuntime.ps1` with
the isolated public-alpha schema names and safety acknowledgement. Once the API
is live but before the Director's 60-second initial delay expires, start one
unchanged `scripts/autonomy/Observe-AutonomyBot.ps1` process for each fixed bot
ID. Each observer must retain transport bytes, raw response bytes, hashes, and
derived samples in its own directory and must prove two live offline samples
before that bot's first Director admission. If any bot is admitted before its
observer boundary, the gate is not PASS.

## Counted runtime proof

Retain frequent `botmetrics snapshot` responses, including every
`T081_DIRECTOR` envelope, and authoritative Game/Login logs. The counted proof
must establish all of the following without an `addbot` command:

1. The Director starts once after server readiness, exposes enabled/valid zone,
   IDs, bounds, attempts/successes/failures/backoff, and goes from zero to the
   exact three configured identities one admission per reconciliation tick.
2. Every admitted identity is in zone `221`, default world and default instance;
   no manual or wrong-zone runtime is counted. Qualified population never
   exceeds maximum `3`. Instantaneous logout/refill dips may be reported, but
   every dip must return to target `3` within a derived, evidence-backed bound.
3. After all three bots reach a fresh targetless, noncombat, stationary,
   full-resource observer boundary, stage one bounded batch of exactly three
   inert `spawnpassive 10004 12` opportunities. The batch is shared world input,
   not a target assignment. Require each bot to independently select a live
   `nearby_mortal` grind target, navigate, cast/damage, receive exact native kill
   credit and positive progression, complete any ordered recovery with fixed
   brain/mover counters while pending, record exactly one debt-free completion,
   and normally self-logout.
4. Prove the Director refills the exact missing identities back to target `3`.
   At the next fresh three-bot safe boundary, repeat one second batch of exactly
   three inert opportunities and require the same full accepted lifecycle for
   every bot. Then prove a second complete refill to target `3`.
5. If shared targeting leaves a still-live unused fixture, it may satisfy a
   later bot in the same declared batch. Do not stage more than six fixtures in
   the counted run, do not direct any bot to a fixture, and do not reinterpret a
   dead/uncredited target as a completion. A bot missing either counted wave is
   a retained non-PASS outcome.
6. Reconcile observer samples, Director envelopes, bot/runtime metrics,
   progression records, and authoritative logs. Require zero tick errors,
   duplicate admission, maximum-bound breach, cross-zone activation, forced
   transition, uncredited kill, observer raw-hash failure, or client process.

The six fixtures are the only allowed gameplay-affecting operator commands.
Read-only roster/debug/metrics queries and runtime lifecycle control must be
separately inventoried. Preserve exact request and response bytes.

## Restart and cleanup

After two accepted waves and the second target refill, stop Game then Login by
the graceful Ctrl+C path. Prove zero Game/Login/client processes, free required
ports, and the Director/BotManager once-only cleanup with zero retained bots and
runtimes. Losslessly move the first-run logs into evidence so the guarded second
startup cannot overwrite them.

Restart Login/Game with distinct PIDs under the same configured Director bytes.
During the 60-second initial delay, pre-arm three fresh observers with two live
offline samples each. Prove the same normalized persistent roster identity,
then a second zero-to-exact-target Director bootstrap, one-at-a-time admission,
zone/default-boundary qualification, maximum `3`, and at least two minutes at a
reconciled target without staging a fixture or issuing gameplay commands.

Stop Game then Login gracefully. Prove zero bots/runtimes/processes/client/
listeners/live logs, untouched MySQL PIDs, exact source/host/observer hashes,
then restore and verify the original deployed `BotConfig.json` bytes. If a
graceful stop fails, leave the process running, retain evidence, and report the
blocker; never force termination.

## Verdict and handoff

Write raw evidence only beneath the fresh external root. Seal it with a complete
relative-path/length/SHA-256 manifest and verify zero missing, mismatched, or
unlisted payloads. Commit only the concise sanitized receipt
`ops/evidence/aaemu12-t041-one-zone-autonomy-v1.yaml` and
`ops/tasks/T-041/HANDOFF.md`.

The handoff must say `PASS`, `FAIL`, or `INCOMPLETE`; identify source, lease,
config, processes, commands, identities, per-wave lifecycle/progression/refill,
restart, manifest, cleanup, retained anomalies, and every unproven boundary.
Stop at the first material invalid gate and do not retry or overwrite evidence.
Do not edit global ledgers or release/reassign the lease. PB-000 alone accepts
the handoff, releases the lease, and decides whether T-037 may activate.
