# Shareable preview

ArcheAge PlayerBots `0.2.0-alpha.6` is suitable for source-level preview testing on the exact AAEmu bases documented below. It is not a drop-in DLL, does not include ArcheAge client/server data, and does not make the experimental 3.0 track production-ready.

## Current validation

| Track | Installer | Automated gate | Runtime status |
| --- | --- | --- | --- |
| ArcheAge 1.2 `r208022` | Installed-state check passed | 2,000 passed plus 4 intentional legacy skips | Supported host track; one fresh Nuian completed seven native starter quests in the retained client-witnessed alpha.6 development run |
| ArcheAge 3.0.4.2 `r336598` | Installed-state check passed with explicit experimental opt-in | 160/160 adapter tests | Isolated testing only; login, lifecycle, class/gear, combat, exact-NPC questing, native selected-hunt and bounded sphere-travel loops, item acquisition, corpse delivery, and reward-mail fallback have each passed, while four-role and scale/recovery gates remain open |

The latest 3.0 physical quest proof accepted quest 312 at Apothecary Nestelle, confirmed its live sphere objective at 0/1, and ran from a staged point 95.4 meters away to one exact 30-meter same-world sphere. AAEmu's native enter-sphere event finalized the objective and auto-completed the quest without fabricated progress. Earlier retained proof covers a three-kill native hunt, item acquisition, corpse delivery, cross-region movement, cleanup, and full-bag reward-mail fallback.

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

- PlayerBots controls existing offline characters and can create a character only under an explicitly configured, dedicated server-owned bot account; it does not create accounts or a random world population.
- Opt-in quest autonomy supports nearby NPC and doodad intake plus one active native monster-hunt or item-gather objective at a time. Unsupported or ambiguous objective shapes suspend safely.
- Quest markers and transfer roads provide a lightweight long-range route layer, while BAI/direct local traversal handles final approaches. This is not a complete navmesh: cliffs, obstacles, and sparse road data can still produce poor paths.
- Do not distribute test databases, matching client data, runtime logs, credentials, or retained local evidence with the source archive.
- Verify the archive SHA-256 against its `.sha256` file and provenance manifest before sharing or installing it.
