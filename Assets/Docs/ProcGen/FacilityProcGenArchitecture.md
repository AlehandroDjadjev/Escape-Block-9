# Facility Procgen Architecture

## Purpose

This document defines the implementation contract for Escape Block 9's procedural facility generation system. The system must reuse the current building, generated room prefabs, materials, scale, doors, lighting style, and gameplay scripts as source material. It must not replace the project with a generic dungeon kit.

This is a design blueprint only. It names the runtime architecture, future files, authoring schema, generation phases, and preservation rules that implementation prompts should follow.

## Source Inventory Status

The current source inventory is tracked in `Assets/Docs/ProcGen/CurrentBuildingInventory.md`. When this architecture document was first created that file was missing, so the contract below was based on the project inventory visible in assets and scripts:

- `Assets/arhitektura/Generated/Room.prefab`
- `Assets/arhitektura/Generated/Bathroom.prefab`
- `Assets/arhitektura/Generated/Shop.prefab`
- `Assets/arhitektura/Generated/Stairs.prefab`
- `Assets/arhitektura/Generated/ExitLobby.prefab`
- `Assets/arhitektura/Generated/Block9Building.prefab`
- `Assets/arhitektura/Door.prefab`
- `Assets/Editor/BuildingGenerator.cs`
- Runtime gameplay scripts under `Assets/Scripts/`

Future prompts should reconcile this document against `CurrentBuildingInventory.md` before implementing systems.

## Decision: Hybrid Abstraction, Custom Solver First

The project should use a hybrid abstraction that can support both a custom DunGen-like solver and a future DunGen adapter.

Do not integrate DunGen directly as the first implementation step. The current project already has a strongly authored school/facility layout, generated prefabs, specific scale constants, nested door prefabs, and gameplay scripts. Direct DunGen lock-in would force current assets into DunGen's asset model before the project has stable metadata, sockets, blockers, connector rules, or validation tools.

Use a custom DunGen-like solver first behind project-owned interfaces. This gives the project deterministic seed replay, exact preservation of current module scale, and explicit control over corridor/stair/fire-exit behavior. Keep the public data model close to DunGen vocabulary: `Tile`, `Doorway`, `DoorwaySocket`, `DungeonFlow`, connectors, blockers, and post-processing passes.

The abstraction boundary must allow a future DunGen backend:

- Project data remains in `TileDefinition`, `DoorwayDefinition`, `DoorwaySocket`, `DungeonFlow`, and `FacilityRunConfig`.
- Runtime generation depends on `IFacilityLayoutSolver`, not on a specific solver package.
- The first backend is `CustomFacilityLayoutSolver`.
- A later optional backend can be `DunGenFacilityLayoutSolverAdapter` if DunGen becomes useful.

## Minimal Runtime Architecture

The minimum useful runtime path is:

```text
FacilityRunConfig
  -> TileCatalog
  -> DungeonFlow graph plan
  -> Tile candidate filtering
  -> Doorway snap placement
  -> Occupancy validation
  -> ResolvedFacilityLayout
  -> Connector/blocker pass
  -> Fire exit/portal pass
  -> Population pass
  -> NavMesh pass
  -> Debug/replay report
```

The first playable milestone should instantiate a deterministic main path using `Room`, `Bathroom`, `Shop`, `Stairs`, `ExitLobby`, and corridor/connector modules extracted or wrapped from the existing `Block9Building` layout. Branches, loops, hazards, and advanced population should build on that same contract rather than introducing a second data model.

## Current Asset Reuse

The existing assets map into the procgen model as follows:

| Existing asset | Procgen role | Notes |
| --- | --- | --- |
| `Room.prefab` | `TileCategory.Room` | Standard classroom module. Primary reusable room tile. |
| `Bathroom.prefab` | `TileCategory.Special` or `TileCategory.Room` with `bathroom` tag | Use uniqueness/max-use rules per floor or flow segment. |
| `Shop.prefab` | `TileCategory.Special` | Good for optional branch, objective, or safe-zone placement. |
| `Stairs.prefab` | `TileCategory.Stair` | Vertical connector tile. Requires explicit vertical doorway sockets. |
| `ExitLobby.prefab` | `TileCategory.Exit` | Critical path exit/fire-exit endpoint candidate. |
| `Door.prefab` | Connector visual/interactable | Used by placed doorway pairs or exit connectors. |
| `Block9Building.prefab` | Inventory/reference source | Do not mutate directly for procgen metadata. Use it to extract scale, corridor dimensions, and possible connector/corridor modules. |
| `BuildingGenerator.cs` | Scale and naming reference | Constants define current style: room width 8, depth 7, height 3.5, corridor width 3, wall thickness 0.2. |

Corridors are not currently standalone module prefabs except as pieces inside `Block9Building.prefab`. The implementation should create additive corridor/connector wrapper prefabs or prefab variants from existing generated corridor geometry rather than inventing a new corridor art style.

## Preservation Contract

Procgen work must preserve current building assets by default:

- Do not modify `Room.prefab`, `Bathroom.prefab`, `Shop.prefab`, `Stairs.prefab`, `ExitLobby.prefab`, `Door.prefab`, or `Block9Building.prefab` directly unless a prompt explicitly asks for source asset changes.
- Prefer wrapper prefabs or prefab variants that add metadata children and components.
- Use existing materials from `Assets/arhitektura/Generated/` and `Assets/GeneratedLighting/`.
- Keep existing visual hierarchy, mesh scale, colliders, and gameplay components intact.
- Add authoring helpers under predictable child names:
  - `Doorways`
  - `Occupancy`
  - `SpawnMarkers`
  - `Connectors`
  - `Blockers`
- Keep helper objects visually unobtrusive and editor-friendly.
- Avoid mass renames, mass reparenting, and destructive scene edits.
- Preserve the existing `BuildingGenerator` as an inventory/reference tool unless it is intentionally replaced by a later prompt.

## Planned Folders

Runtime and authoring code should live under:

- `Assets/Scripts/ProcGen/Authoring/`
- `Assets/Scripts/ProcGen/Data/`
- `Assets/Scripts/ProcGen/Planning/`
- `Assets/Scripts/ProcGen/Placement/`
- `Assets/Scripts/ProcGen/Runtime/`
- `Assets/Scripts/ProcGen/Population/`
- `Assets/Scripts/ProcGen/Navigation/`
- `Assets/Scripts/ProcGen/Debugging/`
- `Assets/Scripts/ProcGen/Validation/`

Editor-only tooling should live under:

- `Assets/Editor/ProcGen/`

Authoring assets should live under:

- `Assets/ProcGen/Configs/`
- `Assets/ProcGen/Catalogs/`
- `Assets/ProcGen/Flows/`
- `Assets/ProcGen/Sockets/`
- `Assets/ProcGen/TilePrefabs/`
- `Assets/ProcGen/TestFixtures/`

Tests should live under:

- `Assets/Tests/EditMode/ProcGen/`
- `Assets/Tests/PlayMode/ProcGen/`

Docs should live under:

- `Assets/Docs/ProcGen/`

## Planned Namespaces

Use these namespaces unless existing code later establishes a stronger convention:

- `EscapeBlock9.ProcGen`
- `EscapeBlock9.ProcGen.Authoring`
- `EscapeBlock9.ProcGen.Data`
- `EscapeBlock9.ProcGen.Planning`
- `EscapeBlock9.ProcGen.Placement`
- `EscapeBlock9.ProcGen.Runtime`
- `EscapeBlock9.ProcGen.Population`
- `EscapeBlock9.ProcGen.Navigation`
- `EscapeBlock9.ProcGen.Debugging`
- `EscapeBlock9.ProcGen.Validation`
- `EscapeBlock9.ProcGen.Editor`

## ScriptableObjects

Planned ScriptableObject files/classes:

| File | Class | Purpose |
| --- | --- | --- |
| `Assets/Scripts/ProcGen/Data/FacilityRunConfig.cs` | `FacilityRunConfig` | Seed, catalog, flow, solver backend, generation toggles, population toggles, nav settings. |
| `Assets/Scripts/ProcGen/Data/TileCatalog.cs` | `TileCatalog` | List of available `TileDefinition` entries and catalog version. |
| `Assets/Scripts/ProcGen/Data/TileDefinition.cs` | `TileDefinition` | Metadata for one authored module prefab or wrapper prefab. |
| `Assets/Scripts/ProcGen/Data/DoorwaySocket.cs` | `DoorwaySocket` | Socket compatibility asset used by doorway anchors. |
| `Assets/Scripts/ProcGen/Data/DungeonFlow.cs` | `DungeonFlow` | Graph rules for main path, branches, special rooms, stairs, exits, loops, and optional portals. |
| `Assets/Scripts/ProcGen/Data/TileConnectionRuleSet.cs` | `TileConnectionRuleSet` | Rule list for allowed tile/category/socket connections. |
| `Assets/Scripts/ProcGen/Data/PopulationTable.cs` | `PopulationTable` | Weighted entries for loot, enemies, lights, audio, hazards, and objectives. |
| `Assets/Scripts/ProcGen/Data/NavBuildProfile.cs` | `NavBuildProfile` | Runtime navmesh surface/link settings. |

## MonoBehaviours

Planned authoring/runtime MonoBehaviour files/classes:

| File | Class | Purpose |
| --- | --- | --- |
| `Assets/Scripts/ProcGen/Authoring/Tile.cs` | `Tile` | Component on wrapper prefab root; references `TileDefinition` or contains local metadata override. |
| `Assets/Scripts/ProcGen/Authoring/Doorway.cs` | `Doorway` | Transform anchor with outward forward vector, socket, connector/blocker refs, and dimensions. |
| `Assets/Scripts/ProcGen/Authoring/OccupancyBounds.cs` | `OccupancyBounds` | Authored volume used for overlap validation. |
| `Assets/Scripts/ProcGen/Authoring/SpawnMarker.cs` | `SpawnMarker` | Marker for loot, enemy, light, audio, hazard, objective, exit, or player spawn. |
| `Assets/Scripts/ProcGen/Authoring/ConnectorAuthoring.cs` | `ConnectorAuthoring` | Enables/spawns visual connector for used doorway pairs. |
| `Assets/Scripts/ProcGen/Authoring/BlockerAuthoring.cs` | `BlockerAuthoring` | Enables/spawns blocker for unused doorways. |
| `Assets/Scripts/ProcGen/Runtime/FacilityGenerator.cs` | `FacilityGenerator` | Scene entry point that runs generation from `FacilityRunConfig`. |
| `Assets/Scripts/ProcGen/Runtime/GeneratedFacilityRoot.cs` | `GeneratedFacilityRoot` | Parent and replay metadata for generated instances. |
| `Assets/Scripts/ProcGen/Debugging/FacilityDebugGizmos.cs` | `FacilityDebugGizmos` | Draws tile bounds, doorway vectors, sockets, graph edges, occupancy, and markers. |
| `Assets/Scripts/ProcGen/Navigation/RuntimeNavMeshBuilder.cs` | `RuntimeNavMeshBuilder` | Builds or updates navmesh and nav links after generation. |

## Runtime Systems

Planned non-MonoBehaviour runtime classes:

| File | Class | Purpose |
| --- | --- | --- |
| `Assets/Scripts/ProcGen/Runtime/SeededRandom.cs` | `SeededRandom` | Local deterministic random wrapper; never depend on global `UnityEngine.Random` for generation choices. |
| `Assets/Scripts/ProcGen/Runtime/GenerationContext.cs` | `GenerationContext` | Seed, sub-seeds, config, catalog, flow, scene context, and log sink. |
| `Assets/Scripts/ProcGen/Planning/FacilityGraphPlanner.cs` | `FacilityGraphPlanner` | Builds logical graph from `DungeonFlow`. |
| `Assets/Scripts/ProcGen/Planning/FacilityGraph.cs` | `FacilityGraph` | Logical nodes/edges before physical placement. |
| `Assets/Scripts/ProcGen/Placement/ITileCandidateFilter.cs` | `ITileCandidateFilter` | Filters tiles by category, tags, max uses, uniqueness, sockets, and flow constraints. |
| `Assets/Scripts/ProcGen/Placement/TileCandidateFilter.cs` | `TileCandidateFilter` | Default candidate filter. |
| `Assets/Scripts/ProcGen/Placement/IFacilityLayoutSolver.cs` | `IFacilityLayoutSolver` | Solver abstraction used by runtime generation. |
| `Assets/Scripts/ProcGen/Placement/CustomFacilityLayoutSolver.cs` | `CustomFacilityLayoutSolver` | First solver backend. Doorway snapping plus occupancy rejection. |
| `Assets/Scripts/ProcGen/Placement/DunGenFacilityLayoutSolverAdapter.cs` | `DunGenFacilityLayoutSolverAdapter` | Optional future adapter; do not implement until DunGen is actually adopted. |
| `Assets/Scripts/ProcGen/Placement/SnapTransformSolver.cs` | `SnapTransformSolver` | Computes tile transform from source doorway to target doorway. |
| `Assets/Scripts/ProcGen/Validation/OccupancyValidator.cs` | `OccupancyValidator` | Checks physical overlaps using authored occupancy volumes. |
| `Assets/Scripts/ProcGen/Validation/ConnectivityValidator.cs` | `ConnectivityValidator` | Checks socket compatibility, critical path reachability, exits, stairs, and unused blockers. |
| `Assets/Scripts/ProcGen/Runtime/ResolvedFacilityLayout.cs` | `ResolvedFacilityLayout` | Physical placement result before instantiation/post-processing. |
| `Assets/Scripts/ProcGen/Runtime/ConnectorBlockerPass.cs` | `ConnectorBlockerPass` | Enables used connectors and unused blockers. |
| `Assets/Scripts/ProcGen/Runtime/FireExitPortalPass.cs` | `FireExitPortalPass` | Handles fire exits, stairs, and optional portal edges. |
| `Assets/Scripts/ProcGen/Population/PopulationPipeline.cs` | `PopulationPipeline` | Runs deterministic marker-based population passes. |
| `Assets/Scripts/ProcGen/Population/LootPopulationPass.cs` | `LootPopulationPass` | Places pickup/objective items. |
| `Assets/Scripts/ProcGen/Population/EnemyPopulationPass.cs` | `EnemyPopulationPass` | Places NPCs/entities and patrol paths. |
| `Assets/Scripts/ProcGen/Population/LightingPopulationPass.cs` | `LightingPopulationPass` | Places or enables lights and flicker/audio ambience. |
| `Assets/Scripts/ProcGen/Population/HazardPopulationPass.cs` | `HazardPopulationPass` | Places hazards after required path objects. |
| `Assets/Scripts/ProcGen/Debugging/SeedReplayReport.cs` | `SeedReplayReport` | Captures seed, flow, catalog version, pass toggles, errors, and placed layout. |

## Editor Tools

Planned editor files/classes:

| File | Class | Purpose |
| --- | --- | --- |
| `Assets/Editor/ProcGen/TileWrapperCreator.cs` | `TileWrapperCreator` | Creates additive wrapper prefabs from current building prefabs. |
| `Assets/Editor/ProcGen/TileAuthoringValidatorWindow.cs` | `TileAuthoringValidatorWindow` | Finds missing doorways, sockets, occupancy, blockers, connectors, and spawn markers. |
| `Assets/Editor/ProcGen/CurrentBuildingInventoryBuilder.cs` | `CurrentBuildingInventoryBuilder` | Optional tool to regenerate `CurrentBuildingInventory.md` from assets. |
| `Assets/Editor/ProcGen/FacilityGenerationPreviewWindow.cs` | `FacilityGenerationPreviewWindow` | Runs fixed-seed previews in editor without saving scenes by default. |
| `Assets/Editor/ProcGen/DoorwaySocketGizmoDrawer.cs` | `DoorwaySocketGizmoDrawer` | Draws doorway forward vectors and labels. |

Editor tools must be additive and must not overwrite source prefabs unless explicitly commanded.

## Module Metadata Schema

`TileDefinition` is the stable metadata contract for a generated module.

| Field | Type | Required | Notes |
| --- | --- | --- | --- |
| `moduleId` | `string` | Yes | Stable ID such as `classroom_standard_a`, `bathroom_a`, `stairs_vertical_a`, `exit_lobby_a`. Never derive solely from prefab name at runtime. |
| `displayName` | `string` | No | Human-readable editor label. |
| `prefab` | `GameObject` | Yes | Wrapper prefab or preserved source prefab. Prefer wrapper prefab. |
| `category` | `TileCategory` | Yes | `Room`, `Corridor`, `Stair`, `Connector`, `Exit`, `Special`, `Portal`, `Utility`. |
| `tags` | `string[]` or `ProcGenTag[]` | Yes | Examples: `classroom`, `bathroom`, `shop`, `north-facing`, `two-floor`, `critical-path`, `branch`, `dead-end-ok`. |
| `doorways` | `DoorwayDefinition[]` or authored `Doorway` components | Yes | Doorway anchors with local transform, socket, connector/blocker, kind, dimensions. |
| `sockets` | `DoorwaySocket[]` | Yes | Usually stored per doorway. A tile can expose aggregate sockets for filtering. |
| `connectorKind` | `ConnectorKind` | Yes | `Door`, `OpenFrame`, `CorridorJoin`, `Stair`, `FireExit`, `Portal`, `Sealed`, `None`. |
| `occupancyVolume` | `Bounds` or `OccupancyBounds[]` | Yes | Authored volumes in tile-local space. Must include walls/colliders that matter for overlap. |
| `spawnMarkers` | `SpawnMarkerDefinition[]` or authored `SpawnMarker` components | No | Marker group, kind, tags, weight, required reachability. |
| `selectionWeight` | `float` | Yes | Default `1`. Used by candidate selection after filtering. |
| `maxUses` | `int` | Yes | `-1` for unlimited. `1` for unique special tiles. |
| `unique` | `bool` | Yes | True means only one use per generated layout. |
| `allowedRotations` | `AllowedRotationSet` | Yes | Examples: `OnlyAuthored`, `Yaw90`, `Yaw180`, `Yaw270`, `AnyRightAngle`. |
| `branchSuitability` | `float` | No | Optional score for branch placement. |
| `deadEndSuitability` | `float` | No | Optional score for dead-end placement. |
| `mainPathSuitability` | `float` | No | Optional score for critical path placement. |
| `floorDelta` | `int` | No | `0` for same-floor tiles, `1` or `-1` for stairs/vertical connectors. |

Recommended enums:

```csharp
public enum TileCategory
{
    Room,
    Corridor,
    Stair,
    Connector,
    Exit,
    Special,
    Portal,
    Utility
}

public enum ConnectorKind
{
    None,
    Door,
    OpenFrame,
    CorridorJoin,
    Stair,
    FireExit,
    Portal,
    Sealed
}

[Flags]
public enum AllowedRotationSet
{
    OnlyAuthored = 1,
    Yaw90 = 2,
    Yaw180 = 4,
    Yaw270 = 8,
    AnyRightAngle = Yaw90 | Yaw180 | Yaw270
}
```

`Doorway` contract:

- Local transform under `Doorways`.
- Local forward points out of the tile.
- References one `DoorwaySocket`.
- Has `ConnectorKind`.
- Can reference a connector prefab or child object.
- Can reference a blocker prefab or child object.
- Has width/height metadata for compatibility and visuals.
- Can mark vertical behavior for stairs.

`SpawnMarker` contract:

- Local transform under `SpawnMarkers`.
- Marker kind: `PlayerStart`, `Loot`, `Objective`, `Enemy`, `PatrolPath`, `Light`, `Audio`, `Hazard`, `Exit`, `Debug`.
- Tags and optional socket/category restrictions.
- Weight and max uses per tile.
- Whether marker must be on the critical path or reachable from the start.

## Generator Phase Order

The generator must run phases in this order:

1. `GraphPlan`
   - Input: `FacilityRunConfig`, `DungeonFlow`, seed.
   - Output: `FacilityGraph`.
   - Plans main path, branches, dead ends, required special rooms, stairs, exits, loops, and optional portal edges.

2. `Placement`
   - Input: `FacilityGraph`, `TileCatalog`.
   - Output: `ResolvedFacilityLayout`.
   - Filters candidates, selects tiles deterministically, snaps doorway anchors, applies allowed rotations, and rejects overlaps.

3. `ConnectorBlocker`
   - Input: placed tiles and used doorway pairs.
   - Output: instantiated connectors and blockers.
   - Enables/open-connects used doorway pairs; seals unused doorway anchors.

4. `FireExitPortal`
   - Input: resolved layout plus flow requirements.
   - Output: reachable exit/stair/portal links.
   - Places or validates fire exits, stair links, optional non-Euclidean/portal edges, and exit signage.

5. `Population`
   - Input: layout, marker groups, population tables.
   - Output: spawned gameplay objects.
   - Runs deterministic sub-passes for required objectives, loot, enemies, lights, audio, hazards, and ambience.

6. `Nav`
   - Input: final instantiated layout.
   - Output: baked/updated runtime navmesh and links.
   - Uses `com.unity.ai.navigation` when available. Nav must run after blockers/connectors and population objects that affect traversal.

7. `DebugTest`
   - Input: all generation artifacts.
   - Output: gizmos, replay data, validation results, and logs.
   - Records seed, catalog version, flow, failures, used tiles, graph edges, placements, and validation results.

Each phase receives a deterministic sub-seed derived from the root seed and phase name.

## Validation Contract

EditMode tests should cover:

- `TileDefinition` schema validation.
- Catalog rejects duplicate `moduleId`.
- Doorway socket compatibility.
- Snap transform math.
- Occupancy overlap detection.
- Max uses and uniqueness.
- Allowed rotations.
- Fixed-seed graph planning.

PlayMode tests should cover:

- Fixed seed can instantiate a minimal playable layout.
- All used doorway pairs have compatible sockets.
- All unused doorways get blockers.
- Critical path is connected.
- Exit/stair nodes are reachable.
- Runtime nav build succeeds when enabled.
- Seed replay report is generated on failure.

Planned test files:

- `Assets/Tests/EditMode/ProcGen/TileCatalogTests.cs`
- `Assets/Tests/EditMode/ProcGen/SnapTransformSolverTests.cs`
- `Assets/Tests/EditMode/ProcGen/OccupancyValidatorTests.cs`
- `Assets/Tests/EditMode/ProcGen/FacilityGraphPlannerTests.cs`
- `Assets/Tests/PlayMode/ProcGen/FacilityGeneratorSmokeTests.cs`
- `Assets/Tests/PlayMode/ProcGen/FacilityReachabilityTests.cs`

## Implementation Milestones

1. Metadata stubs and validation only.
   - Implement `TileDefinition`, `DoorwaySocket`, `Tile`, `Doorway`, `OccupancyBounds`, `SpawnMarker`.
   - Add editor validation with no runtime generation.

2. Current asset wrappers.
   - Create wrapper prefabs for `Room`, `Bathroom`, `Shop`, `Stairs`, `ExitLobby`, and corridor/connector pieces.
   - Add authored doorway anchors, occupancy, blockers/connectors, and spawn markers.

3. Catalog and fixed seed preview.
   - Implement `TileCatalog`, `FacilityRunConfig`, and a minimal `DungeonFlow`.
   - Validate catalog and generate a text/debug-only graph.

4. Custom layout solver.
   - Implement candidate filter, doorway snapping, occupancy validation, and layout result.
   - Instantiate a short deterministic main path.

5. Post-processing passes.
   - Add connectors/blockers, fire exit/stair handling, marker population, and nav.

6. Debug and replay.
   - Add gizmos, seed replay reports, editor preview window, and fixed-seed tests.

## Dependency Policy

Avoid unnecessary dependency lock-in:

- Do not require DunGen in the first implementation.
- Do not encode DunGen asset types into project metadata.
- Do not depend on global `UnityEngine.Random` state for generation.
- Do use installed Unity packages that already match the project, especially `com.unity.ai.navigation`, `com.unity.cinemachine`, `com.unity.inputsystem`, and URP.
- Keep optional solver/package integrations behind interfaces and adapters.

## Future Prompt Entry Points

Future implementation prompts should target one small slice at a time:

- "Implement metadata stubs and validation for `TileDefinition`, `DoorwaySocket`, `Tile`, `Doorway`, and `OccupancyBounds`."
- "Create wrapper prefab authoring for `Room`, `Bathroom`, `Shop`, `Stairs`, and `ExitLobby` using existing assets."
- "Implement `TileCatalog` validation and fixed seed `DungeonFlow` graph planning."
- "Implement `SnapTransformSolver` and `OccupancyValidator` edit-mode tests."
- "Implement `CustomFacilityLayoutSolver` for a short main path only."
- "Implement connector/blocker pass for authored doorways."
- "Implement marker-based population passes for lights, items, enemies, and hazards."
- "Implement runtime navmesh update and reachability tests."
