# Roadmap

The goal is believable, useful, lightweight party members and world population. ArcheAge-native systems remain the baseline unless a measured gap requires more logic.

## Active track policy

AAEmu 1.2 r208022 is the sole active feature and runtime target until the one-zone Population Director passes its soak and recovery gate. AAEmu 3.0 is a frozen compatibility checkpoint: preserve its accepted source, patch, assets, and evidence, and run its clean install/build/adapter regression only at major milestone or release boundaries. Current task dispatch and dependencies live in `ops/BOARD.yaml` and `ops/ROADMAP.md`.

## 0.1 release candidate: foundation

Status: released as `0.1.0-rc.2`; accepted fixes continue under Unreleased.

- Connectionless persistent-character lifecycle.
- Data-driven archetypes and rotations.
- Native-party authorization and role controls.
- Melee, archer, caster, and healer range anchoring.
- Death, invalid-target, kill-credit, and follow recovery.
- Human GM commands and runtime diagnostics.
- Exact population metrics and isolated scale harness.
- Physical execution at 100, 500, and 1,000 bots.

## 0.2: believable combat

- Completed: explicit attack, follow, state, and party work wakes brains shed by inactive-duel cleanup without keeping Idle brains active. A four-role physical regression passed with all roles casting, zero tick errors, and zero runtime overlaps.
- Completed: stationary and close-range Primeval checkpoints. Endless Arrows supplied 33 of 45 hostile-target casts in the four-role sample and 11 of 15 damaging shots after a close escape. The close trial used Snare, Backdrop, and one tangent-dominant destination to settle at 19.182 m; moving-hostile pursuit remains open.
- Completed: a sampled 16.985 m Darkrunner approach held short-range filler and Triple Slash, allowed only legal gap closers during approach, and did not execute Whirlwind Slash until 3.936 m. Boundary regressions reject Whirlwind Slash beyond 5 m and Sunder Earth beyond 9 m.
- Completed: Daggerspell approached from 26.507 m, held an 18.970 m Fireball anchor, and used Fireball for 8 of 11 damaging casts (72.7%). A data-only 4-second shared throttle prevents asynchronous plot-cooldown burst rows from starving the default Fireball lane; the physical pass had no cast failures, tick errors, runtime overlaps, or path requests.
- Completed: with native follow and attack orders both active, Cleric selected a 76.24%-health party member at 35.061 m, retained the human owner as its follow target, moved to a 24.047 m Antithesis standoff, healed the recipient to 100%, and resumed owner-relative movement. The instrumented pass had seven successful casts, zero failures, zero tick errors, zero runtime overlaps, and zero path requests.
- Completed: Primeval's moving-hostile gate. A level-50 melee fixture moved 18.13 m while Primeval reported movement in 10 of 24 retained samples; Endless Arrows starts overlapped the moving segment. The companion 30-second sample produced 16 Endless Arrows in 22 offensive casts (72.73%). The isolated repeat had 12/12 successful casts, a 0.7 ms bot-host p95, zero scans, zero path requests, zero skipped ticks, zero overlaps, and zero tick errors.
- Completed: the continuously moving-owner Cleric gate. The leader moved 34.66 m over 10.038 seconds while the Cleric preserved the follow target in all 37 samples, moved 19.29 m toward a 53.23%-health party member, completed Antithesis before the movement window ended, crossed the 85% support threshold, and resumed leader-directed movement. The four-runtime sample had one successful cast, a 1.2 ms bot-host p95, and no cast failure, path request, skipped tick, runtime overlap, or tick error.
- Automated: stealth loss preempts continuously available rotations, search diagnostics expose the last-known state, and search scans are metered. Physical release and reacquisition remain open.
- Run mortal-combat cohorts at 1, 5, 10, 25, 50, and 100 bots with matched Idle controls.

Mortal fixtures must be staged away from the human observer and unrelated respawning hostiles. The moving-hostile cleanup exposed this harness requirement when the observer and one melee bot died outside the isolated Primeval measurement; both recovered, but that cleanup is not an accepted death-free cohort.

## 0.3: navigation and sustainability

- Completed: bot movement now refreshes AAEmu spatial-region membership and zone transitions only when crossing a 64-meter region boundary. A live quest run crossed from Mayor Gott's region into the neighboring boar region and back; ordinary bounded nearby discovery, combat, and reporting remained available after both crossings.
- Establish an ArcheAge-compatible navigation boundary instead of direct coordinate traversal.
- Add obstacle diagnostics before claiming wall or terrain behavior.
- Measure natural mana exhaustion, rest, recovery, and re-entry without administrator healing.
- Revisit jump only when packets produce correct animation and geometry interaction.

## 0.3: questing vertical slices

- Completed once on 3.0: discover and inspect exact-NPC quest relations, accept quest 330 through AAEmu's native lifecycle, persist its ready state through a graceful server restart, reject a remote report, report only the selected quest at the exact NPC, and retain completion through bot logout/re-add.
- Automated contract: lazy NPC quest indexes, 100-meter scan ceiling, 6-meter interaction ceiling, explicit status, nonnegative reward index, and no support claim for emotion, kill-trigger, or NPC-group starters.
- Completed once on 3.0: quest 293's supplied Magic Crystal used native skill 11684 against exact Nymph object 102887 after normal bot combat stopped at a 50% non-lethal floor. AAEmu accepted the 5-second channel, produced item 8243, advanced the real gather objective from 0/8 to 1/8, and left the Nymph alive at 8,626/18,849 HP. No item, health, or quest progress was fabricated.
- Automated contract: the exact-target non-lethal floor preempts compiled rotations, survives authoritative combat-task replacement after respawn, clears target/follow/destination state atomically, and reports its armed percentage. Native item use selects the exact unit before AAEmu evaluates `UnitReqs`, restores the previous selection on rejection, and retains the selection for a successful delayed effect.
- Completed once on 3.0: one `/botquest acquire` call derived Nymph template 3460, a 50% health ceiling, and skill 11684 from the carried quest item's native requirements; contained combat stopped object 102886 alive at 8,631/18,849 HP, automatically launched the real item skill, and advanced quest 293 from 1/8 to 2/8 with inventory increasing from one result item to two. No separate item-use command or target/item/objective mutation was used.
- Automated contract: the acquisition executor refuses alternative (`OR`) or ambiguous requirement sets, requires one exact NPC, one 1-99% target-health ceiling, and the matching quest context, and invokes its floor callback at most once. Explicit combat/state resets clear the callback instead of allowing stale quest actions.
- Completed once on 3.0: quest 251 was accepted from Mayor Gott with objective/inventory 0/3 and 0, then three normally killed Solzreed Boars were looted through their native corpse containers. The exact transfers advanced objective/inventory 0->1, 1->2, and 2->3; one corpse retained its unrelated loot entry. Native reporting completed the quest, removed all three cleanup items, and delivered reward item 18791 x2 by quest-reward mail because the test bag was full.
- Automated contract: exact-template world discovery is read-only and capped at ten results. Corpse loot requires a dead NPC within 6 meters, an exclusive solo tag by the bot, and exactly one generated corpse item whose ID and native quest link match the active gather act; AAEmu's host loot path performs the transfer.
- Completed once on 3.0: quest 620 was accepted from Landlord Oliviano, its native supply step advanced normally, and `/botquest hunt` derived Plains Razorbeak template 7781 plus the remaining 3-kill goal. The executor selected and killed three exact targets through normal combat, advanced the live objective from 0/3 to 3/3, released its target, returned to automatic Idle, and reported the quest at the exact NPC. Metrics recorded three observed and three credited kills with zero tick errors, skipped ticks, or runtime overlaps.
- Automated contract: a hunt must contain exactly one currently active native `QuestActObjMonsterHunt`, one exact NPC template, a positive remaining count, and at least one living and attackable target inside the lesser of the configured radius and 100 meters. Selection rejects closer wrong-template targets, repeats after each exact kill, stops at the derived native goal, uses true three-dimensional distance, and fails closed when an NPC does not agree with the bot's navigation-height surface.
- Retained negative evidence: quest 3427's Cave Bat objective is physically inside a cave while the current simple heightmap mover projects to the exterior surface. The first attempt exposed that mismatch; after the safety correction, the same live fixture was rejected before combat instead of stranding the bot above the target.
- Completed once on 3.0: quest 312 started at Apothecary Nestelle with its native sphere objective at 0/1. From an isolated staged point 95.4 meters away, `/botquest travel` derived the one same-world 30-meter sphere and exact heightmap destination, normal bot movement entered it, AAEmu finalized the sphere act, and native auto-complete moved the lifecycle to completed.
- Automated contract: travel requires exactly one active `QuestActObjSphere`, an unsatisfied native objective, a static rather than NPC-centered sphere, exactly one same-world geometry result, true 3D distance no greater than 100 meters, and a finite heightmap surface that remains at least 0.25 meters inside the sphere. It clears follow/formations/combat, holds safely in forced Idle, and delegates both motion and objective credit to the existing mover and AAEmu sphere trigger.
- Next: add one separately measured emote objective executor, then define the first bounded objective-transition policy without autonomous quest selection.
- Autonomous quest selection, routing, and chaining remain deferred until those objective executors and navigation boundaries pass independently.

## 0.4: population operations

- Approve a whole-server reserve and latency budget from an empty-server baseline.
- Run the highest budget-qualified cohort for 30 minutes plus normal-logout recovery.
- Introduce a one-zone Population Director with explicit activity and density limits.
- Expand only after zone-level resource and player-experience acceptance.

## ArcheAge 3.0

Status: frozen compatibility checkpoint; no new 3.0 feature or physical acceptance work during the AAEmu 1.2 Population Director milestone.

- Completed: pinned NL0bP/AAEmu base `8c1c943bb2309eefffb9da2aa99a408d0acbb095` for client `3.0.4.2 r336598`.
- Completed: compile-time compatibility layer, version-specific Game/test MSBuild targets, and reviewed 24-file alpha-v4 host patch.
- Completed: non-incremental Game and full-solution builds with zero compiler errors; the current 3.0 adapter suite passed 160/160, including class/gear command, gear-create restart, equipment-visibility packet, persisted-resource, and bounded quest-command coverage.
- Completed: fail-closed dual-track installers and machine-readable compatibility metadata. The 3.0 track requires an explicit experimental flag.
- Completed: acquired and integrity-tested the matching client, 36.81 GB `game_pak`, decrypted `compact.sqlite3`, and `compact.server.table.sqlite3`; retained provenance includes every SHA-256.
- Completed: isolated MySQL schemas and unique Login/Game/Stream/Web API ports; server startup loaded 491,683 translations, 257 zones, 6,606 quests, 36,694 item templates, and 24,175 skills.
- Completed: all 94 PlayerBots rotation/learn-order skill IDs and all 38 passive-buff IDs resolve. The four home anchors retain 4 m Triple Slash, 20 m Endless Arrows, 20 m Flamebolt, and 25 m Antithesis ranges. Thirty-six audited skills have data changes from 1.2 and five rotation skills have target-semantics changes, so physical role validation remains mandatory.
- Completed: 3.0 Web API `@system` control, module schema application, zero-bot metrics, graceful cleanup, and clean restart. The server-start baseline used about 6.35 GB working set and 6.40 GB private memory, so 3.0 gets its own resource budget rather than inheriting the 1.2 scale claim.
- Completed once: native account/character creation, matching launcher authentication, character selection, and world entry without DLL or serializer failure.
- Completed once: rendered bot spawn, normal logout/re-add, follow, and controlled dummy combat. Database/runtime inspection retained the three Darkrunner trees and 23 skills.
- Completed once: `/setclass` refreshed Battlerage/Auramancy/Shadowplay at level 55; `/kit` auto-equipped a grade-5 two-handed weapon, and normal logout/re-add retained class and equipment.
- Completed once: the staged exact-NPC quest 330 accept/report flow, including graceful-restart and bot-reload persistence plus fail-closed distance and unrelated-quest checks.
- Completed once: quest 620's selected exact-NPC hunt loop completed three native kills, released combat state at the native goal, and reported normally; a vertically unreachable cave fixture now fails closed during preflight.
- Completed once: quest 312's selected static sphere objective ran 95.4 meters through the normal mover, finalized through AAEmu's native enter-sphere event, and auto-completed without fabricated progress.
- Next: repeat the login/lifecycle/class/gear and representative quest gates from a clean start, then run four-role party/combat and 0/10/50/100 resource/recovery gates.
- Promotion rule: 3.0 remains server-start-validated alpha until every runtime gate passes; configuration, a successful build, or server startup alone is not a support claim.

## Shelved experiments

- Pairwise collision repulsion moved the target and caused excessive bumping.
- Melee orbit/ring behavior covered only a narrow boss arc, left bots disengaged, and added continuous cost.
- Bot jump motion rendered as vertical sliding rather than a normal player jump.

These are documented findings, not enabled dependencies.
