# T-114 handoff

Verdict: `PASS-source-focused`.

## Source identity

- Control binding: `d147bebce60ee79588e0e430f47969e9ba8662b9`
  (tree `63d0e3ebcebbe9ee5e7156cefbeac38a11fb19ac`).
- Writer dispatch base and candidate parent:
  `d335ce053c58eaebbfda384412ef9be5c9d839da`.
- Accepted integration source:
  `5108bcedb6782a24b0095fab88b021a628c01f8b`.
- The candidate is the single detached child containing this handoff. Its exact
  commit, tree, and stable patch ID are reported to PB-000 after commit creation
  because a commit cannot contain its own identity.

## Changed paths and behavior

- `src/AAEmu.Game/Scripts/Commands/BotDebugCommand.cs`
- `scripts/autonomy/AutonomyObserver.psm1`
- `tests/AAEmu.UnitTests/Game/Scripts/Commands/BotCommandsTests.cs`
- `tests/AAEmu.UnitTests/Bots/Host/BotHostBehaviorTests.cs`
- `scripts/autonomy/tests/Test-AutonomyObserver.ps1`
- `ops/evidence/aaemu12-runtime-recovery-observability-v1.yaml`
- `ops/tasks/T-114/HANDOFF.md`

For a selected present runtime, `botdebug` now emits exactly one invariant line
after `Life delta` and before the unchanged aggregate host line:

`Runtime metrics: brain_steps=<nonnegative long>, mover_steps=<nonnegative long>, errors=<nonnegative int>`

The observer sample is now
`playerbots.autonomy-botdebug-sample.v2` and adds an Int64
`runtime_metrics` object. Online responses require exactly one ASCII-only exact
record. Stable failures are `missing-runtime-metrics`,
`duplicate-runtime-metrics`, and `malformed-runtime-metrics`; offline samples
retain `runtime_metrics: null`. The existing `host_metrics` object, loopback
route, exported functions, AST allowlists, command grammar, and filesystem and
transport boundaries remain unchanged.

No lifecycle, scheduler, mover, brain, combat, recovery, logout, or aggregate
host-metric production source changed.

## Proof

- Final focused `BotCommandsTests`: 46 passed, 0 failed, 0 skipped.
- Final focused `BotHostBehaviorTests`: 34 passed, 0 failed, 0 skipped.
- Deterministic observer qualification: 91 assertions passed; all positive,
  offline-null, missing, duplicate, partial, signed, negative, localized-digit,
  decimal, exponent, overflow, and trailing-text cases passed their gates.
- Multi-runtime regression across three host ticks:
  - pending runtime `ShouldSuspendRuntime=true`;
  - pending brain/mover deltas `0/0`;
  - advancing peer brain/mover deltas `+3/+3`;
  - aggregate host brain/mover deltas `+3/+3`;
  - restoring MP to 100 completed recovery and invoked the pending runtime's
    normal logout callback exactly once.
- Fresh isolated proof checkout:
  `D:\Codex-Labs\t114-aaemu12-source-build-v1`, detached at pinned AAEmu 1.2
  `62e3eb1d87da01194802ac886cd500134facad28` with exactly 28 compatibility
  files and a proof-only link to this writer tree.
- Single complete no-incremental build: 0 errors, 40 retained warnings,
  53.36 seconds. A test-only recompile after the fixture correction was 0
  errors and 33 retained warnings; production source did not change after the
  complete build.
- Compatibility patch SHA-256 remained
  `baa0979a200eb7f4403985d7166092f937033c31650aa11336fba31543d96606`.
  Strict forward apply passed on the clean registered reference, reverse apply
  passed in the isolated checkout, and the reference remained clean and
  detached at the pinned commit.
- Full-suite invocations: 0. `git diff --check`: passed.

## Retained anomaly and boundaries

The first host-focused run was 33 pass / 1 fail because the advancing peer's
real combat brain reached an intentionally absent unit-test `SkillManager`
before incrementing its own brain counter. The pending runtime's suspension
assertions had already passed, so this did not disprove the source diagnosis.
The fixture now uses a deterministic no-op peer brain with the real host
scheduler and mover; no lifecycle behavior was changed speculatively.

T-112 remains an immutable independently integrated runtime FAIL. No live
runtime, Login, Game, client, database, deployed config, registered integration
host/module, external runtime evidence, global ledger, lease, workspace
registry, AAEmu 3.0, scale, soak, or packaging state was started, controlled,
or written. Installation, the full suite, physical per-runtime sampling,
recovery, shutdown-tail closure, and a fresh runtime successor remain unproven.

## Exact integration action

PB-000 must dispatch T-115 to verify the reported candidate is one clean child
of `d335ce053c58eaebbfda384412ef9be5c9d839da`, replay only that candidate onto
the current integration head, acquire any required build-only lease before
writing the registered integration host, verify the seven scoped paths and
patch identity, install the exact replay, rerun the complete command, host, and
observer focused gates, perform one isolated build and exactly one permitted
full-suite wave gate, and record a new integration receipt. Only after that PASS
may PB-000 dispatch a fresh immutable runtime successor using per-runtime
counters without reinterpreting T-112.
