# T-045 contract

## Outcome

Wave 2 has one independently reviewed integration commit containing deterministic evidence tooling, the accepted isolated-database receipt, and the combat/stealth qualification harness, with all source gates green on the registered AAEmu 1.2 host.

## Pass

- Review each exact source commit and handoff against its contract, declared scope, and AAEmu 1.2 target; correct only verified defects inside T-045's union scope.
- Start from the dispatched integration branch, incorporate any newer PB-000 bookkeeping ancestor before source commits, and apply exact heads `43956fb9`, `e0e0825c`, and `9ba01280` without modifying worker worktrees.
- Treat T-043's retained schemas/output as immutable proof subjects: validate its committed receipt and paths but do not rerun provisioning or query/mutate the databases during source integration.
- Re-run the T-007 deterministic tests, T-044 offline harness tests, and focused AAEmu 1.2 combat/command groups through registered workspace `aaemu12_integration`.
- Install the candidate idempotently, build with zero errors, and run the full AAEmu 1.2 suite exactly once.
- Reconcile the compatibility-patch fingerprint to one explicit byte source and document why it differs from any older receipt if it does.
- Do not start Login, Game, client, or database-backed runtime work and do not claim or alter the aaemu12 lease.
- Advance `integration/aaemu12-world` by fast-forward only after every review and gate is green; otherwise retain the candidate and leave the branch unchanged.
- Commit a complete version-1 receipt and handoff with the exact PB-000 database-registration and T-036 activation action.

## Non-goals

- Physical mortal combat, stealth, cleanup, or restart acceptance.
- Scale-budget approval or Population Director integration.
- Editing `ops/BOARD.yaml`, `ops/CURRENT.yaml`, `ops/WORKSPACES.yaml`, `ops/RUNTIME-LEASE.yaml`, worker branches, database schemas, client files, or AAEmu 3.0.
