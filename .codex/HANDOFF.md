# ArcheAge PlayerBots workspace handoff

This file retains completed standalone-module work for the parent ArcheAge integration task. Add new entries without removing earlier history.

## 2026-08-30 — Public documentation and user experience

### Outcome

Reworked the public user journey using `mod-playerbots` as an information-architecture reference: a short front-page explanation, one prominent compatibility requirement, copy/paste installation, a first-bot walkthrough, and task-based installation, configuration, commands, and troubleshooting pages. Maintainer, test, compatibility, and scale material remains available but no longer interrupts the primary setup path.

The documentation now states the product boundary directly: ArcheAge PlayerBots controls selected existing characters. It does not claim random-account populations, automated quest/raid/battleground completion, navmesh navigation, or a public capacity target.

The in-game `/bot` help follows the same user path, replaces the public `develop` topic with `config`, corrects the scale-tool path, and explains supported 1.2 versus experimental server-start-validated 3.0 status. The legacy `develop` and `scale` topic inputs remain accepted for compatibility.

### Branch and implementation commit

- Branch: `docs/public-user-experience`
- Implementation commit: `28a5c60` (`docs: streamline public PlayerBots experience`)

### Files changed

- `README.md`
- `CHANGELOG.md`
- `CONTRIBUTING.md`
- `SECURITY.md`
- `compatibility/README.md`
- `docs/README.md`
- `docs/INSTALLATION.md`
- `docs/CONFIGURATION.md`
- `docs/COMMANDS.md`
- `docs/TROUBLESHOOTING.md`
- `scripts/scale/README.md`
- `src/AAEmu.Game/Bots/README.md`
- `src/AAEmu.Game/Scripts/Commands/AddBot.cs`
- `src/AAEmu.Game/Scripts/Commands/BotHelpCommand.cs`
- `tests/AAEmu.UnitTests/Game/Scripts/Commands/BotHelpCommandTests.cs`

### Verification

- Supported AAEmu 1.2 installer check-only: passed; track `AAEmu12`, state `installed`, status `supported`.
- `dotnet build AAEmu.slnx --no-incremental`: passed with 0 errors and 63 existing warnings.
- `dotnet test AAEmu.UnitTests/AAEmu.UnitTests.csproj --no-build`: passed; 1,734 total, 1,730 passed, 4 intentionally skipped, 0 failed.
- `AAEmu.Game.exe compiler-check`: passed with 0 errors and 0 warnings.
- Static validation: all 18 JSON files, 11 PowerShell scripts, 4 MSBuild target files, local Markdown links, 23 primary command names, Bash installer syntax, and 4 manifest hashes passed.
- `git diff --check`: passed.

### Known limitations

- No live client or bot-behavior run was needed because runtime behavior did not change.
- The experimental 3.0 adapter was not rebuilt in this documentation pass; its existing server-start validation status remains unchanged.
- The user guides are English-only.

### Integration

Merge `docs/public-user-experience` into the standalone module repository, then rebuild the AAEmu host so the updated in-game help and its tests are included. No database migration or compatibility-patch change is required.
