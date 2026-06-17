# Population Passes

Use this reference for loot, enemies, lights, audio, hazards, objectives, and exit-related gameplay.

## Pass Order

Run population after the physical layout is resolved and validated:

1. Connectors and blockers
2. Required objectives and critical path objects
3. Fire exits, stairs, and portal/non-Euclidean links
4. Loot/items
5. Enemies/NPCs
6. Lighting/audio atmosphere
7. Hazards
8. Final navmesh/nav links

## Deterministic Population

- Derive sub-seeds from the run seed and pass name.
- Keep marker selection deterministic.
- Avoid placing required progression objects in unreachable branches.

## Lighting

- Preserve the current lighting style unless the user asks otherwise.
- Prefer marker-driven light placement over arbitrary coordinates.
- Keep flicker/spark behavior isolated in reusable components.

## Dialogue/Audio

The Django backend in `backend/` manages dialogue audio data. For dialogue work, read `backend/README.md` and preserve the JSON tree shape used by Unity.

