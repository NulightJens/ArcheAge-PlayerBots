<p align="center">
  <img src="assets/playerbots-readme-banner.png" alt="ArcheAge PlayerBots — melee, archer, mage, and healer companions overlooking an ArcheAge coastline" width="100%" />
</p>

# ArcheAge PlayerBots

Standalone PlayerBots module for [AAEmu](https://github.com/AAEmu/AAEmu). It runs persistent ArcheAge characters without client connections and gives server operators human-facing commands, party control, data-driven combat rotations, diagnostics, and repeatable scale tests.

This repository is **not an AAEmu fork**. AAEmu is a required host dependency. The module stays in `modules/archeage-playerbots`; a versioned compatibility patch adds the small lifecycle, service-registration, party, and build hooks that AAEmu does not yet expose through a dynamic module API.

## Compatibility

| Component | Status |
| --- | --- |
| ArcheAge 1.2 | Supported on AAEmu base `62e3eb1d87da01194802ac886cd500134facad28` with `r208022` data |
| ArcheAge 3.0 | Compile-validated alpha on NL0bP/AAEmu base `8c1c943bb2309eefffb9da2aa99a408d0acbb095`, client `3.0.4.2 r336598` |
| 3.0 runtime acceptance | Matching client/`game_pak` verified; blocked on decrypted `compact.sqlite3`, `compact.server.table.sqlite3`, and isolated login/lifecycle tests |
| Module version | `0.2.0-alpha.1` |

## Install

Start with a clean, working AAEmu checkout. From the AAEmu root:

```powershell
New-Item -ItemType Directory -Force modules | Out-Null
git clone https://github.com/NulightJens/ArcheAge-PlayerBots modules/archeage-playerbots
& .\modules\archeage-playerbots\scripts\Install-PlayerBots.ps1 -AAEmuRoot $PWD
dotnet build AAEmu.slnx --no-incremental
```

Linux/macOS:

```bash
mkdir -p modules
git clone https://github.com/NulightJens/ArcheAge-PlayerBots modules/archeage-playerbots
./modules/archeage-playerbots/scripts/install-playerbots.sh "$PWD"
dotnet build AAEmu.slnx --no-incremental
```

The installer is deliberately fail-closed: it validates the host lineage and patch before editing AAEmu, refuses conflicting local changes, and will not overwrite a different SQL migration. See [Installation](docs/INSTALLATION.md) for database and upgrade details.

The commands above select the supported 1.2 track automatically. The compile-validated 3.0 adapter is intentionally opt-in while runtime acceptance is outstanding:

```powershell
& .\modules\archeage-playerbots\scripts\Install-PlayerBots.ps1 -AAEmuRoot $PWD -Track AAEmu30 -AllowExperimental
dotnet build AAEmu.sln --no-incremental
```

Do not run the 3.0 track with 1.2 client data or against a live 1.2 database.

The pinned upstream AAEmu revision currently resolves two high-severity transitive dependency advisories. A separate, optional and reviewable [host security baseline patch](compatibility/README.md) upgrades only the affected centrally managed packages; it is kept outside the PlayerBots installer because dependency policy belongs to the server operator.

## First bot

Log in with your own GM character and select an existing offline character ID:

```text
/bot
/addbot 2
/botstate 2 idle
/botarchetype 2
/botrotation 2 show
/botstate 2 grind 1
/removebot 2
```

Replace `2` with the bot character ID. `/removebot` saves and normally logs out the character; it does not delete it. No example depends on a local test-character name.

For normal play, invite spawned bots to an actual ArcheAge party and use `/botcontrol`:

```text
/botcontrol 2 role attacker
/botcontrol 2 follow
/botcontrol 2 attack
/botcontrol 2 passive
/botcontrol 2 stay
```

See [Commands](docs/COMMANDS.md) for every operator command and [Testing](docs/TESTING.md) for the accepted physical cases.

## What is included

- connectionless persistent-character lifecycle with normal save/logout cleanup;
- Darkrunner, Primeval, Daggerspell, Cleric, Abolisher, Reaper, and Templar archetypes;
- data-driven rotations and range-aware melee, archer, caster, and healer behavior;
- native-party authorization, roles, follow, stay, attack, and passive controls;
- action, blackboard, state, rotation, archetype, and resource diagnostics;
- lightweight host scheduling, scan caching, activity governing, and scale metrics;
- an isolated exact-population 0/10/50/100 scale harness.

## Project map

| Path | Purpose |
| --- | --- |
| `src/AAEmu.Game/` | PlayerBots runtime code, commands, configuration, and content |
| `tests/AAEmu.UnitTests/` | Module unit and host-integration tests |
| `build/` | Versioned MSBuild imports that compile module source into the AAEmu host |
| `compatibility/` | Reviewed AAEmu 1.2 and compile-validated 3.0 host-hook patches |
| `sql/` | Module database migration |
| `scripts/scale/` | Repeatable population/resource harness |
| `docs/` | Human installation, command, architecture, development, and testing guides |

The architecture and the reason for source-level host integration are documented in [Architecture](docs/ARCHITECTURE.md). Current priorities are in the [Roadmap](docs/ROADMAP.md), and player-facing changes are in the [Changelog](CHANGELOG.md).

## Known limits

- Direct bot movement is not navmesh navigation and can cross obstructing geometry.
- Native ArcheAge collision/direct pursuit is the baseline; experimental pairwise repulsion and boss-orbit logic are not enabled.
- Bot jump motion does not yet render as a convincing jump on the 1.2 client.
- A 1,000-bot physical run proves execution, not a recommended capacity. Publish a capacity only after approving a whole-server resource budget.
- The 1.2 client may render only roughly 100 nearby characters even when more bots are active server-side.

## Contributing and license

Read [Development](docs/DEVELOPMENT.md) before changing host hooks or per-bot hot paths. Contributions should preserve the lightweight design and include an automated or physical acceptance case.

PlayerBots code is distributed under GPL-3.0-or-later; see [LICENSE.GPL](LICENSE.GPL). AAEmu and ArcheAge PlayerBots are not affiliated with XLGames. All product names and trademarks belong to their respective owners.
