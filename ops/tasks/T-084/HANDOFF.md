# T-084 handoff

Verdict: `PASS-integration-install-build-focused-full-unit`. The exact T-083
serialized Activity Director replacement passed independent product and
concurrency review, was replayed byte-identically onto the Control Tower lease
lineage, installed in the registered AAEmu 1.2 checkout, and qualified without
starting or controlling a runtime. T-041 may proceed after PB-000 releases or
reassigns the lease with fresh exact runtime authority.

## Source identity

- Exact build-only lease and integration parent:
  `5969b84f4aef6b1ade18b82a77d431dae8cbce13`, bound to T-084 thread
  `01a05d70-6f07-7b61-abad-eafc8d12a307`.
- T-083 replacement/parent/tree:
  `b1ffc8006aaa4a4eb4da54a4993057eb8969df96` /
  `dd09b2d6b655f92cd5afa36cd70e7d08b9efd877` /
  `f45f406efbaf68f683e08c36cd5b05e0d28a1031`.
- Rejected T-081 source retained and not merged:
  `370a2ee0cff17ea7b56c9a06dc069ad789245de0`.
- Independently replayed, tested, and installed source/tree:
  `2243b53dcbda7a65ab66123c4bce4864d4c743dd` /
  `731314bf03169ffcc08a383a9652afd9281c75fa`.
- Candidate/replay stable patch ID: `605881a2f64212cfa68bc21e8a851b1074dfba0c`.
  Compatibility patch SHA-256:
  `395a83ab5bf6a4f4f1c0d56289590d6a36ad36b3ab5f87e1701d6aa17ffbefcd`;
  both manifest declarations match. Installed host patch stable ID:
  `51cf303462d170dd61551ee2f4d410ac0e293ce8`.

## Review, installation, and proof

The replacement changes exactly 14 declared paths. Eleven preserved T-081
blobs match byte-for-byte; within the 28-section compatibility patch only
`GameService.cs` and `GameServiceTests.cs` differ from the rejected source.
Review passed bounds/refill/backoff, default-world/zone qualification, no
shrink, wrong-boundary normal cleanup, unforced lifecycle, manual/wrong-zone
isolation, visibility, fail-closed defaults, and graceful shutdown.

One service-owned lifecycle lock/state machine now covers construction and
publication, `TryStart`, scheduler result publication, cancellation, Director
stop, state clearing, and once-only `BotManager.Stop()`. The six deterministic
barrier cases close both T-082 races, repeated concurrent starts/stops, and
normal shutdown ordering; no Director tick acquires the service lock.

The installed module fast-forwarded from `68aaaa3334a408d1d6d21e44472a8984e78618c2`
to the tested source. The exact prior host patch was reversed and the strict-
checked replacement patch applied; the host remains at pinned commit
`62e3eb1d87da01194802ac886cd500134facad28` with 30 receipted status entries.
Installer check-only passed before and after exactly one normal idempotent
installer invocation.

- Complete solution build: `0` errors, `79` retained warnings.
- Final focused selection: `154` unique passed, `0` skipped, `0` failed,
  including all `15` service/concurrency tests. There were `187` passing
  executions because the 33 host tests ran once under an incorrectly high
  minimum-count probe and then once under the corrected green selection.
- Full AAEmu 1.2 suite, exactly one invocation: `1,830` passed, `4` intentional
  legacy-golden skips, `0` failed (`1,834` total).
- Sanitized receipt:
  `ops/evidence/aaemu12-activity-director-integration-v1.yaml`.

No product or gate failure is retained. Operator-only probes recorded in the
receipt include the corrected reserved-variable/readirection checks, the
installer's stale native exit-code quirk after printed success, and the
corrected host-test minimum. Existing dependency/compiler/analyzer warnings
remain, including the SQLitePCLRaw and SSH.NET advisories.

## Runtime state and boundaries

No Login, GameServer, MySQL, database, client, bot runtime, or live server was
started, stopped, controlled, accessed, or queried. Game/Login/client process
counts are zero; ports `1234/1237/1239/1250/1280` are free. Pre-existing MySQL
PIDs `6308` and `8076` were observed and left wholly untouched.

This PASS proves source/install/build/unit behavior only. Physical one-zone
refill, multi-bot autonomous lifecycle, runtime persistence, scale, soak,
packaging, release readiness, and AAEmu 3.0 remain unproven.

## Exact integration action

Fast-forward saved branch `integration/aaemu12-world` from lease commit
`5969b84f4aef6b1ade18b82a77d431dae8cbce13` through the commit containing this
receipt and handoff without rewriting history. PB-000 may then release the
T-084 build-only lease and dispatch T-041 with a fresh exact runtime lease
against installed source `2243b53dcbda7a65ab66123c4bce4864d4c743dd`
and tree `731314bf03169ffcc08a383a9652afd9281c75fa`. T-037 remains blocked until
T-041 passes.
