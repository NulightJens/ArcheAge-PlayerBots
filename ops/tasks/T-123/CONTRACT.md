# T-123 contract: transfer-road world navigation graph

Start from the exact committed source base in `TASK.yaml`. AAEmu 1.2 already
loads `game/worlds/<world>/level_design/zone/**/transfer_path.xml`, converts
its points to world coordinates, and retains the resulting transfer roads
privately. Expose an immutable read-only snapshot through a narrow compatibility
seam; do not read, unpack, or modify the registered client or `game_pak`.

Build a deterministic normalized graph from the authoritative road polylines.
Reject non-finite or degenerate inputs, preserve world/zone/path/type metadata,
weight edges by polyline length, and snap compatible endpoints only within a
bounded configurable tolerance. Explicitly handle zone-boundary joins,
near-parallel ambiguous endpoints, duplicate segments, one-way metadata if
present, disconnected components, and excessive vertical or surface gaps.

Provide nearest-safe-road projection, deterministic A* route selection, and a
route result containing the ordered road waypoints plus enough metadata for
diagnostics. The long-range planner must hand each bounded segment to existing
heightmap movement and use the existing BAI/local navigation seam for the final
off-road approach. It must replan or fail closed when the world changes, a
segment is invalid, or local traversal rejects a waypoint; it must never
teleport or treat a failed local segment as traversed.

Factor host exposure into
`compatibility/aaemu-1.2-r208022-transfer-road-adapter.patch`; never edit the
registered or reference host. Add a read-only debug/export representation that
can later visualize nodes, edges, components, and selected routes without
writing client data. Focused synthetic fixtures must prove coordinate
preservation, length weights, deterministic snapping/ties, cross-zone routing,
disconnected graphs, invalid inputs, slope/gap rejection, projection, replans,
and final-approach handoff. Run an isolated AAEmu 1.2 build with zero errors and
record exact proof in `HANDOFF.md`.

