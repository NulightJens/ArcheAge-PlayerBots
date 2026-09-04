# World navigation

PlayerBots uses a lightweight two-layer navigator:

1. A shared transfer-road graph plans regional travel.
2. Existing local movement follows each bounded waypoint and handles the final approach.

This keeps per-bot state small: one route, a waypoint cursor, and limited retry state.

## Road graph

`WorldRoadGraphBuilder` reads AAEmu's immutable transfer-road data once per world. `WorldRoadRoutePlanner` projects the start and destination onto that graph, finds a legal route, and divides long edges into local segments.

Routes respect world, component, direction, and distance limits. Invalid projections or disconnected road components fail without moving the bot.

## Quest destinations

`BotQuestDestinationIndex` combines AAEmu quest markers with matching static NPC spawns. Exact spawns inside an authored marker are preferred, followed by the marker area and then an unmarked exact spawn.

Static destination indexing is currently available only on the supported AAEmu 1.2 track.

The marker identifies an area; it is not a path. A live target must still be found and validated on arrival.

## Local movement

`WorldRoadNavigationSession` sends one waypoint at a time through the existing heightmap/BAI movement boundary. The route advances only after the bot reaches that waypoint. A rejected segment causes a bounded replan; repeated failure stops the route.

No navigation path teleports the bot. Recovery nudges do not replace the quest's logical destination.

## Current limits

This is not a full CryEngine navmesh. Rocks, cliffs, caves, interiors, water, bridges, and incomplete road data can still block or roughen travel. The correct extension point is the local traversal layer; the shared road and quest indexes should remain cheap and reusable.
