# T-103 handoff

Verdict: `PASS-integration-install-build-focused-full-unit`. The exact T-102
candidate was independently replayed byte-for-byte onto the committed T-103
binding, installed in the registered AAEmu 1.2 checkout, and qualified without
starting or controlling a runtime.

## Source identity

- Dispatch/binding: `14e6951f6e9c97243d7cc84502a2a25e9cedebec` /
  `56e569507e3a889de4b48bcd5d574aec640af60d`.
- Candidate/sole parent/tree:
  `70b577861a5f8fef8df4be40c61af3ba66298ed3` /
  `9a94b21fd4f9855346109cfe7b5b941df13e6b4b` /
  `2476ff1be7040f028882d54ef44b5371e4ed9f14`.
- Exact replay and installed source/tree:
  `bf0ae36fc65eea4f341b936c9f7a961e9d474580` /
  `7f33545311c972d7a0ed31fa7f75a016df3571f7`.
- Candidate/replay stable patch ID:
  `ed539b217262a89a3a7d20b32810582d7a9d5e9c`.
- Compatibility patch SHA-256:
  `baa0979a200eb7f4403985d7166092f937033c31650aa11336fba31543d96606`.
- Game assembly SHA-256:
  `5a2b9e9a32163b6003c0d4feb05fb68a06086bc23b6a7a6d2bfdc455d07942f2`.

## Installation and proof

The candidate changes exactly the declared command source, command tests,
T-102 receipt, and T-102 handoff. All four replay and installed blobs match the
candidate, the stable patch IDs are equal, and no writer branch was merged.
Independent review found only the invariant, round-trip transform diagnostic
and its read-only test; no transform, target, combat, movement, or life-state
mutation was added.

The registered reference remained clean and detached at
`62e3eb1d87da01194802ac886cd500134facad28`; strict forward apply and complete
integration-host reverse checks passed for the unchanged 28-file compatibility
patch. The retained host overlay remained exactly 30 status entries. The clean
detached installed module fast-forwarded from source/tree
`e0af81eb43d244d69114f3e69fbf5efbdfdc9d96` /
`7afaac89eb0d5ef53475064134e69b7e7527eb58` to the exact replay source/tree.
Installer check-only reported `installed/supported`; no normal installer run or
host-seam modification was performed.

- Complete `BotCommandsTests` selection: `46` passed, `0` skipped, `0` failed.
  The transform case proves default world `0`, explicit instance `43` and zone
  `601`, invariant signed coordinates, exact line placement, retained
  diagnostics, no state mutation, and bit-exact yaw round trip `0x3F9E0651`.
- One no-incremental solution build: `0` errors, `79` retained warnings in
  `00:05:17.36`.
- Full AAEmu 1.2 suite, exactly one invocation: `1,864` passed, `4` intentional
  legacy-golden skips, `0` failed (`1,868` total).
- Sanitized receipt:
  `ops/evidence/aaemu12-botdebug-transform-telemetry-integration-v1.yaml`.

## Retained warnings and probes

The clean build retained the established package, compiler, analyzer, and
test-analyzer warning set, including existing SQLitePCLRaw.lib.e_sqlite3 and
SSH.NET high-severity advisories. A first namespace-shaped focused filter
selected zero tests and was stopped by the minimum-count guard before test
execution; the established class-segment tree filter then selected and passed
all 46 cases. Git's normal porcelain output collapsed the sole registered
module child to `modules/`; exact sole-child validation, the 30-entry count,
and reverse patch check passed before host mutation. A final process predicate
self-matched its command text; the corrected exact executable-name and port
snapshot was clean. No product or contracted gate failure remains.

## Runtime state and boundaries

No Login, Game, runtime observer, MySQL/database, or ArcheAge client was
started, stopped, controlled, or queried. Deployed runtime configuration and
external evidence were untouched. Final AAEmu Game/Login process count and
occupied required-port count are both zero; ports
`1234/1237/1239/1250/1280` are free.

T-098 remains an immutable retained FAIL. This PASS proves exact source
integration, installation, focused behavior, the no-incremental build, and the
full unit suite only. Live transform telemetry, predicted one-shot fixture
placement, both autonomous lifecycle/refill waves, distinct-PID restart/dwell,
scale, soak, packaging, release readiness, and AAEmu 3.0 remain unproven.
T-037 remains blocked.

## Exact integration action

Commit only this receipt and handoff after replay commit
`bf0ae36fc65eea4f341b936c9f7a961e9d474580`, then fast-forward saved branch
`integration/aaemu12-world` from binding commit
`56e569507e3a889de4b48bcd5d574aec640af60d` through exactly the replay and
artifact commits without rewriting history. Report the artifact commit and
tree after creation. PB-000 may then release the build-only lease and bind a
fresh versioned runtime successor against replay source/tree
`bf0ae36fc65eea4f341b936c9f7a961e9d474580` /
`7f33545311c972d7a0ed31fa7f75a016df3571f7`. That successor must sample live
transform telemetry and precompute every one-shot fixture coordinate and
pairwise separation before mutation; physical lifecycle/refill/restart proof
remains pending until it passes.
