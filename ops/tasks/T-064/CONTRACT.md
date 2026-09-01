# T-064 contract

## Outcome

The accepted T-062 atomic cohort stimulus and T-063 bounded single-bot
lifecycle wiring are independently replayed onto the current integration
history, installed in the registered AAEmu 1.2 host, and qualified by a clean
full build and one complete unit-suite invocation before another physical
single-bot attempt is authorized.

## Pass

- Start from exact integration head
  `0fcb943552dcefde95bc2af220caab6bfafa77ef`. Verify T-062 candidate
  `4c2d9e8ccdfa1255cef396388ebba2423aa35624` with parent
  `10fb2de0038ecb43d3f2e6cf15f5789e2b5926dd` and T-063 candidate
  `aadcef712c6ca6c1d797375476611b71cbf86178` with parent
  `f22ae76d8a401ad374f74df55a6ef6a675c3ad1f`. Both source worktrees and the
  integration worktree must be clean.
- Review both declared diffs and replay only their source/test/handoff paths.
  Preserve T-062's one synchronous `botattackobject all <targetObjId>` after
  complete cohort setup and baseline snapshot. Preserve T-063's exact-one-bot
  fail-closed activation, one-kill bound, no direct target/destination choice,
  deterministic decision visibility, and deferred normal logout callback.
- Resolve any overlap or regression within the declared write scope only. Do
  not merge either writer's task metadata or any global-ledger state.
- Verify the pinned reference and registered integration host/module match the
  receipted identities before mutation. If mismatched beyond the documented
  installed state, stop under the contingency runbook rather than cleaning,
  resetting, or substituting a checkout.
- Keep the approved AAEmu 1.2 compatibility patch byte-identical at SHA-256
  `0285c8c21133ec00abbd8f5de925c56dc2d87f7d8595fee89a6063b3ff02ca3e`.
  Prove it applies to the clean reference and reverse-checks the installed host;
  do not regenerate or reapply it.
- Advance the installed module through the normal non-destructive Integrator
  commit/fast-forward workflow and record exact tested/installed head and tree.
- Prove the T-062 deterministic harness, directly affected lifecycle/host/
  command tests, installer idempotence, a complete AAEmu 1.2 solution build,
  and one complete unit-suite invocation. Zero failures and zero build errors
  are required; report only intentional skips.
- Recheck clean reference, clean installed module, unchanged tracked host
  status outside the receipted module state, zero Login/Game/client processes,
  and free required ports.
- Commit only the replayed source paths, both retained task handoffs, a
  sanitized integration receipt, and concise T-064 handoff. State the exact
  tested identity and whether a new immutable T-061 proof may be dispatched.

## Non-goals

Starting Login, GameServer, MySQL, or an ArcheAge client; claiming a runtime
lease; accessing schemas/databases; changing the compatibility patch or AAEmu
3.0; modifying T-060/T-061 receipts or global ledgers; live gameplay, fixtures,
Activity Director, scale, soak, or release packaging.
