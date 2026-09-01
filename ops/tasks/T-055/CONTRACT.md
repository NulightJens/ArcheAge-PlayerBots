# T-055 contract

## Outcome

The synthetic loopback `@system` actor receives the active AAEmu 1.2
`MainWorld` and its matching spawn position, allowing world-positioned GM
commands such as `spawnpassive` to execute headlessly without registering a
real player or weakening command authorization.

## Pass

- Reproduce T-054's exact boundary in a deterministic test: a freshly created
  `@system` actor currently has a spawn transform but null `ParentWorld`, so a
  world-positioned command cannot proceed.
- Correct `SystemActor.Create` so the actor's `ParentWorld` and spawn position
  come from the same active world instance. Preserve safe behavior when the
  world manager is unavailable during early startup or isolated tests.
- Do not register the synthetic actor as a player, create an account or
  connection, change its exact name or administrative access, or make it
  persist across loopback requests.
- Add deterministic coverage proving a fresh system actor has the active world,
  preserves access level 100/account 0/no connection, and keeps existing
  headless non-world commands working. Prove the `spawnpassive` rejection is no
  longer caused by null `ParentWorld`; do not fake a PASS by weakening that
  command's world guard.
- Regenerate the AAEmu 1.2 compatibility patch and its SHA-256 in every manifest
  location without changing the frozen AAEmu 3.0 patch or identity. Prove the
  complete 1.2 patch applies cleanly to registered workspace
  `aaemu12_reference` using `git apply --check` only.
- Run parser/manifest/hash checks, relevant focused AAEmu 1.2 tests, the
  deterministic command/controller tests, and `git diff --check`. Retain exact
  commands and counts in the handoff.
- Commit only the declared source, tests, manifest/docs, and concise handoff.
  State the exact integration action and the still-unproven physical boundary.

## Non-goals

Deploying to an AAEmu host; starting Login, GameServer, MySQL, or a client;
claiming a runtime lease; accessing databases; creating physical fixtures;
rerunning combat/stealth; changing `spawnpassive`'s safety guard; Population
Director, scale, soak, release, or AAEmu 3.0 compatibility changes.
