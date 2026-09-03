# T-052 handoff

## Verdict

PASS — both retained error families are post-readiness, shutdown-scoped native
AAEmu 1.2 races. They do not invalidate the observed Game/Web API readiness and
are non-blocking for the next combat/stealth physical attempt. T-050 remains
`INCOMPLETE` for its independent startup-wrapper predicate-conversion failure;
no command API, fixture, combat, stealth, or restart proof was reached.

## Source identity

- Control assignment: detached clean worktree at
  `c7ea086e32351dc64702ce5539f375a6929c8c96`; task owner
  `01a05a9f-d70c-7dd0-9bdb-7e9dd45b4c1f`, worktree `0e7a`.
- Registered clean host reference: `aaemu12_reference` at
  `62e3eb1d87da01194802ac886cd500134facad28`.
- Registered installed module: commit
  `78e75fc8995d871de56f761cc45fe4fd71b2ae3e`, tree
  `92d100838dd4b963edc25245729ea29759deb05a`, clean.
- The traced `BaseBaiLoader`, `ClientFileManager`, `TaskManager`,
  `WorldManager`, `DoodadFuncFinalTask`, and `Transform` files are unchanged
  from the pinned host. The installed `GameService` delta adds PlayerBots
  cleanup before the native shutdown sequence; its retained marker reported
  `remaining_bots=0 remaining_runtimes=0`.

## Evidence identity and chronology

- Receipt `ops/evidence/aaemu12-t050-combat-stealth-v2.yaml`: SHA-256
  `903756f2d50336fa74627f415de7167936f48d39dbbc7237f3753d78f4459b0d`.
- `game-attempt-1-server.log`: SHA-256
  `97fcffdf5dea3f9f3bbac948773eca3e83d38dfba91cb0feba5667548a718dec`.
- `game-attempt-1-error.log`: SHA-256
  `748be933f77b36eb1ab7db2be76df2572fdf088c3043f7d5881f1f0c3f6eb6da`.
- At `19:30:45`, Game network, stream network, and `GameService - Server
  started!` were present, followed by `WebApi server started on
  127.0.0.1:1280`.
- Also at `19:30:45`, graceful shutdown began: Web API stopped,
  `GameService - Stopping daemon...`, PlayerBots zero cleanup, listener/network
  stops, and both world shutdowns. `GameService - Disposing...` was logged at
  `19:30:46`.
- The error log contains exactly two `BaseBaiLoader` collection-modification
  errors and exactly two `WorldManager - GetWorld: No such World Instance 0`
  fatal entries, all at `19:30:46`. Neither family occurs in the server log or
  before readiness/shutdown. Because the files are separate sinks with
  one-second timestamps, their order relative to `Disposing...` within
  `19:30:46` is not provable; they definitively follow shutdown initiation and
  the zero-bot cleanup marker.

## Findings

### BaseBaiLoader collection modification — non-blocking

Direct source evidence: `BaseBaiLoader.LoadBaiFilesFromFolder` catches a broad
exception and logs only `ex.Message` at
`AAEmu.Game/Models/CryEngine/Loaders/BaseBaiLoader.cs:192`; its file discovery
calls `ClientFileManager.GetFilesInDirectory`. That method enumerates the mutable
`Sources` list at `AAEmu.Game/IO/ClientFileManager.cs:157`, while
`ClientFileManager.ClearSources` removes entries from that same list at lines
68-75. `GameService.StopAsync` calls `TaskManager.Stop`, tears worlds down, and
then clears sources. Native `TaskManager.Stop` is a no-op with a TODO to wait for
running tasks, and dispatch uses `Task.Run`. The retained server log confirms
BAI loads and doodad tasks continued after `Stopping daemon...`.

Inference: two in-flight BAI loaders enumerated `Sources` while shutdown removed
its entries, producing the standard list-enumerator exception twice. The absent
stack traces prevent proving the precise enumerator, but the timing, message,
and concrete collection lifecycle agree. This is cleanup noise after readiness,
not a startup or PlayerBots combat/stealth defect.

### World instance 0 missing — non-blocking

Direct source evidence: `WorldManager.GetWorld` emits this exact fatal when
`_worlds.TryGetValue` fails at
`AAEmu.Game/Core/Managers/World/WorldManager.cs:670-675`;
`WorldManager.Stop` clears `_worlds` at line 1303. Since `TaskManager.Stop` does
not stop or await dispatched work, late work can query instance `0` after that
clear. Concrete native paths include the only direct `GetWorld` call under
`Models/Tasks`, `DoodadFuncFinalTask.cs:50`, and transform rebinding at
`Models/Game/World/Transform/Transform.cs:75`.

Inference: two late doodad/world operations queried the default instance after
`_worlds.Clear()`. The error-only sink retained no stack, so the exact caller
between those concrete paths is ambiguous. The entries occur only after
readiness and graceful teardown began, and the receipt proves both processes
exited with all five required ports free; they do not invalidate runtime
readiness or the planned gate.

## Runtime state and retained boundary

No runtime, client, or database was accessed by T-052. T-050's retained final
state remains zero Game/Login processes, ports `1234`, `1237`, `1239`, `1250`,
and `1280` free, no runtime logs left in runtime paths, isolated databases and
configs retained unchanged, and no direct database query or mutation.

No pre-v3 source correction is required for these two families. For v3, retain
the Game server/error logs and classify any recurrence against the ordered
markers `Server started`, `WebApi server started`, `Stopping daemon`, and final
process/port cleanup. Escalate to a bounded native shutdown task only if either
family appears before shutdown initiation or cleanup/restart fails; resolving
the exact post-shutdown caller alone is not a prerequisite for combat/stealth.

## Changed files and proof

- Changed only `ops/tasks/T-052/HANDOFF.md`.
- Proof: read-only SHA-256 verification, exact occurrence counts and lifecycle
  correlation in the two contracted logs, registered source identity checks,
  and direct inspection of the pinned-host/installed-module paths above.
- No build or test run was required or authorized; no runtime state changed.

## Exact PB-000 action

Accept this analysis as T-052 complete and non-blocking, integrate the accepted
T-051 correction through T-053, and dispatch T-054 as the next immutable v3
physical attempt with the lifecycle-scoped error observation above. No separate
pre-v3 source-fix task is warranted by the retained errors.
