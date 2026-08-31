# Shareable preview

ArcheAge PlayerBots `0.2.0-alpha.4` is suitable for source-level preview testing on the exact AAEmu bases documented below. It is not a drop-in DLL, does not include ArcheAge client/server data, and does not make the experimental 3.0 track production-ready.

## Current validation

| Track | Installer | Automated gate | Runtime status |
| --- | --- | --- | --- |
| ArcheAge 1.2 `r208022` | Installed-state check passed | 1,759 passed plus 4 intentional legacy skips | Supported host track; repeat gameplay validation is still recommended for every server |
| ArcheAge 3.0.4.2 `r336598` | Installed-state check passed with explicit experimental opt-in | 159/159 adapter tests | Isolated testing only; login, lifecycle, class/gear, combat, exact-NPC questing, a native selected-hunt loop, item acquisition, corpse delivery, and reward-mail fallback have each passed, while four-role and scale/recovery gates remain open |

The latest 3.0 physical quest proof accepted quest 620, derived exact Plains Razorbeak template 7781 and a three-kill goal from the live native objective, selected and killed three matching targets, advanced 0/3 to 3/3, cleaned up to automatic Idle, and reported at the exact NPC. Earlier retained proof covers native item acquisition, corpse delivery, cross-region movement, cleanup, and full-bag reward-mail fallback.

## Build an immutable preview archive

Run this from any clean PlayerBots worktree:

```powershell
& .\scripts\New-PlayerBotsPreview.ps1 -OutputDirectory C:\playerbots-preview
```

The packager refuses a dirty worktree and existing output names. It resolves one exact Git commit, verifies every declared compatibility-patch and migration hash, creates a Git archive, checks required entries, rejects Git internals, and writes both a JSON provenance manifest and SHA-256 sidecar.

## Install from the archive

Extract the archive so the AAEmu checkout contains this exact path:

```text
AAEmu/
  modules/
    archeage-playerbots/
      playerbots.module.json
```

Then validate before changing the host:

```powershell
& .\modules\archeage-playerbots\scripts\Install-PlayerBots.ps1 -AAEmuRoot $PWD -CheckOnly
```

For the experimental 3.0 track, use the exact pinned 3.0 base and add `-Track AAEmu30 -AllowExperimental`. Follow the full [Installation Guide](INSTALLATION.md) and [3.0 acceptance runbook](AAEMU30-ACCEPTANCE.md); never point it at a live or 1.2 database.

## Preview boundaries

- PlayerBots controls existing offline characters selected by an operator; it does not create accounts or a random world population.
- Quest commands are bounded development slices. One selected exact-NPC hunt can acquire and repeat nearby targets, but autonomous quest selection, route planning, mixed objectives, and objective chaining remain out of scope.
- Movement uses direct coordinates and native collision, not a navmesh. Cross-region membership is fixed, but obstacles can still produce poor paths.
- Do not distribute test databases, matching client data, runtime logs, credentials, or retained local evidence with the source archive.
- Verify the archive SHA-256 against its `.sha256` file and provenance manifest before sharing or installing it.
