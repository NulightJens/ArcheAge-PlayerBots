# T-044 contract

## Outcome

T-036 can execute repeatable physical cohorts through a deterministic, fail-closed harness that distinguishes mortal combat from matched Idle controls and proves stealth loss, bounded search, reacquisition or clean release, cleanup, and restart boundaries.

## Pass

- The harness defines explicit 1, 5, 10, 25, 50, and 100 bot combat cohorts with matched Idle controls, supplied IDs/targets/buff IDs, bounded timeouts, and no implicit data-pack assumptions.
- It records source/build identity, exact population, fixture identities, command stimuli, server-authoritative combat/search transitions, casts/kills/deaths/recovery, resource samples, cleanup, and restart outcomes without committing raw logs.
- Missing identities, non-isolated databases, absent or ambiguous targets, unknown stealth buffs, incomplete cohorts, stale log offsets, or cleanup/restart gaps produce INCOMPLETE rather than PASS.
- Offline tests exercise parsing and verdict logic for mortal combat, stealth loss, in-radius reacquisition, timeout/radius release, matched Idle controls, cleanup, and malformed/partial evidence; tests never sleep.
- Any product correction is minimal, covered by focused AAEmu 1.2 tests, and stays inside the declared combat/diagnostic seams.
- The handoff separates automated/offline proof from every still-unproven physical claim and gives T-036 exact runtime commands.

## Non-goals

- Starting or stopping AAEmu.
- Selecting or creating a database.
- Scale-budget approval or Population Director integration.
- Editing global ledgers, runtime leases, workspace registrations, client files, or AAEmu 3.0 code.
