# T-046 contract

## Outcome

The retained T-045 Wave 2 candidate is independently reconstructed on the current
integration ancestor and receives one fresh, green AAEmu 1.2 full-suite receipt
before `integration/aaemu12-world` advances.

## Pass

- Start from the dispatched `integration/aaemu12-world` ancestor containing this
  contract and the retained T-045 handoff. Do not edit the T-045 worktree.
- Reapply, in order, exact retained commits `0dd9e80a`, `1453c041`, `568af9c3`,
  `bc872d26`, and `225595e`; verify their stable patch identities against retained
  candidate `225595ebe45d5b0c08bfa0a1059cb8ba97669f86`.
- Independently review both T-045 corrections, especially restart-process binding
  and the authorized exact-NPC `botbuffnpc` alias form. Correct only a verified
  defect inside T-046's declared union scope, before the full-suite invocation.
- Validate T-043's committed receipt and retained paths without reprovisioning,
  querying, or mutating either isolated schema.
- Re-run the T-007 deterministic tests, the final 13-scenario T-044 offline
  harness, and focused AAEmu 1.2 combat, command, and access-level tests.
- Install the candidate idempotently into registered `aaemu12_integration`, build
  with zero errors, and invoke the complete AAEmu 1.2 suite exactly once after
  every focused gate is green.
- Write `ops/evidence/aaemu12-wave2-integration-v1.yaml` with exact source,
  installed-module, host, patch, database-receipt, gate, and boundary fingerprints.
- Fast-forward `integration/aaemu12-world` only after every declared gate is
  green and the candidate worktree is clean. Otherwise retain the new candidate,
  commit a blocked handoff, and leave the integration branch unchanged.
- Commit `HANDOFF.md` with source identity, changed files, proof, retained
  failures, runtime state, unproven boundaries, and the exact PB-000 action for
  database registration and T-036 activation.

## Non-goals

- Physical mortal combat, stealth, cleanup, restart, or client acceptance.
- Database provisioning, queries, mutations, resets, drops, or credential changes.
- Starting Login, Game, client, or MySQL processes; claiming the runtime lease.
- Editing global ledgers, worker branches, AAEmu 3.0, or client fixtures.
