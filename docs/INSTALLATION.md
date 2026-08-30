# Installation Guide

This guide covers a clean ArcheAge PlayerBots installation and updating an existing one.

> **Important:** PlayerBots requires a documented AAEmu base and must be cloned at `modules/archeage-playerbots`. The installer checks both requirements before it changes the server.

## Contents

- [Requirements](#requirements)
- [Clean installation](#clean-installation)
- [Database setup](#database-setup)
- [Verify the installation](#verify-the-installation)
- [Experimental ArcheAge 3.0 track](#experimental-archeage-30-track)
- [Updating](#updating)
- [What the installer changes](#what-the-installer-changes)

## Requirements

| Track | AAEmu base | Status |
| --- | --- | --- |
| ArcheAge 1.2 `r208022` | `AAEmu/AAEmu` commit `62e3eb1d87da01194802ac886cd500134facad28` | Supported |
| ArcheAge 3.0.4.2 `r336598` | `NL0bP/AAEmu` commit `8c1c943bb2309eefffb9da2aa99a408d0acbb095` | Experimental; server startup only |

You also need:

- the .NET SDK selected by the AAEmu checkout;
- client and server data that match the selected ArcheAge version;
- an isolated or safely versioned Game database for the first installation.

Never mix 1.2 and 3.0 client data, compact databases, or Game databases.

## Clean installation

### 1. Prepare AAEmu

For a new supported 1.2 server:

```powershell
git clone https://github.com/AAEmu/AAEmu.git
Set-Location AAEmu
git switch -c playerbots-host 62e3eb1d87da01194802ac886cd500134facad28
```

An existing server checkout can also be used when it contains that tested base and has no tracked local changes.

### 2. Clone PlayerBots

From the AAEmu root:

```powershell
New-Item -ItemType Directory -Force modules | Out-Null
git clone https://github.com/NulightJens/ArcheAge-PlayerBots modules/archeage-playerbots
```

The final layout must be:

```text
AAEmu/
  AAEmu.Game/
  AAEmu.UnitTests/
  modules/
    archeage-playerbots/
```

### 3. Check and install

Windows:

```powershell
& .\modules\archeage-playerbots\scripts\Install-PlayerBots.ps1 -AAEmuRoot $PWD -CheckOnly
& .\modules\archeage-playerbots\scripts\Install-PlayerBots.ps1 -AAEmuRoot $PWD
dotnet build AAEmu.slnx --no-incremental
```

Linux or macOS:

```bash
./modules/archeage-playerbots/scripts/install-playerbots.sh "$PWD" --check-only
./modules/archeage-playerbots/scripts/install-playerbots.sh "$PWD"
dotnet build AAEmu.slnx --no-incremental
```

`CheckOnly` is read-only. It reports whether the server is ready for installation or already installed.

## Database setup

The installer places this migration in AAEmu's normal update directory:

```text
SQL/updates/2026-08-25_aaemu_game_bot_archetype_plans.sql
```

Start AAEmu through its normal database-update process so the migration is applied to the Game database. PlayerBots checks its schema at startup and reports a clear error when the migration is missing.

Use the same database backup and versioning practices you use for AAEmu itself.

## Verify the installation

Start Login and Game, then log in with a GM character. Choose an existing character that is currently offline:

```text
/bot
/addbot 2
/botstate 2 grind
/botstate 2 idle
/removebot 2
```

Replace `2` with the offline character's ID. A successful `/removebot` saves and logs out the character; it does not delete it.

Continue with [Configuration](CONFIGURATION.md) or the full [Command Guide](COMMANDS.md).

## Experimental ArcheAge 3.0 track

The 3.0 adapter is available for isolated development only. Matching assets and server startup have been validated, but client login, bot lifecycle, party behavior, combat, and population recovery are still awaiting acceptance.

Use the exact NL0bP/AAEmu base from the requirements table and opt in explicitly:

```powershell
& .\modules\archeage-playerbots\scripts\Install-PlayerBots.ps1 -AAEmuRoot $PWD -Track AAEmu30 -AllowExperimental -CheckOnly
& .\modules\archeage-playerbots\scripts\Install-PlayerBots.ps1 -AAEmuRoot $PWD -Track AAEmu30 -AllowExperimental
dotnet build AAEmu.sln --no-incremental
```

Do not use a live 1.2 database. Before starting the server, follow the [ArcheAge 3.0 acceptance runbook](AAEMU30-ACCEPTANCE.md) to verify the client, `game_pak`, compact databases, ports, and loopback command API.

## Updating

Update the module and validate the installed host before rebuilding:

```powershell
git -C .\modules\archeage-playerbots pull --ff-only
& .\modules\archeage-playerbots\scripts\Install-PlayerBots.ps1 -AAEmuRoot $PWD -CheckOnly
dotnet build AAEmu.slnx --no-incremental
```

When release notes introduce a new compatibility patch, review it before changing a server branch that already contains local AAEmu modifications.

## What the installer changes

PlayerBots stays in its own Git repository. The installer:

- applies the reviewed compatibility patch for the detected AAEmu track;
- adds conditional build imports and the small host hooks PlayerBots needs;
- copies one module migration into `SQL/updates/`.

It refuses an unknown AAEmu lineage, conflicting tracked host changes, a patch that does not apply cleanly, or a different file at the migration path.

The optional 1.2 dependency-security patch in `compatibility/` is not applied automatically. Server owners can review it and current NuGet advisories separately because dependency policy belongs to the host.

If installation does not complete, see [Troubleshooting](TROUBLESHOOTING.md).
