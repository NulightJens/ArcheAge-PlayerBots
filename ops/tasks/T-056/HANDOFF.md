# T-056 handoff

T-056 is green and handoff-ready. The accepted T-055 `@system` world-context
correction was independently replayed onto the resolved assignment head,
installed through the registered module fast-forward workflow, and qualified by
the full AAEmu 1.2 build, focused regressions, and one complete unit-suite run.
T-054 remains `INCOMPLETE` and was not reinterpreted or altered.

## Source and installation identity

- Task/branch: `T-056` / `codex/t056-system-actor-integration`.
- Pre-preparation integration head: `6aece206d74a606c5bfbaa366e8501ab6d804f74`;
  resolved assignment head: `305ac25513aa56a5ef664a53444c4f6eb5e11a01`.
- Accepted T-055 source/parent:
  `68cf32fbe3012364fb0e1dec2c8f6278015e35eb` /
  `3bdeedadbe8818c29079bb495b93fcd8fe7582f8`.
- Independently replayed and tested module source:
  `1d08632d303b35e7c2d5655de5b5e2b704896cc8`, tree
  `1f3befa4dc3c01222b057c28279e82d25fcc176e`.
- Registered installed module: the same commit/tree, clean after proof.
- Registered host: pinned base
  `62e3eb1d87da01194802ac886cd500134facad28`, retaining the exact documented
  30-entry installed state.
- Active patch SHA-256:
  `0285c8c21133ec00abbd8f5de925c56dc2d87f7d8595fee89a6063b3ff02ca3e`;
  stable patch ID `7891d6bb88fd8b76f66067c15f064b611323d642`.
- Receipt: `ops/evidence/aaemu12-system-actor-integration-v1.yaml`.

## Changed files and deployment

The source replay changed exactly T-055's declared eight files: `CHANGELOG.md`,
the active 1.2 compatibility README and patch, `playerbots.module.json`,
`SystemActor.cs`, the controller and `spawnpassive` tests, and T-055's handoff.
T-056 then adds only this handoff and the sanitized receipt. No global ledger,
runtime lease, retained T-054 evidence, or AAEmu 3.0 artifact changed.

The installed module fast-forwarded non-destructively from `c0ae8898806ff3cc3e4a20107247a2b11ab9dcfe`
to the tested replay. The regenerated patch is byte-different but has the same
stable patch ID as the installed patch, applies cleanly to the pinned reference,
and reverse-checks the installed host. It was therefore not reapplied; zero host
source files were overwritten.

## Proof

- Candidate parent, clean worktree, eight-file scope, declared SHA-256, replay
  patch ID, and all eight replayed blobs matched; `git diff --check` passed.
- The pinned reference remained clean and accepted the complete patch. The
  installed host reverse-check passed before and after qualification.
- Four active-track/install manifest hashes reproduced. Installer check-only
  reported `installed/supported` before and after a successful idempotent normal
  run; the host's exact 30-entry status snapshot was unchanged.
- Full solution build: 0 errors, 75 retained warnings.
- Focused tests: 49 passed, 0 skipped, 0 failed: controller/system actor 3,
  `spawnpassive` 10, access levels 3, and character manager 33.
- Full AAEmu 1.2 suite, one invocation: 1,793 passed, 4 declared legacy skips,
  0 failed (1,797 total).
- Final reference and installed-module cleanliness, module commit/tree, host
  reverse-check, process observation, and ports 1234/1237/1239/1250/1280 all
  passed.

## Retained failures, runtime state, and unproven boundary

There are no product or gate failures. The 75 build warnings retain existing
dependency, compiler, analyzer, and test-analyzer findings, including the known
SQLitePCLRaw.lib.e_sqlite3 and SSH.NET advisories. Three corrected operator-only
probe assumptions are recorded in the receipt; none changed a product result.

No Login, GameServer, MySQL, or ArcheAge client process was started, stopped, or
controlled. No runtime lease was claimed or edited, and no database or client
fixture was accessed. Final observation found zero Login/Game/client processes
and no listener on the five required ports. Physical passive-NPC creation,
combat, stealth, cleanup, restart, scale, soak, and Population Director
acceptance remain unproven.

## Exact integration action

PB-000 should fast-forward `integration/aaemu12-world` through the commit
containing this T-056 handoff, record `1d08632d303b35e7c2d5655de5b5e2b704896cc8`
and tree `1f3befa4dc3c01222b057c28279e82d25fcc176e` as the tested and installed
module identity, accept `aaemu12-system-actor-integration-v1`, and mark T-055
integrated and T-056 done. PB-000 may then dispatch a fresh serialized AAEmu
1.2 physical attempt with a new runtime-lease claim and new immutable
`public-alpha-v4` evidence. Never reuse or alter T-054's `public-alpha-v3`
evidence.
