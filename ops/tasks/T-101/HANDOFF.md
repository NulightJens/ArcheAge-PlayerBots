# T-101 handoff

Verdict: `PASS-integration-install-build-focused-full-unit`. The exact T-100
candidate was independently replayed byte-for-byte onto the committed T-101
binding, installed in the registered AAEmu 1.2 checkout, and qualified without
starting or controlling a runtime.

## Source identity

- Dispatch/binding: `a05e94fd8d7f58bce12a968aa1d5b09de78f4a14` /
  `13f681f48698de3572b5b774d9b9e689f414a491`.
- Candidate/sole parent/tree:
  `043bf7aff4619808a050538c6763f0c6ab2e2ee8` /
  `96b8b5004a89a0dec6522753f64d03fd1d732c5b` /
  `5e5a1795466f8d24f2adef4640a47f1f01e75880`.
- Exact replay and installed source/tree:
  `e0af81eb43d244d69114f3e69fbf5efbdfdc9d96` /
  `7afaac89eb0d5ef53475064134e69b7e7527eb58`.
- Candidate/replay stable patch ID:
  `80faacf109fc2c169084c775fe9afb393b21e9f7`.
- Compatibility patch SHA-256:
  `baa0979a200eb7f4403985d7166092f937033c31650aa11336fba31543d96606`.
- Game assembly SHA-256:
  `54f822be1721e00fedfc998a02115c19c573015822c904c7dc6e055c8b1fbfff`.

## Installation and proof

The candidate changes exactly the declared command source, command tests,
T-100 receipt, and T-100 handoff. All four replay blobs and the stable patch ID
match the candidate. The registered reference remained clean and detached at
`62e3eb1d87da01194802ac886cd500134facad28`; strict forward apply and complete
integration-host reverse checks passed for the unchanged 28-file compatibility
patch. The retained host overlay remained exactly 30 status entries.

The installed module moved cleanly from source/tree
`761ffa1e0bd76d06532688f34b45e192a493b239` /
`c36b1255cccb3a782e44e87a56bc9e867a946048` to the exact replay source/tree.
Installer check-only reported `installed/supported`; no normal installer run or
host-seam modification was performed.

- SpawnPassiveNpcCommand focused selection: `40` passed, `0` skipped, `0`
  failed.
- One no-incremental solution build: `0` errors, `79` retained warnings.
- Full AAEmu 1.2 suite, exactly one invocation: `1,863` passed, `4`
  intentional legacy-golden skips, `0` failed (`1,867` total).
- Sanitized receipt:
  `ops/evidence/aaemu12-passive-anchor-default-world-zero-integration-v1.yaml`.

## Runtime state and boundaries

No Login, Game, runtime observer, MySQL/database, or ArcheAge client was
started, stopped, controlled, or queried. Deployed runtime configuration and
external evidence were untouched. Final AAEmu Game/Login process count and
occupied required-port count are both zero; ports
`1234/1237/1239/1250/1280` are free.

T-098 remains an immutable retained FAIL. This PASS proves exact source
integration, installation, the focused guard, the clean build, and the full
unit suite only. Physical default-world fixture placement, both autonomous
lifecycle/refill waves, distinct-PID restart/dwell, scale, soak, packaging,
release readiness, and AAEmu 3.0 remain unproven. T-037 remains blocked.

## Exact integration action

Commit only this receipt and handoff after replay commit
`e0af81eb43d244d69114f3e69fbf5efbdfdc9d96`, then fast-forward saved branch
`integration/aaemu12-world` from binding commit
`13f681f48698de3572b5b774d9b9e689f414a491` through both commits without
rewriting history. PB-000 may then release the build-only lease and prepare a
fresh versioned runtime successor against replay source/tree
`e0af81eb43d244d69114f3e69fbf5efbdfdc9d96` /
`7afaac89eb0d5ef53475064134e69b7e7527eb58`. Physical fixture placement remains
unproven until that successor passes.
