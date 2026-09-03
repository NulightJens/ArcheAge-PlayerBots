# T-059 handoff

T-059 is green and handoff-ready. The accepted T-058 qualified active-bot
anchor was independently replayed onto the exact assignment base, advanced
into the registered AAEmu 1.2 installed module by fast-forward, and qualified
by installer idempotence, the 70 focused tests, a full solution build, and one
complete unit-suite invocation. T-057 remains `INCOMPLETE` and was not
reinterpreted or altered.

## Source and installation identity

- Task/base: `T-059` at resolved assignment head
  `1c338854ff3b2bad16823011805f80652349d6b1`; the contract's
  `06e57bfa427d79d7e96dd311dbb2a7716e64b5cf` is the verified
  pre-preparation parent.
- Accepted T-058 source/parent:
  `5bd425327367b72871aef2eae7e51655a53f7bbc` /
  `fef939a460f262269f2586efd2e794fdcbe6f704`.
- Independently replayed and tested module source:
  `cf57b11474b9e7f3e9ece588dc3aea0a56c02ef9`, tree
  `1e15fa38ef2f91c62f9a5a72709703d3acd1505a`.
- Registered installed module: the same commit/tree, clean after proof.
- Registered host: pinned base
  `62e3eb1d87da01194802ac886cd500134facad28`, retaining the exact documented
  30-entry installed state.
- Active patch SHA-256:
  `0285c8c21133ec00abbd8f5de925c56dc2d87f7d8595fee89a6063b3ff02ca3e`.
- Receipt: `ops/evidence/aaemu12-qualified-anchor-integration-v1.yaml`.

## Review, changed files, and deployment

The source replay changed exactly T-058's six files: `CHANGELOG.md`,
`docs/COMMANDS.md`, `SystemActor.cs`, the controller and `spawnpassive` tests,
and T-058's handoff. T-059 then adds only this handoff and the sanitized
receipt. No global ledger, runtime lease, retained evidence, compatibility
file, manifest, `SpawnPassiveNpcCommand.cs`, or AAEmu 3.0 artifact changed.

Independent review confirmed the stable lowest-character-ID selection from an
active-bot snapshot, detached world-transform capture, same-world and matching
instance checks, non-zero-zone and finite-transform qualification, worldless
fallback, a fresh controller actor per request, zero world-player
registration, and the unchanged `spawnpassive` world guard. All six replayed
blobs and the stable patch ID match the candidate exactly.

The installed module fast-forwarded non-destructively from
`1d08632d303b35e7c2d5655de5b5e2b704896cc8` to the tested replay. The existing
compatibility patch was neither reapplied nor regenerated; it applies to the
clean reference and reverse-checks the installed host. Zero host source files
were overwritten.

## Proof

- Installer check-only reported `installed/supported` before and after a
  successful idempotent normal child-process run. The host's exact 30-entry
  status snapshot remained unchanged.
- All five declared manifest hashes reproduced; compatibility and manifest
  content is byte-identical to the accepted installed baseline.
- Full solution build: 0 errors, 76 warnings.
- Focused tests: 70 passed, 0 skipped, 0 failed: controller 5,
  `spawnpassive` 11, character manager 33, and bot manager 21.
- Full AAEmu 1.2 suite, exactly one invocation: 1,796 passed, 4 declared legacy
  skips, 0 failed (1,800 total).
- Final reference and installed-module cleanliness, module commit/tree, patch
  forward/reverse checks, manifest hashes, host status, process observation,
  and ports 1234/1237/1239/1250/1280 all passed.

## Retained failures, runtime state, and unproven boundary

There are no product or gate failures. The build retained the prior 75
dependency/compiler/analyzer warnings and added one analyzer warning in the
new test helper. Three corrected operator-only probe assumptions are recorded
in the receipt; none changed a product result or mutated the host.

No Login, GameServer, MySQL, ArcheAge client, or AAEmu runtime process was
started, stopped, or controlled. No runtime lease was claimed or edited, and
no database or client fixture was accessed. Final observation found zero
Login/Game/client processes, the same two pre-existing MySQL PIDs `6308` and
`8076` untouched, and no listener on the five required ports. Physical
passive-NPC placement, combat, stealth, cleanup, restart, scale, soak, and
Population Director acceptance remain unproven.

## Exact integration action

PB-000 should fast-forward `integration/aaemu12-world` through the commit
containing this T-059 handoff, record
`cf57b11474b9e7f3e9ece588dc3aea0a56c02ef9` and tree
`1e15fa38ef2f91c62f9a5a72709703d3acd1505a` as the tested and installed module
identity, accept `aaemu12-qualified-anchor-integration-v1`, and mark T-058
integrated and T-059 done. PB-000 may then dispatch a fresh serialized AAEmu
1.2 live-headless physical attempt with a new runtime-lease claim and new
immutable evidence version. Never reuse or alter T-057's `public-alpha-v4`
evidence.
