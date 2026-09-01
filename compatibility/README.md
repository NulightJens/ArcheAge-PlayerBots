# AAEmu compatibility files

This page is a maintainer reference for the versioned AAEmu host patches. Normal installations should use the [Installation Guide](../docs/INSTALLATION.md) and the provided installer rather than applying these files manually.

The patches are intentionally visible and reviewable. They are applied to an AAEmu checkout; no AAEmu-owned source is stored in this module repository.

## ArcheAge 1.2 host hooks

`aaemu-1.2-r208022-v3.patch` is required. It targets AAEmu descendants of `62e3eb1d87da01194802ac886cd500134facad28` and adds the conditional build imports plus lifecycle, service, party, duel, world-query, tick-metric, character, packet, command-API, and host-test hooks PlayerBots needs. Version 3 also registers the GM-only `/botquest` quest-development command alongside the version 2 class, gear, and kit integration. Login logs `Selected Login database schema: <database>` from the resolved `MySQLProvider.Database` value before its updater runs; the message contains no connection or credential fields.

SHA-256: `5b44353fef730367b6dfe29baaa0c5141374163f6616549b6724cead91d82ac3`

The released `aaemu-1.2-r208022.patch` and the version 2 patch remain unchanged; neither is the active Unreleased installer contract.

Use the installer rather than applying this patch manually; it validates lineage, applicability, dirty tracked files, and the migration.

## ArcheAge 3.0 alpha host hooks

`aaemu-3.0.4.2-r336598-alpha-v4.patch` targets exact base `8c1c943bb2309eefffb9da2aa99a408d0acbb095` from NL0bP/AAEmu's `client_version/3.0_client_(2017_04_20)+` branch. It adds the older host-line equivalents of PlayerBots lifecycle, party, world-query, tick-metric, character, command, service-startup, and build hooks. Version 4 also registers the GM-only `/botquest` command alongside version 3's loopback `@system` actor, kit integration, and public bot-equipment visibility.

SHA-256: `9c877d395b7f6dc7dc47dbf1bd4927af3a5d08392434c758cea364385a6ba15e`

The earlier alpha-v2 and alpha-v3 patches remain unchanged; neither is the active installer patch.

This track remains experimental, not runtime-supported. In addition to server-start acceptance, native account/character creation, login/world entry, and one-bot spawn/follow/combat/class/gear/quest paths have passed. A selected exact-NPC hunt completed three native kills and cleaned up automatically; native item acquisition, cross-region quest travel, corpse delivery, cleanup, and reward-mail fallback have also passed once. The clean standalone 3.0 adapter suite now passes 159/159. Install it only with the explicit experimental flag and an isolated matching-client environment. Runtime promotion still requires clean-start repeats, four-role party/combat behavior, scale/resource gates, and clean populated recovery.

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
