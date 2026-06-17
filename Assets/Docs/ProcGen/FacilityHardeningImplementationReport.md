# Facility Hardening Implementation Report

## Scope

This report summarizes the hardening pass that prepares generated facilities for normal gameplay iteration.

## Implemented Changes

### 1. Batch generation and reporting

- Extended `Tools/ProcGen/Validate Seed Batch (100 seeds)` to collect:
  - failure rate
  - normalized failure reasons
  - module usage distribution
- Added markdown export:
  - `Assets/Docs/ProcGen/FacilityHardeningReport.md`

### 2. Seed replay and failure tracking

- Runtime generator now persists failed seeds through `FacilitySeedHistory`.
- HUD replay controls expose:
  - current seed
  - random seed regenerate
  - regenerate current seed
  - copy seed
  - recent failed seeds

### 3. Layout/flow tuning

- Runtime fallback graph tuning updated toward longer core runs and lower dead-end noise:
  - main path `7..10`
  - branch count `1..2`
  - branch length `1..2`
  - loop chance `0.2`
  - fire exit chance `0.9`
  - minimum fire-exit distance `3`

### 4. Module-weight tuning

Updated `TileDefinition` selection weights to reduce over-dominance and improve variety:

- `corridor_straight_8m`: `1.5 -> 1.25`
- `corridor_corner_3m`: `1.0 -> 1.1`
- `corridor_cross_junction_3m`: `1.0 -> 0.7`
- `corridor_dead_end`: `0.8 -> 0.45`
- `room_bathroom`: `1.0 -> 0.75`
- `room_shop_special`: `0.5 -> 0.4`

### 5. Socket-rule tuning

- Tightened fire-exit socket compatibility:
  - `Socket_FireExit` now only accepts `corridor_3m`
  - removes direct fire-exit to `room_door` compatibility

### 6. Population tuning and placeholder replacement hooks

- Tuned baseline probabilities for steadier difficulty ramp.
- Added optional prefab slots in `FacilityPopulationSettings`:
  - `HazardPrefab`
  - `LightFixturePrefab`
  - `AudioEmitterPrefab`
- Population pipeline now instantiates these matching assets when provided, reducing runtime placeholder objects.

### 7. Authoring hardening utility

Added `Tools/ProcGen/Harden Module Authoring (Auto-fix)`:

- scans `Assets/ProcGen/TilePrefabs/`
- auto-fixes common doorway authoring gaps:
  - missing connector IDs
  - missing sockets
  - missing connector/blocker prefab references
- writes:
  - `Assets/Docs/ProcGen/ModuleAuthoringHardeningReport.md`

### 8. Validation invariants and overlays

- Added invariant tests for determinism, connectivity, overlaps, portal validity, blocker safety, and marker safety.
- Added generation debug overlays for graph/layout/connectors/population diagnostics.

## Visual Style Preservation

- Hardening changes preserve existing building style and reuse existing project assets:
  - `Assets/arhitektura/Door.prefab`
  - `Assets/ProcGen/Connectors/OpenFrameConnector.prefab`
  - `Assets/ProcGen/Blockers/WallPanelBlocker.prefab`
- No replacement with generic external dungeon art.

## Original Asset Safety

- No destructive rewrites were made to original base building assets as part of this hardening pass.
- Changes are additive around procgen-specific scripts, metadata assets, and editor utilities.

## Known Limitations

- Batch report generation depends on running editor menu tools in Unity.
- Full visual verification still requires in-editor screenshot/play validation.
- Hazard/light/audio prefab slots are optional; if unset, fallback runtime objects are still used.

