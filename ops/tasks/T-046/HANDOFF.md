# T-046 handoff

## Verdict

PASS. The retained T-045 Wave 2 payload was reconstructed on the dispatched
integration lineage, both corrections were independently reviewed, every focused
gate passed, and the complete AAEmu 1.2 suite was invoked exactly once with a
green result. `integration/aaemu12-world` may advance to the commit containing
this handoff and `ops/evidence/aaemu12-wave2-integration-v1.yaml`.

## Source identity

- Dispatched ancestor: `02d09510192662f26f2ca8d3ca3cff139d584130`.
- Incorporated control-only T-046 setup: `f46e5c29b108b720bdd23d45b2d450bda79ccbd5`.
- Retained T-045 candidate: `225595ebe45d5b0c08bfa0a1059cb8ba97669f86`.
- Reconstructed and tested module source: `9938495eb7f1904ac4575bc0db42fc28a7f35851`.
- The exact T-007, T-043, T-044, restart-binding, and buff-alias patches were
  replayed in order. Their stable patch IDs match the retained candidate, and
  the scripts/product/tests/worker-receipt payload is identical.
- Final integrated commit: the immutable commit containing this handoff, reported
  by the Integrator after creation.

## Review and changed files

- Restart proof now binds the prior PID and normalized process start time to the
  initially qualified process; serialized operator artifacts have regression
  coverage.
- `botbuffnpc` is an alias of the registered and authorized `botbuff` primary.
  AAEmu resolves that alias before access checks, and the exact-NPC form requires
  the explicit fourth ability-level argument. No T-046 source correction was
  needed.
- T-046 adds only
  `ops/evidence/aaemu12-wave2-integration-v1.yaml` and this handoff after the five
  exact replayed commits. No global ledger was edited by the Integrator.

## Proof

- T-007 deterministic receipt tooling: 53 assertions across 18 retained attempts,
  all passed.
- T-044 offline harness: 13 verdict scenarios, parser/JSON checks, and prohibited
  sleep/process-operation scan passed.
- T-043 receipt/path validation: exact four retained artifacts, all SHA-256 and
  size values matched, with 100 unique IDs `20001` through `20100`. The donor is
  clean at `72fd2cc70bba3887e093e8a3800f13fed9b3e111`. Neither schema was queried or
  mutated. The historical receipt's AAEmu 3.0 patch hash remains disclosed and
  unchanged; the canonical AAEmu 1.2 patch hash is
  `afbb6aa2c5d379eb76ad339642c190f4185b4981815aa27c68d0680d28edd046`.
- Registered `aaemu12_integration` installer check and idempotent install passed.
- Single-worker, node-reuse-disabled solution build: 0 errors, 75 retained
  warnings.
- Focused AAEmu 1.2 combat/command/access proof: 120/120 passed (13 manager,
  38 state, 15 task, 43 command, 8 passive-NPC, and 3 access-level tests).
- Complete AAEmu 1.2 suite, invoked exactly once: 1,793 total; 1,789 passed,
  4 intentional legacy skips, 0 failed.
- `git diff --check` and candidate/reference/installed-module cleanliness passed.

## Retained failures and warnings

- No product or gate failure remains.
- Two read-only fingerprint probes initially used PowerShell automatic-variable
  names, and one dump scan used a Windows-incompatible wildcard. Corrected
  read-only probes passed and changed no source, artifact, runtime, or database.
- The build retains existing dependency/compiler/analyzer warnings, including
  the SQLitePCLRaw.lib.e_sqlite3 and SSH.NET high-severity advisory warnings.

## Runtime and unproven boundaries

- No Login server, Game server, client, or MySQL process was started, stopped, or
  controlled. The runtime lease was not claimed or changed.
- Neither isolated schema was queried or mutated, and provisioning was not rerun.
- Global ledgers, the workspace registry, AAEmu 3.0, client fixtures, and retained
  worker worktrees were untouched.
- Physical mortal combat, stealth, cleanup, restart, client, resource-budget,
  scale, and Population Director acceptance remain unproven.

## Exact integration and PB-000 action

Fast-forward `integration/aaemu12-world` to the immutable commit containing this
handoff and the Wave 2 receipt. After confirming that commit, PB-000 should make
one control-only bookkeeping commit that:

1. registers `aaemu12_database_public_alpha_v1` in `ops/WORKSPACES.yaml` with
   Game `aaemu_playerbots_game_public_alpha_v1`, Login
   `aaemu_playerbots_login_public_alpha_v1`, retained directory
   `D:\Codex-Labs\aaemu-1.2-playerbots-database-public-alpha-v1`, donor
   `aaemu12_legacy_t022`, receipt path, provenance hash, and lease-controlled
   access, without credentials;
2. records the accepted integration commit and
   `ops/evidence/aaemu12-wave2-integration-v1.yaml` in `ops/BOARD.yaml` and
   `ops/CURRENT.yaml`, marking T-007, T-043, T-044, and T-046 integrated/done
   while retaining T-045 as the historical red recovery predecessor;
3. updates `aaemu12_integration.installed_module_head` and its evidence pointer,
   and sets `ops/RUNTIME-LEASE.yaml` `aaemu12.database_identity` to the new
   registration; and
4. activates and assigns T-036 under the still-serialized, otherwise unclaimed
   `aaemu12` lease. T-037 and T-041 remain blocked on their declared dependencies.
