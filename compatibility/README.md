# AAEmu compatibility

PlayerBots stays in its own repository. Small versioned patches expose the AAEmu hooks that a module cannot provide by itself. The installer validates the host and applies the correct patch.

## Supported track

`aaemu-1.2-r208022-v4.patch` targets AAEmu commit `62e3eb1d87da01194802ac886cd500134facad28`.

It adds the build import and the lifecycle, identity, quest, road-data, command, service, and test seams required by the 1.2 module.

## Experimental track

`aaemu-3.0.4.2-r336598-alpha-v4.patch` targets commit `8c1c943bb2309eefffb9da2aa99a408d0acbb095` from NL0bP/AAEmu's 3.0 client branch.

The 3.0 adapter is experimental. Alpha.6 quest and road automation is disabled there because the host does not expose the required APIs.

It is for isolated compatibility testing. It is not the supported gameplay target and does not include the 1.2-only bot identity factory.

## Other files

Older patches remain for released-version provenance. `aaemu-1.2-security-baseline.patch` is an optional host dependency update; PlayerBots does not apply it automatically.

Do not edit a released patch in place. Add a new version, verify it in a fresh pinned host checkout, and update `playerbots.module.json` with its SHA-256.
