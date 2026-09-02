# T-116 handoff

Verdict: `PASS-corrected-count-green-receipt-reuse-integration`. T-115 remains
`BLOCKED-full-suite-count-mismatch` under its own contract. T-116 independently
proved that T-114 added one discovered test, reused T-115's unchanged green
build/full-suite fingerprint, replayed the same seven blobs, and passed all
fresh no-build focused gates without a build or full-suite rerun.

## Source identity

- Preparation/binding/tree:
  `0ff36bf5a512602cd1f14e3685c1cbab98ac513b` /
  `ad05f0235ae7fee4723de0259a3b13267876012d` /
  `fd53ec5bc7f60cd6e4a9ddc7cf35e908fdbcd165`.
- T-114 candidate/parent/tree:
  `3b9824e5250caf90a6347cb286e31deda993a0cf` /
  `d335ce053c58eaebbfda384412ef9be5c9d839da` /
  `e342e61030ae78ab411fa7cb6a109839199ca610`.
- Retained T-115 replay/parent/tree:
  `5f544acffdbef48b83278978b68c3f62b1441808` /
  `21ae2ebe9c39205e3fb2874d1eb1b128f6e9e78f` /
  `6bf7927fc28ca163ffa67da6a280fce4f22d4460`.
- T-116 replay/parent/tree:
  `39f748fb3904584b50e1dabc0cfb0b3045793165` /
  `ad05f0235ae7fee4723de0259a3b13267876012d` /
  `7a9b2c3296bb5aee03c0016a4a7a72bb4c75073d`.
- Candidate, T-115 replay, and T-116 replay stable patch ID:
  `f2172268436a17d0642b837ed5e0696b81c316d6`.

All three changes contain exactly the contracted seven paths with exact blob
identity. Every build, test, script, and compatibility input is byte-identical
between the T-115 and T-116 replays. The artifact commit containing this
handoff is reported after creation because it cannot contain its own identity.

## Corrected discovery and retained execution proof

- `BotCommandsTests` is `46 -> 46`; no method was added or removed.
  `BotDebug_TransformTelemetry_IsInvariantRoundTripAndReadOnly` was strengthened
  in place.
- `BotHostBehaviorTests` is `33 -> 34`; the only new method is
  `MultipleRuntimes_PendingRecoveryCountersStayFixedWhilePeerAndHostAdvance`.
- The exact T-107 baseline `1,870 passed + 4 skipped` therefore advances by one
  to `1,871 passed + 4 skipped`, not T-115's contracted `1,872 + 4`.

The retained T-115 handoff was copied byte-identically from its required
worktree: length `6725`, Git blob
`01af6882c7143960bfa2b2fefb3a0ab05fff52c2`. Its blocked verdict is unchanged.

The immutable T-115 build log is length `65490`, SHA-256
`5f642388d853a5cd191d089fe00dcc6211143e423ffa4403cb6baf2b021c28a9`,
and independently parses to one successful build, 79 warnings, zero errors.
The sole full-suite log is length `1404`, SHA-256
`bb8b3ed89578c304e3114b7885528b09c5bd6811ccd4a4208aaf91f6facccf08`,
with one summary: 1,875 total, 1,871 succeeded, four skipped, zero failed. The
skips are exactly the Darkrunner PvE/PvP and Primeval PvE/PvP legacy golden
cases. Exactly one T-115 full-suite log exists and no second invocation was
found.

## Installation and fresh proof

The pinned reference remains clean/detached at
`62e3eb1d87da01194802ac886cd500134facad28`. Strict reference apply-check and
complete host reverse-check passed. The integration host retains exactly 30
status entries: 28 compatibility paths plus the migration and `modules/`
entries. Compatibility patch SHA-256 remains
`baa0979a200eb7f4403985d7166092f937033c31650aa11336fba31543d96606`.

Only the clean installed-module pointer moved, from the T-115 replay/tree to
the T-116 replay/tree. It remains clean and detached. No build ran. The Game
assembly SHA-256 remains
`0b770b543b167f7ae57fcb8eb9ad18357e75a5241b0b170a9b1fa4ac05f564f6`;
the test assembly and executable remain
`b26876024985b334a28a540681645336ad8b81ef8f9a8a7f4644f8e101adf29e`
and `ff3585bfdb955a2ebb04bbfdd48b6e7f3ab6a9758336be969d426520f6ea6e52`.
The 134-file Game and 234-file test binary manifests were unchanged before and
after focused execution.

Fresh verification:

- Observer: exactly 91 assertions; log SHA-256
  `0599174720ad450002d73adc98363fce53e0a49abf8bff4513583b4b8add017b`;
  18-artifact manifest SHA-256
  `60427417de8bd136811e0e81f450cbfb6bbf8b7a551e93faf868edfa8cad7c48`.
- Direct prebuilt `BotCommandsTests`: 46 passed, zero failed/skipped; log/report
  SHA-256 `59fa343ae4100f1478059ea6758a12d28c4c5b0272d61b167f85aec4ded4556a` /
  `3560bee8c6b6a213c57d3ae634d73d74d205a6526c6a95e87cc78fd6500761f7`.
- Direct prebuilt `BotHostBehaviorTests`: 34 passed, zero failed/skipped;
  log/report SHA-256
  `41e6d90f0e3016d4b5c4a0bed672b6c6f53ffa2b276678fe5d83b9a155ea8c4a` /
  `7a7f72ced3faf086f9bd8560c7f068979c267c978efa6a81714ed6a36661177b`.

T-116 build invocations: `0`. T-116 full-suite invocations: `0`. Reused T-115
full-suite invocations: exactly `1`.

## Changed files and exact integration action

Replay commit `39f748fb3904584b50e1dabc0cfb0b3045793165` contains only the
seven T-114 paths. The following artifact commit must contain only:

- this `ops/tasks/T-116/HANDOFF.md`;
- `ops/evidence/aaemu12-runtime-recovery-observability-integration-v1.yaml`;
- the exact retained `ops/tasks/T-115/HANDOFF.md`.

Fast-forward saved branch `integration/aaemu12-world` from the exact binding
through the replay and then this three-path artifact commit, with no merge,
reset, or history rewrite. The installed module stays at the replay commit,
not the artifact commit. PB-000 alone updates the global ledgers and releases
the build-only lease.

## Runtime state and unproven boundaries

T-116 did not start or control Login, Game, a live observer, database, MySQL,
or client; it did not edit deployed config or external runtime evidence. T-112
remains an immutable independently integrated FAIL, and T-115 remains blocked
under its own incorrect count contract.

Fresh physical per-runtime sampling, both recovery waves, cooperative
shutdown-tail observer closure, distinct-PID restart/rebootstrap, dwell, scale,
soak, packaging, release readiness, and AAEmu 3.0 release-boundary compatibility
remain unproven. A fresh runtime successor requires a new PB-000 lease after
this integration is recorded.
