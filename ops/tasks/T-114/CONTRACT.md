# T-114 contract: per-runtime recovery suspension observability

Start only from PB-000's exact committed T-114 thread/worktree binding based
on saved integration head `5108bcedb6782a24b0095fab88b021a628c01f8b`.
T-113 independently integrated T-112 as a retained FAIL. Do not reinterpret
or amend that result: the T-112 validator read shared `Host metrics`
brain/mover totals from three concurrent bots, so activity by a different bot
made the totals advance during the per-bot recovery window. Read-only source
inspection shows `BotHostTask` calls `LifeController.Step` and, when
`ShouldSuspendRuntime` is true, continues before `StepMover` and `StepBrain`.
`BotRuntimeMetrics` already owns per-runtime `BrainSteps`, `MoverSteps`, and
`Errors`. This task corrects the missing per-bot observation surface and proves
the existing suspension behavior; it must not change lifecycle decisions,
scheduling, movement, combat, recovery, logout, or host metric semantics.

In `BotDebugCommand`, while the selected bot's runtime is present, emit exactly
one invariant line inside the runtime diagnostic block:

`Runtime metrics: brain_steps=<nonnegative long>, mover_steps=<nonnegative long>, errors=<nonnegative int>`

Read the three values from that selected runtime's `Metrics`. Use invariant
integer formatting and stable field order. Keep the existing aggregate
`Host metrics` line byte-compatible and clearly separate; do not relabel or
reuse shared host totals as per-runtime values. Preserve every existing command
grammar, transform line, lifecycle diagnostic, error, and no-mutation boundary.

Extend the observation-only parser's typed sample with `runtime_metrics`.
For an online botdebug response, require exactly one exact runtime-metrics line
and parse all three counters invariantly into 64-bit values without accepting
signs, localized digits, decimals, exponent notation, overflow, trailing text,
duplicate lines, or partial fields. A missing, duplicate, malformed, negative,
or overflow runtime record must classify the whole online response as malformed
with a stable diagnostic. Offline responses retain null runtime metrics. Keep
the existing aggregate `host_metrics` object and command/transport allowlists;
do not add a mutating route, command, parameter, filesystem mode, or external
dependency.

Add focused command tests that register a runtime with deliberately distinct
runtime and host counter values, execute `botdebug` under a non-English culture,
and prove the exact invariant runtime line occurs once in the runtime block.
Prove existing transform/lifecycle/host diagnostics and bot state remain
unchanged. Add observer parser tests for the positive record, online missing,
duplicate, malformed, signed/negative, overflow, and offline-null cases. Update
the exact PowerShell AST allowlists only for unavoidable parser-local syntax;
do not weaken them.

Strengthen `BotHostBehaviorTests` with a deterministic multi-runtime recovery
case. Put one runtime into pending natural recovery and keep a second runtime
eligible to execute. Snapshot both runtimes' own metrics and the shared host
totals, advance multiple host ticks, and prove all of the following together:

- the pending runtime reports `ShouldSuspendRuntime=true`;
- its own brain and mover counters remain bit-for-bit fixed;
- the second runtime advances at least one own brain or mover counter;
- the corresponding aggregate host total advances because of that second
  runtime; and
- restoring the pending runtime to full resources completes the existing
  normal lifecycle/logout path exactly once.

Keep the existing single-runtime suspension regression green. If the
multi-runtime proof disproves the source diagnosis, stop and report the retained
failure without changing lifecycle behavior speculatively.

Run the complete focused `BotCommandsTests` and `BotHostBehaviorTests`, plus the
complete deterministic autonomy-observer PowerShell test. Perform one isolated
AAEmu 1.2 build with zero errors and retain the exact warning count. Verify the
compatibility patch remains byte-identical at SHA-256
`baa0979a200eb7f4403985d7166092f937033c31650aa11336fba31543d96606`
and still applies cleanly to the pinned clean reference. Do not install into the
registered integration host and do not run the full suite.

Commit only the five source/test paths, the T-114 receipt, and concise handoff.
Report candidate commit/parent/tree/patch ID, exact runtime line and parser
schema/diagnostics, focused counts, build warnings, patch identity/apply proof,
multi-runtime counter deltas, unchanged production behavior, anomalies, and
unproven physical runtime boundary. PB-000 will dispatch a separate T-115
Integrator/install/full-suite task before any fresh runtime successor.
