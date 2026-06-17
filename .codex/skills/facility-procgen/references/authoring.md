# Module Authoring

Use this reference for Tile metadata, Doorways, sockets, occupancy bounds, blockers, connectors, and prefab wrapping.

## Tile Metadata

Each generated module should eventually expose:

- Stable module id
- Module category: room, corridor, stair, connector, exit, special
- Bounds or occupancy volume
- Doorway list
- Allowed sockets/tags
- Optional weight/cost
- Optional branch/dead-end suitability
- Optional population marker groups

## Doorways

Doorways should be Transform anchors with:

- Forward direction pointing out of the Tile
- Socket compatibility data
- Optional connector prefab/reference
- Optional blocker prefab/reference
- Optional width/height metadata

## Occupancy

Use occupancy bounds for layout solving, not renderer bounds alone.

Bounds should:

- Include the playable footprint.
- Include walls and collision surfaces that matter for overlap.
- Avoid huge decorative overhangs unless they block traversal.

## Existing Content Wrapping

When an existing scene object must become a Tile:

1. Create or reuse a prefab/variant where safe.
2. Add metadata to the wrapper or variant.
3. Keep original meshes/materials intact.
4. Add helper objects under predictable child names:
   - `Doorways`
   - `Occupancy`
   - `SpawnMarkers`
   - `Connectors`
   - `Blockers`

