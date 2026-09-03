# Quest autonomy

Quest autonomy is an opt-in AAEmu 1.2 feature. It uses the server's normal quest, combat, loot, and report APIs; it does not grant progress directly.

## Enable it

Both switches ship disabled:

```json
{
  "QuestIntakeEnabled": true,
  "QuestCompletionEnabled": true
}
```

Reload with `/reloadbotconfig`.

## Decision order

The bot keeps valid current work instead of changing goals every tick. When it needs new work, it groups destinations within 500 metres as local, then orders by distance, ready-to-report state, main-story status, and quest ID. Regional work is considered after local work.

Intake discovers eligible NPC and doodad starters, approaches through normal movement, and accepts through AAEmu. Completion supports:

- exact monster-hunt objectives;
- quest-linked item gathering from corpses owned by the bot;
- supported NPC, doodad, and journal reporting;
- waiting for respawns and native quest-state updates.

Quest markers and static spawns locate objectives. Transfer roads guide long travel, while local movement handles the final approach. See [World navigation](WORLD-NAVIGATION.md).

## Safety and limits

Ambiguous, mixed, unsupported, cross-world, or unreachable work suspends with a reason. The bot revalidates live targets before combat or interaction and never teleports to complete a quest.

This is not universal quest support. Scripted item use, conversations, vehicles, complex gathering, caves, and sparse navigation data may require new objective handlers.

Use `/botdebug <id>` for the current intake, lifecycle, target, and route decisions. The optional [live monitor](../scripts/autonomy/README.md) presents the same state as a static board.
