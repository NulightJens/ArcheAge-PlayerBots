# Configuration

PlayerBots works with its default settings. Change the configuration only when you need startup bots, different behavior ranges, or tighter performance limits.

## Configuration file

The running server reads:

```text
Configurations/BotConfig.json
```

This path is relative to the `AAEmu.Game` executable. Edit the file in the server's runtime directory, then reload it in game:

```text
/reloadbotconfig
```

The module's source template is at `modules/archeage-playerbots/src/AAEmu.Game/Configurations/BotConfig.json`. A rebuild copies that template into a new output directory.

If a configuration edit cannot be parsed, PlayerBots keeps the last valid settings and writes a warning to the server log.

## Start bots with the server

Add existing offline character IDs to `AutoSpawnCharacterIds`:

```json
{
  "AutoSpawnCharacterIds": [2, 3, 4],
  "AutoSpawnState": "grind",
  "AutoSpawnDelayMs": 2000
}
```

| Setting | Purpose |
| --- | --- |
| `AutoSpawnCharacterIds` | Existing characters to log in as bots after Game starts |
| `AutoSpawnState` | Initial state: `idle`, `grind`, `questing`, `roaming`, `following`, `resting`, or `free` |
| `AutoSpawnDelayMs` | Delay after server startup before the characters are loaded |

Each character is loaded independently. A character that is already online or cannot be loaded does not stop the remaining IDs.

## Common behavior settings

| Setting | Default | Purpose |
| --- | ---: | --- |
| `SearchRadius` | `60` | Maximum nearby search radius in metres |
| `ReengageRange` | `60` | Distance at which a bot gives up re-engaging a target |
| `FleeDistance` | `15` | Preferred distance used by flee behavior |
| `RestThresholdPercent` | `50` | Health or mana percentage that can trigger rest behavior |
| `RespawnDelaySeconds` | `5` | Delay before bot respawn handling |
| `FollowStopBand` | `0.6` | Small distance band that prevents follow-position jitter |
| `GlobalSkillDelayMs` | `600` | Minimum shared delay between bot skill attempts |

Combat rotations use live skill-template ranges. `AttackRange` and `BowRange` are fallback values; changing them does not override the client data for a known skill.

## Autonomous nearby quest intake

Quest intake is deliberately disabled by default. To let an otherwise idle bot discover nearby quest-giver NPCs, walk to the best candidate, and accept every eligible quest offered by that NPC through AAEmu's normal quest authority, enable:

```json
{
  "QuestIntakeEnabled": true,
  "QuestIntakeScanRadius": 60,
  "QuestIntakeInteractionRadius": 6,
  "QuestIntakeRetryBackoffMs": 30000
}
```

Main-story candidates are ranked ahead of side quests. Candidates must be in the bot's current world, within both `SearchRadius` and `QuestIntakeScanRadius`, and compatible with the current heightmap surface. Rejected quests are retried only after the configured backoff. `/botdebug <characterId>` reports the current NPC, quest, decision reason, counters, and retry time.

This controller covers local discovery, normal movement, and quest acceptance. It does not create bot characters, reset quest state, execute arbitrary or mixed quest objectives, report completed quests, chain quest completion, or route between distant zones. Direct movement still has the terrain limitations described below.

## Activity and performance

| Setting | Default | Purpose |
| --- | ---: | --- |
| `ActivityPercent` | `100` | Percentage of independent background bots eligible for normal brain work |
| `ActivityWindowMs` | `30000` | Time window used to rotate background activity |
| `ActivityRealPlayerRadius` | `150` | Keeps nearby bots active around real players |
| `HostTickBudgetMs` | `30` | Bot-host pressure budget used by the activity governor |
| `ServerTickBudgetMs` | `0` | Whole-server pressure budget; `0` leaves this input disabled |
| `MetricsLogIntervalMs` | `60000` | Interval for periodic bot metrics logging |

Bots in combat, following, or under a forced state remain active. `ActivityPercent` primarily controls independent background work.

Start with the defaults. If the server needs fewer background updates, lower `ActivityPercent` gradually and watch command response and server latency. Do not set `ServerTickBudgetMs` until you have measured an empty-server baseline and chosen an acceptable budget.

Useful live commands:

```text
/botmetrics reset
/botmetrics snapshot
/botmetrics activity 50
```

The `activity` command changes the percentage for the current server process only. Edit `BotConfig.json` to make it persistent.

## Archetypes and rotations

The included archetypes are:

| Role | Archetypes |
| --- | --- |
| Melee | Darkrunner |
| Archer | Primeval |
| Caster | Daggerspell, Reaper |
| Healer/support | Cleric, Templar |
| Tank | Abolisher |

Runtime data is stored under:

```text
Data/BotArchetypes.json
Data/BotRotations/*.json
```

After editing runtime data, reload it without restarting Game:

```text
/reloadbotarchetype
/botrotation all reload
```

Invalid archetype data leaves the last valid definitions active. Rotation errors are reported in the server log.

## Experimental movement settings

Jump and obstacle-probe settings are experimental. `ObstacleJumpEnabled` and `AmbientJumpEnabled` are disabled in the shipped configuration. Direct movement is not navmesh navigation, and enabling these options does not make obstructed terrain safe.

For exact command syntax, see [Commands](COMMANDS.md). For configuration problems, see [Troubleshooting](TROUBLESHOOTING.md).
