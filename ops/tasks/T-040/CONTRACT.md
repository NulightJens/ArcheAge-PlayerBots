# T-040 contract

## Outcome

PlayerBots has a stable, versioned roster identity model and persistence boundary suitable for later residency, ownership, group, and governance relationships.

## Pass

- Identity is anchored to the authoritative AAEmu character ID and rejects duplicates.
- The roster retains enabled state, profile, home zone, desired life state, and schema version.
- Store tests prove create/read/update and restart-style round trips without relying on dictionary iteration order.
- Unknown schema versions and invalid foreign identities fail closed.
- A forward-only SQL migration may be supplied; tests must not destructively reset a database.

## Non-goals

- Spawning/despawning bots.
- Population density policy.
- Guild, election, property, crime, or economy behavior.
- Editing shared BotManager, BotHost, BotConfig, manifest, or release files.
