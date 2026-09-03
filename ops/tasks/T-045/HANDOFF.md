# T-045 handoff

## Verdict

INTEGRATION BLOCKED. The reviewed source candidate is retained and its verified
full-suite defect was corrected inside T-045's union scope, but the Wave 2 full
AAEmu 1.2 suite was invoked exactly once and that invocation was red. No green
`aaemu12-wave2-integration-v1` receipt was written, and
`integration/aaemu12-world` was not advanced.

## Source identity

- Codex worktree: `C:\Users\jensh\.codex\worktrees\698c\PB-W00-control`.
- Starting state: clean detached HEAD at
  `d510b8d02537ac9626710fd8fb5756c04a94ffe3`.
- PB-000 setup incorporated by fast-forward:
  `33466305616b0b77bb5c2b9caa8368f90a638001`.
- Exact worker commits reviewed: T-007
  `43956fb9e6f1c3cc88cff0f8385f7dd7ef1ebb4e`, T-043
  `e0e0825c35b5f0863e43b677a416c4f819419716`, and T-044
  `9ba012805124e667185257449d7ec15ce147406f`.
- Stable worker patch IDs: T-007
  `8d8757b0433260d88c2bc074c3045af08d227596`, T-043
  `0c64e68ed0ed2cbbddbdb5d6af618e707994146d`, and T-044
  `6c78b0fb4e1028ce685e5004093c3dd23a4a8b0d`.
- Cherry-picked commits: T-007
  `0dd9e80a375d419d9cf724a30ad16f23af889803`, T-043
  `1453c04138e076b4c22c9b9549ce16fbc53ec3b8`, and T-044
  `568af9c3adac6d667524df2c0176a60e37ae9274`.
- T-045 corrections: restart identity binding
  `bc872d2614718dee177f5f7e219ee43866f9c5ee` and exact-NPC buff alias
  authorization `225595ebe45d5b0c08bfa0a1059cb8ba97669f86`.
- Tested and installed retained source:
  `225595ebe45d5b0c08bfa0a1059cb8ba97669f86`.

## Review and corrections

- Each exact worker commit, task, contract, handoff, changed path, ancestry, and
  stable patch identity was reviewed. No worker worktree was modified.
- T-044's restart verifier did not bind the prior PID/start time to the initially
  qualified process. It now performs that binding, handles serialized timestamp
  values deterministically, and has explicit regression coverage.
- The full suite found that T-044 introduced `botbuffnpc` as an unregistered
  primary command. The correction retains `/botbuffnpc` as an alias of the
  already-authorized `botbuff` primary command and requires an explicit fourth
  ability-level argument for the exact-NPC form. Focused access and command tests
  prove the corrected seam.

## Database receipt validation

- The committed `aaemu12-database-public-alpha-v1` receipt and exact retained
  directory were validated without provisioning and without querying or mutating
  either schema.
- The directory contains exactly the two declared donor dumps,
  `retained-bot-ids.json`, and `seed-evidence.json`; all four byte sizes and
  SHA-256 values match the receipt.
- The retained manifest contains exactly 100 unique IDs, `20001` through
  `20100`. Seed evidence identifies Game
  `aaemu_playerbots_game_public_alpha_v1`, Login
  `aaemu_playerbots_login_public_alpha_v1`, template character `2`, bot count
  `100`, and seed `t021-seed-v1`.
- The registered donor `aaemu12_legacy_t022` is clean at
  `72fd2cc70bba3887e093e8a3800f13fed9b3e111`. The receipt's provisioning-script,
  task, contract, and gate hashes reproduce from the original Windows checkout
  bytes.
- Canonical compatibility fingerprint byte source for AAEmu 1.2 is the working
  file `compatibility/aaemu-1.2-r208022-v3.patch` at retained source
  `225595ebe45d5b0c08bfa0a1059cb8ba97669f86`; its SHA-256 is
  `afbb6aa2c5d379eb76ad339642c190f4185b4981815aa27c68d0680d28edd046`.
  T-043's earlier receipt recorded
  `9c877d395b7f6dc7dc47dbf1bd4927af3a5d08392434c758cea364385a6ba15e`,
  which is the distinct frozen AAEmu 3.0 alpha-v4 patch hash, not a byte variant
  of the AAEmu 1.2 patch. The historical receipt was retained unchanged and this
  discrepancy is explicit here.

## Proof

- T-007 deterministic evidence tooling: PASS, 53 assertions across 18 retained
  translator attempts at the final retained source.
- T-044 offline harness: PASS, 13 verdict scenarios with no sleeps at the final
  retained source.
- Registered `aaemu12_integration` installer check: PASS, supported AAEmu 1.2
  lineage and already-installed patch. Installer reruns were idempotent.
- Initial no-incremental solution build: PASS, 0 errors and 76 warnings.
- Declared combat/command groups before the access correction: PASS, 117/117
  (13 manager, 38 state, 15 task, 43 command, and 8 passive-NPC tests).
- Corrective single-node overlay build: PASS, 0 errors and 40 warnings.
- Corrective focused proof: `BotAccessLevelsTests` 3/3 and `BotCommandsTests`
  43/43 passed.
- Full AAEmu 1.2 suite, invoked exactly once: RED, 1,793 total; 1,788 passed,
  4 intentional legacy skips, and 1 failed. The failure was
  `AccessLevels_ContainsPrimaryNameOfEveryBotCommand` for the unregistered
  `botbuffnpc` primary name. The source defect is corrected and focused-green,
  but the required full gate remains red because it was not rerun.
- `git diff --check`: PASS.

## Retained failures and warnings

- The sole full-suite result is retained as the integration blocker. The final
  source has no green full-suite proof and must not be integrated under T-045.
- The first corrective overlay build waited on stale reusable MSBuild workers
  from the completed solution build. It was canceled with normal Ctrl+C; the
  single-node, node-reuse-disabled retry passed.
- Builds retain the existing dependency/compiler/analyzer warnings, including
  the SQLitePCLRaw.lib.e_sqlite3 and SSH.NET high-severity advisory warnings.

## Runtime and workspace state

- No Login server, Game server, client, or database-backed runtime was started.
- Neither new schema was queried or mutated. No provisioning was rerun.
- The `aaemu12` runtime lease, global ledgers, workspace registry, client, and
  AAEmu 3.0 were not changed by T-045.
- `aaemu12_reference` remains clean and detached at
  `62e3eb1d87da01194802ac886cd500134facad28`.
- The registered `aaemu12_integration` host retains its receipted patch and
  migration state. Its installed module is clean and detached at
  `225595ebe45d5b0c08bfa0a1059cb8ba97669f86`.

## Unproven boundaries

- A green full AAEmu 1.2 suite for retained source `225595ebe45d5b0c08bfa0a1059cb8ba97669f86`
  remains unproven.
- No physical mortal combat, stealth, cleanup, restart, client, resource-budget,
  scale, or Population Director acceptance is claimed.

## Exact integration and PB-000 action

Do not fast-forward `integration/aaemu12-world`; leave it at its current clean
head. PB-000 must not register the new database identity in
`ops/WORKSPACES.yaml`, set `aaemu12.database_identity`, mark T-007/T-043/T-044
integrated, or activate/assign T-036 from this handoff.

The next authorized recovery integration task should start from retained source
`225595ebe45d5b0c08bfa0a1059cb8ba97669f86`, review the two T-045 corrections,
install/build it in registered `aaemu12_integration`, and run its own declared
full-suite gate once. Only after that gate is green may an Integrator
fast-forward `integration/aaemu12-world`. PB-000 may then make one control-only
bookkeeping commit that:

1. registers `aaemu12_database_public_alpha_v1` with the exact Game/Login schema
   names, retained output directory, donor workspace, receipt, provenance hash,
   and lease-controlled access, without credentials;
2. records the accepted integrated commit and receipt in BOARD/CURRENT and marks
   T-007, T-043, T-044, and the recovery integration complete;
3. sets `aaemu12.database_identity` to that registration; and
4. activates and assigns T-036 under the still-serialized `aaemu12` lease.
