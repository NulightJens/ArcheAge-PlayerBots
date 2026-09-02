# Server-owned bot identities

PlayerBots can create a native AAEmu character under a dedicated server-owned
game account and immediately admit it through the ordinary `BotManager` spawn
lifecycle. The path does not fabricate a `GameConnection`, add the character to
a connection's character-selection collection, or emit client packets.

## Host and configuration requirements

AAEmu 1.2 must have both compatibility patches, in this order:

1. `compatibility/aaemu-1.2-r208022-v3.patch`
2. `compatibility/aaemu-1.2-r208022-bot-identity-factory.patch`

Provision a game-account row reserved exclusively for server-owned bots. Do not
use a player account and do not log the reserved account into the game server.
The factory deliberately uses a read-only account lookup and fails if the row is
missing or currently online; it never creates an account implicitly.

Set these environment variables before starting AAEmu.Game:

- `AAEMU_PLAYERBOTS_ACCOUNT_ID`: the non-zero ID of the reserved game account.
- `AAEMU_PLAYERBOTS_ROSTER_PATH`: optional absolute roster path. The default is
  `Data/PlayerBots/bot-roster.json` beneath the AAEmu application directory.

If the account ID is absent or invalid, the command remains registered but every
creation fails closed with `configuration_unavailable`.

## Command

```text
createbot <name> <race> <gender> <archetype> <level> [here|race-spawn]
```

`create_bot` is an equivalent alias. Both commands require access level 100 in
the AAEmu 1.2 access-level configuration.

- Race and gender are enum names, matched case-insensitively. Numeric enum input
  is rejected.
- Level is an unsigned decimal form in the inclusive range from 1 through the
  AAEmu server's current player-level cap.
- Archetype is matched case-insensitively against the loaded bot-archetype
  definitions.
- `race-spawn` is the default and uses AAEmu's native race/gender start template.
- `here` captures the command character's finite position and rotation. The
  host accepts it only when the caller is in a valid zone of the default world
  instance, because AAEmu's persisted character format does not retain arbitrary
  instance identity. Instance or zone mismatches fail without allocation.

Results use a stable machine-readable prefix:

```text
BOT_IDENTITY status=success code=created_and_admitted id=... name=... level=... race=... gender=... zone=...
BOT_IDENTITY status=failure code=... reason=...
```

## Authoritative creation flow

The module validates the request and resolves a level-aware archetype plan. A
narrow host authority then owns all native operations:

1. Verify the pre-provisioned account is present and offline.
2. Revalidate level, name, race, gender, ability plan, and placement.
3. Allocate the character ID and reserve the normalized name with AAEmu's
   managers.
4. Initialize the native race/gender template, faction, spawn, body, starter
   inventory/equipment and supplies, action slots, actabilities, abilities,
   default and starting skills, and empty quest state.
5. Set character and active-ability experience to AAEmu's exact value for the
   requested level without firing level-up hooks or packets.
6. Persist through `Character.SaveDirectlyToDatabase()`.
7. Persist the archetype plan and authoritative bot-roster entry.
8. Admit the persisted character through the existing `BotManager.SpawnBot`
   path, which performs the normal full load, archetype application, world
   registration, runtime registration, and spawn.

At level one, only the archetype's starting ability is active. The second and
third trees remain `None` until the archetype's configured unlock levels. This
is a progression plan, not a shortcut to high-level skills or equipment. At
higher levels, AAEmu initializes exact experience while `BotArchetypeManager`
learns and equips only entries allowed by the requested level.

## Failure and compensation

Creation is serialized at the module and host boundaries. Account, name,
template, ID, placement, persistence, roster, archetype, and admission failures
produce structured results. Nothing is admitted unless native persistence and
both policy registrations succeed.

If a post-persistence policy or admission stage fails, the module removes only
the just-created roster and archetype entries. The host accepts compensation
only for an identity created by the current in-process request and the exact
configured account. It soft-tombstones the retained character row for audit and
releases its live name registration. Successful admission closes that rollback
guard. There is intentionally no create-path deletion or retirement command;
future lifecycle retirement must remain a separate reversible workflow.

Existing persisted bots are unchanged: normal startup and explicit `addbot`
continue to load by authoritative character ID and use the same `SpawnBot`
lifecycle.
