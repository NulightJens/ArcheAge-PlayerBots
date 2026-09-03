# Environment contingency runbook

This runbook prevents path ambiguity, lost context, and one broken environment from stopping parallel source work.

## Missing or mismatched workspace

Do not choose a similar checkout. Record the exact missing path or revision in the task handoff. Continue only work that stays inside the task's module write scope, and mark any host, client, build, or physical gate as `validation-pending`.

## Dirty reference checkout

Do not clean, reset, stash, or repurpose it. Retain it as evidence, provision the next versioned clean checkout from the pinned commit, verify it, and update `ops/WORKSPACES.yaml` in a Control Tower commit.

## Contaminated or failed integration host

Retain the current integration host and its evidence. Create an incremented path such as `aaemu-1.2-r208022-integration-v2`; never overwrite the registry entry until the replacement passes lineage and clean-state checks. Runtime and full-suite acceptance remain blocked, while independent source tasks continue.

## Missing or wrong client

Never modify or substitute another client. Block only physical client/runtime acceptance. Unit, adapter, installation, and build work may continue if their own inputs are valid.

## Host seam change required

Writers do not edit a deployed host. Open a bounded host-adapter task whose deliverable is a reviewed compatibility patch plus tests in the module repository. The Integrator alone applies it to the lease-controlled host.

## Runtime lease conflict

Wait or run source-only tests. A timeout, stalled chat, or quiet process does not transfer the lease. The Control Tower changes ownership only after shutdown and cleanup evidence is recorded.

## Control Tower context compacts

Chat history is disposable. Rehydrate from `AGENTS.md`, `ops/PROJECT.yaml`, `ops/CURRENT.yaml`, `ops/BOARD.yaml`, `ops/WORKSPACES.yaml`, this runbook, and the relevant task handoffs. Do not replay old work unless the ledger says its proof is missing.

## Worker loses context or task disappears

Reread the required startup files and the assigned task contract. If the task cannot be recovered, leave its branch and worktree untouched, mark the lane `recovery-needed`, and dispatch a new task against that retained branch. Never silently duplicate the outcome on a new branch.

## Safe forward path

When recovery would normally delete, reset, or overwrite state, retain the old state and create a versioned replacement. Every replacement gets a new registry ID or versioned path and an evidence note explaining why it exists.
