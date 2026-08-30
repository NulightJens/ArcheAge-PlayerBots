<p align="center">
  <img src="assets/playerbots-readme-banner.png" alt="ArcheAge PlayerBots — melee, archer, mage, and healer companions overlooking an ArcheAge coastline" width="100%" />
</p>

<p align="center">
  <a href="https://github.com/NulightJens/ArcheAge-PlayerBots/actions/workflows/validate.yml"><img src="https://github.com/NulightJens/ArcheAge-PlayerBots/actions/workflows/validate.yml/badge.svg" alt="Build and tests" /></a>
</p>

# ArcheAge PlayerBots Module

ArcheAge PlayerBots is an [AAEmu](https://github.com/AAEmu/AAEmu) module that lets existing characters play as server-controlled companions while their clients are offline.

Features include:

- logging in an existing character as a bot and saving it through normal logout;
- native party roles and follow, stay, attack, and passive orders;
- melee, archer, caster, healer, and tank behavior;
- seven included archetypes with data-driven combat rotations;
- in-game configuration reloads, diagnostics, and performance metrics;
- optional server-start spawning for selected character IDs.

## Installation

> **Important:** PlayerBots must be installed into a compatible AAEmu checkout at `modules/archeage-playerbots`. It is not a drop-in DLL and it will not build against an arbitrary AAEmu revision. The installer validates the server before making changes.

### Quick start

From the root of a clean, compatible AAEmu checkout on Windows:

```powershell
New-Item -ItemType Directory -Force modules | Out-Null
git clone https://github.com/NulightJens/ArcheAge-PlayerBots modules/archeage-playerbots
& .\modules\archeage-playerbots\scripts\Install-PlayerBots.ps1 -AAEmuRoot $PWD
dotnet build AAEmu.slnx --no-incremental
```

On Linux or macOS:

```bash
mkdir -p modules
git clone https://github.com/NulightJens/ArcheAge-PlayerBots modules/archeage-playerbots
./modules/archeage-playerbots/scripts/install-playerbots.sh "$PWD"
dotnet build AAEmu.slnx --no-incremental
```

The default installer track supports ArcheAge 1.2 `r208022`. ArcheAge 3.0 is available only as an experimental, server-start-validated track; it is not ready for normal gameplay servers.

See the **[Installation Guide](docs/INSTALLATION.md)** for supported AAEmu versions, database setup, updates, and the experimental 3.0 track.

## First bot

Log in with a GM character and choose the ID of an existing offline character:

```text
/addbot 2
/botstate 2 grind
```

Invite the bot through the normal ArcheAge party UI, then give it a role and an order:

```text
/botcontrol 2 role attacker
/botcontrol 2 follow
/botcontrol 2 attack
```

When finished, save and log out the bot:

```text
/removebot 2
```

Replace `2` with your character ID. `/removebot` does not delete the character.

## Documentation

Browse the [PlayerBots Guide](docs/README.md) or jump directly to a task:

| Guide | Description |
| --- | --- |
| **[Installation Guide](docs/INSTALLATION.md)** | Install, verify, update, or select a supported server track |
| **[Configuration](docs/CONFIGURATION.md)** | Configure startup bots, behavior, and performance settings |
| **[Commands](docs/COMMANDS.md)** | Everyday party commands and advanced GM tools |
| **[Troubleshooting](docs/TROUBLESHOOTING.md)** | Fix common install, build, database, and gameplay problems |

Run `/bot` in game for the quick command guide or `/help <command>` for exact arguments.

## Compatibility

| ArcheAge version | Status |
| --- | --- |
| 1.2 `r208022` | Supported on the documented AAEmu base |
| 3.0.4.2 `r336598` | Experimental; matching assets and server startup are validated, but client login and bot gameplay acceptance are still open |

PlayerBots uses a small, versioned AAEmu compatibility patch because AAEmu does not yet expose every lifecycle and command hook through a module API. The module remains a separate repository and the installer applies the matching integration automatically.

## Current limits

- PlayerBots controls existing characters you select; it does not generate random bot accounts or claim automated quest, raid, or battleground completion.
- Bots use direct pursuit and native collision, not navmesh navigation, so obstructed terrain can cause poor paths.
- Jump presentation and stealth search behavior are still experimental.
- No public server-capacity claim is made; measure your own server before increasing bot populations.
- The 3.0 track is for isolated testing only until its remaining gameplay gates pass.

## Contributing

Bug reports and focused pull requests are welcome. Read [Contributing](CONTRIBUTING.md) and [Development](docs/DEVELOPMENT.md) before changing host hooks or performance-sensitive bot behavior.

PlayerBots code is distributed under GPL-3.0-or-later; see [LICENSE.GPL](LICENSE.GPL). AAEmu and ArcheAge PlayerBots are not affiliated with XLGames. All product names and trademarks belong to their respective owners.
