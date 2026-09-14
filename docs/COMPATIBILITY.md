# Version compatibility

PlayerBots has integrations for different ArcheAge server versions. Choose the
integration that matches your server and game client.

| Game version | Status | Installation |
| --- | --- | --- |
| ArcheAge 1.2 r208022 | Current public preview | Use the alpha.7 download and [installation guide](INSTALLATION.md). |
| ArcheAge 3.0.4.2 r336598 | Frozen compatibility checkpoint | Retained separately; outside the current installer. |
| ArcheAge CN 10.0.2.13 r575 | Working headless prototype | Requires its separate 10.x host integration; public package pending. |

## ArcheAge 10

A working PlayerBots prototype exists for **ArcheAge CN 10.0.2.13 r575** on
AAEmu's native-zone server. This is the exact game version demonstrated; support
for every other 10.x release has not been established.

The prototype runs its bot without a graphical game client. It has demonstrated:

- Persistent character creation, admission, saving and re-entry.
- Continuous walking and stopping, with native obstacle queries and local detours.
- Normal combat, retaliation, loot, NPC interaction and world-object interaction.
- Finding, accepting and completing a main-story quest, then discovering the next
  eligible quest through normal game rules.

This is a working single-bot prototype. Broader autonomous behavior, party and
population systems are still being ported. Long-distance and cross-zone travel,
bridges, interiors, cutscenes and appearance through another player's client
remain unfinished or unverified.

### Installation status

The 10.x prototype uses its own module and host compatibility changes. The
**0.2.0-alpha.7 download and `install.py` install the 1.2 integration**; they do
not contain the separate 10.x port. A public 10.x package is still pending.

The demonstrated 10.x setup requires Windows, .NET 10, MySQL 8.0, the matching
AAEmu native-zone host, and locally supplied game data and native dedicated
runtime for CN 10.0.2.13 r575. Its World server hosts the Game services and uses
native ZoneHosts. Those game assets and native binaries are not included in
PlayerBots source distributions.

The prototype currently receives bounded actions through a local control API.
The 1.2 `/addbot` and party-command walkthrough does not describe its controls.
The eventual 10.x package needs its own installation and usage guide alongside
the same PlayerBots product.

## ArcheAge 1.2

The current public preview targets **ArcheAge 1.2 r208022** and its pinned AAEmu
host. Follow [Installation](INSTALLATION.md), then [Playing with bots](PLAYING.md)
for companions, party orders, class choices and duels. Quest autonomy and
complex navigation remain experimental.

[Back to PlayerBots](../README.md) · [Changelog](../CHANGELOG.md)
