# T-116 contract: corrected-count integration with green receipt reuse

Act only after PB-000 commits the exact T-116 task/thread/worktree binding and
build-only lease. T-115 stopped correctly because its contract expected two
new discovered tests, but its sole full-suite execution was green at 1,871
passed, the exact four intentional skips, and zero failed. This task may accept
that execution only by independently proving the contract count was wrong and
the complete build/test fingerprint remains unchanged. Never rerun the full
suite and do not perform a build.

Verify T-114 candidate `3b9824e5250caf90a6347cb286e31deda993a0cf`,
sole parent `d335ce053c58eaebbfda384412ef9be5c9d839da`, tree
`e342e61030ae78ab411fa7cb6a109839199ca610`, and stable patch ID
`f2172268436a17d0642b837ed5e0696b81c316d6`. Verify T-115 replay
`5f544acffdbef48b83278978b68c3f62b1441808`, sole parent
`21ae2ebe9c39205e3fb2874d1eb1b128f6e9e78f`, tree
`6bf7927fc28ca163ffa67da6a280fce4f22d4460`, and the same patch ID. Both
must contain exactly the seven T-114 paths and the exact blobs listed in the
T-115 contract. The registered installed module must be clean/detached at the
T-115 replay/tree before any action.

Read T-115's retained handoff only from
`C:\Users\jensh\.codex\worktrees\e53c\PB-W00-control\ops\tasks\T-115\HANDOFF.md`.
Require exact length `6725` and Git blob
`01af6882c7143960bfa2b2fefb3a0ab05fff52c2`; copy it byte-identically into
this task's commit scope. Audit these immutable logs in place:

- full suite: `D:\Codex-Labs\aaemu-1.2-r208022-integration-v1\AAEmu.UnitTests\bin\T115-full-suite-once.log`,
  length `1404`, SHA-256
  `bb8b3ed89578c304e3114b7885528b09c5bd6811ccd4a4208aaf91f6facccf08`;
- no-incremental build: `D:\Codex-Labs\aaemu-1.2-r208022-integration-v1\AAEmu.UnitTests\bin\T115-no-incremental-solution-build.log`,
  length `65490`, SHA-256
  `5f642388d853a5cd191d089fe00dcc6211143e423ffa4403cb6baf2b021c28a9`.

Parse the logs independently. Require build zero errors and 79 warnings. Require
the sole full suite to report 1,875 total, 1,871 succeeded, four skipped, zero
failed, and exactly these skips:
`Legacy_Darkrunner_Pve_MatchesGolden`,
`Legacy_Darkrunner_Pvp_MatchesGolden`,
`Legacy_Primeval_Pve_MatchesGolden`, and
`Legacy_Primeval_Pvp_MatchesGolden`. Prove there is exactly one T115 full-suite
log and no evidence of a second invocation.

Independently count actual `[Test]` methods in the T-114 candidate parent and
candidate. Require `BotCommandsTests` `46 -> 46` because
`BotDebug_TransformTelemetry_IsInvariantRoundTripAndReadOnly` was strengthened,
not added. Require `BotHostBehaviorTests` `33 -> 34` with the only new method
`MultipleRuntimes_PendingRecoveryCountersStayFixedWhilePeerAndHostAdvance`.
Therefore the exact T-107 full-suite baseline `1,870 + 4` advances by one to
the observed and correct `1,871 + 4`; never reinterpret T-115 as having passed
its own incorrect contract.

Replay the same seven candidate blobs byte-identically onto PB-000's exact
T-116 binding commit, require matching stable patch ID, and prove every build,
test, script, and compatibility input is byte-identical to the retained T-115
replay. Verify the pinned reference, 30-entry host overlay, compatibility patch
SHA-256 `baa0979a200eb7f4403985d7166092f937033c31650aa11336fba31543d96606`,
installed output assemblies, and Game assembly SHA-256
`0b770b543b167f7ae57fcb8eb9ad18357e75a5241b0b170a9b1fa4ac05f564f6`
are unchanged. Move only the clean installed module HEAD to the byte-equivalent
new replay; do not rebuild.

Run a fresh deterministic observer qualification in a new versioned output
path and require exactly 91 assertions. Run the already-built complete
`BotCommandsTests` and `BotHostBehaviorTests` selections without build and
require 46/46 and 34/34. These focused executions must use the unchanged T115
assemblies and must not trigger compilation. Record their reports and hashes.

After all gates pass, commit an artifact containing only the exact T-115
handoff, `ops/evidence/aaemu12-runtime-recovery-observability-integration-v1.yaml`,
and `ops/tasks/T-116/HANDOFF.md` after the seven-path replay. Fast-forward saved
branch `integration/aaemu12-world` through exactly the replay and artifact
commits without history rewrite. Report all identities, corrected discovery
proof, reused log and assembly fingerprints, fresh focused results, no-build
and no-full-suite counts, installed/saved clean state, runtime prohibition, and
remaining physical boundary. PB-000 alone updates global ledgers and releases
the build-only lease before a fresh runtime successor.
