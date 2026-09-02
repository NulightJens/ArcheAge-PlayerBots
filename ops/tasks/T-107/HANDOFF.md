# T-107 handoff

Verdict: `PASS-integration-install-build-focused-full-unit`. The exact T-106
four-path candidate was independently replayed byte-for-byte onto the committed
T-107 binding, installed in the registered AAEmu 1.2 integration checkout, and
qualified without starting or controlling a runtime.

## Source identity

- Dispatch/binding: `0ebac76ed88a80b3a75c1f13478da269821b4e96` /
  `85435f215db854960d460127d9f1b2e7bad57036` (binding tree
  `02be073ab84453e15e3318ce76bd68d16f9d7fb1`).
- Candidate/sole parent/tree:
  `8e091a7c45ea3ad4403dc0926d071b4156c8949a` /
  `8b7a10101910c704b5f2d08129d7468cf36b8796` /
  `5fd50e919afab1caeac909fbaf88637ca943cab6`.
- Exact replay and installed source/tree:
  `037b4a87dd25df74fc8db5506c1cbc7fe3301b44` /
  `a1b9302625a65e10dfa9b7e11393a67134f914e8`.
- Candidate/replay stable patch ID:
  `1658582af94e18e4376ec0e8ffde12926d32fa0b`.
- Compatibility patch SHA-256:
  `baa0979a200eb7f4403985d7166092f937033c31650aa11336fba31543d96606`.
- Game assembly SHA-256:
  `2385d16554b1fc9df1c77d45612b39c8fdac20721f20eac31ec9d0c32cc71696`.

## Changed files

Replay commit `037b4a87dd25df74fc8db5506c1cbc7fe3301b44` changes exactly:

- `src/AAEmu.Game/Scripts/Commands/SpawnPassiveNpcCommand.cs`
- `tests/AAEmu.UnitTests/Game/Scripts/Commands/SpawnPassiveNpcCommandTests.cs`
- `ops/evidence/aaemu12-passive-anchor-yaw-offset-v1.yaml`
- `ops/tasks/T-106/HANDOFF.md`

The following artifact commit changes only this handoff and
`ops/evidence/aaemu12-passive-anchor-yaw-offset-integration-v1.yaml`.

## Installation and proof

All four replay and installed blobs match the candidate, the stable patch IDs
are equal, and no writer branch was merged. The command grammar remains exactly
`spawnpassive <npcTemplateId> [distance] [anchorBotId] [yawOffsetDegrees]`, and
the invalid-angle error remains exactly
`Yaw offset degrees must be a finite invariant number from -180 through 180.`

The registered reference remained clean and detached at
`62e3eb1d87da01194802ac886cd500134facad28`; strict forward apply and complete
integration-host reverse checks passed for the unchanged 28-file compatibility
patch. The host overlay remained exactly 30 status entries. The clean detached
installed module fast-forwarded from source/tree
`bf0ae36fc65eea4f341b936c9f7a961e9d474580` /
`7f33545311c972d7a0ed31fa7f75a016df3571f7` to the exact replay source/tree.
Installer check-only reported `installed/supported`; no normal installer run or
host-seam modification occurred.

- Complete `SpawnPassiveNpcCommandTests` selection: `46` passed, `0` skipped,
  `0` failed in `2.019s`, covering invariant/range parsing, bit-exact zero and
  signed-angle geometry, default world `0`, retained anchor guards, and no
  source-state mutation.
- One no-incremental solution build: `0` errors, `79` retained warnings in
  `00:00:35.89`.
- Full AAEmu 1.2 suite, exactly one invocation: `1,870` passed, `4` intentional
  legacy-golden skips, `0` failed (`1,874` total) in `27.437s`.
- Sanitized receipt:
  `ops/evidence/aaemu12-passive-anchor-yaw-offset-integration-v1.yaml`.

## Retained warnings and anomalies

The clean build retained the established package, compiler, analyzer, and
test-analyzer warning set, including existing SQLitePCLRaw.lib.e_sqlite3 and
SSH.NET high-severity advisories. No product or contracted gate failure remains.
Three pre-proof operator probes were retained in the receipt: a combined-grammar
literal was initially sought in source instead of its exact command/help parts;
two generated `apply_patch` envelopes were rejected before mutation; and an
unstaged patch-ID probe omitted the two new files until all four exact paths were
staged. Final blob, patch-ID, source, installed, and gate checks all passed.

## Runtime state and unproven boundaries

No Login, Game, runtime observer, MySQL/database, or ArcheAge client was
started, stopped, controlled, or queried. Deployed runtime configuration and
external evidence were untouched. Final AAEmu Game/Login process count and
occupied required-port count are both zero; ports
`1234/1237/1239/1250/1280` are free.

T-104 remains an immutable retained runtime FAIL. This PASS proves exact source
integration, installation, focused behavior, the no-incremental build, and the
full unit suite only. Physical angular placement, both autonomous
lifecycle/refill waves, distinct-PID restart/rebootstrap, two-minute dwell,
scale, soak, packaging, release readiness, and AAEmu 3.0 remain unproven.
T-037 remains blocked.

## Exact integration action

Commit only this receipt and handoff after replay commit
`037b4a87dd25df74fc8db5506c1cbc7fe3301b44`, then fast-forward saved branch
`integration/aaemu12-world` from binding commit
`85435f215db854960d460127d9f1b2e7bad57036` through exactly the replay and
artifact commits without rewriting history. Report the artifact commit and tree
after creation. PB-000 may then release the build-only lease and bind fresh
T-108 v7 runtime evidence against replay source/tree
`037b4a87dd25df74fc8db5506c1cbc7fe3301b44` /
`a1b9302625a65e10dfa9b7e11393a67134f914e8`.
