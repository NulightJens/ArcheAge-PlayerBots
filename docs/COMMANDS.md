# Operator Commands

Run `/bot` in game for the quick start and `/help <command>` for exact arguments. Examples use `<gm-character-name>`; replace it with the operator's current GM character, never a hard-coded test name.

| Command | Access | Purpose |
| --- | ---: | --- |
| `/bot [topic]` | Everyone | Topic-based PlayerBots help. |
| `/botcontrol <id> <verb> [role]` | Party owner | Native-party follow, stay, attack, passive, and role control. |
| `/addbot <id>` | GM | Log in an existing offline character as a bot. |
| `/removebot <id>` | GM | Save and normally log out a bot. |
| `/botstate <id> [state\|free] [killGoal]` | GM | Inspect or force idle, grind, questing, roaming, following, or resting. |
| `/movebot <id> <x> <y> <z> [walk\|run]` | GM | Give one explicit movement destination. |
| `/botfollow <id\|all> <name\|stop\|status> [...]` | GM | Direct follow and deterministic staging formation. |
| `/botattackobject <id\|all> <npcObjId>` | GM | Run a contained fight against one NPC object. |
| `/botarchetype <id> [force\|reroll]` | GM | Inspect or re-evaluate a class/equipment plan. |
| `/botrotation <id\|all> show\|reload` | GM | Inspect or reload rotations. |
| `/botrotation <id> set <rotationId>` | GM | Override one bot's rotation. |
| `/botduel <id1> <id2>` | GM | Start a bot-versus-bot duel. |
| `/botjump <id\|name\|all>` | GM | Queue the experimental bot jump path. |
| `/spawnpassive <npcTemplateId> [distance]` | GM | Spawn a stationary development target. |
| `/botactions <id> [co\|nc]` | GM | Show recent decisions. |
| `/botvalues <id> [filter]` | GM | Inspect blackboard values. |
| `/botdebug <id>` | GM | Show detailed runtime state. |
| `/botbuff <id> <buffId\|-buffId> [abLevel]` | GM | Apply or remove a data-pack buff from one bot for controlled development tests. |
| `/botstrategy ...` | GM | Inspect or alter engine strategies. |
| `/botmetrics [snapshot\|reset\|activity]` | GM | Capture host and whole-server measurements. |
| `/reloadbotconfig` | GM | Reload `Configurations/BotConfig.json`. |
| `/reloadbotarchetype` | GM | Reload archetypes, retaining the last valid set on failure. |

## Native party control

Spawn a bot, invite its character through the normal client party UI, then assign a role and order:

```text
/botcontrol 2 role healer
/botcontrol 2 follow
/botcontrol 2 attack
/botcontrol 2 passive
/botcontrol 2 stay
```

Roles are `tank`, `healer`, and `attacker`. Authorization follows live party membership; unrelated players cannot control a bot by guessing its ID.

## GM staging

Direct follow is useful when staging a physical test without a native party:

```text
/botfollow all <gm-character-name> 3 auto 1.5
/botfollow all status
/botfollow all stop
```

The optional values are rear gap, column count (`auto` allowed), and spacing. This is deterministic formation placement, not collision avoidance.

## Controlled bot buffs

`/botbuff` does not require a client-side selection. Positive IDs apply a known buff template; negative IDs remove it:

```text
/botbuff 2 <buffId>
/botbuff 2 -<buffId>
```

Buff IDs are client-data specific. This is a GM development surface for repeatable state tests, not a PlayerBots combat ability or a substitute for natural mana/recovery testing.

## Headless local administration

The Web API accepts the same commands with synthetic actor `@system`:

```powershell
$body = @{ character = '@system'; arguments = '2' } | ConvertTo-Json
Invoke-RestMethod -Method Post `
  -Uri 'http://127.0.0.1:1280/api/commands/addbot' `
  -ContentType 'application/json' -Body $body
```

`@system` has administrator access and no user authentication. Keep the API bound to loopback; never expose port 1280 publicly.
