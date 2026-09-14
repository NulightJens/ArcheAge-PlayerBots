<p align="center">
  <img src="assets/playerbots-readme-banner.png" alt="ArcheAge PlayerBots companions overlooking an ArcheAge coastline" width="100%" />
</p>

# ArcheAge PlayerBots

PlayerBots adds server-controlled player characters to [AAEmu](https://github.com/AAEmu/AAEmu). Bring an offline character into the world, invite it to your party, and give it simple orders.

The server owner installs PlayerBots. Players join with the matching ArcheAge client; no AI service or agent software is required.

- Persistent characters with normal saving and logout.
- Follow, stay, attack, and passive party commands.
- Melee, archer, mage, healer, and tank behavior using native skills.
- Configurable archetypes, rotations, startup bots, and diagnostics.
- Experimental creation of fresh characters and automatic starter quests on ArcheAge 1.2.

## Installation

PlayerBots is a source module compiled with a compatible AAEmu host. For ArcheAge 1.2, download the
[0.2.0-alpha.7 preview](https://github.com/NulightJens/ArcheAge-PlayerBots/releases/tag/v0.2.0-alpha.7),
then follow the [installation guide](docs/INSTALLATION.md) for the pinned host,
setup commands and database migration.

| Track | Status |
| --- | --- |
| ArcheAge 1.2 r208022 | Current preview; pinned AAEmu host and .NET 10 required |
| ArcheAge 3.0.4.2 r336598 | Frozen compatibility checkpoint; outside this preview installer |
| [ArcheAge CN 10.0.2.13 r575](docs/COMPATIBILITY.md#archeage-10) | Working headless prototype with a separate 10.x integration |

The 10.x prototype has demonstrated movement, combat, loot, interactions, quest
completion and save/re-entry. Its separate integration is not yet included in the
public downloads. See [version compatibility](docs/COMPATIBILITY.md) for details.

Choose an exact release when installing or updating. The [changelog](CHANGELOG.md)
describes what has changed.

## First companion

For the 1.2 preview, log in as a GM after installation and choose an existing offline character. Replace `2` with its character ID:

```text
/addbot 2
```

Invite the bot through the normal party UI, then issue an order:

```text
/botcontrol 2 role attacker
/botcontrol 2 follow
```

Select a living target and use `/botcontrol 2 attack` to attack it, or `/botcontrol 2 passive` to stop attacking. When finished, `/removebot 2` saves and logs out the character.

See [playing with bots](docs/PLAYING.md) for other roles, class and gear tools, and duels. Creating a fresh character has a separate [experimental walkthrough](docs/PLAYING.md#create-a-new-bot-instead).

## Guides

| I want to… | Read |
| --- | --- |
| Check game-version support | [Compatibility](docs/COMPATIBILITY.md) |
| Install or update | [Installation](docs/INSTALLATION.md) |
| Control bots in game | [Playing with bots](docs/PLAYING.md) |
| Choose a class and equipment | [Classes and gear](docs/PLAYING.md#choose-a-class-and-equipment) |
| Practice a duel | [Duels](docs/PLAYING.md#practice-a-duel) |
| Solve a problem | [Troubleshooting](docs/PLAYING.md#if-an-order-does-not-work) |
| See what's changed | [Changelog](CHANGELOG.md) |

[Companion tools](FAMILY.md) include Route Recorder, Client Autopilot and Navigation
Extractor. Install only the tools you need. You can [report a bug or suggest an
improvement](https://github.com/NulightJens/ArcheAge-PlayerBots/issues).

## Current limits

This is an experimental preview. Quest autonomy is opt-in, and complex routes,
obstacles, caves and interiors can still cause problems. Full autonomous leveling,
arenas, reliable long PvP fights and large living-world populations remain in
development. Start with one companion and the simple commands above.

## Acknowledgements

This ArcheAge implementation draws on the [Playerbots family](https://github.com/mod-playerbots/mod-playerbots), with AAEmu providing the native game systems. PlayerBots is distributed under [GPL-3.0-or-later](LICENSE.GPL); retained file-specific notices still apply. AAEmu and PlayerBots are not affiliated with XLGames.
