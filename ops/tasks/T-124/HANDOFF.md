# T-124 handoff: integrated bot-intelligence wave

## Outcome

T-121, T-122, and T-123 were replayed conflict-free onto
`integration/aaemu12-world`. The merged source passed 251 immediate focused and
regression tests and a corrected no-incremental AAEmu 1.2 solution build with
zero errors. The single T-124 full-suite invocation correctly remains retained
as a gate failure: it exposed three merged regressions before the corrections
were committed. Final full-suite verification is therefore delegated to the
fresh T-125 gate instead of erasing that result with an in-task rerun.

## Source identity

- Integration base: `af840ab8a90640f3e1ff964490a3e241ef5b999b`
- T-121 candidate: `b48708522ebfc26a43124f6e43a9de7bed55af35`
- T-121 replay: `c62e591`
- T-122 candidate: `215dbbbd12464ec41eff87251eee2cd348a2c5f2`
- T-122 replay: `c4b8085`
- T-123 implementation: `9340ceb8f58c0be8aa00e83e530bce442bfa5273`
- T-123 replay: `bf2ca43`
- T-123 handoff replay: `d771db2`
- Corrective integration commit: `1a52e7b44fab76939d9409561bdfa4739f1425e6`
- Corrected tree: `3f473420369dc46bcf3884c90cfc140b58d30c34`

## Proof

- Fresh retained build-only host:
  `D:\Codex-Labs\aaemu-1.2-r208022-t124-integration-v1`
- Host base: `62e3eb1d87da01194802ac886cd500134facad28`
- Initial merged focused gate: 192 passed, zero failed.
- One T-124 full suite: 1,954 passed, four intentionally skipped, three
  failed, 1,961 total. Failures were one disabled idle-tick allocation
  invariant and two stable `botdebug` output compatibility invariants.
- Corrections: disabled intake/lifecycle fast paths avoid lock allocation; the
  stable quest-intake line and output ordering are preserved while new giver
  and lifecycle telemetry remains available.
- Corrected merged gate: 251 passed, zero failed, zero skipped.
- Corrected no-incremental `AAEmu.slnx` build: zero errors, 91 retained
  warnings, 10.50 seconds.
- Game assembly SHA-256:
  `9A74BA78E03C40B54A4EFA8015D4117CA1B2650D5407ADF939532E6B803E6B86`
- Unit-test assembly SHA-256:
  `BCE05BB12E4F75600464D6800FAE748731D0445C08445B7C63C410F33B26127D`
- Receipt: `ops/evidence/aaemu12-bot-intelligence-wave-integration-v1.yaml`

## Runtime and boundaries

The registered host, database, client, and T-120 runtime lease were untouched.
The healthy retained T-120 processes remain owned by T-120. T-124 proves source
integration only; arbitrary-level identity creation, signpost quest completion
and chaining, and transfer-road navigation remain physically unproven until a
separately leased live-smoke task.

## Exact next action

Run exactly one corrected full AAEmu 1.2 suite under T-125 against commit
`1a52e7b44fab76939d9409561bdfa4739f1425e6`. Only a green T-125 receipt may
release this combined candidate to a separately leased live smoke.
