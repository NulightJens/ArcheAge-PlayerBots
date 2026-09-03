# T-096 handoff

## Source identity and outcome

- PB-000 binding commit and candidate parent:
  `1ebfd0ec47138ebf15268befde9ed1671aa05ef8`.
- Dispatch base: `e298724c9940b11bef6ea5da359d8f6ea23fda8a`.
- Accepted module source: `e284c739ae168fc95fb77a91bf0f23bd5dd2f6a4`;
  the binding had no `src`, `tests`, or `compatibility` difference from that
  source.
- Candidate head: the single detached commit containing this handoff; PB-000
  receives its exact hash at completion.
- Outcome: `ActivityDirectorInitialDelayMs` now has one shared five-minute
  (`300000` ms) ceiling in validation and runtime `TimeSpan` conversion.
  `180000` ms is preserved end-to-end; the prior `60000` ms behavior remains
  valid inside the widened range.

## Changed and regenerated paths

- `src/AAEmu.Game/Models/Game/Bots/BotConfig.cs`
- `tests/AAEmu.UnitTests/Game/Core/Managers/BotManagers/BotConfigTests.cs`
- `ops/evidence/aaemu12-activity-director-delay-range-v1.yaml`
- `ops/tasks/T-096/HANDOFF.md`
- `compatibility/aaemu-1.2-r208022-v3.patch` was regenerated from the isolated
  pinned host and remained byte-identical, so it has no Git content delta.

No path outside the declared T-096 write scope was modified.

## Focused proof and clean build

- Proof workspace:
  `D:\Codex-Labs\t096-aaemu12-source-build-v1`, freshly cloned and detached at
  AAEmu 1.2 host base `62e3eb1d87da01194802ac886cd500134facad28`.
- `BotConfigTests`: 32 passed, 0 failed, 0 skipped.
- `BotActivityDirectorTaskTests`: 8 passed, 0 failed, 0 skipped.
- Total focused proof: 40 passed, 0 failed, 0 skipped.
- Required cases passed: negative to zero; `60000` unchanged; `180000`
  unchanged; `300000` unchanged; `300001` to `300000`; and the codebase's
  runtime configuration conversion carried `180000` to `00:03:00`.
- Final no-incremental, no-restore AAEmu 1.2 unit-test-project build: PASS,
  40 retained warnings, 0 errors. The warnings are the pinned package advisory
  and existing host/module compiler, analyzer, and TUnit diagnostics; none is
  introduced by the two T-096 files.
- Full-suite invocations: 0. The integration-wave full-suite gate is
  intentionally unconsumed.

## Compatibility patch proof

- Regenerated host diff: the same 28 AAEmu seam files.
- SHA-256 before and after regeneration:
  `baa0979a200eb7f4403985d7166092f937033c31650aa11336fba31543d96606`.
- Strict reverse-check passed in the applied proof workspace.
- `git apply --check --whitespace=error-all` passed against registered
  read-only reference `aaemu12_reference`, still clean and detached at
  `62e3eb1d87da01194802ac886cd500134facad28` afterward.
- The patch remains host-only by design. Its Game/test project imports compiled
  the corrected module source and test payload in the proof build; duplicating
  those module-owned files into the host patch would violate that ownership
  seam.

## Retained failures, runtime state, and unproven boundaries

- T-094 remains a retained FAIL: deployed `180000` was clamped to `60000` by
  the accepted predecessor source. T-096 corrects the cause but does not
  reinterpret or overwrite T-094 evidence.
- No source, focused-test, build, patch-apply, or patch-integrity failure
  remains in T-096.
- No runtime was started, stopped, inspected, or controlled. No registered
  integration host/module, deployed configuration, database, client, external
  evidence root, global ledger, runtime lease, packaging, scale, soak, or
  AAEmu 3.0 state was written or controlled.
- Installation, the complete AAEmu 1.2 suite, and physical confirmation of a
  180-second first admission remain pending.

## Exact integration action

PB-000 must dispatch an independent Integrator. The Integrator must verify the
reported T-096 candidate is one clean child of binding commit
`1ebfd0ec47138ebf15268befde9ed1671aa05ef8`, cherry-pick only that candidate
onto the current accepted `integration/aaemu12-world` head, and acquire a
fresh build-only lease before writing the registered AAEmu 1.2 integration
host/module. It must verify patch SHA-256
`baa0979a200eb7f4403985d7166092f937033c31650aa11336fba31543d96606`, install
the exact candidate, rerun the 32 BotConfig and 8 Director focused tests, run a
clean build and exactly one complete AAEmu 1.2 suite, and record a fresh
integration receipt. Only after that integration is accepted may PB-000 bind a
new runtime successor to prove the configured 180-second admission delay; do
not reuse or overwrite the retained T-094 evidence root.
