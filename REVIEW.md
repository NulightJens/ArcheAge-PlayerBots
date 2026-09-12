# Review rules

Reader: anyone reviewing a pull request here, including outside contributors. `CONTRIBUTING.md` is the contributor guide; `AGENTS.md` points agents to `docs/agents/`.

## This repository

The public release of the ArcheAge PlayerBots module for AAEmu 1.2. It is produced from a private workshop by an allowlist copy, never by merge, so private history is never an ancestor of `main`.

## Belongs here

`src/`, `tests/`, `build/`, `sql/`, `assets/`, install and preview scripts under `scripts/`, public docs under `docs/`, public host patches under `compatibility/`, `CHANGELOG.md`, `CONTRIBUTING.md`, `SECURITY.md`, the licences, this repository's own `README.md` and `AGENTS.md`.

## Does not belong here

`internal/`, `provenance/`, `eng/`, `tools/`, `history/`, `STATE.md`, `CLAUDE.md`, private task records, machine paths, evidence. Any reference in `src/` or `tests/` to `Autopilot`, `BotClient`, `Telemetry`, `DeveloperConsole`, `OfflineBai`, `PrebuiltRoad` or `NavigationDebugExporter`: those are add-ons with their own repositories, and the boundary guard fails on them.

## Also request changes when

- A pull request rewrites a released host patch, `playerbots.module.json` or a migration hash in place; released artifacts are immutable, a new version is added instead.
- The 3.0 adapter changes beyond keeping it compiling.
- Public docs gain undefined terms, machine paths or private links.

## Every pull request must

- Name one task (a record under `internal/tasks/` in the workshop, or the brief it came from) and stay inside it.
- State the check it ran and the commit it ran on.
- Pass the boundary guard where one exists, and carry no evidence, binaries, machine paths, process ids, character names or credentials.
- Write anything for a person in short factual sentences; define a term or drop it.

## Request changes when

- A file belongs in another repository of the family (see the table in the workshop's `internal/steward/CHARTER.md`).
- Something already exists elsewhere: a duplicated helper, a second "start here" or "current" page, a repeated build guide.
- The change does more than the task asked: an extra feature, a refactor of untouched files, a formatting sweep, a new dependency with no line in the task.
- History is rewritten, a branch is force-pushed or deleted, or `main` is pushed to directly.

## Never

- Merge, close a pull request you did not open, delete a branch, deploy, tag or publish. Those are Jens's calls; list candidates with reasons.
