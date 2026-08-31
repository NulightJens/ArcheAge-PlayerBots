# T-042 contract

## Outcome

Wave 1 has one independently reviewed integration commit containing T-038, T-039, and T-040, with focused regressions and the complete AAEmu 1.2 unit suite proven against the registered integration environment.

## Pass

- Review each source commit and handoff against its task contract, declared write scope, and the active AAEmu 1.2 target; correct only defects inside T-042's union write scope.
- Build the candidate from the dispatched `integration/aaemu12-world` HEAD, verify that it descends from dispatch commit `8a956da5`, and apply exact source heads `c89b77b6e`, `dcc27f42`, and `a9966305` without modifying any worker checkout.
- Re-run all three focused regression groups through workspace `aaemu12_integration`; legacy or preview checkouts are not proof inputs.
- Run the full AAEmu 1.2 suite once for the merged candidate and retain a complete version-1 receipt fingerprint.
- Do not start a client, Login, Game, or database and do not acquire the runtime lease.
- Advance `integration/aaemu12-world` with a fast-forward only after every required review and gate is green. If any gate is red, retain the candidate and leave the integration branch unchanged.
- Write a concise handoff with the integrated commit, changed files, proof, retained failures, environment state, unproven runtime boundaries, and the exact Control Tower bookkeeping action.

## Non-goals

- Live movement, lifecycle, roster, or restart acceptance.
- Population Director wiring.
- Runtime or database provisioning.
- AAEmu 3.0 compatibility regression.
- Editing `ops/BOARD.yaml`, `ops/CURRENT.yaml`, track decisions, runtime leases, workspace registry entries, or worker source branches.
