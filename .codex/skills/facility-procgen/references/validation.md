# Validation And Debugging

Use this reference for tests, debug gizmos, seed replay, overlap checks, and Unity verification.

## Required Checks

- Check Unity compilation after script edits.
- Check Unity console after scene/prefab/script changes.
- Verify generated layouts from fixed seeds.
- Validate occupancy overlaps.
- Validate all used Doorways connect compatible sockets.
- Validate unused Doorways have blockers.
- Validate critical path reachability.
- Validate exits and stairs are reachable.

## Debug Views

Prefer debug gizmos or editor tooling for:

- Tile bounds
- Doorway forward vectors
- Socket labels
- Occupancy overlap volumes
- Logical graph edges
- Physical connections
- Main path vs branch paths
- Spawn markers

## Seed Replay

Expose a run config containing:

- Seed
- Flow asset/config
- Module catalog version
- Population toggles
- Scene/build context

Report seed and config values when a generated layout fails.

## Unity MCP Verification

When using Unity MCP:

1. Read editor state.
2. Inspect hierarchy/components before mutation.
3. Apply changes.
4. Check compilation/console.
5. Capture screenshots when visual correctness matters.
6. Save only intentional scene edits.

