# T-072 handoff

Verdict: `PASS-integration-install-build-focused-full-unit`.

## Source identity

- Declared integration head: `d71a52b10e78be9ca6a91a3b1d5822a5ef199740`.
- Exact committed build-only lease: `5a6ef23f7a89e419f25bf2b39f90497b8df8bd4f`
  for T-072 thread `01a05c9d-4322-7f22-97dc-bb46803ecd6b`.
- Accepted T-071 source/parent:
  `16e8c2fe707f7bd96e4b6977dd6b7c441124c63f` /
  `a1c15016e3e7ccc121c0bad048dc7bb9d5c7aa8f`.
- Independently replayed, tested, and installed source:
  `68aaaa3334a408d1d6d21e44472a8984e78618c2`, tree
  `9fa74d8057df8b5eb276ad223a10ed9b12791f88`.
- Candidate/replay stable patch ID:
  `37a69861959b384d0a5225d74454ba9a271480d9`.
- Receipt: `ops/evidence/aaemu12-natural-recovery-integration-v1.yaml`.

## Changed files

- `src/AAEmu.Game/Bots/Life/BotLifeController.cs`
- `src/AAEmu.Game/Scripts/Commands/BotDebugCommand.cs`
- `tests/AAEmu.UnitTests/Bots/Life/BotLifeControllerTests.cs`
- `tests/AAEmu.UnitTests/Bots/Host/BotHostBehaviorTests.cs`
- `tests/AAEmu.UnitTests/Game/Scripts/Commands/BotCommandsTests.cs`
- `ops/tasks/T-071/HANDOFF.md`
- `ops/evidence/aaemu12-natural-recovery-integration-v1.yaml`
- `ops/tasks/T-072/HANDOFF.md`

All six replayed blobs match T-071 exactly. Review confirmed controller-owned
pending/completed recovery observations, mover/brain suspension while native
world regeneration remains active, targetless/noncombat/movement-clear gates,
fail-closed invalid resources, one exact completion snapshot and progression
record, and the unchanged one-shot logout callback.

## Installation and proof

The retained T-071 pre-clarification build event changed ignored host outputs
only. Before normal T-072 mutation, the registered host still had its exact
30-entry snapshot `3ba59378bacebcfafe53e84f59201a0eba09140b`, and the installed module was clean
at `4287ec53e998ecff2e5eb9670908166957a79662` / tree
`1c6820f2f47ec79a3aab40174e1b448f894f67e4`.

The installed module fast-forwarded non-destructively to the tested source.
Installer check-only passed before and after one idempotent normal run. The
compatibility patch remained byte-identical at SHA-256
`0285c8c21133ec00abbd8f5de925c56dc2d87f7d8595fee89a6063b3ff02ca3e`,
and the tracked host snapshot did not change.

- Complete solution build: `0` errors, `76` retained warnings.
- Directly affected selection: `100` passed, `0` skipped, `0` failed.
- Full AAEmu 1.2 suite, exactly one invocation: `1,811` passed, `4`
  intentional legacy-golden skips, `0` failed (`1,815` total).

No product or gate failure is retained. Existing dependency, compiler,
analyzer, and test-analyzer warnings remain visible in the receipt.

## Runtime state and unproven boundaries

No Login, GameServer, MySQL, client, database, or live runtime was started,
stopped, controlled, or accessed. Two pre-existing MySQL processes were left
untouched. Login/Game/client process count is zero and ports
`1234/1237/1239/1250/1280` are free. T-065 remains `INCOMPLETE` and T-069
remains `FAIL`; neither retained evidence root changed.

Physical native recovery timing and logs, live botdebug output, normal live
self-logout, later autonomous iterations, and clean restart remain unproven.

## Exact integration action

Fast-forward the saved `integration/aaemu12-world` branch from build-only lease
commit `5a6ef23f7a89e419f25bf2b39f90497b8df8bd4f` through the commit containing this
handoff, preserving that ledger-only descendant. After the saved branch and
installed source identities are rechecked, PB-000 may release or reassign the
lease and dispatch a fresh immutable one-bot runtime proof against source
`68aaaa3334a408d1d6d21e44472a8984e78618c2` and tree
`9fa74d8057df8b5eb276ad223a10ed9b12791f88` with a new evidence root.
