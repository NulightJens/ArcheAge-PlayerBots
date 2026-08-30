# Installation

## Requirements

- A clean AAEmu checkout containing one tested base:
  - supported 1.2: `62e3eb1d87da01194802ac886cd500134facad28`;
  - compile-validated 3.0 alpha: `8c1c943bb2309eefffb9da2aa99a408d0acbb095` from NL0bP/AAEmu's `client_version/3.0_client_(2017_04_20)+` branch.
- The .NET SDK selected by AAEmu's `global.json`.
- Matching server and client data for the selected track. Never mix the 1.2 and 3.0 data sets.
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

### Compile-validated 3.0 alpha

The 3.0 adapter is available for isolated development, but it is not a supported runtime release yet. The explicit flag prevents accidental installation into an unverified production environment:

```powershell
& .\modules\archeage-playerbots\scripts\Install-PlayerBots.ps1 -AAEmuRoot $PWD -Track AAEmu30 -AllowExperimental -CheckOnly
& .\modules\archeage-playerbots\scripts\Install-PlayerBots.ps1 -AAEmuRoot $PWD -Track AAEmu30 -AllowExperimental
dotnet build AAEmu.sln --no-incremental
dotnet test .\AAEmu.UnitTests\AAEmu.UnitTests.csproj --no-build
```

Runtime acceptance additionally requires the matching `3.0.4.2 r336598` `game_pak`, `compact.sqlite3`, and `compact.server.table.sqlite3`. The retained build passed with zero errors and the combined host/adapter unit suite passed 151/151, but login, serializer, one-bot lifecycle, party, combat, and shutdown recovery remain blocked until those assets are available.

## What installation changes

The module remains a separate Git repository. Installation selects a reviewed compatibility patch for the detected track and copies one migration into `SQL/updates/`. The 1.2 patch updates 26 host files; the 3.0 alpha patch updates 21 host files. The patches:

- imports the module's Game and test MSBuild targets;
- registers bot services and startup/shutdown lifecycle;
- supplies party, duel, world-query, tick-metric, character, packet, and command-API hooks;
- adapts host tests to the new injected dependencies.

No AAEmu-owned source is copied into this repository, and module source is not copied into AAEmu project directories.

## Optional tested host security baseline

The pinned 1.2 upstream base resolves `SQLitePCLRaw.lib.e_sqlite3` 2.1.11 and `SSH.NET` 2025.1.0, which NuGet currently flags with high-severity advisories. PlayerBots does not silently take ownership of AAEmu dependency policy. Operators can review and explicitly apply the tested 1.2 package-only baseline:

```powershell
git apply --check .\modules\archeage-playerbots\compatibility\aaemu-1.2-security-baseline.patch
git apply .\modules\archeage-playerbots\compatibility\aaemu-1.2-security-baseline.patch
dotnet restore AAEmu.slnx
dotnet list AAEmu.slnx package --vulnerable --include-transitive
```

The retained clean validation reported no vulnerable packages after this optional patch. Re-evaluate advisories at install time because package security data changes.

The untouched 3.0 baseline independently resolves `SQLitePCLRaw.lib.e_sqlite3` 2.1.11 and currently reports `GHSA-2m69-gcr7-jv3q`. No 3.0 dependency upgrade is bundled in this alpha adapter; that host policy must be reviewed separately.

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
- **3.0 assets missing:** stop before runtime startup; obtain the exact `3.0.4.2 r336598` data set and stage it with a versioned test database.
- **3.0 serializer mismatch:** confirm the launcher/client, `game_pak`, compact databases, and emulator revision all belong to the same 3.0 lineage.
