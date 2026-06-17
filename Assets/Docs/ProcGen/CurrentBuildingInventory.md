# Current Building Inventory

This inventory captures the reusable current-building pieces used for the first Escape Block 9 procedural module catalog. It is based on the generated Block 9 prefab set and dimensions authored in `Assets/Editor/BuildingGenerator.cs`.

## Source Prefabs

| Asset | Reuse role | Notes |
| --- | --- | --- |
| `Assets/arhitektura/Generated/Room.prefab` | Normal room Tile | Classroom-style room with existing wall, floor, ceiling, window/board/light dressing. |
| `Assets/arhitektura/Generated/Bathroom.prefab` | Special room Tile | Utility/special room candidate; keep existing stall and fixture dressing. |
| `Assets/arhitektura/Generated/Shop.prefab` | Special/large room Tile | Branch or objective/safe-zone candidate. |
| `Assets/arhitektura/Generated/ExitLobby.prefab` | Start and fire-exit Tile | Reused as both entrance/start and fire-exit endpoint variants. |
| `Assets/arhitektura/Generated/Stairs.prefab` | Stair/vertical-link Tile | Existing vertical traversal module for floor changes. |
| `Assets/arhitektura/Door.prefab` | Door connector visual | Reused by room and exit doorway connectors. |
| `Assets/arhitektura/Generated/Block9Building.prefab` | Scale/layout reference | Source of corridor, wing, connector, stair, and fire-exit proportions; do not mutate for metadata. |

## Measured Dimensions

| Measurement | Value | Source |
| --- | --- | --- |
| Room width | 8m | `RoomWidth` |
| Room depth | 7m | `RoomDepth` |
| Room height | 3.5m | `RoomHeight` |
| Corridor width | 3m | `CorridorWidth` |
| Wall thickness | 0.2m | `WallThickness` |
| Door opening | 1.2m x 2.2m | `DoorWidth`, `DoorHeight` |
| Stair run | 5.04m | `18` steps x `0.28m` step run |

## First Catalog Selection

The first catalog intentionally uses the smallest viable set of reusable pieces needed for deterministic facility generation:

| Module ID | Source | Category | Purpose |
| --- | --- | --- | --- |
| `start_exit_lobby` | `ExitLobby.prefab` wrapper | Exit | Start/entrance module with player-start and exit markers. |
| `corridor_straight_8m` | Constructed from existing dimensions/materials | Corridor | Main straight corridor segment. |
| `corridor_corner_3m` | Constructed from existing dimensions/materials | Corridor | Right-angle corridor turn. |
| `corridor_t_junction_3m` | Constructed from existing dimensions/materials | Corridor | Branch junction. |
| `corridor_cross_junction_3m` | Constructed from existing dimensions/materials | Corridor | Loop/cross junction. |
| `room_classroom` | `Room.prefab` wrapper | Room | Standard normal room. |
| `room_bathroom` | `Bathroom.prefab` wrapper | Special | Utility/special branch room. |
| `room_shop_special` | `Shop.prefab` wrapper | Special | Larger/special branch room. |
| `corridor_dead_end` | Constructed from existing dimensions/materials | Corridor | Dead-end/cap module. |
| `fire_exit_lobby` | `ExitLobby.prefab` wrapper | Exit | Fire-exit endpoint. |
| `stairs_vertical` | `Stairs.prefab` wrapper | Stair | Vertical floor transition. |

## Preservation Notes

- Source generated building prefabs are inventory/reference assets and should remain unmodified by procgen authoring.
- Converted modules live under `Assets/ProcGen/TilePrefabs` as wrappers or variants with additive authoring helpers.
- Standalone corridor modules are constructed only because the current project does not have standalone corridor prefabs; they reuse Block 9 dimensions and generated materials.
- Placeholder connector/blocker prefabs should remain simple and use existing materials until stronger in-building assets are extracted.
