# T-068 contract

## Outcome

The accepted T-067 lifecycle progression/resource observability correction is
independently replayed onto current integration history, installed in the
registered AAEmu 1.2 host, and qualified by a clean full build and exactly one
complete unit-suite invocation before another physical one-bot attempt is
authorized.

## Pass

- Start from exact integration head
  `7b1ff7b879f40bf2bea8ed82d715534e4c75e3c1`. Verify T-067 candidate
  `4253d0875c5165095621e1e6ff4c672fa21a51d8` with parent
  `301d5ec1001d762b32fbca132eaee06aa8dd44b3`. Both worktrees must be clean.
- Review the declared diff and replay only its five source/test paths and
  T-067 handoff. Preserve one immutable accepted-activity baseline, one
  immutable accepted-logout completion snapshot, signed native deltas, stable
  inventory summary/fingerprint, structured pre-callback logging, explicit
  unavailable degradation, and `/botdebug` visibility.
- Prove that observability cannot change activity selection, targeting,
  combat, loot, recovery, persistence, logout acceptance, callback timing, or
  duplicate-tick behavior. Resolve regressions only within the declared scope.
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
- Run directly affected tests, installer idempotence, a complete AAEmu 1.2
  solution build, and the full AAEmu 1.2 unit suite exactly once. Zero failures
  and zero build errors are required; report only intentional skips.
- Recheck clean reference, clean installed module, unchanged tracked host
  status outside the receipted module state, zero Login/Game/client processes,
  and free required ports.
- Commit only the replayed paths, T-067 handoff, a sanitized integration
  receipt, and T-068 handoff. Fast-forward the saved integration branch through
  the qualified handoff commit without modifying global ledgers. State the
  exact tested identity and whether a fresh immutable runtime proof may proceed.

## Non-goals

Starting Login, GameServer, MySQL, or an ArcheAge client; claiming a runtime
lease; accessing schemas/databases; live gameplay; modifying retained T-065
evidence; changing the compatibility patch, AAEmu 3.0, lifecycle/combat/loot/
recovery behavior beyond accepted T-067 blobs, T-041/T-037, soak, or release
packaging.
