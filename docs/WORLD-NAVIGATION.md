# Transfer-road world navigation

PlayerBots can use AAEmu 1.2's already loaded transfer/cart road polylines as a
long-range world-routing layer. Client archives are not read by the module and
the client or `game_pak` is never written.

## Data boundary

`compatibility/aaemu-1.2-r208022-v4.patch` includes the narrow host method
`TransferGameData.GetTransferRoadsSnapshot()`. AAEmu finishes a
load into a private replacement dictionary and atomically publishes an
immutable store with a monotonically increasing revision. A capture copies all
roads and points into read-only snapshot records; callers cannot reach the host
dictionaries, `TransferRoads`, or mutable `WorldSpawnPosition` instances.

`AaemuTransferRoadSnapshotProvider` immediately copies that host snapshot into
module-owned types. The AAEmu 1.2 host model retains world, zone, path name,
path type, cell, and world-coordinate points. It does not retain an explicit
one-way flag, so this adapter labels those roads `Bidirectional`. The shared
module model also supports `ForwardOnly` and `ReverseOnly` for a future host
seam that exposes direction metadata.

The earlier transfer-road adapter is retained as development history. Normal
installations use the complete v4 host patch; source workers do not edit the
registered reference checkout or the deployed runtime host.

## Graph normalization

`WorldRoadGraphBuilder` performs these steps deterministically:

1. Reject null, non-finite, too-short, degenerate, overlong, over-steep, or
   excessive-vertical-step polylines.
2. Sort valid roads by exact metadata and IEEE-754 coordinate bits, then retain
   one deterministic representative of duplicate geometry.
3. Snap endpoints only in the same world, within planar and vertical bounds,
   and on compatible known surfaces. Cross-zone endpoints may join.
4. Leave equally near, non-coincident, near-parallel candidates disconnected
   with an `AmbiguousEndpoint` issue. Transitive clusters that exceed the
   original bounds are also rejected.
5. Preserve every accepted source point exactly. Each graph edge's `Weight` is
   its three-dimensional polyline length. Small endpoint-to-canonical-node
   connectors are separate traversal costs, so the A* Euclidean heuristic
   remains admissible.
6. Assign stable edge IDs, weak component IDs, and a content generation ID.

A road's own endpoints are never tolerance-collapsed merely because the road
is short. Only exactly coincident endpoints may self-close a loop.

Defaults are intentionally conservative and configurable through
`WorldRoadGraphBuildOptions`:

| Control | Default | Purpose |
| --- | ---: | --- |
| `EndpointSnapTolerance` | 2 m | Maximum planar endpoint join |
| `EndpointVerticalTolerance` | 1.5 m | Maximum vertical endpoint join |
| `MaximumPointGap` | 100 m | Reject sparse/broken source segments |
| `MaximumVerticalStep` | 15 m | Reject vertical discontinuities |
| `MaximumSlope` | 2.0 | Reject implausible rise/run |

Rejected roads do not enter the graph. Issues remain available for diagnostics;
routes can still use independent valid components.

## Projection and routing

`WorldRoadRoutePlanner` first selects the deterministic nearest safe projection
for each endpoint. Projection is limited by three-dimensional distance,
vertical gap, world, and known surface compatibility. It then evaluates legal
partial-edge connections and runs deterministic distance-weighted A* over the
normalized graph. Equal-cost alternatives use stable edge/direction signatures.

The result contains:

- source revision and generation ID;
- start and destination road projections;
- component ID and total traversal weight;
- ordered, bounded world-coordinate waypoints;
- ordered road steps with edge, zone, path, type, direction, and partial
  distance metadata.

Disconnected components, unsafe projections, invalid requests, or direction
constraints return a typed failure with no waypoints. Waypoint segments are
subdivided to `MaximumLocalSegmentLength`; the projection limit cannot be
configured above that bound.

## Local movement handoff

The road graph does not replace local collision or terrain navigation.
`WorldRoadNavigationSession` hands one bounded waypoint at a time to
`ILocalRouteTraversal`. `NavigationBoundaryLocalRouteTraversal` is the adapter
for existing movement:

- road waypoints use the configured heightmap/local navigation boundary;
- the final road-to-objective leg uses a distinct BAI/local reachability
  boundary;
- an accepted boundary result publishes the normal movement destination;
- there is no teleport operation.

A published waypoint is not considered traversed. The session advances only
when a later caller position is within arrival tolerance. A local rejection
causes a bounded replan; a repeated rejection, unavailable local navigation,
invalid segment, exhausted replan budget, or rejected final approach fails
closed. A changed graph generation cancels the active waypoint and replans from
the reported current position.

Typical wiring is:

```csharp
var graphs = new TransferRoadGraphProvider(
    new AaemuTransferRoadSnapshotProvider(),
    new WorldRoadGraphBuilder(graphOptions));

var local = new NavigationBoundaryLocalRouteTraversal(
    heightmapBoundary,
    baiFinalApproachBoundary,
    point => mover.SetDestination(bot, point));

var session = new WorldRoadNavigationSession(
    graphs,
    new WorldRoadRoutePlanner(routeOptions),
    local,
    new RoadRouteEndpoint(worldId, botPosition),
    new RoadRouteEndpoint(worldId, objectivePosition),
    navigationOptions);
```

The owner calls `Advance(currentBotPosition)` from its normal movement loop and
retains the session until it reports `Completed` or `Failed`.

## Quest destination indexing

`BotQuestDestinationIndex` is the lightweight quest-to-travel bridge. It builds
one immutable static-NPC spawn index per live `WorldInstance` and shares it
across every bot. NPC quest-start associations are a separate lazy projection,
so objective and report routing do not pay the intake-index cost. The only
per-bot travel state is its current quest, semantic destination, route cursor,
and bounded retry state.

For monster and item-gather objectives the resolver combines three sources:

1. the active quest component ID;
2. read-only quest marker spheres already loaded by AAEmu from client data; and
3. exact monster or loot-source templates mapped to server static spawns.

An exact matching spawn inside an authored marker is preferred, followed by the
marker area itself, then an unmarked exact spawn. The marker selects where the
quest happens; it is not a navmesh and never directly moves the bot. The chosen
point flows through `BotTravelRoutePlanner`, which prefers transfer roads and
uses the BAI/local boundary for the final deviation. A nearby live object must
still be revalidated before interaction or combat.

## Debug representation

`WorldNavigationDebugExporter.Serialize(graph, route)` returns deterministic,
LF-terminated JSON containing nodes, edges, source metadata, components,
validation issues, and an optional selected route. It intentionally has no
file-writing API. A server-side diagnostics command may choose an operator
approved output location later; this layer cannot write client data.
