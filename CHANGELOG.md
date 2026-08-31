# ArcheAge PlayerBots Changelog

Player-facing patch notes come first; developer packaging detail follows.

## Unreleased

### Staged quest controls

- Added an exact-target non-lethal health floor to `/botattackobject`, allowing normal bot combat to disengage at a requested 1-99% threshold for quest-item interactions without mutating target HP.
- Added `/botquest scan`, `inspect`, `status`, `accept`, and `report` as bounded GM development controls for exact-NPC quest discovery and one selected quest lifecycle. This is a vertical slice, not an autonomous questing claim.
- Expanded read-only quest inspection with exact NPC, NPC-group, doodad, item, distance, sphere, cleanup, and selective-reward fields so objective fixtures can be chosen from native data without guessing identifiers.
- Added bounded read-only `/botquest nearby` exact-template inspection for live NPC object IDs, health, distance, and position.
- Added `/botquest talk` for one selected active quest and exact nearby NPC. It invokes only that quest's live native talk acts, verifies an objective increase, and fails closed on team-shared acts to prevent cross-character broadcasts.
- Added `/botquest use <botId> <questId> <npcObjId>` for one narrow native item-objective slice. It requires exactly one active gather act and one carried quest-linked Supply item, then asks AAEmu's normal item-skill engine to cast against the exact living NPC; it never mints the result item or broadcasts quest progress.
- Added `/botquest acquire <botId> <questId> <npcObjId>` as a bounded one-target executor. It derives the exact NPC template and health ceiling from the carried quest item's native unit requirements, runs normal contained combat to that ceiling, disengages, and automatically launches the real item skill. Ambiguous or alternative requirement branches fail closed.
- Added read-only `/botquest locate <botId> <npcTemplateId>` for explicit exact-template discovery across the bot's current world, sorted by distance and capped at ten results without selecting or moving the bot.
- Added `/botquest loot <botId> <questId> <corpseObjId>` for one bounded native corpse-acquisition slice. It requires the selected quest's only gather act, a nearby dead NPC exclusively tagged by that bot, and exactly one generated item matching both the gather item and its native quest link; the host loot implementation performs the transfer, and no item is created by PlayerBots.
- Added `/botquest hunt <botId> <questId>` for one bounded exact-NPC monster-hunt slice. It derives the target template and remaining kill goal from exactly one active native hunt act, repeatedly selects the nearest legal match, and returns to automatic Idle after native kill credit reaches the goal.
- Added true three-dimensional and navigation-height safety checks to exact quest-hunt preflight and target retention. Vertically stacked or cave fixtures that the current heightmap mover cannot safely reach now fail closed.
- Added explicit `/movebot <id> <x> <y> <z> teleport` for isolated GM fixture staging without presenting teleportation as autonomous travel proof.
- Added compile-time 1.2 and 3.0 corpse-loot adapters while retaining fail-closed ownership and item-identity checks in the shared command.
- Fixed long-distance bot movement leaving the character registered in its spawn region. Movement now refreshes AAEmu visibility and zone membership only when crossing a 64-meter spatial-region boundary, keeping nearby discovery, combat, and quest interaction authoritative without adding per-tick region mutations.
- Expanded `/botquest status` with the active gather act, item ID, native objective count, inventory count, and cleanup flag so a completed channel can be verified independently of cast acceptance.
- Fixed connectionless native item use so AAEmu sees the exact selected NPC while evaluating pre-cast unit requirements. Rejected casts restore the previous selection; accepted channels retain the exact target for delayed native effects.
- Enforced the exact-target non-lethal floor ahead of compiled engine rotations and replaced stale combat tasks after bot respawn, preventing autonomous rotations from bypassing a quest staging floor.
- Physically passed quest 293's first native acquisition on 3.0: normal bot combat stopped exact Nymph object 102887 alive at 8,955/18,849 HP, skill 11684 was accepted from the carried Magic Crystal, and AAEmu advanced item 8243 and the gather objective from 0/8 to 1/8 without fabricated state.
- Physically passed the repeatable one-command acquisition on 3.0: one `/botquest acquire` call stopped exact Nymph object 102886 alive at 8,631/18,849 HP, automatically launched skill 11684, and advanced the native objective and inventory from 1/8 and one result item to 2/8 and two result items without a separate use command.
- Physically passed quest 251's full corpse-delivery lifecycle on 3.0. Three normal Solzreed Boar kills and three exact native corpse transfers advanced objective/inventory 0->1->2->3; an unrelated corpse entry remained untouched. Reporting at Mayor Gott completed the quest, consumed all three cleanup items, and placed reward item 18791 x2 in native quest-reward mail because the 50-slot test bag was full.
- Physically passed cross-region movement during that lifecycle: the bot crossed a 64-meter region boundary toward the boars and back, and ordinary bounded nearby discovery found the exact NPCs after both crossings.
- Passed fresh dual-track gates for the corpse-delivery and movement-region slices: 1.2 at 1,753 passed plus 4 intentional skips and 3.0 at 159/159. The live 3.0 Game build completed with 32 existing warnings and 0 errors.
- Physically passed quest 620's selected hunt on 3.0: three exact Plains Razorbeak kills advanced the native objective 0/3 to 3/3, produced three observed and three credited kills, cleared the target, restored automatic Idle, and reported normally with zero tick errors, skipped ticks, or runtime overlaps.
- Retained and corrected a physical cave-path failure from quest 3427: after a Cave Bat target below the terrain exposed the simple heightmap mover's vertical limitation, the new preflight rejected the same unreachable fixture before combat. Fresh gates passed 1,759 plus 4 intentional skips on 1.2 and 159/159 on 3.0; the live 3.0 Game build completed with 32 existing warnings and 0 errors.
- Added the first read-only `AAEmu.ClientDriver` slice: deterministic client process/window discovery, ArcheAge log lifecycle parsing, and a loopback-only JSON status API with a black-box validation script.
- Fixed `/botattackobject` object lookup for loopback `@system` commands so exact objects discovered by `/botquest nearby` can be inspected and attacked through the live API.
- Quest scanning uses lazy NPC-to-quest indexes built once after AAEmu loads quest templates; it does not rescan all 6,606 3.0 quests during bot brain ticks.
- Acceptance and reporting fail closed unless the bot is within 6 meters of the exact supported NPC. Emotion, kill-trigger, and NPC-group starters remain deferred.
- Reporting invokes only the selected active quest's live report act. It does not use AAEmu's broadcast report helper, which can advance multiple active quests sharing the same NPC.
- Live 3.0 quest 330 acceptance survived a graceful server save/restart, rejected a report at 17.9 meters, completed at the exact reporter, left nearby quest 6198 inactive, and remained completed after normal bot logout/re-add.
- Published new immutable full installer patches for both host lines: 1.2 v3 and 3.0 alpha-v4. Previous compatibility patches remain byte-identical; the new versions add the explicit GM access contract for `/botquest`.

### Human class and gear controls

- Restored `/setclass [botId] <archetype> [level]` from the T-009 development path. It replaces all three skill trees, rebuilds active/passive skills, saves the final archetype, refreshes compatible gear, and normally respawns the bot so clients receive a fresh sheet.
- Added target-first `/botgear [botId] show|equip|inspect` (`/botequip` alias) for localized equipment reporting, server-authoritative bag evaluation, and the stock client's read-only remote character detail.
- Added `/botgear [botId] create <grade> <prefix> <armor> <weapon>`. It creates a complete Magnificent loadout at the requested grade, resolves stat-compatible fallbacks when a literal family piece does not exist, immediately evaluates equipment, prints the actual English item names, saves the bot, and restarts it for a fresh client snapshot.
- Changed successful `/botgear ... create` operations to restart the affected bot through the same normal logout/login lifecycle used by `/setclass`. This replaces the unreliable 3.0 live equipment-delta refresh with a fresh character snapshot and reports restart or respawn failure honestly.
- Integrated AAEmu's `/kit` command with live PlayerBots so compatible kit items are evaluated, equipped, and saved immediately instead of remaining unused in the bag.
- Live 3.0 acceptance set `Darkrunnerbot` to Battlerage/Auramancy/Shadowplay at level 55 with 23 skills and 5 passives, equipped a grade-5 two-handed kit weapon, and retained both class and equipment across normal logout/re-add.
- Live 3.0 acceptance created `/botgear 2 create celestial flame leather nodachi`, equipped all 15 requested slots, and retained the identical item instances across normal logout/re-add. The active data pack resolved the missing literal Flame pieces honestly as Magnificent Desert leather, a Lightning Bow, and a Quake Flute while retaining Flame jewelry and the Flame Nodachi.
- Corrected equipment reporting to read each item's authoritative equipment-container slot; 3.0 no longer mislabels a main-hand weapon by its list position.
- Made equipment visibility an enforced bot policy: every 3.0 bot publishes **public** after spawning, newly nearby clients receive the public flag with its world snapshot, and `/botgear inspect` reasserts public before sending character details. Connectionless bots cannot drift back to private after logout/re-add, and no database preference or per-tick work was added.
- Passed the clean 1.2 installer suite at 1,742 total (1,738 passed, 4 intentional skips) and the clean 3.0 adapter suite at 158/158.

### Documentation and onboarding

- Added a fail-closed source-preview packager that requires a clean Git commit, verifies compatibility-patch and migration hashes, checks archive contents, excludes Git internals, and emits JSON provenance plus a SHA-256 sidecar.
- Added a shareable-preview guide with exact support claims, archive installation steps, and explicit exclusions for clients, databases, credentials, runtime logs, and local evidence.
- Reworked the public README and user guides around installation, first bot, party control, configuration, commands, and troubleshooting.
- Clarified that ArcheAge PlayerBots controls selected existing characters and does not claim random-account populations, automated raid or battleground completion, or a public server-capacity target.
- Simplified `/bot` help topics and updated compatibility guidance for the supported 1.2 track and experimental, server-start-validated 3.0 track.

### ArcheAge 3.0 server-start adapter

- Added a standalone, opt-in adapter for NL0bP/AAEmu client `3.0.4.2 r336598`, pinned to base `8c1c943bb2309eefffb9da2aa99a408d0acbb095`.
- Added version-specific build/test compatibility plus a reviewed host patch covering lifecycle, startup/shutdown, party events, world lookup, tick metrics, combat attribution, persistence, operator command registration, kit auto-equip, bot equipment visibility, and the loopback `@system` actor.
- Added fail-closed dual-track PowerShell and Bash installation. The 3.0 path requires an explicit experimental flag while runtime acceptance is outstanding.
- Passed non-incremental 3.0 Game and full-solution compilation with zero errors and the complete 158/158 adapter suite. Coverage includes persisted HP/MP restoration, aggro cleanup, kill attribution, tick-metric maxima, connectionless administrative access, Web API actor resolution, human-facing class/gear/quest commands, the equipment-visibility packet contract, and the gear-create restart lifecycle.
- Acquired and integrity-checked the matching compact databases, then passed isolated 3.0 Login/Game startup, module migration, loopback status/metrics, zero-bot graceful cleanup, and clean restart on non-1.2 ports. Runtime support is not claimed until client login, one-bot lifecycle, four-role combat, and scale/recovery gates pass.
- Audited all 94 configured skill IDs and 38 passive-buff IDs against 3.0 data. The four role anchors retain their expected ranges; 36 skills differ from 1.2 and five rotation skills changed target semantics, which are explicit physical-test gates rather than assumed parity.
- Added a read-only 3.0 asset-provenance preflight that verifies the pinned emulator lineage, all three required files, SQLite headers, recorded SHA-256 hashes, and explicit rejection of known 1.2 compact databases before runtime startup.

### Lifecycle and control

- Fixed an inactive-duel lifecycle hole that could leave a bot in Combat state with no scheduled brain, presenting physically as only half of a party fighting.
- Explicit attack, follow, non-idle state, free-active, and authorized native-party commands now reattach a shed brain on demand while preserving the low-cost detached Idle path.
- Added regressions for direct command wake-up, native-party command wake-up, and runtime reuse after reactivation.
- Physically reproduced two detached brains, then verified all four roles reactivated and cast against one passive target: 150/150 successful cast attempts, zero tick errors, zero runtime overlaps, and a 1.557 ms maximum bot-host tick.
- In that physical sample, Endless Arrows was 33 of 45 Primeval hostile-target casts (73.3%).
- Passed a 16.985 m Darkrunner approach sample: short-range filler and Triple Slash were held, while Tiger Strike, Overwhelm, Shadowsmite, Charge, and Whirlwind Slash succeeded only inside their legal ranges. Automated effect-radius boundaries retain 5.00/5.01 m coverage for Whirlwind Slash and 9.00/9.01 m for Sunder Earth.
- Passed a 3.042 m Primeval close-target sample: Snare and Backdrop fired legally, a single tangent-dominant escape destination produced 17.445 m of lateral travel versus 4.573 m outward, and the bot settled at 19.182 m. Endless Arrows then supplied 11 of 15 damaging shots (73.3%) with zero cast failures, tick errors, runtime overlaps, or path requests.
- Fixed Daggerspell burst rows starving its Fireball baseline during AAEmu's asynchronous plot-cooldown window. One shared 4-second special-damage throttle retained the Fireball default lane without adding per-tick work; the corrected 26.024-second physical pass held 18.970 m and produced 8 Fireballs in 11 damaging casts (72.7%), with zero cast failures, tick errors, runtime overlaps, or path requests.
- Passed the native-party Cleric recipient gate while follow and attack orders competed. The Cleric retained the human party owner as its follow target, selected a 76.24%-health party member at 35.061 m, moved to a 24.047 m Antithesis standoff, healed the recipient to 100%, and resumed owner-relative movement. The 16.858-second instrumented pass had seven successful casts, zero failures, zero tick errors, zero runtime overlaps, and zero path requests.
- Passed Primeval's moving-hostile gate against a level-50 melee fixture. The target moved 18.13 m, Primeval reported movement in 10 of 24 retained samples, and Endless Arrows starts overlapped the moving segment. A companion 30-second sample kept Endless Arrows at 16 of 22 offensive casts (72.73%); the isolated resource repeat completed 12/12 casts with a 0.7 ms bot-host p95 and no scans, path requests, skipped ticks, overlaps, or tick errors.
- Passed the continuously moving-owner Cleric gate. During a 10.038-second leader movement covering 34.66 m, the Cleric retained its follow target in all 37 samples, repositioned 19.29 m toward a 53.23%-health party member, completed Antithesis while the leader was still moving, raised the recipient to 91.13%, and resumed leader-directed movement inside the same input window. The four-runtime pass had one successful cast, zero failures, zero path requests, zero skipped ticks, zero runtime overlaps, and zero tick errors; bot-host p95 was 1.2 ms.
- Tightened the physical-test protocol: mortal fixtures must be staged away from the human observer and unrelated respawning hostiles. A post-measurement cleanup caused observer and melee-bot deaths and is explicitly excluded from the accepted ranged result.
- Added a high-priority stealth-loss transition so an available rotation cannot starve search behavior when the current target disappears into stealth.
- Added search-state, last-known-position, duel-opponent, and target-stealth diagnostics; legacy search scans now contribute to the same lightweight host metrics as engine scans.
- Added the GM-only `/botbuff` command for applying and removing data-pack buffs without a selected client target, enabling reproducible stealth-state trials.
- Passed the isolated automated stealth milestone with the complete 1,732-test suite: 1,728 passed, 4 intentionally skipped, and 0 failed. Client-visible loss, release, and reacquisition remain a physical gate.

### Next combat pass

- Physically validate stealth loss, search, release, and reacquisition using a verified 1.2 stealth buff.
- Run mortal-combat cohorts at 1, 5, 10, 25, 50, and 100 bots with matched Idle controls.

## 0.1.0-rc.2 - 2026-08-29

### Release automation

- Made the hosted Windows test step run from the AAEmu root so .NET discovers AAEmu's Microsoft Testing Platform configuration.
- Replaced hard-coded Unix test-artifact paths with the operating system's temporary directory.
- Added the command-script compiler and vulnerable-package audit to the hosted release gate.
- No PlayerBots runtime behavior changed from `0.1.0-rc.1`.

## 0.1.0-rc.1 - 2026-08-29

### Standalone release

- Extracted PlayerBots into its own repository and history; AAEmu is now an explicit host dependency rather than bundled project content.
- Added PowerShell and Bash installers with compatibility, dirty-tree, patch, and migration conflict checks.
- Added conditional MSBuild imports so module source and tests remain physically owned by this repository while compiling with AAEmu.
- Added a machine-readable compatibility manifest and CI recipe for a clean AAEmu install/build/test.
- Preserved the former combined repository as integration history; it is not the module distribution format.

### Patch highlights

- Added connectionless PlayerBots backed by real, persistent AAEmu characters.
- Added Darkrunner, Primeval, Daggerspell, Cleric, Abolisher, Reaper, and Templar profiles with data-driven rotations.
- Added native-party control with follow, stay, attack, passive, tank, healer, and attacker roles.
- Added `/bot`, `/bots`, and `/bothelp` plus human GM commands for lifecycle, movement, combat, diagnostics, and resource measurement.

### Combat and movement

- Anchored melee, archer, caster, and healer behavior around archetype-appropriate ranges.
- Made Endless Arrows the Primeval's dominant filler; a retained controlled sample produced 16 of 21 offensive casts (76.19%).
- Removed legacy autoattack fallback from tested data rotations so ranged characters do not enter melee merely to autoattack.
- Added healer-recipient selection and movement toward Antithesis range.
- Added target-death recovery, invalid-target release, kill goals, authoritative kill credit, and return-to-follow behavior.
- Added stable low-cost variation to native-party follow positions without per-tick random sampling.
- Retained ArcheAge's native collision and direct-pursuit behavior. Pairwise repulsion and boss-orbit movement are not enabled.

### Operations and verification

- Added action history, blackboard, strategy, rotation, archetype, state, and live metrics commands.
- Added an isolated 0/10/50/100 measurement harness and graceful-shutdown evidence contracts.
- Added activity governing, scan TTL caches, timing histograms, process resource sampling, and recovery metrics.
- Physically exercised 100, 500, and 1,000 simultaneous bots with normal logout cleanup.
- Verified native-party relog/rebind, leader logout/relogin, kick/rejoin authorization, moving-owner combat priority, Cleric healing after relog, and post-combat regrouping.

### Known limitations

- Supported target is ArcheAge 1.2 `r208022`; 3.0 is a separate compatibility track.
- Direct movement is not navmesh navigation.
- Jump packets and vertical motion exist, but the 1.2 client does not display a convincing bot jump.
- No maximum supported population is claimed until a whole-server budget is approved.
