# PlayerBots repository operating contract

This repository is the durable source of truth. Chats perform bounded work; chat history is not program memory.

## Required startup

Before changing files, read only:

1. `ops/PROJECT.yaml`
2. `ops/CURRENT.yaml`
3. `ops/GOALS.yaml`
4. `ops/DELIVERY-MODEL-V2.md`
5. `ops/BOARD.yaml`
6. `ops/WORKSPACES.yaml`
7. `ops/ENVIRONMENT-CONTINGENCY.md`
8. The assigned `ops/tasks/T-NNN/TASK.yaml` and `CONTRACT.md`

Do not reconstruct current state from old chats, chronological status diaries, raw logs, or `C:\aaemu-playerbots\.planning`. Those are retained evidence, not dispatch authority.

## Track policy

- AAEmu 1.2 r208022 at host commit `62e3eb1d87da01194802ac886cd500134facad28` is the sole active feature and runtime target.
- AAEmu 3.0 is frozen as a compatibility checkpoint. Run its install/build/adapter gate only at major integration or release boundaries.
- Shared behavior stays in this module. Version-specific host calls stay behind compatibility seams.

## Outcome-driven delivery

- `ops/GOALS.yaml` owns product status. Task count, source gates, receipts, and handoffs do not advance the roadmap.
- At most one product goal is the active vertical slice. It owns source, focused proof, deployment, correction, and live acceptance.
- Keep failures in the active slice instead of creating preparation, failure-translation, replay, and integration task chains.
- New work uses at most three concurrent durable workspaces: Control Tower/integration, one slice worktree when needed, and the lease-controlled AAEmu integration/runtime checkout. Retained historical worktrees do not authorize parallel roadmap claims.
- A task may edit only its declared `write_scope`.
- Only the Control Tower edits `ops/BOARD.yaml`, `ops/CURRENT.yaml`, or track decisions.
- Only an Integrator changes an integration branch or `main`.
- Writer tasks must not modify deployed AAEmu hosts as source trees.
- Subagents may perform bounded research, test design, log analysis, or review. Independent writers require independent worktrees.

## Workspace routing

- Refer to server, client, module, and evidence locations by the IDs in `ops/WORKSPACES.yaml`; never select a path because its name looks current.
- Obey each workspace's `access` mode. `read-only`, `evidence-only`, and `frozen` locations are never writer targets.
- The AAEmu 1.2 reference checkout must remain clean at the pinned commit. Installation and build proofs use the lease-controlled integration checkout.
- Client fixtures are read-only. Never edit a client or `game_pak` as part of a source task.
- If a registered path is missing, dirty beyond its declared state, or at the wrong revision, follow `ops/ENVIRONMENT-CONTINGENCY.md` instead of substituting another checkout.

## Verification and runtime

- Run focused AAEmu 1.2 tests in writer tasks.
- Deploy a focused green behavior to the registered runtime early; do not accumulate a multi-feature wave before live feedback.
- Run the full 1.2 suite at the end of an accepted vertical slice or after shared infrastructure changes, not once per task.
- Reuse a green evidence receipt when its complete fingerprint is unchanged.
- Starting, deploying, controlling, or stopping a live runtime requires the lease in `ops/RUNTIME-LEASE.yaml`.
- Every feature acceptance requires the same fingerprinted candidate on a real server plus an active client witness and native server outcome markers. Headless tests support regression, persistence, and scale but cannot produce a product PASS by themselves.
- Pure module source work may continue while a runtime or client fixture is unavailable, but it remains `source-green` or `integrated`, never product-complete.
- Commands may stage a fixture, start a scenario, and inspect it. They may not choose each target, teleport a route, inject quest credit, or complete an objective for the AI.
- Never force-stop a process. Use the runtime's graceful command or Ctrl+C path and retain shutdown evidence.

## Handoff

Every implementation task ends with a concise `HANDOFF.md` containing source identity, deployed identity, state-ladder position, changed files, proof, retained failures, runtime/client state, unproven boundaries, and the exact next action. Do not paste raw logs into handoffs.
