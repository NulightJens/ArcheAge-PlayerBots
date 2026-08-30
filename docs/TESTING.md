# Testing

## Automated release gate

Install into a clean compatible AAEmu checkout, then run:

```powershell
dotnet build AAEmu.slnx --no-incremental
dotnet test AAEmu.UnitTests\AAEmu.UnitTests.csproj --no-build
```

Also run `scripts/Install-PlayerBots.ps1 -CheckOnly` after installation to verify idempotent compatibility detection.

## One-bot smoke test

1. Start Login and Game and log in one GM character.
2. Spawn one known offline bot with `/addbot <id>`.
3. Inspect `/botstate <id>`, `/botarchetype <id>`, and `/botrotation <id> show`.
4. Run one-kill grinding with `/botstate <id> grind 1`.
5. Confirm acquisition, movement, facing, legal casts, kill credit, target release, and Idle recovery.
6. Run `/removebot <id>` and confirm normal logout with no retained runtime.

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

## Population/resource gate

The harness under `scripts/scale/` measures exact 0/10/50/100 populations, process CPU/memory, allocation/GC, server and bot-host latency, scans, casts, cadence, lifecycle, and recovery. Run it only against task-owned runtime paths and new versioned test databases.

Collect an empty-server baseline first. Approve explicit whole-server limits, then repeat. Without an approved policy the verdict is `INCOMPLETE`, not a capacity pass.

## Evidence record

Retain source/build identity, client/server version, data provenance, bot IDs and archetypes, exact population, start position, target object/template, duration, stop condition, cast/action counts, errors, resource snapshots, cleanup result, and client presentation separately from server truth.

Keep raw logs, videos, snapshots, and databases outside Git; commit stable scripts, anonymized fixtures, schemas, and summarized results only.
