# ProcGen Gameplay Iteration Guide

This guide documents the hardened procedural facility pipeline for day-to-day gameplay iteration.

## Add A New Room Module

1. Create a prefab variant under `Assets/ProcGen/TilePrefabs/` that preserves existing building meshes/materials.
2. Add a `Tile` component and set:
   - `moduleId` to a stable ID (for example `room_lab_small`)
   - category/tags for solver filtering
   - selection weight and max-use behavior
3. Add child groups:
   - `Doorways`
   - `Occupancy`
   - `SpawnMarkers`
   - `Connectors`
   - `Blockers`
4. Add at least one `OccupancyBounds` volume that covers walkable footprint and collision walls.
5. Add doorway anchors with outward forward vectors.
6. Create a `TileDefinition` asset in `Assets/ProcGen/Catalogs/Definitions/` and point it at the prefab.
7. Include the definition in `InitialBlock9TileCatalog`.
8. Run:
   - `Tools/ProcGen/Harden Module Authoring (Auto-fix)`
   - `Tools/ProcGen/Tile Authoring Validator`

## Add A Connector

1. Create/reuse connector prefab under `Assets/ProcGen/Connectors/`.
2. On each relevant `Doorway`, set:
   - `connectorKind`
   - `connectorPrefab` (or connector object reference)
3. Ensure the doorway socket pairing is compatible.
4. Validate in physical preview (`Tools/ProcGen/Generate Physical Layout Preview`) and verify connected openings use the connector.

## Add A Socket

1. Create a `DoorwaySocket` asset under `Assets/ProcGen/Sockets/`.
2. Set `socketName` and `compatibleSocketNames`.
3. Keep compatibility symmetric (A allows B and B allows A), otherwise solver connections fail.
4. Assign the socket to relevant doorways and rerun batch validation.

## Add Blockers

1. Create/reuse blocker prefab under `Assets/ProcGen/Blockers/`.
2. Assign `blockerPrefab` on each doorway that can be unused.
3. Run `Tools/ProcGen/Validate Seed Batch (100 seeds)` and verify blocker safety checks pass.

## Debug A Failed Seed

1. Reproduce with `FacilityRuntimeGenerator` seed replay HUD:
   - use `Current Seed`
   - click `Regenerate Seed`
2. Inspect debug overlays on generated root:
   - graph edges (main/branch/loop/fire-exit/portal)
   - blocked connectors
   - occupancy boxes
   - population marker usage
3. Check failed-seed logs in `Application.persistentDataPath/procgen_failed_seeds.jsonl`.
4. Run `Tools/ProcGen/Validate Seed Batch (100 seeds)` and review:
   - `Assets/Docs/ProcGen/FacilityHardeningReport.md`
   - console failure lines for seed-specific reasons

## Tune Layout Config

Primary knobs:

- `FacilityGraphPlanConfig.MainPathLengthRange`
- `FacilityGraphPlanConfig.BranchCountRange`
- `FacilityGraphPlanConfig.BranchLengthRange`
- `FacilityGraphPlanConfig.LoopChance`
- `FacilityGraphPlanConfig.FireExitChance`
- `FacilityGraphPlanConfig.MinimumMainPathDistanceForFireExit`
- `FacilityGraphPlanConfig.PortalChance`

Secondary knobs:

- `TileDefinition.selectionWeight` to control module frequency
- `DoorwaySocket` compatibility lists to tighten/relax connector rules
- `FacilityPopulationSettings` probabilities for loot/enemy/hazard/light/audio/fire-exit risk/reward

Recommended workflow:

1. Adjust one cluster of knobs at a time.
2. Run batch validation across at least 100 seeds.
3. Compare failure-rate/failure-reason deltas and module usage distribution.
4. Keep deterministic replay enabled while tuning.

