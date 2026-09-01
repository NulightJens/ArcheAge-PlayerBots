# T-064 handoff

T-064 is green. The accepted T-062 atomic combat fan-out and T-063 bounded
single-bot lifecycle were independently reviewed, replayed, installed, and
qualified against the registered AAEmu 1.2 host. A new immutable T-061 runtime
proof may now be dispatched; the retained T-061 v1 failure remains unchanged.

## Source and installation identity

- Declared source baseline: `0fcb943552dcefde95bc2af220caab6bfafa77ef`.
  The task-dispatch and resolved-owner commits after it changed only task/owner
  metadata; their replayed source paths were byte-identical to the baseline.
- Accepted T-062 source/parent: `4c2d9e8ccdfa1255cef396388ebba2423aa35624`
  / `10fb2de0038ecb43d3f2e6cf15f5789e2b5926dd`.
- Accepted T-063 source/parent: `aadcef712c6ca6c1d797375476611b71cbf86178`
  / `f22ae76d8a401ad374f74df55a6ef6a675c3ad1f`.
- Independently replayed, tested, and installed source:
  `401dd353ef37aa858763b41dc2e5c1663446ff57`, tree
  `6803d1d7e1d287798390e5aed0b835b9c8f12aef`.
- Registered host: pinned base
  `62e3eb1d87da01194802ac886cd500134facad28`, retaining the exact documented
  30-entry status snapshot `3ba59378bacebcfafe53e84f59201a0eba09140b`.
- Active compatibility patch SHA-256:
  `0285c8c21133ec00abbd8f5de925c56dc2d87f7d8595fee89a6063b3ff02ca3e`.
- Receipt: `ops/evidence/aaemu12-lifecycle-fanout-integration-v1.yaml`.

## Review and deployment

The replay changed exactly the 13 T-062/T-063 source, test, documentation, and
handoff paths declared by T-064. Every replayed blob matches its accepted
candidate. No worker task metadata, global ledger, runtime lease, retained
T-060/T-061 evidence, compatibility file, manifest, database, client, or AAEmu
3.0 artifact changed.

Review confirmed one complete cohort setup and baseline snapshot before a
single synchronous `botattackobject all <targetObjId>` stimulus, with per-ID
debug and cleanup retained. It also confirmed exact-one-bot fail-closed
activation, a one-kill grind bound, no target/destination selection,
deterministic `nearby_mortal` decision visibility, authoritative kill credit,
a targetless/noncombat logout boundary, and the normal persisted logout
callback deferred until after runtime iteration and locks.

The installed module fast-forwarded non-destructively from
`cf57b11474b9e7f3e9ece588dc3aea0a56c02ef9` to the tested replay. Installer
check-only passed before and after an idempotent normal installer run. The
compatibility patch was neither reapplied nor regenerated, no host source file
was overwritten, and the host status snapshot remained unchanged.

## Proof

- Deterministic combat harness: PASS for 13 verdict scenarios, six atomic
  cohort plans, byte-stable generation, overwrite refusal, and zero sleeps.
- Complete solution build: 0 errors, 76 retained warnings.
- Directly affected lifecycle/host/combat/manager/command selection: 141 passed,
  0 skipped, 0 failed.
- Full AAEmu 1.2 unit suite, exactly one invocation: 1,803 passed, 4 intentional
  legacy golden skips, 0 failed (1,807 total).
- Final reference, task worktrees, and installed module were clean; the patch
  forward/reverse checks, exact source/install identity, host snapshot, zero
  Login/Game/client processes, and free ports 1234/1237/1239/1250/1280 passed.

## Retained failures and runtime boundary

There is no product or gate failure. Three corrected operator-only probe
assumptions are retained in the receipt; none mutated source, host, or evidence.
No runtime, client, MySQL, database, or lease was started, controlled, accessed,
or changed. T-060 remains `INCOMPLETE`, and T-061 v1 remains `FAIL`. Physical
activity selection, mortal combat completion, normal persisted logout, and
restart persistence still require a fresh serialized runtime proof.

## Exact integration action

The Integrator must fast-forward the saved `integration/aaemu12-world` branch
through this handoff commit without changing global ledgers. PB-000 may then
accept the receipt and dispatch a new immutable T-061 proof version with a new
AAEmu 1.2 runtime-lease claim. The new proof must use tested/installed source
`401dd353ef37aa858763b41dc2e5c1663446ff57` and tree
`6803d1d7e1d287798390e5aed0b835b9c8f12aef`; never reuse or modify retained
`aaemu12-t061-one-bot-autonomy-v1` evidence.
