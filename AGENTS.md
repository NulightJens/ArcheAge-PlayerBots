# PlayerBots repository operating contract

This repository is the durable source of truth. Chats perform bounded work; chat history is not program memory.

## Required startup

Before changing files, read only:

1. `ops/PROJECT.yaml`
2. `ops/CURRENT.yaml`
3. `ops/BOARD.yaml`
4. `ops/WORKSPACES.yaml`
5. `ops/ENVIRONMENT-CONTINGENCY.md`
6. The assigned `ops/tasks/T-NNN/TASK.yaml` and `CONTRACT.md`

Do not reconstruct current state from old chats, chronological status diaries, raw logs, or `C:\aaemu-playerbots\.planning`. Those are retained evidence, not dispatch authority.

## Track policy

- AAEmu 1.2 r208022 at host commit `62e3eb1d87da01194802ac886cd500134facad28` is the sole active feature and runtime target.
- AAEmu 3.0 is frozen as a compatibility checkpoint. Run its install/build/adapter gate only at major integration or release boundaries.
- Shared behavior stays in this module. Version-specific host calls stay behind compatibility seams.

## Task isolation

- One outcome equals one task, chat, branch, and worktree.
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
- Run the full 1.2 suite once per integration wave, not once per task.
- Reuse a green evidence receipt when its complete fingerprint is unchanged.
- Starting, deploying, controlling, or stopping a live runtime requires the lease in `ops/RUNTIME-LEASE.yaml`.
- Pure module source work may continue while a runtime or client fixture is unavailable, but physical acceptance must remain explicitly pending.
- Never force-stop a process. Use the runtime's graceful command or Ctrl+C path and retain shutdown evidence.

## Handoff

Every task ends with a concise `HANDOFF.md` containing source identity, changed files, proof, retained failures, runtime state, unproven boundaries, and the exact integration action. Do not paste raw logs into handoffs.
