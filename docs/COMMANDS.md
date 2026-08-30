# Commands

Run `/bot` in game for the quick guide. Run `/help <command>` whenever you need the exact arguments for one command.

Most server-management commands require GM access. `/botcontrol` is available through the bot's live party relationship.

## Get started

Use the ID of an existing character that is currently offline:

```text
/addbot 2
/botstate 2 grind
```

Stop the bot and save it through normal logout:

```text
/botstate 2 idle
/removebot 2
```

| Command | Purpose |
| --- | --- |
| `/bot [topic]` | Show the in-game PlayerBots guide |
| `/addbot <characterId>` | Log in an existing offline character as a bot |
| `/removebot <characterId>` | Save and log out an active bot |
| `/botstate <id>` | Show the bot's current state |
| `/botstate <id> <state> [killGoal]` | Force a state and optionally stop grinding after a number of kills |
| `/botstate <id> free` | Return the bot to automatic state control |

Available states are `idle`, `grind`, `questing`, `roaming`, `following`, and `resting`.

## Party control

Invite the active bot through the normal ArcheAge party UI. Assign its party role, then give it an order:

```text
/botcontrol 2 role healer
/botcontrol 2 follow
/botcontrol 2 attack
/botcontrol 2 passive
/botcontrol 2 stay
```

Roles are `tank`, `healer`, and `attacker`.

| Order | Result |
| --- | --- |
| `follow` | Follow the party owner in formation |
| `stay` | Hold the current position |
| `attack` | Enable normal party combat behavior |
| `passive` | Stop aggressive party behavior |
| `role <role>` | Set the bot's party job |

Authorization follows current party membership and ownership. An unrelated player cannot control a bot by guessing its ID.

## Direct GM control

These commands are useful for staging bots outside a normal party:

| Command | Purpose |
| --- | --- |
| `/botfollow <id\|all> <characterName>` | Follow an online character |
| `/botfollow <id\|all> stop` | Stop direct follow |
| `/botfollow <id\|all> status` | Show direct-follow status |
| `/movebot <id> <x> <y> <z> [walk\|run]` | Move one bot toward coordinates |
| `/botattackobject <id\|all> <npcObjId>` | Attack one exact NPC object |
| `/botreset <id>` | Clear current combat work and start a fresh target search |
| `/botduel <id1> <id2>` | Start a duel between two bots |

Direct follow accepts optional formation settings:

```text
/botfollow all <characterName> 3 auto 2.5
```

The optional values are rear distance, column count (`auto` allowed), and spacing. The formation is deterministic; it is not collision avoidance or navmesh pathfinding.

## Archetypes and rotations

| Command | Purpose |
| --- | --- |
| `/botarchetype <id>` | Show the bot's assigned archetype |
| `/botarchetype <id> force` | Re-evaluate the character and apply its plan |
| `/botarchetype <id> reroll` | Choose a new eligible archetype |
| `/botrotation <id\|all> show` | Show the active rotation |
| `/botrotation <id> set <rotationId>` | Override one bot's rotation |
| `/botrotation <id\|all> reload` | Reload rotation files |
| `/reloadbotarchetype` | Reload `Data/BotArchetypes.json` |
| `/reloadbotconfig` | Reload `Configurations/BotConfig.json` |

See [Configuration](CONFIGURATION.md) before editing runtime data.

## Diagnostics

| Command | Purpose |
| --- | --- |
| `/botdebug <id>` | Show movement, target, combat, follow, and runtime state |
| `/botactions <id> [co\|nc]` | Show recent combat or non-combat action attempts |
| `/botvalues <id> [filter]` | Show computed blackboard values |
| `/botstrategy <id\|all> <co\|nc\|de> <operation>` | List or change engine strategies |
| `/botmetrics snapshot` | Capture the current metrics window |
| `/botmetrics reset` | Start a fresh metrics window |
| `/botmetrics activity <0-100>` | Change activity percentage until the server restarts |

For strategy operations, use `?` to list, `+name` to add, `-name` to remove, or `~name` to toggle. Example:

```text
/botstrategy 2 co ?
```

## Development commands

These commands are intended for controlled testing, not everyday play:

| Command | Purpose |
| --- | --- |
| `/spawnpassive <npcTemplateId> [distance]` | Spawn a stationary, non-retaliating target |
| `/botbuff <id> <buffId> [abLevel]` | Apply a known data-pack buff |
| `/botbuff <id> -<buffId>` | Remove that buff |
| `/botjump <id\|name\|all>` | Queue the experimental server-side jump |
| `/exportworld [outDir]` | Export loaded 1.2 world geometry for development |

Buff and skill IDs are specific to the active client data. Do not assume an ID from 1.2 has the same meaning in 3.0.

## Local command API

AAEmu's loopback Web API can run the same commands with the synthetic `@system` actor:

```powershell
$body = @{ character = '@system'; arguments = '2' } | ConvertTo-Json
Invoke-RestMethod -Method Post `
  -Uri 'http://127.0.0.1:1280/api/commands/addbot' `
  -ContentType 'application/json' -Body $body
```

> **Security:** `@system` has administrator access and no user authentication. Keep the command API bound to `127.0.0.1`; never expose its port publicly.
