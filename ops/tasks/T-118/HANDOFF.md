# T-118 handoff: interactive PlayerBots live demo

## Verdict

`CLEANUP-COMPLETE`. After the user explicitly ended the old interactive session,
the ArcheAge client and launcher were closed normally, Game and then Login were
stopped through the accepted graceful Ctrl+C paths, the exact original
BotConfig bytes were restored, all five listeners became free, and both live-log
directories were left empty without deleting logs.

The earlier live-demo handoff had one retained verification limitation: the
user stopped the initial post-launch Computer Use check with the physical
Escape key. Launcher process and top-level window presence were then verified
through read-only OS metadata. During cleanup, Computer Use independently
observed the running client and launcher and closed both normally.

## Cleanup receipt

- Authorization: the user explicitly said `ready` to PB-000 after being told
  this would end T-118 for the quest-intake rebuild.
- Cleanup snapshot: `2026-09-02T08:14:51.5858490Z`.
- Client: ArcheAge PID `60880` closed normally through Computer Use; final
  process count `0`.
- Launcher: AAEmu Launcher PID `62516` closed normally through its returned
  `btnClose` UI control; final process count `0`.
- Game: PID `23160` exited after the accepted graceful Ctrl+C wrapper. The log
  records Director `reason=graceful_shutdown` and PlayerBots shutdown cleanup
  with `remaining_bots=0` and `remaining_runtimes=0`.
- Login: PID `103064` exited after Game through the accepted graceful Ctrl+C
  wrapper; the log records application shutdown, daemon stop, and internal
  listener stop.
- Final relevant-process inventory: zero `AAEmu.Game`, `AAEmu.Login`,
  `archeage`, `AAEmu.Launcher`, and PlayerBots observer processes.
- Final listener counts: ports `1234`, `1237`, `1239`, `1250`, and `1280` each
  have count `0` and no owning PID.
- Final deployed BotConfig length/SHA-256: `1772` bytes /
  `7b57a52149f6059cbcd972c38b161c46781d6730dbb3c6b93890171ae8c43202`.
- Preserved source length/SHA-256: `1772` bytes /
  `7b57a52149f6059cbcd972c38b161c46781d6730dbb3c6b93890171ae8c43202`.
- Final deployed and preserved hashes match exactly.
- Final live-log counts: Game `0`; Login `0`.
- Six session logs were retained rather than deleted under
  `D:\Codex-Labs\sessions\T-118\interactive-live-demo-v1\cleanup-v1\logs`.
- No database, source, client fixture, reference checkout, global ledger, or
  evidence root was accessed for mutation.
- Retained runtime anomalies: one earlier malformed CommandController request
  produced a missing-JSON-property exception, and graceful Game shutdown
  emitted one native PhysicsManager disposed-service-provider tail error. Both
  are retained in the session logs; neither prevented graceful exit, zero bot
  cleanup, zero processes, or zero listeners.

## Source identity and scope

- Task owner thread: `01a06077-2a1b-7733-be91-e4bae28f2d55`.
- Task worktree: `C:\Users\jensh\.codex\worktrees\1d32\PB-W00-control`.
- Worker base/preparation commit: `10f7dbafbb34876fb865c695b584ab3e1bf923a7`.
- Exact binding commit: `85776c50ae8cf7d50c460a2b47f32965b3bacd4b`;
  sole parent `10f7dbafbb34876fb865c695b584ab3e1bf923a7`; tree
  `bc02254e2e3c9fc384821d1f5f6dfc43670243d4`.
- Host commit: `62e3eb1d87da01194802ac886cd500134facad28`.
- Installed module source/tree:
  `39f748fb3904584b50e1dabc0cfb0b3045793165` /
  `7a9b2c3296bb5aee03c0016a4a7a72bb4c75073d`.
- Compatibility patch SHA-256:
  `baa0979a200eb7f4403985d7166092f937033c31650aa11336fba31543d96606`.
- Installed Game assembly SHA-256:
  `0b770b543b167f7ae57fcb8eb9ad18357e75a5241b0b170a9b1fa4ac05f564f6`.
- Registered client executable SHA-256:
  `a6fbd9ff58bf749fd995489f34acb01e1bc84d4d5a533721b0c017562766fc2a`.
- Repository change: only `ops/tasks/T-118/HANDOFF.md`.
- No source, tests, global ledgers, database, reference checkout, client fixture,
  or retained evidence root was written. No build or test command was run.

## Reversible interactive configuration

- Deployed config:
  `D:\Codex-Labs\aaemu-1.2-r208022-integration-v1\AAEmu.Game\bin\Debug\net10.0\Configurations\BotConfig.json`.
- Exact original bytes retained at:
  `D:\Codex-Labs\sessions\T-118\interactive-live-demo-v1\config\BotConfig.original.bin`.
- Preserved length/SHA-256: `1772` bytes /
  `7b57a52149f6059cbcd972c38b161c46781d6730dbb3c6b93890171ae8c43202`.
- Historical interactive deployed SHA-256:
  `b2c205aa44c42043a59a0d2f21347952dc58c6fb51a41e90e4a9b2dfa8977470`.
- Historical live assignments: enabled `true`, zone `221`, character IDs
  `[20001,20002,20003]`, population `2/3/3`, initial delay `2000` ms,
  reconciliation interval `5000` ms, retry backoff `30000` ms.
- Cleanup restored the deployed file from the preserved exact bytes. Its final
  SHA-256 is the original
  `7b57a52149f6059cbcd972c38b161c46781d6730dbb3c6b93890171ae8c43202`.
- `SearchRadius` is restored to and remains `60`; all interactive Director
  changes are gone.

## Live runtime startup proof (historical)

- Session root:
  `D:\Codex-Labs\sessions\T-118\interactive-live-demo-v1`.
- Accepted ordered startup wrapper receipt:
  `D:\Codex-Labs\sessions\T-118\interactive-live-demo-v1\runtime\start.json`.
- Login PID `103064` owned loopback listeners `1234` and `1237` during the
  session; it is now stopped.
- Game PID `23160` owned loopback listeners `1239`, `1250`, and `1280` during
  the session; it is now stopped.
- Game status API: `GET http://127.0.0.1:1280/status` returned HTTP `200`;
  latest snapshot reported three players online.
- Login and Game error logs contained zero lines; their server logs contained
  zero `ERROR` or `FATAL` entries at startup verification.
- Director started valid and enabled in zone `221` at
  `2026-09-02T05:01:59.2545707Z`.
- Admission `20001`: `spawn_succeeded` at
  `2026-09-02T05:02:01.3337190Z`.
- Admission `20002`: `spawn_succeeded` at
  `2026-09-02T05:02:06.3859137Z`.
- Admission `20003`: `spawn_succeeded` at
  `2026-09-02T05:02:11.4084065Z`.
- Latest observed Director tick:
  `live_qualified=3`, `attempts=3`, `successes=3`, `failures=0`,
  `last_result=steady`, `reason=target_satisfied`.

## Launcher and user boundary (historical)

- Launcher executable:
  `D:\AAEmu\Launcher\AAEmu.Launcher\AAEmu.Launcher.exe`.
- Launcher PID `62516` was responding during the session and is now closed.
- Read-only OS metadata reports top-level title `AAEmu Launcher`, window handle
  `13112516`, and the expected executable path.
- The registered client junction still targets
  `D:\AAEmu\Client\ArcheAge 1.2 (r208022) for AAEmu`.
- The launcher was opened through the computer-use skill. The user pressed
  Escape before the skill could return its post-launch window list/screenshot;
  no authentication dialog, credential field, login/play action, or in-game
  control was automated.

The client was observed in game before cleanup. T-118 did not automate
authentication or gameplay, and cleanup did not enter, read, or transmit
credentials. The client and launcher are now closed.

## Lease and retained boundaries after cleanup

- The committed binding still assigns the `aaemu12` lease to T-118 under commit
  `85776c50ae8cf7d50c460a2b47f32965b3bacd4b`; the runtime is now clean and
  ready for PB-000 to release or transfer that lease in a Control Tower commit.
- Login, Game, ArcheAge, the launcher, PlayerBots observers, and all five
  listeners are absent. The original config is restored and live-log locations
  are empty.
- T-117's retained v10 evidence root was inspected read-only, not reused, and
  not written.
- The interactive session was not a qualification run; user-driven gameplay
  remains outside the proof boundary.
- Retained anomaly: launcher window presence is proven by OS metadata, not by a
  post-launch computer-use screenshot, because the user interrupted Computer
  Use with Escape.

## Exact integration action

No source integration is requested. PB-000 should cherry-pick only this updated
single-file handoff commit onto the control history after binding commit
`85776c50ae8cf7d50c460a2b47f32965b3bacd4b`, record T-118 cleanup complete,
release or transfer the now-clean `aaemu12` lease, and unblock the prepared
quest-intake integration without replaying or deleting any retained session
artifact.
