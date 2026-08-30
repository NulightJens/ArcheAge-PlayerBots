# Troubleshooting

This page covers the most common ArcheAge PlayerBots installation and gameplay problems.

## Installation and build

### `Clone this repository at ... modules\archeage-playerbots`

**Cause:** The module is not at the exact path required by its build imports.

**Fix:** Clone or place the repository here:

```text
<AAEmu root>/modules/archeage-playerbots
```

Run the installer from the AAEmu root.

### `Could not identify exactly one supported AAEmu lineage`

**Cause:** The AAEmu checkout does not contain a tested base commit, or it contains history from more than one supported track.

**Fix:** Use the AAEmu repository and base listed in the [Installation Guide](INSTALLATION.md). Fetch the host repository's history if the correct base exists upstream but is missing locally. You can then pass `-Track AAEmu12` or `-Track AAEmu30` only after confirming the checkout is the intended version.

### `The compatibility patch does not apply cleanly`

**Cause:** The AAEmu source differs from the tested base or already contains conflicting host changes.

**Fix:** Start from the documented base or review and port the compatibility patch in a separate server branch. Do not force-apply the patch.

### `AAEmu has tracked local changes`

**Cause:** The installer found tracked changes before applying the host patch.

**Fix:** Commit the intended AAEmu changes to your server branch, then run `-CheckOnly` again. The installer refuses to mix an unreviewed patch with unrelated working changes.

### `A different migration already exists`

**Cause:** `SQL/updates/2026-08-25_aaemu_game_bot_archetype_plans.sql` exists but does not match the module migration.

**Fix:** Compare the two files and keep the version required by your installed module. The installer will not overwrite a different migration.

### Build succeeds but PlayerBots commands do not exist

**Cause:** The module targets were not imported into AAEmu or the server is running an older build.

**Fix:**

1. Confirm the module is at `modules/archeage-playerbots`.
2. Run the installer in check-only mode; it should report `state: installed`.
3. Rebuild without incremental output:

   ```powershell
   dotnet build AAEmu.slnx --no-incremental
   ```

4. Start the newly built Game executable and run `/bot`.

## Database

### PlayerBots schema or table is missing

**Cause:** AAEmu did not apply the module migration to the Game database.

**Fix:** Confirm this file exists in the host checkout:

```text
SQL/updates/2026-08-25_aaemu_game_bot_archetype_plans.sql
```

Run AAEmu's normal database updater against the intended Game database, then restart Game. Do not point a 3.0 test server at a 1.2 or live database.

## Configuration

### Changes to `BotConfig.json` have no effect

**Cause:** You may have edited the module source template instead of the file beside the running Game executable, or the file was not reloaded.

**Fix:** Edit `Configurations/BotConfig.json` in the runtime directory and run:

```text
/reloadbotconfig
```

Check the Game log for a parse warning. Invalid JSON leaves the previous valid configuration active.

### Startup bots do not appear

**Cause:** A configured character ID is invalid, already online, or cannot be loaded.

**Fix:** Confirm every ID in `AutoSpawnCharacterIds` belongs to an existing offline character. Search the Game log for `BOT ev=autospawn`; each ID has its own result.

## Bot behavior

### `/addbot` cannot load the character

**Cause:** The ID does not exist, the character is already online, or the character is already active as a bot.

**Fix:** Use the ID of an existing offline character. Stop an active bot with `/removebot <id>` before logging it in normally.

### A bot appears but does not fight

**Fix:** Check the bot in this order:

```text
/botstate 2
/botarchetype 2
/botrotation 2 show
/botdebug 2
/botstate 2 grind
```

Replace `2` with the bot ID. Confirm the bot has a valid archetype and rotation, is alive, and is close enough to an appropriate hostile target.

### Party orders are rejected

**Cause:** `/botcontrol` follows live ArcheAge party ownership. A player cannot control an unrelated bot by guessing its ID.

**Fix:** Invite the bot through the normal party UI, make sure the controlling character owns the party relationship, then retry the role or order.

### A bot takes a poor route or crosses obstructed terrain

**Cause:** Current movement uses direct pursuit and native collision, not navmesh pathfinding.

**Fix:** Move the group to open terrain or stop and reissue the order. Obstacle jump settings are experimental and do not provide full pathfinding.

## ArcheAge 3.0

### The 3.0 installer asks for `-AllowExperimental`

**Cause:** The 3.0 track has passed asset and server-start validation, but gameplay acceptance is incomplete.

**Fix:** Use the supported 1.2 track for normal servers. For isolated 3.0 development, follow the [3.0 acceptance runbook](AAEMU30-ACCEPTANCE.md) and opt in explicitly.

### The 3.0 client cannot log in or reports serializer/data errors

**Cause:** The launcher, client, `game_pak`, compact databases, or AAEmu revision may not belong to the same `3.0.4.2 r336598` lineage.

**Fix:** Stop the test and verify every asset with the provided provenance preflight. Server startup alone does not prove client compatibility.

## Local command API safety

The synthetic `@system` actor has administrator access and no user authentication. Keep AAEmu's command Web API bound to `127.0.0.1`; never expose its port publicly.

If a reproducible problem remains, open a GitHub issue with the module commit, AAEmu commit, ArcheAge track, command used, relevant log lines, and the smallest reproduction you can provide. Do not post credentials, database contents, or private player data.
