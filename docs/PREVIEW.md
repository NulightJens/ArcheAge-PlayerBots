# Shareable preview

Version `0.2.0-alpha.6` targets AAEmu 1.2 commit `62e3eb1d87da01194802ac886cd500134facad28`. The 3.0 adapter is experimental.

## Build an archive

Run from a clean PlayerBots worktree:

```powershell
& .\scripts\New-PlayerBotsPreview.ps1 -OutputDirectory C:\playerbots-preview
```

The packager resolves one commit, verifies patch and migration hashes, and writes a ZIP, JSON manifest, and SHA-256 file. The ZIP contains product source, public documentation, installers, active compatibility patches, and the live monitor. It excludes tests, internal operations, evidence, and development harnesses.

## Install it

Extract the module to:

```text
AAEmu/modules/archeage-playerbots
```

Then run:

```powershell
& .\modules\archeage-playerbots\scripts\Install-PlayerBots.ps1 -AAEmuRoot $PWD -CheckOnly
& .\modules\archeage-playerbots\scripts\Install-PlayerBots.ps1 -AAEmuRoot $PWD
dotnet build AAEmu.slnx --no-incremental
```

For 3.0, use its pinned host and add `-Track AAEmu30 -AllowExperimental`.

The archive contains no AAEmu source, client data, databases, credentials, runtime logs, or local test evidence. Verify its checksum before sharing it.
