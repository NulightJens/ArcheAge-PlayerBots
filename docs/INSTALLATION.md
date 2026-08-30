# Installation

## Requirements

- A clean AAEmu checkout containing the tested 1.2 base commit `62e3eb1d87da01194802ac886cd500134facad28`.
- The .NET SDK selected by AAEmu's `global.json`.
- A working AAEmu 1.2 `r208022` server and client data set.
- A versioned, isolated database for first installation and testing.

## Install beside AAEmu

Clone this repository at the exact module path:

```text
AAEmu/
  AAEmu.Game/
  AAEmu.UnitTests/
  modules/
    archeage-playerbots/    <- this repository
```

Windows:

```powershell
Set-Location C:\path\to\AAEmu
New-Item -ItemType Directory -Force modules | Out-Null
git clone https://github.com/NulightJens/ArcheAge-PlayerBots modules/archeage-playerbots
& .\modules\archeage-playerbots\scripts\Install-PlayerBots.ps1 -AAEmuRoot $PWD
dotnet build AAEmu.slnx --no-incremental
```

Use `-CheckOnly` to validate compatibility without changing the host:

```powershell
& .\modules\archeage-playerbots\scripts\Install-PlayerBots.ps1 -AAEmuRoot $PWD -CheckOnly
```

Linux/macOS equivalents are in `scripts/install-playerbots.sh` and use `--check-only`.

## What installation changes

The module remains a separate Git repository. Installation applies `compatibility/aaemu-1.2-r208022-v2.patch` to 26 existing AAEmu source/project/test files and copies one migration into `SQL/updates/`. The patch:

- imports the module's Game and test MSBuild targets;
- registers bot services and startup/shutdown lifecycle;
- supplies party, duel, world-query, tick-metric, character, packet, and command-API hooks;
- adapts host tests to the new injected dependencies.

No AAEmu-owned source is copied into this repository, and module source is not copied into AAEmu project directories.

## Optional tested host security baseline

The pinned upstream base resolves `SQLitePCLRaw.lib.e_sqlite3` 2.1.11 and `SSH.NET` 2025.1.0, which NuGet currently flags with high-severity advisories. PlayerBots does not silently take ownership of AAEmu dependency policy. Operators can review and explicitly apply the tested package-only baseline:

```powershell
git apply --check .\modules\archeage-playerbots\compatibility\aaemu-1.2-security-baseline.patch
git apply .\modules\archeage-playerbots\compatibility\aaemu-1.2-security-baseline.patch
dotnet restore AAEmu.slnx
dotnet list AAEmu.slnx package --vulnerable --include-transitive
```

The retained clean validation reported no vulnerable packages after this optional patch. Re-evaluate advisories at install time because package security data changes.

## Database

AAEmu's updater must apply `2026-08-25_aaemu_game_bot_archetype_plans.sql` to the test or deployment Game database. The runtime also checks the bot schema during startup. Back up or version databases according to your normal AAEmu operating procedure.

## Upgrade

Commit the AAEmu host integration changes and the migration file in your own server branch. Then update the module repository and rebuild. Do not blindly reapply a newer compatibility patch over local host changes: run the installer's check-only mode first and review its release notes.

## Troubleshooting

- **Wrong module path:** clone exactly at `modules/archeage-playerbots`.
- **Base commit missing:** fetch AAEmu history, then retry.
- **Patch does not apply:** use the supported base or port/review the host hooks against your AAEmu revision.
- **Tracked local changes:** commit them in your AAEmu server branch before installation.
- **Different migration exists:** compare both files manually; the installer intentionally refuses to overwrite it.
- **Commands not discovered:** confirm both MSBuild imports are present, then perform a non-incremental rebuild.
