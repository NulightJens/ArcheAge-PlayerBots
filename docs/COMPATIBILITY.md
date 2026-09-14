# Version compatibility

Choose the PlayerBots integration that matches your server and game client.

| Game version | Availability |
| --- | --- |
| ArcheAge 1.2 r208022 | Available in the [alpha.7 download](https://github.com/NulightJens/ArcheAge-PlayerBots/releases/tag/v0.2.0-alpha.7). |
| ArcheAge 3.0.4.2 r336598 | Earlier compatibility version; not included in the current download. |
| ArcheAge CN 10.0.2.13 r575 | Working server-bot prototype; download not yet available. |

## ArcheAge 10

A working prototype runs on **ArcheAge CN 10.0.2.13 r575** using AAEmu's
native-zone server. The bot runs on the server without its own game client.

The prototype can:

- Create a persistent character, save it and bring it back later.
- Walk, stop and take local detours around obstacles.
- Fight, collect loot and interact with NPCs and world objects.
- Find, accept and complete a main-story quest, then find the next eligible quest.

It currently supports one bot. Broader automatic questing, parties and populations
remain in development. Long-distance travel, zone changes, bridges, interiors,
cutscenes and how the bot appears to other players still need work or verification.
Other 10.x client versions are not covered by this prototype.

### Installation status

The 10.x integration is not included in the **0.2.0-alpha.7 download or `install.py`**.
Those install the 1.2 version. A separate 10.x download is not yet available.

The prototype requires Windows, .NET 10, MySQL 8.0, the matching AAEmu native-zone
server, and locally supplied CN 10.0.2.13 r575 game data and native dedicated runtime.
Game files and native binaries are not included with PlayerBots.

Its controls use a local API. The 1.2 `/addbot` and party-command guide does not
apply to this version.

## ArcheAge 1.2

The current download targets **ArcheAge 1.2 r208022** and the AAEmu revision named
in [Installation](INSTALLATION.md). Then follow [Playing with bots](PLAYING.md)
for companions, party orders, classes and duels. Automatic questing and complex
navigation remain experimental.

[Back to PlayerBots](../README.md) · [Changelog](../CHANGELOG.md)
