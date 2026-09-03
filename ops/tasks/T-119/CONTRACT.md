# T-119 contract: autonomous nearby quest intake

Start only from PB-000's exact committed T-119 binding and source base
`cb9232d23312f719a90dc2ca51e6f5a561402fde`. The live T-118 Login, Game,
launcher, client, deployed module, and Director configuration are outside this
task. This is a source correction, not a live-session mutation or a claim that
general quest completion already exists.

Add one opt-in production quest-intake controller owned by each `BotRuntime`.
When quest intake is disabled, behavior must remain byte-for-byte equivalent at
the decision boundary. When enabled, the host must give quest intake first
refusal before `BotLifeController` can activate the one-kill grind lifecycle.
The controller may act only for a living, world-valid, unforced, combat-idle,
targetless bot that is not following, falling, jumping, recovering, logging
out, or already moving for unrelated work. It must fail closed on malformed or
missing state and must never teleport, fabricate quest progress, force a quest,
or bypass AAEmu requirements.

Discover candidates from the bot's existing cached nearby-NPC blackboard value
and resolve every object through its current `ParentWorld`. Accept only living
NPC objects in the same world/instance with finite transforms and a finite,
heightmap-reachable target volume inside the configured bounded scan radius.
Use `QuestManager.GetPlayerBotNpcQuestStarts(npc.TemplateId)`; do not rescan all
quest templates per tick. Exclude already-active quests and completed
non-repeatable quests before planning. AAEmu remains the authority for every
other start requirement.

Define a main-story candidate as a quest with a nonzero `ChapterIdx` or
`QuestIdx`. Select deterministically by main-story rank first, then distance,
NPC object ID, and quest ID. Group all currently eligible starts on the chosen
NPC so the bot does not oscillate between quests on one giver. Within that NPC,
attempt main-story quests before side quests and quest ID ascending. After the
main-story candidate is handled, continue discovering and accepting eligible
side quests around the bot; do not stop merely because one quest was accepted.

For a selected NPC outside the configured interaction radius, request normal
run movement through `BotManager.SetBotDestination`; retain enough target state
to avoid replanning each tick. Revalidate the NPC, world, transform, distance,
and controller safety guards on every step. Once in range, stop only the
controller-owned movement and call `bot.Quests.AddQuestFromNpc(questId,
npc.ObjId)` for each planned candidate. Clear only the temporary NPC target the
normal AAEmu call assigned after each attempt. Record success only when AAEmu
returns true and the quest is authoritatively active. A rejection must remain a
rejection, enter bounded per-candidate retry backoff, and must not spin or block
other eligible quests at that NPC.

Expose a read-only controller view with state, selected NPC/quest, story rank,
last decision/reason, accepted and rejected counts, and timestamps. Emit
structured bounded logs for selection, movement request, accepted quest,
rejection, invalidation, and idle/no-candidate transitions. Add one stable
`/botdebug` line for the selected runtime; this observability must not mutate
the bot. Reset all controller target/backoff/counters on a fresh runtime.

Add validated configuration for enabled state, scan radius, interaction radius,
and rejection retry backoff. Default enabled state must be false so installing
the source cannot silently change existing Director behavior. Clamp numeric
values to safe finite production bounds, require interaction radius not to
exceed scan radius, serialize them in the default JSON, and document that the
feature performs intake only—not objective execution, reporting, arbitrary
long-range routing, bot-character creation, or quest-state reset.

Focused tests must prove at least: disabled equivalence; all fail-closed guards;
cached nearby scan use; same-world and finite/reachable filtering; story-first
selection even when farther than a side quest; deterministic ties; movement
without teleport; no repeated destination churn; revalidation after despawn or
world change; in-range main-then-side acceptance; normal AAEmu rejection with
backoff; one rejected quest not starving another; active/completed duplicate
filtering; target cleanup; no fabricated success; fresh-runtime reset; and host
precedence preventing simultaneous grind activation. Extend config and debug
tests for exact validated settings and the stable view.

Run the complete new controller tests and directly affected host, config, and
command suites. Perform one isolated AAEmu 1.2 build with zero errors, retain
the exact warning count, and prove compatibility patch SHA-256
`baa0979a200eb7f4403985d7166092f937033c31650aa11336fba31543d96606`
unchanged and cleanly applicable to the pinned reference. Do not install, run
the full suite, or start/control Login, Game, a client, database, observer, or
the T-118 session.

Commit only declared paths and a concise `HANDOFF.md` with candidate identity,
changed behavior, focused/build proof, retained failures, current T-118
untouched state, and the exact independent integration/install/runtime action.
Physical acceptance requires a later clean runtime where an eligible level-one
Nuian bot starts beside the user, autonomously selects the local main-story
giver, walks into range, accepts the main story and eligible nearby side quests,
and exposes matching server/client-visible state without per-quest commands.
