# Existing Building Preservation

Use this reference before modifying Unity scenes, prefabs, materials, generated architecture, or gameplay objects.

## Preservation Order

1. Reuse existing prefabs directly.
2. If direct reuse is unsafe, create prefab variants that add procedural metadata/components.
3. If a scene-only object is not a prefab, create a prefab variant from it without destructively changing the source scene.
4. Add invisible authoring helpers such as connector anchors, occupancy volumes, and spawn markers.
5. Add missing blockers/connectors using existing meshes/materials where possible.
6. Create new visual geometry only when required for generation correctness.
7. Match existing scale, material style, naming convention, lighting approach, and gameplay architecture.

## Hard Rules

- Do not mass-rename existing content.
- Do not mass-reparent existing scene hierarchies.
- Do not delete or rewrite art assets without an explicit reason.
- Do not replace the current building with generic dungeon-kit art.
- Keep user-authored scene changes unless the user explicitly asks to revert them.

## Preferred Unity Pattern

- Add metadata components to prefab variants or wrapper GameObjects.
- Keep generated authoring helpers visually unobtrusive.
- Use existing materials from `Assets/arhitektura/Generated/` where possible.
- Save scenes only after intentional changes and verification.

