# Quest autonomy

Quest autonomy is an opt-in PlayerBots lifecycle for native AAEmu quests. It
extends nearby intake to NPCs and doodads such as the Desireen Signpost, then
executes the initially supported objective shape: one active monster-hunt
objective with one authoritative counter.

The implementation does not contain fixture object IDs, template IDs,
coordinates, or quest IDs. It discovers current world objects and reads their
quest templates at runtime.

## Safety and authority

The controller never creates an active quest record, changes an objective
counter, marks a quest ready, or completes a quest directly.

- NPC acceptance uses `CharacterQuests.AddQuestFromNpc`.
- Doodad acceptance revalidates the current doodad quest function and uses
  `CharacterQuests.AddQuestFromDoodad`.
- Monster credit comes only from AAEmu's normal kill event lifecycle. A dead or
  missing target starts a bounded observation window; unchanged counters cause
  target reselection, not synthetic credit.
- Reporting is allowed only for an active Ready quest whose current NPC,
  doodad, or journal endpoint and selected reward match its templates.
- Completion is observed only when AAEmu removes the quest from the active
  journal after a report dispatch.

Unsupported or ambiguous objectives, endpoints, rewards, non-finite world
state, ownership conflicts, world changes, death, disappearance, and timeouts
all fail closed. Controller-owned target/filter or movement state is released,
and the reason plus retry time is exposed through debug state.

## Host adapter

AAEmu 1.2 requires both compatibility patches, in this order:

1. `compatibility/aaemu-1.2-r208022-v3.patch`
2. `compatibility/aaemu-1.2-r208022-doodad-quest-adapter.patch`

The second patch adds four narrow surfaces:

- bounded nearby doodad discovery (100-metre and 256-result hard ceilings);
- read-only resolution of one current doodad quest function;
- read-only access to one quest objective counter;
- guarded native reporting with a 10-metre hard interaction ceiling.

The adapter targets only the pinned AAEmu 1.2 r208022 host lineage. Apply it to
an isolated checkout for build or review; it is not an AAEmu 3.0 adapter.

## Configuration

Both features default to disabled. `QuestIntakeEnabled` controls discovery and
acceptance; `QuestCompletionEnabled` controls objective execution and report.

```json
{
  "QuestIntakeEnabled": true,
  "QuestIntakeScanRadius": 60.0,
  "QuestIntakeInteractionRadius": 6.0,
  "QuestIntakeRetryBackoffMs": 30000,
  "QuestCompletionEnabled": true,
  "QuestObjectiveScanRadius": 60.0,
  "QuestReportScanRadius": 60.0,
  "QuestReportInteractionRadius": 6.0,
  "QuestTargetSelectionTimeoutMs": 30000,
  "QuestProgressObservationMs": 3000,
  "QuestCompletionObservationMs": 5000,
  "QuestCompletionRetryBackoffMs": 30000
}
```

Configured objective and report scan radii are also limited by `SearchRadius`
and the 100-metre hard ceiling. Report interaction radius is limited to 10
metres. Validation clamps timeouts and backoffs to bounded ranges.

## Selection and lifecycle

Intake keeps the existing main-story-first order, then distance, giver kind,
object ID, and quest ID. All quests exposed by the selected giver are attempted
in deterministic main-story/quest-ID order through native acceptance.

For active quests, a Ready quest is handled before an in-progress quest; ties
use main-story status and quest ID. A supported objective selects the nearest
living, hostile, exact-template NPC, with object ID as the stable tie-break.
The existing movement and combat task performs travel and attacks. When Ready,
the controller chooses the single supported endpoint and the lowest offered
selective-reward index (or zero when no selective reward exists).

After authoritative completion, the host yields in the same tick to nearby
quest intake. No per-quest operator command is part of the lifecycle.

## Diagnostics

`/botdebug <characterId>` prints stable `Quest intake` and `Quest lifecycle`
lines with giver, quest, objective target and progress, report endpoint, reward,
decision reason/timestamps, retry, and counters.

Bounded structured log events use `ev=quest_intake_*` and
`ev=quest_lifecycle_*`. Material events cover discovery/selection, movement,
acceptance/rejection, objective target selection, authoritative progress,
no-credit observation, report dispatch/rejection, completion, suspension, and
rescan.

## Initial limitation

Only a single active `QuestActObjMonsterHunt` objective is executable. Multiple
active objectives and other objective act types remain visible as a safe
suspension reason; adding support requires a separate explicit controller path
and focused tests.
