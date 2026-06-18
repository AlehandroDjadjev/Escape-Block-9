using System;
using System.Collections.Generic;
using EscapeBlock9.ProcGen.Authoring;
using EscapeBlock9.ProcGen.Data;
using UnityEngine;

namespace EscapeBlock9.ProcGen.Runtime
{
    public static class FacilityHorrorVisualPass
    {
        private const string OverlayShaderName = "EscapeBlock9/HorrorOverlayPulse";
        private const string VeinShaderName = "EscapeBlock9/HorrorVeinGlow";
        private const string GrimeMaterialResource = "Horror/Mat_HorrorGrime";
        private const string VeinMaterialResource = "Horror/Mat_HorrorVeins";
        private const string ShadowMaterialResource = "Horror/Mat_HorrorShadow";

        public static void Apply(Transform root, IReadOnlyDictionary<int, Tile> instanceTiles, int seed)
        {
            if (root == null || instanceTiles == null || instanceTiles.Count == 0)
            {
                return;
            }

            var random = new System.Random(seed ^ unchecked((int)0xA51D1E5));
            Material grimeMaterial = CreateMaterial(
                GrimeMaterialResource,
                OverlayShaderName,
                new Color(0.11f, 0.095f, 0.075f, 0.48f),
                "RuntimeHorrorGrime");
            Material seepMaterial = CreateMaterial(
                VeinMaterialResource,
                VeinShaderName,
                new Color(0.22f, 0.26f, 0.18f, 0.28f),
                "RuntimeHorrorSeep");
            Material shadowMaterial = CreateMaterial(
                ShadowMaterialResource,
                OverlayShaderName,
                new Color(0.02f, 0.024f, 0.03f, 0.58f),
                "RuntimeHorrorShadow");

            foreach (KeyValuePair<int, Tile> pair in instanceTiles)
            {
                Tile tile = pair.Value;
                if (tile == null)
                {
                    continue;
                }

                TreatRenderers(tile, random);
                if (!TryGetWorldBounds(tile, out Bounds bounds))
                {
                    continue;
                }

                ApplyTileOverlays(tile, bounds, random, grimeMaterial, seepMaterial, shadowMaterial);
            }

            AddWorldFogAndColor();
        }

        private static void TreatRenderers(Tile tile, System.Random random)
        {
            Renderer[] renderers = tile.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                {
                    continue;
                }

                Material[] sourceMaterials = renderer.sharedMaterials;
                Material[] materials = new Material[sourceMaterials.Length];
                for (int j = 0; j < materials.Length; j++)
                {
                    Material sourceMaterial = sourceMaterials[j];
                    if (sourceMaterial == null)
                    {
                        continue;
                    }

                    Material material = new Material(sourceMaterial)
                    {
                        name = $"{sourceMaterial.name}_HorrorRuntime",
                        hideFlags = HideFlags.DontSave
                    };
                    materials[j] = material;

                    float architecturalWeight = ResolveArchitecturalWeight(material.name, renderer.name);
                    if (architecturalWeight <= 0f)
                    {
                        continue;
                    }

                    Color original = ReadMaterialColor(material);
                    float sicklyNoise = 0.82f + (float)random.NextDouble() * 0.22f;
                    Color coldBase = new Color(
                        original.r * 0.32f + 0.05f,
                        original.g * 0.34f + 0.06f,
                        original.b * 0.4f + 0.07f,
                        original.a);
                    Color mildewTint = new Color(0.16f, 0.18f, 0.14f, original.a);
                    Color steelTint = new Color(0.11f, 0.13f, 0.16f, original.a);
                    Color target = Color.Lerp(coldBase, steelTint, 0.45f * architecturalWeight);
                    target = Color.Lerp(target, mildewTint, 0.3f + ((float)random.NextDouble() * 0.18f));
                    target *= sicklyNoise;
                    target.a = original.a;
                    WriteMaterialColor(material, Color.Lerp(original, target, Mathf.Clamp01(0.86f * architecturalWeight)));

                    if (material.HasProperty("_Smoothness"))
                    {
                        material.SetFloat("_Smoothness", Mathf.Lerp(material.GetFloat("_Smoothness"), 0.58f, 0.42f * architecturalWeight));
                    }

                    if (material.HasProperty("_Metallic"))
                    {
                        material.SetFloat("_Metallic", Mathf.Lerp(material.GetFloat("_Metallic"), 0.12f, 0.28f * architecturalWeight));
                    }
                }

                renderer.sharedMaterials = materials;
            }
        }

        private static float ResolveArchitecturalWeight(string materialName, string rendererName)
        {
            string combined = $"{materialName} {rendererName}".ToLowerInvariant();
            if (combined.Contains("wall") || combined.Contains("floor") || combined.Contains("ceiling") ||
                combined.Contains("stairs") || combined.Contains("corridor"))
            {
                return 1f;
            }

            if (combined.Contains("bathroom") || combined.Contains("metal") || combined.Contains("fixture") ||
                combined.Contains("door") || combined.Contains("window"))
            {
                return 0.65f;
            }

            if (combined.Contains("desk") || combined.Contains("chair") || combined.Contains("shelf") ||
                combined.Contains("counter") || combined.Contains("wood"))
            {
                return 0.35f;
            }

            return 0f;
        }

        private static Color ReadMaterialColor(Material material)
        {
            if (material.HasProperty("_BaseColor"))
            {
                return material.GetColor("_BaseColor");
            }

            if (material.HasProperty("_Color"))
            {
                return material.GetColor("_Color");
            }

            return Color.white;
        }

        private static void WriteMaterialColor(Material material, Color color)
        {
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", color);
            }
        }

        private static bool TryGetWorldBounds(Tile tile, out Bounds bounds)
        {
            bounds = default;
            Renderer[] renderers = tile.GetComponentsInChildren<Renderer>(true);
            bool hasBounds = false;
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            return hasBounds && bounds.size.x > 0.25f && bounds.size.z > 0.25f;
        }

        private static void ApplyTileOverlays(
            Tile tile,
            Bounds bounds,
            System.Random random,
            Material grimeMaterial,
            Material seepMaterial,
            Material shadowMaterial)
        {
            int count = ResolveOverlayCount(tile.Category);
            for (int i = 0; i < count; i++)
            {
                float pick = (float)random.NextDouble();
                if (pick < 0.34f)
                {
                    AddWallStain(tile.transform, bounds, random, grimeMaterial, i);
                }
                else if (pick < 0.58f)
                {
                    AddFloorShadow(tile.transform, bounds, random, shadowMaterial, i);
                }
                else if (pick < 0.82f)
                {
                    AddCeilingLeak(tile.transform, bounds, random, seepMaterial, i);
                }
                else
                {
                    AddCornerShadow(tile.transform, bounds, random, shadowMaterial, i);
                }
            }
        }

        private static int ResolveOverlayCount(TileCategory category)
        {
            switch (category)
            {
                case TileCategory.Room:
                    return 6;
                case TileCategory.Special:
                case TileCategory.Exit:
                    return 7;
                case TileCategory.Stair:
                    return 4;
                case TileCategory.Corridor:
                    return 3;
                default:
                    return 2;
            }
        }

        private static void AddWallStain(Transform parent, Bounds bounds, System.Random random, Material material, int index)
        {
            bool xSide = random.NextDouble() > 0.5;
            bool maxSide = random.NextDouble() > 0.5;
            float y = Mathf.Lerp(bounds.min.y + 0.8f, bounds.max.y - 0.45f, (float)random.NextDouble());
            float x = xSide ? (maxSide ? bounds.max.x - 0.02f : bounds.min.x + 0.02f) : Mathf.Lerp(bounds.min.x, bounds.max.x, (float)random.NextDouble());
            float z = !xSide ? (maxSide ? bounds.max.z - 0.02f : bounds.min.z + 0.02f) : Mathf.Lerp(bounds.min.z, bounds.max.z, (float)random.NextDouble());
            float width = Mathf.Lerp(0.85f, Mathf.Max(1.1f, xSide ? bounds.size.z * 0.42f : bounds.size.x * 0.42f), (float)random.NextDouble());
            float height = Mathf.Lerp(0.85f, Mathf.Max(0.9f, bounds.size.y * 0.42f), (float)random.NextDouble());
            Vector3 normal = xSide
                ? (maxSide ? Vector3.left : Vector3.right)
                : (maxSide ? Vector3.back : Vector3.forward);
            Vector3 position = new Vector3(x, y, z) + normal * 0.03f;
            Quaternion rotation = Quaternion.LookRotation(-normal, Vector3.up) *
                                  Quaternion.Euler(0f, 0f, (float)random.NextDouble() * 14f - 7f);
            AddDecalQuad(parent, $"HorrorWallStain_{index}", position, rotation, new Vector2(width, height), material);
        }

        private static void AddFloorShadow(Transform parent, Bounds bounds, System.Random random, Material material, int index)
        {
            float x = Mathf.Lerp(bounds.min.x + 0.45f, bounds.max.x - 0.45f, (float)random.NextDouble());
            float z = Mathf.Lerp(bounds.min.z + 0.45f, bounds.max.z - 0.45f, (float)random.NextDouble());
            float sx = Mathf.Lerp(0.95f, Mathf.Max(1.2f, bounds.size.x * 0.56f), (float)random.NextDouble());
            float sz = Mathf.Lerp(0.75f, Mathf.Max(0.95f, bounds.size.z * 0.44f), (float)random.NextDouble());
            float yaw = (float)random.NextDouble() * 360f;
            Quaternion rotation = Quaternion.Euler(90f, yaw, 0f);
            AddDecalQuad(parent, $"HorrorFloorShadow_{index}", new Vector3(x, bounds.min.y + 0.03f, z), rotation, new Vector2(sx, sz), material);
        }

        private static void AddCeilingLeak(Transform parent, Bounds bounds, System.Random random, Material material, int index)
        {
            float x = Mathf.Lerp(bounds.min.x + 0.35f, bounds.max.x - 0.35f, (float)random.NextDouble());
            float z = Mathf.Lerp(bounds.min.z + 0.35f, bounds.max.z - 0.35f, (float)random.NextDouble());
            float width = Mathf.Lerp(0.7f, Mathf.Max(0.8f, bounds.size.x * 0.28f), (float)random.NextDouble());
            float depth = Mathf.Lerp(0.5f, Mathf.Max(0.65f, bounds.size.z * 0.24f), (float)random.NextDouble());
            float yaw = (float)random.NextDouble() * 360f;
            Quaternion rotation = Quaternion.Euler(-90f, yaw, (float)random.NextDouble() * 8f - 4f);
            AddDecalQuad(parent, $"HorrorCeilingLeak_{index}", new Vector3(x, bounds.max.y - 0.03f, z), rotation, new Vector2(width, depth), material);
        }

        private static void AddCornerShadow(Transform parent, Bounds bounds, System.Random random, Material material, int index)
        {
            bool useMinX = random.NextDouble() > 0.5;
            bool useMinZ = random.NextDouble() > 0.5;
            float x = useMinX ? bounds.min.x + 0.04f : bounds.max.x - 0.04f;
            float z = useMinZ ? bounds.min.z + 0.04f : bounds.max.z - 0.04f;
            float height = Mathf.Lerp(1.6f, Mathf.Max(1.8f, bounds.size.y * 0.82f), (float)random.NextDouble());
            float width = Mathf.Lerp(0.22f, 0.38f, (float)random.NextDouble());
            Vector3 diagonal = new Vector3(useMinX ? 1f : -1f, 0f, useMinZ ? 1f : -1f).normalized;
            Quaternion rotation = Quaternion.LookRotation(-diagonal, Vector3.up);
            AddDecalQuad(
                parent,
                $"HorrorCornerShadow_{index}",
                new Vector3(x, bounds.min.y + (height * 0.5f), z) + diagonal * 0.04f,
                rotation,
                new Vector2(width, height),
                material);
        }

        private static void AddDecalQuad(Transform parent, string name, Vector3 position, Quaternion rotation, Vector2 size, Material material)
        {
            GameObject primitive = GameObject.CreatePrimitive(PrimitiveType.Quad);
            primitive.name = name;
            primitive.transform.SetParent(parent, true);
            primitive.transform.SetPositionAndRotation(position, rotation);
            primitive.transform.localScale = new Vector3(size.x, size.y, 1f);

            Collider collider = primitive.GetComponent<Collider>();
            if (collider != null)
            {
                if (Application.isPlaying)
                {
                    UnityEngine.Object.Destroy(collider);
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(collider);
                }
            }

            Renderer renderer = primitive.GetComponent<Renderer>();
            if (renderer != null && material != null)
            {
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
        }

        private static Material CreateMaterial(string resourcePath, string shaderName, Color color, string name)
        {
            Material template = Resources.Load<Material>(resourcePath);
            if (template != null)
            {
                Material copy = new Material(template)
                {
                    name = name
                };
                ApplyMaterialProperties(copy, color);
                return copy;
            }

            Shader shader = Shader.Find(shaderName) ?? Shader.Find("Unlit/Color") ?? Shader.Find("Sprites/Default");
            Material material = new Material(shader)
            {
                name = name,
                color = color
            };
            ApplyMaterialProperties(material, color);
            return material;
        }

        private static void ApplyMaterialProperties(Material material, Color color)
        {
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", color);
            }

            if (material.HasProperty("_PulseSpeed"))
            {
                material.SetFloat("_PulseSpeed", 0.85f);
            }

            if (material.HasProperty("_NoiseScale"))
            {
                material.SetFloat("_NoiseScale", 18f);
            }
        }

        private static void AddWorldFogAndColor()
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.026f, 0.03f, 0.036f, 1f);
            RenderSettings.fogDensity = 0.012f;
        }
    }
}
