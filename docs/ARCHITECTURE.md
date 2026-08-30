# Architecture

## Why this is a module repository

ArcheAge PlayerBots owns its runtime, content, tests, data, migration, installers, and release history. AAEmu owns the emulator. Operators combine the two at build time rather than consuming a repository that republishes the entire emulator.

AAEmu currently discovers command scripts from its executing Game assembly and does not expose a complete dynamic module lifecycle or dependency-injection extension point. A separately compiled DLL would either create a circular dependency on AAEmu.Game or leave commands and host lifecycle undiscoverable.

The current solution is a static source module:

1. clone the module under `AAEmu/modules/archeage-playerbots`;
2. apply a reviewed compatibility patch that adds narrow host hooks and conditional MSBuild imports;
3. compile module source into `AAEmu.Game` and module tests into `AAEmu.UnitTests`;
4. keep all PlayerBots-owned files and future development in this repository.

This is the closest practical equivalent to the AzerothCore module model until AAEmu provides a stable module API.

## Runtime layers

| Layer | Responsibility |
| --- | --- |
| `Bots/Host` | Scheduling, cadence, activity gating, metrics, runtime ownership, cleanup |
| `Bots/Kernel` | Actions, triggers, values, strategies, decision queue |
| `Bots/Content` | Default strategies, rotations, triggers, and actions |
| `Bots/Body` | Facing, legal-range checks, casting, movement, stuck handling |
| `Bots/Social` | Native-party ownership, roles, orders, and follow formation |
| `Core/Managers/Bots` | Persistent character lifecycle, archetypes, combat coordination |
| `Models/Tasks/Bots` | Low-level movement and combat tasks |
| `Scripts/Commands` | Human and local-administration control surface |

## Lightweight rules

- Use cached/TTL world queries instead of a global scan per bot per tick.
- Express behavior through small actions, triggers, values, or data rotations.
- Do not add pairwise bot separation or continuous crowd scoring without measured need and budget.
- Prefer native ArcheAge party, combat, collision, skill, and persistence systems.
- Measure both bot-host time and whole-server time; optimizing one can move cost elsewhere.
- Any scale claim must include lifecycle cleanup and recovery, not just peak population.

## Host compatibility boundary

The patch is versioned rather than hidden in an installer. Review it like ordinary source code. When supporting a new AAEmu revision, create a new named patch and manifest entry; do not silently mutate the old compatibility contract.

A future AAEmu module API should replace the patch with registration interfaces for services, startup/shutdown, command discovery, party events, character lifecycle, world queries, and tick metrics. Module source/content can remain unchanged when that boundary exists.
