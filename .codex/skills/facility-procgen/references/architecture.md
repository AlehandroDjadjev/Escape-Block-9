# Generator Architecture

Use this reference for system design, class names, data flow, deterministic seeds, and layout solving.

## Target Data Flow

```text
Seeded Run Config
  -> Existing Building Inventory
  -> Module Catalog + Metadata
  -> Logical Graph Planner
  -> Candidate Filter / Connection Rules
  -> Snap Transform Solver
  -> Collision / Occupancy Validator
  -> Resolved Physical Layout
  -> Connector + Blocker Pass
  -> Fire Exit / Stair / Portal Pass
  -> Population Passes
  -> Runtime NavMesh / Links
  -> Debug + Seed Replay + Tests
```

## Core Concepts

- `Tile`: reusable room, corridor, stair, lobby, bathroom, shop, or connector module.
- `Doorway`: transform anchor used to snap one Tile to another.
- `DoorwaySocket`: compatibility filter for Doorways.
- `DungeonFlow`: seeded logical plan for main path, branches, locks, exits, stairs, and special rooms.
- `TileConnectionRule`: filter that determines whether two Doorways/Tiles may connect.
- `OccupancyBounds`: authored physical volume used for overlap checks.
- `Connector`: object enabled or spawned for a used doorway pair.
- `Blocker`: object enabled or spawned for an unused doorway.

## Determinism

- Pass an explicit seed into every planning, solving, and population phase.
- Avoid `UnityEngine.Random` global state for generator decisions unless the state is locally initialized/restored.
- Log or expose enough seed/config data for replay.

## Suggested Namespaces

Use existing namespaces if the project establishes them. Otherwise prefer:

- `EscapeBlock9.Procgen`
- `EscapeBlock9.Procgen.Authoring`
- `EscapeBlock9.Procgen.Runtime`
- `EscapeBlock9.Procgen.Debugging`

