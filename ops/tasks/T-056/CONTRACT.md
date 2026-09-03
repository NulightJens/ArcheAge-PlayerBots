# T-056 contract

## Outcome

The accepted T-055 `@system` world-context correction is independently replayed
onto the current integration history, installed in the registered AAEmu 1.2
host, and qualified by a clean full build and complete unit suite before any
new physical attempt is authorized.

## Pass

- Start from exact integration head
  `6aece206d74a606c5bfbaa366e8501ab6d804f74`. Verify T-055 candidate
  `68cf32fbe3012364fb0e1dec2c8f6278015e35eb`, parent
  `3bdeedadbe8818c29079bb495b93fcd8fe7582f8`, declared eight-file scope,
  clean worktree, and 1.2 patch SHA-256
  `0285c8c21133ec00abbd8f5de925c56dc2d87f7d8595fee89a6063b3ff02ca3e`.
- Independently replay only T-055's declared source/test/docs diff onto this
  integration history. Record the resulting tested source commit and tree; do
  not merge unrelated task or global-ledger state.
- Verify the pinned reference and registered integration host/module match the
  accepted T-053/T-054 identities before mutation. If the source identity is
  mismatched or dirty beyond the documented installed state, stop under the
  environment contingency instead of cleaning or substituting a checkout.
- Prove the regenerated patch has the same stable patch ID as the previously
  installed patch and applies cleanly to `aaemu12_reference`. Do not reapply an
  already-installed equivalent patch merely because its canonical byte hash
  changed.
- Move the installed module forward through the normal non-destructive
  Integrator commit/fast-forward workflow. Intentionally overwrite only normal
  build/test outputs and the literal installed-module files represented by the
  accepted source commit; retain exact final module head/tree.
- Prove installer idempotence, manifest/hash reproduction, the new controller
  and `spawnpassive` regressions, existing relevant character/access and module
  groups, complete AAEmu 1.2 solution build, and one full AAEmu 1.2 unit suite.
  Zero failures and zero build errors are required; report intentional skips.
- Recheck clean reference, clean installed module, exact host/module status,
  zero Login/Game/client processes, and free ports 1234/1237/1239/1250/1280.
- Commit only the replayed T-055 files, a sanitized integration receipt, and a
  concise handoff with the exact next physical-attempt authorization action.

## Non-goals

Starting or controlling Login, GameServer, MySQL, or the ArcheAge client;
claiming a runtime lease; accessing schemas or databases; creating fixtures;
rerunning combat/stealth; editing global ledgers or retained T-054 evidence;
Population Director, scale, soak, release, or AAEmu 3.0.
