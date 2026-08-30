# AAEmu compatibility files

These patches are intentionally visible and reviewable. They are applied to an AAEmu checkout; no AAEmu-owned source is stored in this module repository.

## ArcheAge 1.2 host hooks

`aaemu-1.2-r208022-v2.patch` is required. It targets AAEmu descendants of `62e3eb1d87da01194802ac886cd500134facad28` and adds the conditional build imports plus lifecycle, service, party, duel, world-query, tick-metric, character, packet, command-API, and host-test hooks PlayerBots needs. Version 2 also registers the GM-only `/botbuff` development command in the host access table.

SHA-256: `5c174a63bbdf94e4e49421787558bf8224407d36748d560fdf9643f6c9c188c3`

The released `aaemu-1.2-r208022.patch` remains unchanged for `0.1.0-rc.2`; it is not the active Unreleased installer contract.

Use the installer rather than applying this patch manually; it validates lineage, applicability, dirty tracked files, and the migration.

## ArcheAge 3.0 alpha host hooks

`aaemu-3.0.4.2-r336598-alpha.patch` targets exact base `8c1c943bb2309eefffb9da2aa99a408d0acbb095` from NL0bP/AAEmu's `client_version/3.0_client_(2017_04_20)+` branch. It adds the older host-line equivalents of PlayerBots lifecycle, party, world-query, tick-metric, character, command, service-startup, and build hooks.

SHA-256: `3e7c792a1e77864617bde197ad89c7b7948d420ce12fb15f2a759e39a018acc6`

This track is compile-validated, not runtime-supported. The retained non-incremental Game and full-solution builds passed with zero errors, and the combined host/adapter unit suite passed 151/151. Install it only with the explicit experimental flag and an isolated matching-client environment. Runtime promotion requires matching `3.0.4.2 r336598` data, login/serializer acceptance, one-bot lifecycle, party/combat behavior, metrics, and clean shutdown recovery.

## Optional host dependency baseline

`aaemu-1.2-security-baseline.patch` changes only `Directory.Packages.props`:

- `Microsoft.Data.Sqlite` 10.0.9 → 10.0.11;
- `Testcontainers` 4.12.0 → 4.14.0;
- `Testcontainers.MySql` 4.12.0 → 4.14.0.

SHA-256: `64d0fb4ba543a86670a4e8d9fa5859548fe23217b842f4b1660852e4131371ae`

It removes the known high-severity transitive advisories present in the pinned AAEmu base under the retained validation environment. It is optional because the AAEmu host owner—not the PlayerBots installer—should decide dependency policy. Review it and current NuGet advisories before applying it.

## Porting to another AAEmu revision

Do not edit a released patch in place. Create a newly named compatibility patch, install the module in a fresh checkout of the new host revision, run the complete build/test/compiler-check gate, update the manifest, and record the new contract in the changelog.
