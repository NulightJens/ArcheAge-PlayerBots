# Playing with bots

PlayerBots adds server-controlled characters to an AAEmu server. You use normal game commands to spawn companions, form a party and practice combat.

**Who installs it:** the AAEmu server owner. Other players join the configured server with the matching ArcheAge client. This is not a client mod that adds bots to someone else's server.

Use the matching ArcheAge 1.2 r208022 client and the server owner's installed PlayerBots preview. See [Installation](INSTALLATION.md).

## Bring in an existing character

Use a dedicated character that is currently offline. Log in as a GM and replace `2` with that character's database ID:

```text
/addbot 2
```

The character appears as a bot. This reuses a saved character; it does not create a new one.

Invite it through the normal party UI. As its party leader, give it a role and order:

```text
/botcontrol 2 role attacker
/botcontrol 2 follow
```

The bot should follow your character. Roles are `attacker`, `tank` and `healer`.

## Fight with your companion

Select a living combat target in the game, keep it selected, then run:

```text
/botcontrol 2 attack
```

The command uses your selected target. It does not choose a target for you or override the server's combat rules.

To stop aggressive behavior and hold position:

```text
/botcontrol 2 passive
/botcontrol 2 stay
```

Only the bot's current party leader can give it orders.

## Practice a duel

**You against a bot:** use the game's normal duel request on an active bot. The bot accepts after roughly one to five seconds. Normal server duel restrictions still apply.

**Two bots against each other:** as a GM, admit two different offline characters, then use their bot IDs:

```text
/addbot 2
/addbot 3
/botduel 2 3
```

Both bots must be alive, in the same instance and free of an existing duel. The command also rejects expedition membership. Let the native duel finish before moving on.

Duels are experimental in this preview. Open-world faction PvP, arenas, balanced matchups and reliable long fights remain in development.

## Create a new bot instead

Fresh creation is experimental and needs a server-owned dedicated game account. The server operator sets that account's numeric ID in the environment before starting the Game service:

```powershell
$env:AAEMU_PLAYERBOTS_ACCOUNT_ID = '<dedicated-account-id>'
```

Then a GM can create a level-one character at the native race start:

```text
/createbot FreshNuian Nuian Female Abolisher 1 race-spawn
```

Use the new ID reported by the command for subsequent orders. Creation saves the new character and admits it as a bot. It does not create the account. Keep real player accounts separate.

Automatic questing is opt-in and still under development. Newly created bots cannot yet be relied on to level through the full game on their own.

## Choose a class and equipment

A GM can inspect available archetypes with `/setclass` and apply a configured class to an active bot:

```text
/setclass 2 Darkrunner
/botgear 2 show
```

Omitting the level retains the current level. The class command refreshes skill trees/equipment and saves/restarts that bot, so use it deliberately between activities. The existing command reference also contains advanced GM gear creation; ordinary users do not need it to start.

## Save and bring the bot back later

When finished, the GM runs:

```text
/removebot 2
```

This saves and logs out the character. It does not delete it. Use `/addbot 2` to bring that same character back later.

## If an order does not work

| Symptom | Check |
|---|---|
| Unknown bot command | The server must have the correct PlayerBots installation and compiled commands. |
| Bot cannot be added | Check the character ID and make sure the character is offline. |
| Party control refused | The bot must be in your party, and you must be its current party leader. |
| Attack asks for a target | Select a living target before issuing the order and keep it selected. |
| Fresh creation reports configuration unavailable | The server's dedicated bot account ID is missing or invalid. |
| Native duel refused | Check alive/same-instance/existing-duel/expedition restrictions. |
| Movement or questing stalls | Inspect the bot and preserve its progress; current navigation and questing have known limitations. |

Run `/bot`, `/help botcontrol` or `/botdebug 2` for in-game help.
