---
name: facility-procgen
description: Build and maintain the Escape Block 9 Unity procedural facility generation system. Use when Codex works on DunGen-style indoor generation, Tile prefabs, Doorways, DoorwaySockets, DungeonFlow planning, occupancy validation, connector/blocker passes, population passes, seed replay, debug tools, or Unity MCP automation for this project's modular facility pipeline.
---

# Facility Procgen

Use this skill to implement or review the procedural indoor facility generator for Escape Block 9.

## Required Context

Read the project root `AGENTS.md` first. It defines the global routing and project-level constraints.

Read `.codex/agents/facility-procgen-agent.md` when planning or implementing multi-step work.

When editing Unity through MCP, also use the installed `unity-mcp-orchestrator` skill.

## Reference Routing

Load only the references needed for the task:

- `references/preservation.md`: use before changing scenes, prefabs, materials, generated architecture, or existing gameplay objects.
- `references/architecture.md`: use for generator architecture, class naming, data flow, layout solving, and deterministic seeds.
- `references/authoring.md`: use for Tile metadata, Doorways, sockets, occupancy bounds, blockers, connectors, and prefab wrapping.
- `references/population.md`: use for loot, enemies, lights, audio, hazards, objectives, exits, and runtime population passes.
- `references/validation.md`: use for tests, debug gizmos, seed replay, overlap checks, and Unity console/compile verification.

## Implementation Workflow

1. Inspect existing project assets before designing new structures.
2. Choose the smallest subsystem that satisfies the user request.
3. Prefer additive scripts/components/prefab variants.
4. Keep generator behavior deterministic from explicit seeds.
5. Validate through editor checks, tests, or screenshots as appropriate.
6. Report touched files and any remaining risks.

## Prompt Files

Use reusable prompts from `.codex/prompts/` when starting a focused thread:

- `facility-procgen-audit.prompt.md`
- `facility-procgen-implementation.prompt.md`
- `facility-procgen-unity-mcp.prompt.md`

