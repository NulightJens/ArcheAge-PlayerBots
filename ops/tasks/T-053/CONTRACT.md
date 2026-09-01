# T-053 contract

## Outcome

The accepted T-051 exact-schema/growing-log correction is independently replayed
onto the current integration branch and registered AAEmu 1.2 installation, with
one complete green receipt suitable for authorizing a fresh physical v3 attempt.

## Pass

- Start from exact integration head `e3b440d6ad9dc49012090639450d5f423fddfb85`.
  Verify T-051 is exactly `09dc81467805b6d857dd2f2e173080c863870e49`
  with parent `c7ea086e32351dc64702ce5539f375a6929c8c96`, declared
  file scope, clean worktree, and patch SHA-256
  `b3ee8cfbe2aad1c7d4cf207f37c3ae4eb422bd21266e6d66320c5ca57ef01d91`.
  Verify T-052 is accepted and non-blocking.
- Independently replay only T-051's declared diff onto this integration history.
  Record the resulting integration source commit; do not merge unrelated state.
- Verify the clean pinned reference and the registered integration host/module
  still match the accepted T-049/T-050 identities before mutation. If dirty or
  mismatched beyond documented installed state, stop under the contingency plan.
- Reconstruct the complete patch in a new versioned scratch checkout. Require
  clean apply and exact final blob IDs `403332171f6b92a88a10082885b54dd302fd6026`
  for `AAEmu.Game/GameService.cs` and
  `843d861208dfad4f5be58e1d38b96ed0311ca869` for
  `AAEmu.UnitTests/Services/GameServiceTests.cs`.
- Update the registered integration host by intentionally overwriting only those
  two literal reconstructed files. Do not reapply already-installed hunks. Move
  the installed module forward through a normal non-destructive Integrator
  commit/fast-forward workflow and record its exact head/tree.
- Prove installer idempotence; deterministic guard 32/32; focused
  `GameServiceTests` 9/9; all relevant focused module/runtime tests; complete
  AAEmu 1.2 solution build; and the full AAEmu 1.2 unit suite with only declared
  intentional skips. Zero failures and zero build errors are required.
- Reproduce manifest/patch hashes, PowerShell parser gates, credential scan,
  reference cleanliness, installed-module cleanliness, and `git diff --check`.
- Do not start any runtime. Preserve or quarantine all scratch material. Commit
  a sanitized receipt and concise handoff with exact T-054 authorization action.

## Non-goals

- Login/Game/client/MySQL startup, schema access, physical combat/stealth, direct
  database work, runtime lease changes, global-ledger edits, Population Director,
  scale, soak, release, or AAEmu 3.0.
