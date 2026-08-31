# T-038 contract

## Outcome

Every bot destination request crosses one explicit navigation decision boundary before movement. The boundary returns an observable accepted, unavailable, invalid-surface, or unreachable result and never silently substitutes unsafe direct traversal.

## Pass

- Deterministic tests cover finite geometry, invalid surfaces, height/surface disagreement, unavailable navigation data, and an accepted reachable request.
- Rejected requests issue no movement and expose a bounded diagnostic reason.
- Existing valid same-surface movement remains available through an explicitly named compatibility policy.
- No claim of general wall, cave, water, ship, or terrain avoidance is made.

## Non-goals

- Population Director integration.
- Selecting a pathfinding library.
- Live cave-navigation acceptance.
- Editing shared BotHost, BotManager, BotConfig, manifest, or release files.
