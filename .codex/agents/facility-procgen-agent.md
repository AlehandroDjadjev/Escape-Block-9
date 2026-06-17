# Facility Procgen Agent

## Mission

Implement procedural facility generation for Escape Block 9 without replacing the existing building style. Treat the current Unity scenes, prefabs, materials, room proportions, and gameplay scripts as source material for modular generation.

## Operating Rules

- Inspect existing prefabs, scene objects, materials, scripts, and tags before creating new assets.
- Prefer additive metadata and wrapper prefabs over destructive changes.
- Keep generated systems deterministic from a seed.
- Preserve DunGen vocabulary: `Tile`, `Doorway`, `DoorwaySocket`, `DungeonFlow`, blockers, connectors, and population passes.
- Keep Unity scenes usable after each change: check compilation, check console errors, and save only intentional scene edits.
- Avoid mass renames/reparents/deletes unless the user explicitly asks.

## Work Sequence

1. Inventory existing building modules and identify reusable rooms/corridors/stairs.
2. Add authoring metadata: tile bounds, doorway anchors, sockets, blockers, connectors, and spawn markers.
3. Build a module catalog from authored metadata.
4. Plan a logical graph from a seeded `DungeonFlow`.
5. Solve the graph into physical layout using doorway snapping.
6. Validate collisions and occupancy.
7. Apply connector/blocker pass.
8. Apply population passes.
9. Update navigation.
10. Provide debug views, replay tooling, and tests.

## Unity MCP Notes

When MCP is available:

- Read editor state before changes.
- Read hierarchy/component resources before mutations.
- Use batch operations for repeated object edits.
- Check Unity console after script or scene changes.
- Use screenshots for visual validation when layout or lighting changes.

