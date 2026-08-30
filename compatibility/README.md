# AAEmu compatibility files

This page is a maintainer reference for the versioned AAEmu host patches. Normal installations should use the [Installation Guide](../docs/INSTALLATION.md) and the provided installer rather than applying these files manually.

The patches are intentionally visible and reviewable. They are applied to an AAEmu checkout; no AAEmu-owned source is stored in this module repository.

## ArcheAge 1.2 host hooks

`aaemu-1.2-r208022-v2.patch` is required. It targets AAEmu descendants of `62e3eb1d87da01194802ac886cd500134facad28` and adds the conditional build imports plus lifecycle, service, party, duel, world-query, tick-metric, character, packet, command-API, and host-test hooks PlayerBots needs. Version 2 also registers the GM-only development commands and connects the host `/kit` command to PlayerBots equipment evaluation.

SHA-256: `edabf16963d53d58ff6cb1244611b09ed3589d94b1e4825d60494a1bd67b4802`

The released `aaemu-1.2-r208022.patch` remains unchanged for `0.1.0-rc.2`; it is not the active Unreleased installer contract.

Use the installer rather than applying this patch manually; it validates lineage, applicability, dirty tracked files, and the migration.

## ArcheAge 3.0 alpha host hooks

`aaemu-3.0.4.2-r336598-alpha-v3.patch` targets exact base `8c1c943bb2309eefffb9da2aa99a408d0acbb095` from NL0bP/AAEmu's `client_version/3.0_client_(2017_04_20)+` branch. It adds the older host-line equivalents of PlayerBots lifecycle, party, world-query, tick-metric, character, command, service-startup, and build hooks. Version 3 also gives the loopback Web API its connectionless `@system` actor, bypasses account lookup only for that actor type, connects `/kit` to PlayerBots equipment evaluation, and publishes every bot's equipment visibility to newly nearby clients.

SHA-256: `7b3e9d8872cf708522631a11bb051bd9976c31d4f934eee0c9dfc1e90eec633c`

The earlier `aaemu-3.0.4.2-r336598-alpha-v2.patch` remains unchanged as the pre-equipment-visibility contract; it is not the active installer patch.

This track remains experimental, not runtime-supported. In addition to server-start acceptance, native account/character creation, login/world entry, and one-bot spawn/follow/combat/class/gear paths have passed once. The 3.0 suite now passes 157/157. Install it only with the explicit experimental flag and an isolated matching-client environment. Runtime promotion still requires clean-start repeats, four-role party/combat behavior, scale/resource gates, and clean populated recovery.

The earlier `aaemu-3.0.4.2-r336598-alpha.patch` remains unchanged as the compile-only alpha contract; it is not the active installer patch.

## Optional host dependency baseline

`aaemu-1.2-security-baseline.patch` changes only `Directory.Packages.props`:

- `Microsoft.Data.Sqlite` 10.0.9 → 10.0.11;
- `Testcontainers` 4.12.0 → 4.14.0;
- `Testcontainers.MySql` 4.12.0 → 4.14.0.

SHA-256: `64d0fb4ba543a86670a4e8d9fa5859548fe23217b842f4b1660852e4131371ae`

It removes the known high-severity transitive advisories present in the pinned AAEmu base under the retained validation environment. It is optional because the AAEmu host owner—not the PlayerBots installer—should decide dependency policy. Review it and current NuGet advisories before applying it.

## Porting to another AAEmu revision

Do not edit a released patch in place. Create a newly named compatibility patch, install the module in a fresh checkout of the new host revision, run the complete build/test/compiler-check gate, update the manifest, and record the new contract in the changelog.
