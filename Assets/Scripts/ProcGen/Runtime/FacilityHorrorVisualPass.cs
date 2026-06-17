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
                new Color(0.095f, 0.004f, 0.012f, 0.64f),
                "RuntimeHorrorGrime");
            Material veinMaterial = CreateMaterial(
                VeinMaterialResource,
                VeinShaderName,
                new Color(0.72f, 0.018f, 0.004f, 0.46f),
                "RuntimeHorrorVeins");
            Material shadowMaterial = CreateMaterial(
                ShadowMaterialResource,
                OverlayShaderName,
                new Color(0.005f, 0.006f, 0.008f, 0.78f),
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

                ApplyTileOverlays(tile, bounds, random, grimeMaterial, veinMaterial, shadowMaterial);
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
                    float sicklyNoise = 0.75f + (float)random.NextDouble() * 0.35f;
                    Color target = new Color(
                        original.r * 0.33f + 0.055f,
                        original.g * 0.24f + 0.025f,
                        original.b * 0.22f + 0.035f,
                        original.a);
                    target = Color.Lerp(target, new Color(0.18f, 0.035f, 0.055f, original.a), 0.22f * architecturalWeight);
                    target *= sicklyNoise;
                    target.a = original.a;
                    WriteMaterialColor(material, Color.Lerp(original, target, Mathf.Clamp01(0.82f * architecturalWeight)));

                    if (material.HasProperty("_Smoothness"))
                    {
                        material.SetFloat("_Smoothness", Mathf.Lerp(material.GetFloat("_Smoothness"), 0.82f, 0.45f * architecturalWeight));
                    }

                    if (material.HasProperty("_Metallic"))
                    {
                        material.SetFloat("_Metallic", Mathf.Lerp(material.GetFloat("_Metallic"), 0.08f, 0.35f * architecturalWeight));
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
            Material veinMaterial,
            Material shadowMaterial)
        {
            int count = ResolveOverlayCount(tile.Category);
            for (int i = 0; i < count; i++)
            {
                float pick = (float)random.NextDouble();
                if (pick < 0.46f)
                {
                    AddWallStain(tile.transform, bounds, random, grimeMaterial, i);
                }
                else if (pick < 0.78f)
                {
                    AddFloorShadow(tile.transform, bounds, random, shadowMaterial, i);
                }
                else
                {
                    AddVein(tile.transform, bounds, random, veinMaterial, i);
                }
            }
        }

        private static int ResolveOverlayCount(TileCategory category)
        {
            switch (category)
            {
                case TileCategory.Room:
                    return 4;
                case TileCategory.Special:
                case TileCategory.Exit:
                    return 5;
                case TileCategory.Stair:
                    return 3;
                case TileCategory.Corridor:
                    return 2;
                default:
                    return 1;
            }
        }

        private static void AddWallStain(Transform parent, Bounds bounds, System.Random random, Material material, int index)
        {
            bool xSide = random.NextDouble() > 0.5;
            bool maxSide = random.NextDouble() > 0.5;
            float y = Mathf.Lerp(bounds.min.y + 0.8f, bounds.max.y - 0.45f, (float)random.NextDouble());
            float x = xSide ? (maxSide ? bounds.max.x - 0.018f : bounds.min.x + 0.018f) : Mathf.Lerp(bounds.min.x, bounds.max.x, (float)random.NextDouble());
            float z = !xSide ? (maxSide ? bounds.max.z - 0.018f : bounds.min.z + 0.018f) : Mathf.Lerp(bounds.min.z, bounds.max.z, (float)random.NextDouble());
            float width = Mathf.Lerp(0.7f, Mathf.Max(0.8f, xSide ? bounds.size.z * 0.34f : bounds.size.x * 0.34f), (float)random.NextDouble());
            float height = Mathf.Lerp(0.65f, Mathf.Max(0.7f, bounds.size.y * 0.58f), (float)random.NextDouble());
            Vector3 scale = xSide ? new Vector3(0.035f, height, width) : new Vector3(width, height, 0.035f);
            AddPrimitive(parent, $"HorrorWallStain_{index}", new Vector3(x, y, z), Quaternion.identity, scale, material);
        }

        private static void AddFloorShadow(Transform parent, Bounds bounds, System.Random random, Material material, int index)
        {
            float x = Mathf.Lerp(bounds.min.x + 0.45f, bounds.max.x - 0.45f, (float)random.NextDouble());
            float z = Mathf.Lerp(bounds.min.z + 0.45f, bounds.max.z - 0.45f, (float)random.NextDouble());
            float sx = Mathf.Lerp(0.75f, Mathf.Max(0.9f, bounds.size.x * 0.5f), (float)random.NextDouble());
            float sz = Mathf.Lerp(0.45f, Mathf.Max(0.7f, bounds.size.z * 0.34f), (float)random.NextDouble());
            Quaternion rotation = Quaternion.Euler(0f, (float)random.NextDouble() * 360f, 0f);
            AddPrimitive(parent, $"HorrorFloorShadow_{index}", new Vector3(x, bounds.min.y + 0.026f, z), rotation, new Vector3(sx, 0.035f, sz), material);
        }

        private static void AddVein(Transform parent, Bounds bounds, System.Random random, Material material, int index)
        {
            bool xAxis = random.NextDouble() > 0.5;
            float x = Mathf.Lerp(bounds.min.x + 0.3f, bounds.max.x - 0.3f, (float)random.NextDouble());
            float z = Mathf.Lerp(bounds.min.z + 0.3f, bounds.max.z - 0.3f, (float)random.NextDouble());
            float y = Mathf.Lerp(bounds.min.y + 0.12f, bounds.max.y - 0.35f, (float)random.NextDouble());
            float length = Mathf.Lerp(1.2f, Mathf.Max(1.3f, (xAxis ? bounds.size.x : bounds.size.z) * 0.42f), (float)random.NextDouble());
            Vector3 scale = xAxis ? new Vector3(length, 0.025f, 0.055f) : new Vector3(0.055f, 0.025f, length);
            Quaternion rotation = Quaternion.Euler((float)random.NextDouble() * 10f - 5f, (float)random.NextDouble() * 18f - 9f, (float)random.NextDouble() * 8f - 4f);
            AddPrimitive(parent, $"HorrorVein_{index}", new Vector3(x, y, z), rotation, scale, material);
        }

        private static void AddPrimitive(Transform parent, string name, Vector3 position, Quaternion rotation, Vector3 scale, Material material)
        {
            GameObject primitive = GameObject.CreatePrimitive(PrimitiveType.Cube);
            primitive.name = name;
            primitive.transform.SetPositionAndRotation(position, rotation);
            primitive.transform.localScale = scale;
            primitive.transform.SetParent(parent, true);

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
            RenderSettings.fogColor = new Color(0.018f, 0.006f, 0.011f, 1f);
            RenderSettings.fogDensity = 0.034f;
        }
    }
}
