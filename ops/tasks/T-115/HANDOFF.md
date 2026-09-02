# T-115 handoff

Verdict: `BLOCKED-full-suite-count-mismatch`. The exact T-114 replay was
installed and every observer, focused, and build gate passed, but the one
permitted full-suite invocation reported `1,871` passed, four skipped, zero
failed (`1,875` total), not the contracted `1,872` passed plus four skipped
(`1,876` total). The full suite was not repeated.

## Source identity

- Preparation/binding: `e47bcf03468bdeeee73877c95936a1a969e58eb5` /
  `21ae2ebe9c39205e3fb2874d1eb1b128f6e9e78f` (binding tree
  `ccbe2a00d5cc75d3992d0f318ba1934568190460`). The binding exactly names task
  `01a0604f-ad55-70f2-a138-001a1b6fd92f`, client task
  `client-new-thread:4d5d7429-1e31-4234-b650-40d3ffee5906`, and this worktree.
- Candidate/sole parent/tree:
  `3b9824e5250caf90a6347cb286e31deda993a0cf` /
  `d335ce053c58eaebbfda384412ef9be5c9d839da` /
  `e342e61030ae78ab411fa7cb6a109839199ca610`.
- Exact replay/sole parent/tree:
  `5f544acffdbef48b83278978b68c3f62b1441808` /
  `21ae2ebe9c39205e3fb2874d1eb1b128f6e9e78f` /
  `6bf7927fc28ca163ffa67da6a280fce4f22d4460`.
- Candidate and replay stable patch ID:
  `f2172268436a17d0642b837ed5e0696b81c316d6`.
- Candidate and replay contain exactly the contracted seven paths and all seven
  blobs match the contract. No writer branch was merged and `git diff --check`
  passed.

The production diff is limited to one invariant per-runtime `botdebug` line
and the observation-only schema-v2 parser. No lifecycle, scheduler, mover,
brain, combat, recovery, logout, or aggregate host-metric semantic source
changed.

## Changed files

Replay commit `5f544acffdbef48b83278978b68c3f62b1441808` changes exactly:

- `src/AAEmu.Game/Scripts/Commands/BotDebugCommand.cs`
- `scripts/autonomy/AutonomyObserver.psm1`
- `tests/AAEmu.UnitTests/Game/Scripts/Commands/BotCommandsTests.cs`
- `tests/AAEmu.UnitTests/Bots/Host/BotHostBehaviorTests.cs`
- `scripts/autonomy/tests/Test-AutonomyObserver.ps1`
- `ops/evidence/aaemu12-runtime-recovery-observability-v1.yaml`
- `ops/tasks/T-114/HANDOFF.md`

The retained working-tree change is only this `ops/tasks/T-115/HANDOFF.md`.
The contracted integration receipt path remains absent.

## Installation and successful gates

- The reference remained clean and detached at
  `62e3eb1d87da01194802ac886cd500134facad28`.
- Strict reference apply-check and complete integration-host reverse-check
  passed with `--whitespace=error-all`.
- The registered host retained exactly 30 status entries: the accepted 28
  compatibility-patch paths plus only the migration and `modules/` entries.
- The installed module moved cleanly from source/tree
  `037b4a87dd25df74fc8db5506c1cbc7fe3301b44` /
  `a1b9302625a65e10dfa9b7e11393a67134f914e8` to exact replay source/tree
  `5f544acffdbef48b83278978b68c3f62b1441808` /
  `6bf7927fc28ca163ffa67da6a280fce4f22d4460`; it remains clean and detached.
- Compatibility patch SHA-256 remained
  `baa0979a200eb7f4403985d7166092f937033c31650aa11336fba31543d96606`;
  installer check-only reported `installed/supported`.
- Deterministic observer qualification: exactly 91 assertions passed, including
  the fixed route, command, parameter, type, and AST allowlists.
- Complete `BotCommandsTests`: 46 passed, zero failed, zero skipped in 1.731s.
- Complete `BotHostBehaviorTests`: 34 passed, zero failed, zero skipped in
  0.782s. The multi-runtime test passed pending deltas `0/0`, advancing peer
  and aggregate host deltas `+3/+3`, full MP restoration, and exactly one
  pending-runtime logout.
- Preparatory test-project build: zero errors, 72 warnings in 38.54s.
- The single no-incremental solution build passed with zero errors and 79
  retained warnings in 32.24s. Warning families were `NU1903`, `CS0169`,
  `CS0618`, `CS8632`, `CS9113`, `CA1852`, `CA1859`, `CA2000`, `TUnit0023`,
  `TUnit0046`, and `TUnitAssertions0005`; the complete log is retained.
- Game assembly SHA-256:
  `0b770b543b167f7ae57fcb8eb9ad18357e75a5241b0b170a9b1fa4ac05f564f6`.

## Retained blocker

Exactly one full suite was invoked:

- actual: 1,871 passed, four skipped, zero failed, 1,875 total;
- required: 1,872 passed, four skipped, zero failed, 1,876 total;
- exact skips: `Legacy_Darkrunner_Pve_MatchesGolden`,
  `Legacy_Darkrunner_Pvp_MatchesGolden`, `Legacy_Primeval_Pve_MatchesGolden`,
  and `Legacy_Primeval_Pvp_MatchesGolden`.

Read-only source reconciliation found 46 `[Test]` methods in
`BotCommandsTests` in both candidate parent and candidate; T-114 strengthens
the existing `BotDebug_TransformTelemetry_IsInvariantRoundTripAndReadOnly`
case rather than adding a case. `BotHostBehaviorTests` changes from 33 to 34
`[Test]` methods by adding
`MultipleRuntimes_PendingRecoveryCountersStayFixedWhilePeerAndHostAdvance`.
Thus the exact candidate supplies one new discovered test, matching the actual
T-107 total delta from 1,874 to 1,875. T-115 may not reinterpret or amend the
contract, add source, or repeat the full suite, so this remains a gate failure.

The full-suite log SHA-256 is
`bb8b3ed89578c304e3114b7885528b09c5bd6811ccd4a4208aaf91f6facccf08`;
the no-incremental build log SHA-256 is
`5f642388d853a5cd191d089fe00dcc6211143e423ffa4403cb6baf2b021c28a9`.
Those retained logs are respectively
`D:\Codex-Labs\aaemu-1.2-r208022-integration-v1\AAEmu.UnitTests\bin\T115-full-suite-once.log`
and
`D:\Codex-Labs\aaemu-1.2-r208022-integration-v1\AAEmu.UnitTests\bin\T115-no-incremental-solution-build.log`.
No T-115 integration receipt or artifact commit was created. Saved branch
`integration/aaemu12-world` remains clean at the binding
`21ae2ebe9c39205e3fb2874d1eb1b128f6e9e78f` and was not advanced.

## Runtime state and unproven boundaries

No Login, Game, live observer, MySQL/database, or ArcheAge client was started,
stopped, controlled, or accessed. Deployed runtime configuration and external
runtime evidence were untouched. Final Game/Login process count and occupied
required-port count are zero; ports `1234/1237/1239/1250/1280` are free.

T-112 remains an immutable independently integrated runtime FAIL. Physical
per-runtime sampling, both recovery waves, shutdown-tail closure, distinct-PID
restart/rebootstrap, dwell, scale, soak, packaging, release readiness, and
AAEmu 3.0 remain unproven. T-037 remains blocked.

## Exact next action

PB-000 must retain this T-115 worktree, replay commit, clean installed module,
and once-only full-suite evidence, then decide whether the count contract is
corrected to the exact candidate's one-test delta or a new source candidate is
required. Any new full-suite invocation requires a fresh committed task and
lease; this T-115 task must not rerun it. Do not fast-forward the saved branch
through the replay unless a newly authorized integration task resolves the
count gate.
