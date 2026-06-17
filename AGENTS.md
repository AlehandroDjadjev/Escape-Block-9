# AGENTS.md - Escape Block 9 Codex Routing

This repository is a Unity first-person facility project. Codex should preserve the existing building, rooms, corridors, props, materials, scripts, and gameplay style unless the user explicitly asks for replacement.

## Required Read Order

For procedural facility generation, modular room authoring, connector/socket work, layout validation, population passes, or related Unity automation, read these files in order:

1. `.codex/skills/facility-procgen/SKILL.md`
2. `.codex/agents/facility-procgen-agent.md`
3. The reference files named by the skill for the task:
   - `.codex/skills/facility-procgen/references/preservation.md`
   - `.codex/skills/facility-procgen/references/architecture.md`
   - `.codex/skills/facility-procgen/references/authoring.md`
   - `.codex/skills/facility-procgen/references/population.md`
   - `.codex/skills/facility-procgen/references/validation.md`
4. A prompt from `.codex/prompts/` only when the user asks for that workflow or a thread needs a reusable prompt.

When changing Unity scenes, prefabs, scripts, or assets through MCP, also use the installed `unity-mcp-orchestrator` skill and check Unity console/compiler state after changes.

## Core Role

Act as the implementation agent for a Unity first-person facility procedural generation system inspired by DunGen-style modular indoor generation.

Build a deterministic runtime generator that uses the existing building as the source of truth for:

- Visual style
- Scale
- Gameplay affordances
- Scene organization
- Existing prefab/material conventions

Do not replace the current architecture with a generic dungeon kit.

## Primary Objective

Implement a procedural indoor facility generator with these capabilities:

1. Reuse current room/interior prefabs and scene objects as modular room modules.
2. Add metadata, connectors, sockets, occupancy bounds, spawn markers, and validation tools around existing content.
3. Generate deterministic layouts from a seed.
4. Build a logical facility graph first, then solve it into a physical 3D layout.
5. Snap rooms/corridors using doorway/connector anchors.
6. Prevent physical overlaps using authored occupancy bounds and validation.
7. Support main path, branches, dead ends, loops, stairs/vertical links, fire exits, and optional portal/non-Euclidean edges.
8. Run deterministic population phases for loot, enemies, lights, audio, hazards, objectives, and exit-related gameplay.
9. Bake or update runtime navigation after generation.
10. Provide seed replay, debug gizmos, test scenes, and automated validation.

## DunGen Vocabulary

Use this vocabulary in class names, docs, and comments unless the existing project already has stronger names:

- A room prefab is a `Tile`.
- A doorway or connection point is a `Doorway`.
- Doorway compatibility is filtered by `DoorwaySocket`.
- Used doorway pairs spawn or enable connectors such as open doorframes.
- Unused doorways spawn or enable blockers such as walls, closed doors, rubble, or sealed panels.
- A `DungeonFlow` controls main path, branches, and special-room placement.
- Tile connection rules decide which modules can connect.
- Post-processing/runtime events handle gameplay and generation cleanup.

## Architecture Target

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

