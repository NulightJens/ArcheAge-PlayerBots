# Contributing

Thank you for improving ArcheAge PlayerBots.

1. Install the module in a clean compatible AAEmu checkout as described in the [Installation Guide](docs/INSTALLATION.md).
2. Create a focused branch in the PlayerBots module repository. Use a separate AAEmu integration branch for host-patch work.
3. Keep new module code under `src/AAEmu.Game` and tests under `tests/AAEmu.UnitTests`.
4. Run a non-incremental build and the complete AAEmu unit suite.
5. Describe player-visible behavior, resource impact, and any physical acceptance evidence in the pull request.

Changes to hot paths must stay bounded and measurable. Avoid per-bot global scans, pairwise crowd calculations, and per-tick randomness. Prefer data rotations, cached world values, and native ArcheAge systems.

Do not commit credentials, raw player data, server databases, client assets, recordings, runtime logs, or generated evidence. Read [Development](docs/DEVELOPMENT.md) and [Testing](docs/TESTING.md) for the full review and evidence checklist.
