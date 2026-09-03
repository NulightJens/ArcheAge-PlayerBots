# T-082 handoff

## Verdict

REJECTED at the mandatory candidate-semantics gate. T-081 contains a
release-blocking start/stop race in the AAEmu 1.2 `GameService` compatibility
seam. Per contract, T-082 did not replay the candidate, write the registered
host, install, build, run focused tests, consume a full-suite invocation, write
an integration receipt, or advance the saved integration branch. T-041 remains
blocked.

## Source identity

- Integrator dispatch commit: `aeaa51c7bf4a5ed1d54aecc07d234c9b399de04d`.
- Exact committed build-only lease descendant incorporated before host review:
  `10dcb4c2205ec9027ab01914bcab50a63d8e54a7`, tree
  `156b71a9d4407cb993e20c1ec6cfd9ea5a32436f`.
- T-081 candidate: `370a2ee0cff17ea7b56c9a06dc069ad789245de0`,
  tree `999f427b5c127ab8b116c5a7cb85abff029cabdd`.
- Exact candidate parent: `069960e3d977767a3738b729c8e9f83767a78e8e`,
  tree `d4e662b8b691e234a0597f97d6845db9af128c33`.
- The candidate is a single child of that parent and changes exactly the 13
  T-082-declared T-081 paths. Its detached writer worktree was clean at the
  exact candidate.
- Candidate compatibility patch SHA-256:
  `5e30b0fdcbe6e9defac6426917fce22d2dcb61f860d4fb982d0dc4cf2657e2a8`.
  Both AAEmu 1.2 declarations in `playerbots.module.json` match it.
- `git diff --check` passed. Strict
  `git apply --check --whitespace=error-all` passed against the clean registered
  reference at `62e3eb1d87da01194802ac886cd500134facad28`.

## Rejection defect

The candidate patch's `GameService.StartActivityDirector()` first changes
`_activityDirectorStarted` from 0 to 1, then constructs and publishes
`_activityDirector`. `StopActivityDirector()` independently changes the flag
back to 0 and returns immediately when `_activityDirector` is still null.

This permits the following valid interleaving:

1. Startup wins the flag compare/exchange and pauses before publishing the
   Director instance.
2. Shutdown clears the flag, observes a null instance, and returns; normal
   `BotManager.Stop()` cleanup may then proceed.
3. Startup resumes, constructs, starts, and schedules the recurring Director.

The result can be a live recurring Director after the required shutdown
ordering has already passed. A second interleaving can schedule the task before
the scheduled-state flag is published, allowing shutdown to miss scheduler
cancellation. Stopping the task in that narrower case prevents bot work but
still leaves an uncancelled recurring scheduler entry.

The deterministic source suite covers sequential start idempotence, sequential
stop ordering, and Director-task tick overlap, but it has no concurrent
`GameService` start/stop proof. The implementation therefore violates T-081's
explicit race-safe start/stop requirement and T-082's Director-before-
`BotManager.Stop()` review gate. T-082 is not authorized to repair it.

## Host and no-runtime state

- Registered AAEmu 1.2 host remained at
  `62e3eb1d87da01194802ac886cd500134facad28` with the same expected 30-entry
  receipted overlay; its stable patch ID is
  `7891d6bb88fd8b76f66067c15f064b611323d642`, exactly matching the installed
  baseline patch. Reverse apply-check passed and the only expected untracked
  roots remain the receipted migration and installed module. No host or build
  output was written.
- Installed module remained clean and detached at
  `68aaaa3334a408d1d6d21e44472a8984e78618c2`, tree
  `9fa74d8057df8b5eb276ad223a10ed9b12791f88`; its installed compatibility
  patch SHA-256 remains
  `0285c8c21133ec00abbd8f5de925c56dc2d87f7d8595fee89a6063b3ff02ca3e`.
- AAEmu Game, Login, and ArcheAge client process count: 0.
- Required loopback ports `1234`, `1237`, `1239`, `1250`, and `1280`: all free.
- Two pre-existing `mysqld` processes, PIDs `6308` and `8076`, were observed
  and left wholly untouched. No database or client was accessed or queried.
- No runtime process was started, stopped, or controlled.

## Proof not consumed

- Installer check-only invocations: 0.
- Normal installer invocations: 0.
- T-082 solution-build invocations: 0; warning/error counts: not produced.
- T-082 focused-test invocations: 0; pass/skip/fail counts: not produced.
- T-082 full-suite invocations: 0; pass/intentional-skip/fail counts: not
  produced.
- Integration receipt
  `ops/evidence/aaemu12-activity-director-integration-v1.yaml`: not written.
- Saved `integration/aaemu12-world` remains at the lease/binding descendant
  `10dcb4c2205ec9027ab01914bcab50a63d8e54a7`; it was not advanced through the
  rejected source.
- One read-only preflight command initially used PowerShell's reserved `$Host`
  variable; its host Git sub-checks failed without mutation and were rerun
  successfully with a task-specific variable.

## Retained boundaries and exact next action

No physical Director refill, one-zone multi-bot activity, scale, soak,
packaging, release readiness, database behavior, client behavior, or AAEmu 3.0
compatibility is claimed. The build-only lease remains PB-000-owned state until
PB-000 records its release.

Do not integrate T-081 candidate
`370a2ee0cff17ea7b56c9a06dc069ad789245de0`, do not create the integration
receipt, and do not authorize T-041. PB-000 should dispatch a bounded source
correction that serializes publication/scheduling against stop and proves the
concurrent start/stop interleavings. A later dedicated integrator must review
that exact replacement candidate, install/build it under a new exact committed
build-only lease, run focused tests and exactly one full AAEmu 1.2 suite, and
only then fast-forward `integration/aaemu12-world` if every gate is green.
