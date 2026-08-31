# Testing

## Automated release gate

Install into a clean compatible AAEmu checkout, then run:

```powershell
dotnet build AAEmu.slnx --no-incremental
dotnet test AAEmu.UnitTests\AAEmu.UnitTests.csproj --no-build
```

Also run `scripts/Install-PlayerBots.ps1 -CheckOnly` after installation to verify idempotent compatibility detection.

## Deterministic client observation

Use the read-only loopback client driver for repeatable process, window, and client-log lifecycle assertions:

```powershell
.\scripts\Test-ClientDriver.ps1
dotnet run --project tools\AAEmu.ClientDriver -- status --log "$env:USERPROFILE\Documents\AAClassic\ArcheAge.log"
```

The staged architecture and the boundary between the client driver and Computer Use are documented in [CLIENT-TEST-DRIVER.md](CLIENT-TEST-DRIVER.md).

To prove an already authenticated native character can enter a gameplay world, use the fail-closed Stage 4 runner. It requires an exact PID/HWND and corroborates the pre-existing character-server authorization plus fresh world-loading markers with the loopback Web API's offline-to-online character transition:

```powershell
.\scripts\Test-RealClientWorld.ps1 `
  -LauncherProfile <profile.json> `
  -EvidenceDirectory <new-evidence-directory> `
  -GameplayAssertionSpec <exact-gameplay-assertions.json> `
  -ProcessId <exact-pid> `
  -WindowHandle <exact-decimal-handle> `
  -ExpectedCharacterName <native-character> `
  -HostRoot <retained-aaemu-host>
```

## One-bot smoke test

1. Start Login and Game and log in one GM character.
2. Spawn one known offline bot with `/addbot <id>`.
3. Inspect `/botstate <id>`, `/botarchetype <id>`, and `/botrotation <id> show`.
4. Run one-kill grinding with `/botstate <id> grind 1`.
5. Confirm acquisition, movement, facing, legal casts, kill credit, target release, and Idle recovery.
6. Run `/removebot <id>` and confirm normal logout with no retained runtime.

## Selected native hunt gate

Use a fresh or still-completable quest with exactly one active `QuestActObjMonsterHunt`. Never reset or repeat a completed nonrepeatable quest just to reuse a fixture.

1. Inspect the quest and status, stage the bot within the isolated test runtime, and accept through the exact live starter. Use `/movebot ... teleport` only when fixture staging—not autonomous travel—is under test.
2. Verify `/botquest status` shows the native Progress step, one exact monster-hunt act, and its current objective count.
3. Use `/botquest nearby <id> <npcTemplateId>` and require at least one living, attackable candidate within the configured bounded radius and on the bot's navigation-height surface.
4. Reset metrics and run `/botquest hunt <id> <questId>`. Do not issue manual attack or quest-progress commands during the sample.
5. Require the live objective to advance through normal kill credit, a new exact target to be selected after each incomplete kill, and the executor to stop at its derived remaining goal.
6. Require `/botstate` to return to automatic `Idle` with no target and `/botmetrics snapshot` to show observed kills equal credited kills with zero skipped ticks, runtime overlaps, and tick errors.
7. Report at the exact live reporter and require the quest lifecycle to become completed.

Also retain one fail-closed vertical case when available. A cave or stacked-world NPC that is close in planar distance but disagrees with the bot's navigation-height surface must be rejected before combat; do not count that rejection as route completion.

## Selected native sphere-travel gate

Use a fresh or still-completable quest with exactly one active static `QuestActObjSphere`. Never reset or repeat a completed nonrepeatable quest just to reuse a fixture.

1. Inspect and accept the quest through its exact native starter, then require `/botquest status` to show one active sphere act at `0/1` and exactly one same-world destination.
2. Stage the bot only for fixture setup, outside the sphere but no more than 100 meters from its heightmap-projected center. Keep a real player within the configured activity radius so AAEmu evaluates the region normally.
3. Run `/botquest travel <id> <questId>` once. Require the command to report the exact component, sphere, radius, true 3D distance, surface offset, and destination.
4. Do not issue movement or quest-progress commands while it runs. Require ordinary bot movement to enter the sphere and `/botquest status` to show native progress, Ready, or completed lifecycle.
5. Retain the server's `QuestActObjSphere.FinalizeAction` and queued evaluation sequence. For an auto-complete quest, also require native auto-complete finalization and normal quest removal.
6. Retain fail-closed tests for mixed/multi-act steps, completed objectives, NPC-centered spheres, zero/multiple same-world destinations, non-finite geometry, destination distance above 100 meters, and a projected surface outside the sphere.

This gate proves one local static destination, not obstacle-aware routing. A long-range, dynamic, ambiguous, or vertically unsafe destination must fail before movement and is not a route-completion result.

## Four-role party

Use one Darkrunner, Primeval, Daggerspell, and Cleric.

1. Spawn all four and invite them through the native party system.
2. Assign roles with `/botcontrol` and issue `follow`.
3. Begin moving and enter combat before stopping; combat must override follow movement.
4. Confirm melee holds short-range attacks until legal, Primeval stays near Endless Arrows range, Daggerspell anchors around Fireball range, and Cleric repositions toward an injured recipient.
5. Kill the target and verify target release plus regrouping.
6. Relog one bot and verify party authorization and rotation attachment.

## Controlled target

```text
/spawnpassive 7511 3
/botattackobject all <npcObjId>
/botattackobject status <npcObjId>
```

`7511` is the level-40 Elite Training Scarecrow in the verified 1.2 data. Treat template IDs as data-pack-specific. Passive targets are useful for rotation and positioning; natural hostile camps are required for chase, reacquisition, mortality, and repeated-kill tests.

## Moving-owner Cleric gate

This checks that follow movement does not starve party support while the human leader is moving.

1. Use a native party owned by the logged-in GM character. Put every bot except the Cleric and one recipient in Idle.
2. Spawn a passive `7511` at least 30 m from unrelated hostiles and note its object ID.
3. Put the Cleric in Idle, restore health and mana, then damage the recipient below the configured support threshold.
4. Reset metrics, start a contained Cleric attack on the passive target, and immediately re-issue native follow:

   ```text
   /botmetrics reset
   /botattackobject <clericId> <npcObjId>
   /botcontrol <clericId> follow
   ```

5. Move the GM character continuously for at least 10 seconds. Sample the leader position plus `/botdebug <clericId>` and `/botdebug <recipientId>` throughout the window.
6. Require the Cleric to preserve the GM character as `FollowTarget`, enter the combat brain, reposition toward the recipient, successfully complete Antithesis before leader movement ends, cross the support threshold, and resume leader-directed movement.
7. Snapshot `/botmetrics`; require zero cast failures, skipped ticks, runtime overlaps, and tick errors. Record scans and path requests instead of assuming they are zero.
8. Stop with `/botstate <clericId> idle`, restore all fixtures, and verify no target, follow target, or movement destination remains.

The accepted reference moved the leader 34.66 m over 10.038 seconds. The Cleric retained follow in 37/37 samples, moved 19.29 m toward the recipient, raised it from 53.23% to 91.13%, and resumed movement before the input window ended. This is a behavior gate, not a whole-server capacity claim.

## Brain reactivation regression

This checks the low-cost lifecycle boundary where an inactive duel is allowed to shed its combat task, but later player work must wake it again.

1. Put two nearby fixtures in Idle and confirm `/botdebug <id>` reports `IsActive: False`.
2. Run `/botduel <id1> <id2>` and let it end normally.
3. Confirm both participants report `Combat task running: False`.
4. Restore health and mana, spawn a passive `7511`, reset metrics, and run `/botattackobject all <npcObjId>`.
5. Within one second, confirm all selected bots report `Combat task running: True`; after the sample, require a successful cast from every intended role plus zero tick errors and runtime overlaps.
6. Repeat the detached setup, then issue `/botfollow <id> <GM-character-name> 3 auto 1.5`; confirm the bot is moving toward the named character and its combat task is running again.

Do not replace `<GM-character-name>` with a repository-specific character. Use the GM character logged into the test server.

## Mana lanes

- Controlled rotation: use `/heal <exact-character-name>` before and after the observation window, never during it.
- Natural sustainability: do not heal; capture low-mana time, last legal cast, rest entry, recovery, and re-entry.

Never report a controlled-heal case as proof of natural sustainability.

## Stealth loss and reacquisition

Use a verified stealth buff ID from the active client data; never assume a template ID transfers between 1.2 and 3.0.

1. Put one attacker and one target in an isolated duel or contained hostile fixture.
2. Reset metrics and capture `/botdebug <attackerId>` before loss.
3. Apply stealth with `/botbuff <targetId> <stealthBuffId>` while the attacker has a continuously usable rotation.
4. Require the attacker to enter Searching, clear its live target, retain a last-known position, and increment the search-scan metric without a tick error or runtime overlap.
5. Remove stealth with `/botbuff <targetId> -<stealthBuffId>` while the target remains inside the bounded search area.
6. Require reacquisition and legal combat re-entry. Repeat with the target outside the search timeout/radius and require clean release instead.
7. Stop both bots in Idle and verify that target, movement destination, and retained runtime work are cleared.

An automated trigger/search/metrics pass is necessary but does not substitute for client-visible loss and reacquisition evidence.

## Deterministic client image assertions

Use `AAEmu.ClientDriver assert-image` for stable screenshot anchors. The command compares exact top-down RGB region hashes and exact BMP templates from a strict JSON spec; it deliberately has no fuzzy tolerance or OCR fallback. A mismatch returns structured evidence with exit code `3`, while an invalid or ambiguous spec fails closed. See `docs/CLIENT-TEST-DRIVER.md` for the schema and safety bounds.

## Population/resource gate

The harness under `scripts/scale/` measures exact 0/10/50/100 populations, process CPU/memory, allocation/GC, server and bot-host latency, scans, casts, cadence, lifecycle, and recovery. Run it only against task-owned runtime paths and new versioned test databases.

Collect an empty-server baseline first. Approve explicit whole-server limits, then repeat. Without an approved policy the verdict is `INCOMPLETE`, not a capacity pass.

## Evidence record

Retain source/build identity, client/server version, data provenance, bot IDs and archetypes, exact population, start position, target object/template, duration, stop condition, cast/action counts, errors, resource snapshots, cleanup result, and client presentation separately from server truth.

Keep raw logs, videos, snapshots, and databases outside Git; commit stable scripts, anonymized fixtures, schemas, and summarized results only.

For the 3.0 alpha, run `scripts/Test-AAEmu30Runtime.ps1` after each clean startup. Keep its zero-bot result beside the asset-provenance record, then use the same metrics contract for one-bot lifecycle, four-role behavior, and scale gates. Server-start success is a prerequisite, not a substitute for those physical cases.
