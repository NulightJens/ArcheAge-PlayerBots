# AAEmu compatibility files

These patches are intentionally visible and reviewable. They are applied to an AAEmu checkout; no AAEmu-owned source is stored in this module repository.

## Required host hooks

`aaemu-1.2-r208022-v2.patch` is required. It targets AAEmu descendants of `62e3eb1d87da01194802ac886cd500134facad28` and adds the conditional build imports plus lifecycle, service, party, duel, world-query, tick-metric, character, packet, command-API, and host-test hooks PlayerBots needs. Version 2 also registers the GM-only `/botbuff` development command in the host access table.

SHA-256: `5c174a63bbdf94e4e49421787558bf8224407d36748d560fdf9643f6c9c188c3`

The released `aaemu-1.2-r208022.patch` remains unchanged for `0.1.0-rc.2`; it is not the active Unreleased installer contract.

Use the installer rather than applying this patch manually; it validates lineage, applicability, dirty tracked files, and the migration.

## Optional host dependency baseline

`aaemu-1.2-security-baseline.patch` changes only `Directory.Packages.props`:

- `Microsoft.Data.Sqlite` 10.0.9 → 10.0.11;
- `Testcontainers` 4.12.0 → 4.14.0;
- `Testcontainers.MySql` 4.12.0 → 4.14.0.

SHA-256: `64d0fb4ba543a86670a4e8d9fa5859548fe23217b842f4b1660852e4131371ae`

It removes the known high-severity transitive advisories present in the pinned AAEmu base under the retained validation environment. It is optional because the AAEmu host owner—not the PlayerBots installer—should decide dependency policy. Review it and current NuGet advisories before applying it.

## Porting to another AAEmu revision

Do not edit a released patch in place. Create a newly named compatibility patch, install the module in a fresh checkout of the new host revision, run the complete build/test/compiler-check gate, update the manifest, and record the new contract in the changelog.
