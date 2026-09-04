# Bot identities

AAEmu 1.2 can create a persistent character for PlayerBots. The module never creates an account or reuses a player's account.

## Setup

Create a dedicated game account, then set its numeric ID before starting Game:

```powershell
$env:AAEMU_PLAYERBOTS_ACCOUNT_ID = '700'
```

`AAEMU_PLAYERBOTS_ROSTER_PATH` may override the roster file. The default is `Data/PlayerBots/bot-roster.json` below the Game runtime directory.

## Create a bot

Run this as an administrator:

```text
/createbot <name> <race> <gender> <archetype> <level> [here|race-spawn]
```

For a level-one Nuian at its native start:

```text
/createbot FreshNuian Nuian Female Abolisher 1 race-spawn
```

`here` uses the GM's current world position. `race-spawn` uses AAEmu's native race start. The command validates the name, race, gender, archetype, level, placement, and account before writing anything.

The character is saved normally and added to the module roster. `/removebot <id>` logs it out; it does not delete it.

## Common failures

- `configuration_unavailable`: the account ID is missing, zero, or invalid.
- `invalid_placement`: use `here` while fully in the world, or use `race-spawn`.
- `invalid_race` or `invalid_archetype`: use a named AAEmu race and a configured PlayerBots archetype.
- `name_unavailable`: choose another character name.

This factory is not available on the experimental 3.0 adapter.
