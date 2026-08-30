# PlayerBots runtime

This directory contains the PlayerBots host, decision engine, behavior, movement, social control, and operator surfaces compiled into `AAEmu.Game`.

User documentation lives at the repository root:

- [Installation](../../../docs/INSTALLATION.md)
- [Configuration](../../../docs/CONFIGURATION.md)
- [Commands](../../../docs/COMMANDS.md)
- [Troubleshooting](../../../docs/TROUBLESHOOTING.md)

## Runtime layout

| Directory | Responsibility |
| --- | --- |
| `Host` | Runtime ownership, scheduling, activity, and metrics |
| `Kernel` | Actions, triggers, values, strategies, and decision queues |
| `Content` | Default strategies, rotations, triggers, and actions |
| `Body` | Casting, legal range, facing, movement, and stuck handling |
| `Social` | Party ownership, roles, orders, and formations |
| `Ops` | Startup spawning and operator tasks |

Human-editable runtime data is loaded from `Configurations/BotConfig.json`, `Data/BotArchetypes.json`, and `Data/BotRotations/*.json` beside the running Game executable.

Performance-sensitive changes must keep scans bounded or cached and must be validated against both bot-host and whole-server timing. See [Architecture](../../../docs/ARCHITECTURE.md), [Development](../../../docs/DEVELOPMENT.md), and [Testing](../../../docs/TESTING.md).
