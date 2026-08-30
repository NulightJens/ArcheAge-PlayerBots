# Bots

## Human operation

PlayerBots are normal persistent AAEmu characters controlled by the server when their clients are offline. The primary operator workflow is in-game:

```text
/bot
/addbot <characterId>
/botstate <characterId> grind
/botstate <characterId> idle
/removebot <characterId>
```

`/bot` provides topic pages for party, combat, development, diagnostics, scale, and current limitations. The complete operator and contributor reference is in `Docs/wiki/PlayerBots.md`; the repeatable acceptance cases are in `Docs/wiki/PlayerBots-Testing.md`.

Human-editable content:

- `Configurations/BotConfig.json` controls cadence, awareness, movement experiments, autospawn, and resource governing.
- `Data/BotArchetypes.json` defines class plans, skill trees, gear preferences, and rotation attachment.
- `Data/BotRotations/*.json` defines conditional combat actions.

Reload config with `/reloadbotconfig`, archetypes with `/reloadbotarchetype`, and rotations with `/botrotation all reload`.

## Optional server-start population

Configure the character IDs to spawn when the game server finishes starting in
`Configurations/BotConfig.json`:

```json
{
  "AutoSpawnCharacterIds": [2, 3, 4],
  "AutoSpawnState": "grind",
  "AutoSpawnDelayMs": 2000
}
```

`AutoSpawnState` accepts `idle`, `grind`, `questing`, `roaming`, `following`,
`resting`, or `free`. The task runs once after the configured delay. Each
character is attempted independently, and one failure does not stop the other
IDs. The server logs one `BOT ev=autospawn` line per ID.

The local command API can also run bot commands without a live character by using
the synthetic `@system` actor:

```sh
curl -X POST http://127.0.0.1:1280/api/commands/addbot \
  -H 'Content-Type: application/json' \
  -d '{"character":"@system","arguments":"2"}'
```

> **Security note.** `@system` runs commands at access level 100 without user authentication over the command API socket. Bind `WebApiNetwork.Host` to `127.0.0.1` and never expose port 1280 beyond the machine.

## Scale and resource measurements

`ActivityPercent` defaults to `100` when it is omitted: every independent bot is
eligible for normal brain work. `HostTickBudgetMs` retains its existing 30 ms
default. `ServerTickBudgetMs` defaults to `0`, which deliberately disables
whole-server governor input until the no-bot measurement establishes a budget.
Do not treat either default as a demonstrated capacity target.

The admin command `/botmetrics snapshot` emits one `T021_METRICS` JSON document
using schema `t021.scale-metrics.v1`. It includes whole-server work and interval
p50/p95/p99/max, bot-host latency, activity and cadence, scans, decisions, casts,
stuck recovery, spawn/despawn, allocations, GC, process CPU and memory. The
server currently has no bot pathfinder request API, so `pathRequests` remains
zero and is retained to make that absence explicit.

Use `/botmetrics reset` immediately before each steady-state measurement window.
`/botmetrics activity <0-100>` changes the activity percentage in memory only;
it does not persist configuration. A scale result is valid only when the
external gate also records exact bot count, process samples, database query
rates, hardware, build and commit identity. Bot-loop timing by itself is not a
capacity result.
