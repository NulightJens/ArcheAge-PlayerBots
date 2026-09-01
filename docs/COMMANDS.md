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
| `/movebot <id> <x> <y> <z> [walk\|run\|teleport]` | Move one bot toward coordinates, or explicitly teleport it for isolated GM staging |
| `/botattackobject <id\|all> <npcObjId> [stopAtHpPercent]` | Attack one exact NPC object, optionally stopping non-lethally at 1-99% HP |
| `/botreset <id>` | Clear current combat work and start a fresh target search |
| `/botduel <id1> <id2>` | Start a duel between two bots |

The optional `stopAtHpPercent` floor on `/botattackobject` is checked before each combat decision. Once the exact target reaches that 1-99% threshold, the bot clears the target and returns to `Idle`; the command does not write target HP directly.

`/movebot ... teleport` is an explicit GM-only fixture-staging tool. It does not prove autonomous travel or route safety; use `walk` or `run` when movement itself is under test.

Direct follow accepts optional formation settings:

```text
/botfollow all <characterName> 3 auto 2.5
```

The optional values are rear distance, column count (`auto` allowed), and spacing. The formation is deterministic; it is not collision avoidance or navmesh pathfinding.

## Archetypes and rotations

| Command | Purpose |
| --- | --- |
| `/setclass [id] <archetype> [level]` | Replace all three skill trees, refresh skills and equipment, save, and respawn the bot. Omit `id` when targeting a live bot |
| `/botarchetype <id>` | Show the bot's assigned archetype |
| `/botarchetype <id> force` | Re-evaluate the character and apply its plan |
| `/botarchetype <id> reroll` | Choose a new eligible archetype |
| `/botrotation <id\|all> show` | Show the active rotation |
| `/botrotation <id> set <rotationId>` | Override one bot's rotation |
| `/botrotation <id\|all> reload` | Reload rotation files |
| `/reloadbotarchetype` | Reload `Data/BotArchetypes.json` |
| `/reloadbotconfig` | Reload `Configurations/BotConfig.json` |

See [Configuration](CONFIGURATION.md) before editing runtime data.

`/setclass` is also the repair command when a bot's persisted trees or learned skills do not match its intended role. With no arguments it lists the available archetypes. Omit the level to retain the current one:

```text
/setclass 2 Darkrunner 55
/setclass 3 Primeval 55
/setclass 4 Daggerspell 55
/setclass 5 Cleric 55
```

## Bot equipment

The normal AAEmu `/kit <characterName> <kitName>` command now asks PlayerBots to equip the best compatible items immediately after placing a kit in a live bot's bag.

| Command | Purpose |
| --- | --- |
| `/botgear [id] show` | List equipped slots with localized item names, grades, template IDs, and instance IDs |
| `/botgear [id] equip` | Re-evaluate and equip the best compatible bag items |
| `/botgear [id] inspect` | Synchronize the bot and send its read-only character detail to the GM client |
| `/botgear [id] create <grade> <prefix> <armor> <weapon>` | Create, equip, save, and restart the bot with a complete Magnificent loadout |

The ID is optional when the GM currently targets a live bot. For example, target the bot and run:

```text
/botgear create celestial flame leather nodachi
/botgear show
```

`grade` accepts AAEmu's item-grade names from `Crude` through `Mythic`. `prefix` is the Magnificent stat family (`earth`, `flame`, `gale`/`wind`, `life`, or `wave`), `armor` is `cloth`, `leather`, or `plate`, and `weapon` is any primary weapon available for that family, such as `nodachi`, `greatsword`, `greataxe`, `staff`, or `club`. The command adds seven armor pieces, a necklace, two earrings, two rings, the requested primary weapon, a bow, and an instrument, then runs normal bot equipment evaluation, saves the character, and restarts that bot through the normal logout/login lifecycle so nearby clients receive a fresh visual snapshot.

Some Magnificent families do not contain a literal seven-piece armor set, bow, or instrument with the requested prefix. In that case PlayerBots selects the closest complete stat-compatible Magnificent set and prints every actual English item name instead of pretending an exact item exists.

`/botequip` is an alias for `/botgear`. ArcheAge's remote-detail protocol is read-only: the stock client cannot safely drag equipment into another character's inventory. PlayerBots forces every connectionless bot's equipment visibility to **public** at spawn and whenever a new client sees it; `/botgear inspect` reasserts that state before sending the detail packet. Use `/kit`, `/botgear ... create`, and `/botgear ... equip` for server-authoritative changes.

## Staged quest development

These commands expose deliberately narrow quest vertical slices for GM testing. They do not enable autonomous quest selection, routing, or objective chaining.

| Command | Purpose |
| --- | --- |
| `/botquest scan <id> [radius]` | List nearby exact-NPC quest starters and active reporters, bounded to 100 meters |
| `/botquest nearby <id> <npcTemplateId> [radius]` | List live NPCs matching one exact template with object ID, health, distance, and position, bounded to 100 meters |
| `/botquest locate <id> <npcTemplateId>` | Read-only exact-template discovery across the bot's current world, sorted by distance and capped at ten results |
| `/botquest inspect <id> <questId>` | Show the quest's localized name and structured AAEmu acts, including exact native fixture identifiers |
| `/botquest status <id> <questId>` | Show active step/status/objective state or completed/inactive lifecycle |
| `/botquest accept <id> <questId>` | Accept through AAEmu's normal lifecycle while within 6 meters of the exact starter |
| `/botquest talk <id> <questId>` | Advance only that active quest's exact-NPC talk acts within 6 meters; team-shared acts fail closed |
| `/botquest use <id> <questId> <npcObjId>` | Use that selected quest's one carried quest-linked supply item against one exact living NPC through AAEmu's native item-skill engine |
| `/botquest acquire <id> <questId> <npcObjId>` | Derive one exact NPC and health ceiling from the selected quest item's native skill requirements, run contained combat to that ceiling, disengage, and launch the native item skill |
| `/botquest loot <id> <questId> <corpseObjId>` | Transfer exactly one quest-linked item from an exact nearby corpse exclusively tagged by the bot through AAEmu's native loot machinery |
| `/botquest hunt <id> <questId>` | Derive one exact active monster-hunt target and remaining kill count, then repeatedly select and kill nearby matching NPCs |
| `/botquest travel <id> <questId>` | Run to one exact same-world static sphere destination within 100 meters and let AAEmu's native enter-sphere event advance the objective |
| `/botquest report <id> <questId> [rewardIndex]` | Report only that selected active quest while within 6 meters of its exact reporter |

Only plain exact-NPC starters, talk objectives, reporters, the narrow selected-quest supply-item path, one native corpse-loot path, one exact monster-hunt path, and one static sphere-travel path are supported in this milestone. `use` requires the selected quest to be active at Progress with exactly one active item-gather act and exactly one carried Supply item whose native template links it to that quest and defines a use skill. It asks AAEmu to cast that real item skill; a successful command means the cast started, not that the objective advanced. `acquire` adds one bounded combat executor: the item's skill must expose one non-OR requirement set containing exactly one target NPC template, one 1-99% target-health ceiling, and the matching progress-quest context. The caller still selects the exact live object; ambiguous native requirements fail closed. `loot` requires a dead NPC within 6 meters, an exclusive solo tag by that bot, and exactly one generated corpse entry whose item ID and native `loot_quest_id` match the selected quest's only gather act. It delegates the actual transfer to the host loot implementation and never creates an item. `hunt` requires exactly one currently active native monster-hunt act, rejects group and mixed objective sets, derives the remaining count, and uses the normal combat and quest-credit paths until that goal is met. `travel` requires exactly one active static sphere act and exactly one same-world geometry result; it clears follow and combat work, runs to the heightmap-projected center, and leaves objective credit to AAEmu's live sphere trigger. After any channel, loot transfer, hunt, or travel, verify the result with `/botquest status`.

NPC groups, mixed objectives, team-shared talk acts, general-purpose item use, team or shared corpse claims, emotion, kill-trigger starters, NPC-centered and multi-sphere travel, autonomous quest selection/routing, objective chaining, and reward-choice policy remain future work. Exact hunt and travel selection are bounded to 100 meters, use true three-dimensional distance, and reject destinations that do not agree with the bot's navigation-height surface. This deliberately fails closed on cave or stacked-world fixtures that the current heightmap mover cannot reach safely. Report-time cleanup for a native item-delivery quest is part of this slice; broader delivery policy remains deferred. Use `/movebot ... teleport` only to stage the bot between exact live objects during isolated controlled tests.

Inspection is read-only. For supported act types it reports the exact NPC, NPC-group, doodad, item, distance, sphere, cleanup, and selective-reward fields loaded by AAEmu; a printed identifier is evidence about the quest template, not permission to fabricate that fixture or advance the objective.
The `nearby` and `locate` verbs are read-only and are intended to resolve exact live object IDs for native fixtures instead of guessing object allocation order. `nearby` enforces the bounded local scan; `locate` is an explicit GM discovery tool across the bot's current world and never selects, moves, or attacks a result.

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

Each request creates a fresh, unregistered actor. When active bots exist, the
actor copies the matching world, instance, non-zero zone, coordinates, and
rotation from the qualified bot with the lowest character ID. Bots with no
world, a mismatched instance, ZoneId `0`, or a non-finite transform are ignored.
If no bot qualifies, the actor remains worldless: worldless commands such as
`addbot` still work, while world-positioned commands such as `spawnpassive`
fail closed at their existing world guard. Add a qualified bot before invoking
world-positioned fixture commands through the loopback API.

> **Security:** `@system` has administrator access and no user authentication. Keep the command API bound to `127.0.0.1`; never expose its port publicly.
