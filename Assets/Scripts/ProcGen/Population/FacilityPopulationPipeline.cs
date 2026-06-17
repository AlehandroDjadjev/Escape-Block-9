using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using EscapeBlock9.ProcGen.Authoring;
using EscapeBlock9.ProcGen.Data;
using EscapeBlock9.ProcGen.Placement;
using EscapeBlock9.ProcGen.Planning;
using EscapeBlock9.ProcGen.Runtime;
using UnityEngine;

namespace EscapeBlock9.ProcGen.Population
{
    public enum PopulationMarkerStatus
    {
        Unused,
        Used,
        SkippedRule,
        SkippedBlocked
    }

    [Serializable]
    public struct PopulationMarkerUsage
    {
        public string MarkerId;
        public SpawnMarkerKind Kind;
        public int NodeId;
        public float Danger;
        public Vector3 WorldPosition;
        public PopulationMarkerStatus Status;
        public string Reason;
        public string SpawnedObjectName;
    }

    [Serializable]
    public struct PopulationSpawnRecord
    {
        public string Phase;
        public int NodeId;
        public string MarkerId;
        public string SpawnKind;
        public string SpawnedObjectName;
        public Vector3 WorldPosition;
    }

    [Serializable]
    public sealed class FacilityPopulationSettings
    {
        [Header("Prefabs")]
        public GameObject LootPrefab;
        public GameObject EnemyPrefab;
        public GameObject ObjectivePrefab;
        public GameObject HazardPrefab;
        public GameObject LightFixturePrefab;
        public GameObject AudioEmitterPrefab;

        [Header("Spawn Rules")]
        [Range(0f, 1f)] public float BaseLootChance = 0.38f;
        [Range(0f, 1f)] public float LootDepthBonus = 0.5f;
        [Range(0f, 1f)] public float BaseEnemyChance = 0.06f;
        [Range(0f, 1f)] public float EnemyDepthBonus = 0.68f;
        [Range(0f, 1f)] public float BaseHazardChance = 0.1f;
        [Range(0f, 1f)] public float HazardDepthBonus = 0.45f;
        [Range(0f, 1f)] public float LightChance = 0.5f;
        [Range(0f, 1f)] public float AudioAmbienceChance = 0.58f;
        [Range(0f, 1f)] public float FireExitRewardChance = 0.75f;
        [Range(0f, 1f)] public float FireExitRiskChance = 0.62f;
        public float BlockerClearanceRadius = 1.05f;
        public float SpawnHeightOffset = 0.02f;

        [Header("Generated Light Settings")]
        [Min(0f)] public float MinLightIntensity = 0.55f;
        [Min(0f)] public float MaxLightIntensity = 1.05f;
        [Min(0.1f)] public float LightRange = 7f;
        public Color LightColor = new Color(0.62f, 0.78f, 0.9f, 1f);
        public LightShadows LightShadows = LightShadows.None;
        public bool EnableLightFlicker = true;

        [Header("Debug")]
        public bool EnableVerbosePopulationLogs = true;
    }

    public sealed class FacilityPopulationReport
    {
        public FacilityPopulationReport(
            int seed,
            IReadOnlyList<PopulationMarkerUsage> markerUsage,
            IReadOnlyList<PopulationSpawnRecord> spawns,
            IReadOnlyList<string> errors)
        {
            Seed = seed;
            MarkerUsage = markerUsage;
            Spawns = spawns;
            Errors = errors;
        }

        public int Seed { get; }
        public IReadOnlyList<PopulationMarkerUsage> MarkerUsage { get; }
        public IReadOnlyList<PopulationSpawnRecord> Spawns { get; }
        public IReadOnlyList<string> Errors { get; }

        public string ToDebugString()
        {
            int used = 0;
            int blocked = 0;
            int skippedRule = 0;
            for (int i = 0; i < MarkerUsage.Count; i++)
            {
                switch (MarkerUsage[i].Status)
                {
                    case PopulationMarkerStatus.Used:
                        used++;
                        break;
                    case PopulationMarkerStatus.SkippedBlocked:
                        blocked++;
                        break;
                    case PopulationMarkerStatus.SkippedRule:
                        skippedRule++;
                        break;
                }
            }

            var builder = new StringBuilder();
            builder.AppendLine($"Population report seed={Seed}");
            builder.AppendLine($"  markers={MarkerUsage.Count} used={used} skippedRule={skippedRule} skippedBlocked={blocked}");
            builder.AppendLine($"  spawns={Spawns.Count} errors={Errors.Count}");
            for (int i = 0; i < Errors.Count; i++)
            {
                builder.AppendLine($"  error[{i + 1}] {Errors[i]}");
            }

            return builder.ToString();
        }
    }

    [DisallowMultipleComponent]
    public sealed class FacilityPopulationMetadata : MonoBehaviour
    {
        [SerializeField] private List<PopulationMarkerUsage> markerUsage = new List<PopulationMarkerUsage>();
        [SerializeField] private List<PopulationSpawnRecord> spawns = new List<PopulationSpawnRecord>();
        [SerializeField] private List<string> errors = new List<string>();

        public IReadOnlyList<PopulationMarkerUsage> MarkerUsage => markerUsage;
        public IReadOnlyList<PopulationSpawnRecord> Spawns => spawns;
        public IReadOnlyList<string> Errors => errors;

        public void Apply(FacilityPopulationReport report)
        {
            markerUsage.Clear();
            spawns.Clear();
            errors.Clear();
            if (report == null)
            {
                return;
            }

            markerUsage.AddRange(report.MarkerUsage);
            spawns.AddRange(report.Spawns);
            errors.AddRange(report.Errors);
        }
    }

    [ExecuteAlways]
    [RequireComponent(typeof(FacilityPopulationMetadata))]
    public sealed class FacilityPopulationDebugOverlay : MonoBehaviour
    {
        [SerializeField] private float markerRadius = 0.12f;
        [SerializeField] private bool drawUsedMarkers = true;
        [SerializeField] private bool drawUnusedMarkers = true;
        [SerializeField] private bool drawBlockedMarkers = true;
        [SerializeField] private Color usedColor = new Color(0.2f, 1f, 0.35f, 1f);
        [SerializeField] private Color unusedColor = new Color(0.9f, 0.25f, 0.25f, 1f);
        [SerializeField] private Color blockedColor = new Color(1f, 0.65f, 0.1f, 1f);
        private FacilityPopulationMetadata metadata;

        private void OnValidate()
        {
            markerRadius = Mathf.Max(0.02f, markerRadius);
            metadata = GetComponent<FacilityPopulationMetadata>();
        }

        private void OnDrawGizmos()
        {
            if (metadata == null)
            {
                metadata = GetComponent<FacilityPopulationMetadata>();
            }

            if (metadata == null)
            {
                return;
            }

            for (int i = 0; i < metadata.MarkerUsage.Count; i++)
            {
                PopulationMarkerUsage marker = metadata.MarkerUsage[i];
                switch (marker.Status)
                {
                    case PopulationMarkerStatus.Used:
                        if (!drawUsedMarkers)
                        {
                            continue;
                        }
                        Gizmos.color = usedColor;
                        break;
                    case PopulationMarkerStatus.SkippedBlocked:
                        if (!drawBlockedMarkers)
                        {
                            continue;
                        }
                        Gizmos.color = blockedColor;
                        break;
                    default:
                        if (!drawUnusedMarkers)
                        {
                            continue;
                        }
                        Gizmos.color = unusedColor;
                        break;
                }

                Gizmos.DrawSphere(marker.WorldPosition, markerRadius);
                Vector3 dir = Vector3.up * (markerRadius * 2.2f);
                Gizmos.DrawLine(marker.WorldPosition, marker.WorldPosition + dir);
            }
        }
    }

    [DisallowMultipleComponent]
    public sealed class ProcGenHazardTrigger : MonoBehaviour
    {
        [SerializeField] private string hazardId = "procgen_hazard";
        [SerializeField] private string message = "Hazard encountered.";

        private void OnTriggerEnter(Collider other)
        {
            if (other == null || !other.CompareTag("Player"))
            {
                return;
            }

            Debug.LogWarning($"[{hazardId}] {message}");
        }
    }

    public sealed class FacilityPopulationPipeline
    {
        private static readonly Type ItemPickupType = Type.GetType("ItemPickup, Assembly-CSharp");
        private static readonly Type FlickeringFluorescentType = Type.GetType("FlickeringFluorescent, Assembly-CSharp");
        private static readonly FieldInfo ItemIdField = ItemPickupType?.GetField("itemId", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo ItemDisplayNameField = ItemPickupType?.GetField("itemDisplayName", BindingFlags.Instance | BindingFlags.NonPublic);
        private readonly FacilityPopulationSettings settings;

        public FacilityPopulationPipeline(FacilityPopulationSettings settings)
        {
            this.settings = settings ?? new FacilityPopulationSettings();
        }

        public FacilityPopulationReport Populate(
            Transform populationRoot,
            FacilityGraph graph,
            ResolvedFacilityLayout layout,
            IReadOnlyDictionary<int, Tile> instantiatedTilesByNode,
            PostLayoutConnectionMetadata connectionMetadata)
        {
            if (populationRoot == null)
            {
                throw new ArgumentNullException(nameof(populationRoot));
            }

            if (graph == null)
            {
                throw new ArgumentNullException(nameof(graph));
            }

            if (layout == null)
            {
                throw new ArgumentNullException(nameof(layout));
            }

            if (instantiatedTilesByNode == null)
            {
                throw new ArgumentNullException(nameof(instantiatedTilesByNode));
            }

            var errors = new List<string>();
            var markerUsages = GatherMarkers(graph, layout, instantiatedTilesByNode, connectionMetadata, errors);
            var spawnRecords = new List<PopulationSpawnRecord>();
            var streams = new NamedRandomStreams(layout.Seed);
            int startNodeId = graph.MainPathNodeIds.Count > 0 ? graph.MainPathNodeIds[0] : -1;

            ResolvePlayerStart(markerUsages, startNodeId, streams.Stream("population/player-start"), errors);
            ResolveObjectives(populationRoot, markerUsages, spawnRecords, streams.Stream("population/objectives"), errors);
            ResolveFireExitRiskReward(populationRoot, markerUsages, spawnRecords, streams.Stream("population/fire-exits"), connectionMetadata, errors);
            ResolveLoot(populationRoot, markerUsages, spawnRecords, streams.Stream("population/loot"));
            ResolveEnemies(populationRoot, markerUsages, spawnRecords, streams.Stream("population/enemies"));
            ResolveHazards(populationRoot, markerUsages, spawnRecords, streams.Stream("population/hazards"));
            ResolveLights(populationRoot, markerUsages, spawnRecords, streams.Stream("population/lights"));
            ResolveAudio(populationRoot, markerUsages, spawnRecords, streams.Stream("population/audio"));

            if (settings.EnableVerbosePopulationLogs)
            {
                Debug.Log(new FacilityPopulationReport(layout.Seed, markerUsages, spawnRecords, errors).ToDebugString());
            }

            return new FacilityPopulationReport(layout.Seed, markerUsages, spawnRecords, errors);
        }

        private List<PopulationMarkerUsage> GatherMarkers(
            FacilityGraph graph,
            ResolvedFacilityLayout layout,
            IReadOnlyDictionary<int, Tile> instantiatedTilesByNode,
            PostLayoutConnectionMetadata connectionMetadata,
            ICollection<string> errors)
        {
            var usage = new List<PopulationMarkerUsage>();
            var mainPathIndex = new Dictionary<int, int>();
            for (int i = 0; i < graph.MainPathNodeIds.Count; i++)
            {
                mainPathIndex[graph.MainPathNodeIds[i]] = i;
            }

            Dictionary<int, PlacedTile> placedByNode = BuildPlacedByNode(layout);
            for (int i = 0; i < graph.Nodes.Count; i++)
            {
                FacilityGraphNode node = graph.Nodes[i];
                if (!instantiatedTilesByNode.TryGetValue(node.Id, out Tile tile) || tile == null)
                {
                    errors.Add($"Missing instantiated tile for node {node.Id}.");
                    continue;
                }

                SpawnMarker[] markers = tile.GetSpawnMarkers();
                bool hasLightMarker = false;
                for (int markerIndex = 0; markerIndex < markers.Length; markerIndex++)
                {
                    SpawnMarker marker = markers[markerIndex];
                    if (marker == null)
                    {
                        continue;
                    }

                    hasLightMarker |= marker.Kind == SpawnMarkerKind.Light;
                    Vector3 worldPosition = marker.transform.position + Vector3.up * settings.SpawnHeightOffset;
                    var state = new PopulationMarkerUsage
                    {
                        MarkerId = NormalizeMarkerId(marker, node.Id, markerIndex),
                        Kind = marker.Kind,
                        NodeId = node.Id,
                        Danger = ComputeDanger(node, mainPathIndex, graph.MainPathNodeIds.Count),
                        WorldPosition = worldPosition,
                        Status = PopulationMarkerStatus.Unused,
                        Reason = string.Empty,
                        SpawnedObjectName = string.Empty
                    };

                    if (marker.RequireCriticalPath && !mainPathIndex.ContainsKey(node.Id))
                    {
                        state.Status = PopulationMarkerStatus.SkippedRule;
                        state.Reason = "Requires critical path.";
                    }
                    else if (IsBlockedByDoorway(connectionMetadata, worldPosition))
                    {
                        state.Status = PopulationMarkerStatus.SkippedBlocked;
                        state.Reason = "Near blocked doorway.";
                    }
                    else if (Physics.CheckSphere(worldPosition, settings.BlockerClearanceRadius * 0.45f, ~0, QueryTriggerInteraction.Ignore))
                    {
                        if (LooksLikeBlocker(worldPosition, settings.BlockerClearanceRadius * 0.5f))
                        {
                            state.Status = PopulationMarkerStatus.SkippedBlocked;
                            state.Reason = "Physics overlap with blocker.";
                        }
                    }

                    usage.Add(state);
                }

                if (!hasLightMarker && placedByNode.TryGetValue(node.Id, out PlacedTile placedTile))
                {
                    usage.Add(CreateSyntheticLightMarker(node, placedTile, ComputeDanger(node, mainPathIndex, graph.MainPathNodeIds.Count)));
                }
            }

            return usage;
        }

        private PopulationMarkerUsage CreateSyntheticLightMarker(FacilityGraphNode node, PlacedTile placedTile, float danger)
        {
            Bounds bounds = BuildTileBounds(placedTile);
            Vector3 position = new Vector3(bounds.center.x, bounds.max.y - 0.2f, bounds.center.z);
            return new PopulationMarkerUsage
            {
                MarkerId = $"auto_ceiling_light_{node.Id}",
                Kind = SpawnMarkerKind.Light,
                NodeId = node.Id,
                Danger = danger,
                WorldPosition = position + Vector3.up * settings.SpawnHeightOffset,
                Status = PopulationMarkerStatus.Unused,
                Reason = string.Empty,
                SpawnedObjectName = string.Empty
            };
        }

        private static Bounds BuildTileBounds(PlacedTile tile)
        {
            if (tile != null && tile.OccupancyBoxes.Count > 0)
            {
                Bounds bounds = tile.OccupancyBoxes[0].Bounds;
                for (int i = 1; i < tile.OccupancyBoxes.Count; i++)
                {
                    bounds.Encapsulate(tile.OccupancyBoxes[i].Bounds);
                }

                return bounds;
            }

            return new Bounds(tile != null ? tile.Position : Vector3.zero, new Vector3(3f, 3.5f, 3f));
        }

        private void ResolvePlayerStart(
            IList<PopulationMarkerUsage> markers,
            int startNodeId,
            SeededRandom random,
            ICollection<string> errors)
        {
            int chosen = ChooseMarker(markers, SpawnMarkerKind.PlayerStart, random, m => m.NodeId == startNodeId);
            if (chosen < 0)
            {
                errors.Add("No usable PlayerStart marker found.");
                return;
            }

            SetUsed(markers, chosen, "player-start", null);
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player == null)
            {
                errors.Add("Player start marker selected, but no object with tag 'Player' exists.");
                return;
            }

            Vector3 position = markers[chosen].WorldPosition;
            player.transform.position = position;
        }

        private void ResolveObjectives(
            Transform root,
            IList<PopulationMarkerUsage> markers,
            ICollection<PopulationSpawnRecord> spawns,
            SeededRandom random,
            ICollection<string> errors)
        {
            int objectiveMarker = ChooseMarker(markers, SpawnMarkerKind.Objective, random, m => m.Danger >= 0.2f);
            if (objectiveMarker < 0)
            {
                objectiveMarker = ChooseMarker(markers, SpawnMarkerKind.Loot, random, m => m.Danger >= 0.35f);
            }

            if (objectiveMarker < 0)
            {
                errors.Add("Objective phase skipped: no suitable marker found.");
                return;
            }

            GameObject objective = SpawnGameplayPrefab(root, settings.ObjectivePrefab != null ? settings.ObjectivePrefab : settings.LootPrefab, markers[objectiveMarker], "objective_key");
            if (objective == null)
            {
                errors.Add("Objective phase failed: objective prefab is missing.");
                return;
            }

            TryConfigureItemPickup(objective, "objective_exit_key", "Exit Authorization Key");
            SetUsed(markers, objectiveMarker, "objective", objective.name);
            spawns.Add(SpawnRecord("objective", markers[objectiveMarker], "objective", objective.name));
        }

        private void ResolveFireExitRiskReward(
            Transform root,
            IList<PopulationMarkerUsage> markers,
            ICollection<PopulationSpawnRecord> spawns,
            SeededRandom random,
            PostLayoutConnectionMetadata connectionMetadata,
            ICollection<string> errors)
        {
            if (connectionMetadata == null || connectionMetadata.FireExits.Count == 0)
            {
                return;
            }

            for (int i = 0; i < connectionMetadata.FireExits.Count; i++)
            {
                FireExitRuntimeMetadata fireExit = connectionMetadata.FireExits[i];
                int localLoot = ChooseMarker(markers, SpawnMarkerKind.Loot, random, m => m.NodeId == fireExit.NodeId);
                int localHazard = ChooseMarker(markers, SpawnMarkerKind.Hazard, random, m => m.NodeId == fireExit.NodeId);
                if (localHazard < 0)
                {
                    localHazard = ChooseMarker(markers, SpawnMarkerKind.Debug, random, m => m.NodeId == fireExit.NodeId);
                }

                if (localLoot >= 0 && random.Chance(settings.FireExitRewardChance))
                {
                    GameObject reward = SpawnGameplayPrefab(root, settings.LootPrefab, markers[localLoot], "fire_exit_reward");
                    if (reward != null)
                    {
                        TryConfigureItemPickup(reward, $"fire_exit_cache_{fireExit.EdgeId}", "Emergency Cache");
                        SetUsed(markers, localLoot, "fire-exit-reward", reward.name);
                        spawns.Add(SpawnRecord("fire-exit-reward", markers[localLoot], "loot", reward.name));
                    }
                }

                if (localHazard >= 0 && random.Chance(settings.FireExitRiskChance))
                {
                    GameObject hazard = SpawnHazard(root, markers[localHazard], settings.HazardPrefab, $"fire_exit_hazard_{fireExit.EdgeId}");
                    SetUsed(markers, localHazard, "fire-exit-risk", hazard != null ? hazard.name : string.Empty);
                    spawns.Add(SpawnRecord("fire-exit-risk", markers[localHazard], "hazard", hazard != null ? hazard.name : "<none>"));
                }
            }

            if (settings.LootPrefab == null)
            {
                errors.Add("Fire exit reward phase: loot prefab is not configured.");
            }
        }

        private void ResolveLoot(
            Transform root,
            IList<PopulationMarkerUsage> markers,
            ICollection<PopulationSpawnRecord> spawns,
            SeededRandom random)
        {
            if (settings.LootPrefab == null)
            {
                return;
            }

            for (int i = 0; i < markers.Count; i++)
            {
                PopulationMarkerUsage marker = markers[i];
                if (marker.Kind != SpawnMarkerKind.Loot || marker.Status != PopulationMarkerStatus.Unused)
                {
                    continue;
                }

                float chance = Mathf.Clamp01(settings.BaseLootChance + settings.LootDepthBonus * marker.Danger + (IsDeadEndMarker(marker) ? 0.1f : 0f));
                if (!random.Chance(chance))
                {
                    continue;
                }

                GameObject loot = SpawnGameplayPrefab(root, settings.LootPrefab, marker, "loot");
                if (loot == null)
                {
                    continue;
                }

                TryConfigureItemPickup(loot, $"loot_{marker.NodeId}_{i}", "Supplies");
                SetUsed(markers, i, "loot", loot.name);
                spawns.Add(SpawnRecord("loot", markers[i], "loot", loot.name));
            }
        }

        private void ResolveEnemies(
            Transform root,
            IList<PopulationMarkerUsage> markers,
            ICollection<PopulationSpawnRecord> spawns,
            SeededRandom random)
        {
            if (settings.EnemyPrefab == null)
            {
                return;
            }

            for (int i = 0; i < markers.Count; i++)
            {
                PopulationMarkerUsage marker = markers[i];
                if (marker.Kind != SpawnMarkerKind.Enemy || marker.Status != PopulationMarkerStatus.Unused)
                {
                    continue;
                }

                float chance = Mathf.Clamp01(settings.BaseEnemyChance + settings.EnemyDepthBonus * marker.Danger);
                if (marker.Danger < 0.2f)
                {
                    chance *= 0.25f;
                }

                if (!random.Chance(chance))
                {
                    continue;
                }

                GameObject enemy = SpawnGameplayPrefab(root, settings.EnemyPrefab, marker, "enemy");
                if (enemy == null)
                {
                    continue;
                }

                SetUsed(markers, i, "enemy", enemy.name);
                spawns.Add(SpawnRecord("enemy", markers[i], "enemy", enemy.name));
            }
        }

        private void ResolveHazards(
            Transform root,
            IList<PopulationMarkerUsage> markers,
            ICollection<PopulationSpawnRecord> spawns,
            SeededRandom random)
        {
            for (int i = 0; i < markers.Count; i++)
            {
                PopulationMarkerUsage marker = markers[i];
                bool markerEligible = marker.Kind == SpawnMarkerKind.Hazard || (marker.Kind == SpawnMarkerKind.Debug && marker.Danger > 0.5f);
                if (!markerEligible || marker.Status != PopulationMarkerStatus.Unused)
                {
                    continue;
                }

                float chance = Mathf.Clamp01(settings.BaseHazardChance + settings.HazardDepthBonus * marker.Danger);
                if (!random.Chance(chance))
                {
                    continue;
                }

                GameObject hazard = SpawnHazard(root, marker, settings.HazardPrefab, $"hazard_{marker.NodeId}_{i}");
                SetUsed(markers, i, "hazard", hazard != null ? hazard.name : string.Empty);
                spawns.Add(SpawnRecord("hazard", markers[i], "hazard", hazard != null ? hazard.name : "<none>"));
            }
        }

        private void ResolveLights(
            Transform root,
            IList<PopulationMarkerUsage> markers,
            ICollection<PopulationSpawnRecord> spawns,
            SeededRandom random)
        {
            var lightMarkersByNode = new Dictionary<int, List<int>>();
            var nodeOrder = new List<int>();
            for (int i = 0; i < markers.Count; i++)
            {
                PopulationMarkerUsage marker = markers[i];
                if (marker.Kind != SpawnMarkerKind.Light || marker.Status != PopulationMarkerStatus.Unused)
                {
                    continue;
                }

                if (!lightMarkersByNode.TryGetValue(marker.NodeId, out List<int> nodeMarkers))
                {
                    nodeMarkers = new List<int>();
                    lightMarkersByNode[marker.NodeId] = nodeMarkers;
                    nodeOrder.Add(marker.NodeId);
                }

                nodeMarkers.Add(i);
            }

            for (int nodeOrderIndex = 0; nodeOrderIndex < nodeOrder.Count; nodeOrderIndex++)
            {
                List<int> nodeMarkers = lightMarkersByNode[nodeOrder[nodeOrderIndex]];
                if (nodeMarkers.Count == 0)
                {
                    continue;
                }

                if (!random.Chance(settings.LightChance))
                {
                    continue;
                }

                int i = nodeMarkers[random.RangeInclusive(0, nodeMarkers.Count - 1)];
                PopulationMarkerUsage marker = markers[i];
                GameObject lightRoot = SpawnGameplayPrefab(root, settings.LightFixturePrefab, marker, $"ProcGenLight_{marker.NodeId}_{i}");
                if (lightRoot == null)
                {
                    lightRoot = new GameObject($"ProcGenLight_{marker.NodeId}_{i}");
                    lightRoot.transform.SetParent(root, true);
                    lightRoot.transform.position = marker.WorldPosition;
                }

                Light light = lightRoot.GetComponentInChildren<Light>();
                if (light == null)
                {
                    light = lightRoot.AddComponent<Light>();
                }

                light.type = LightType.Point;
                float minIntensity = Mathf.Max(0f, settings.MinLightIntensity);
                float maxIntensity = Mathf.Max(minIntensity, settings.MaxLightIntensity);
                light.intensity = Mathf.Lerp(minIntensity, maxIntensity, marker.Danger);
                light.range = Mathf.Max(0.1f, settings.LightRange);
                light.color = settings.LightColor;
                light.shadows = settings.LightShadows;
                if (settings.EnableLightFlicker)
                {
                    TryAddFlicker(lightRoot);
                }

                SetUsed(markers, i, "light", lightRoot.name);
                spawns.Add(SpawnRecord("light", markers[i], "light", lightRoot.name));
            }
        }

        private void ResolveAudio(
            Transform root,
            IList<PopulationMarkerUsage> markers,
            ICollection<PopulationSpawnRecord> spawns,
            SeededRandom random)
        {
            for (int i = 0; i < markers.Count; i++)
            {
                PopulationMarkerUsage marker = markers[i];
                if (marker.Kind != SpawnMarkerKind.Audio || marker.Status != PopulationMarkerStatus.Unused)
                {
                    continue;
                }

                if (!random.Chance(settings.AudioAmbienceChance))
                {
                    continue;
                }

                GameObject ambience = SpawnGameplayPrefab(root, settings.AudioEmitterPrefab, marker, $"ProcGenAmbience_{marker.NodeId}_{i}");
                if (ambience == null)
                {
                    ambience = new GameObject($"ProcGenAmbience_{marker.NodeId}_{i}");
                    ambience.transform.SetParent(root, true);
                    ambience.transform.position = marker.WorldPosition;
                }

                AudioReverbZone zone = ambience.GetComponent<AudioReverbZone>();
                if (zone == null)
                {
                    zone = ambience.AddComponent<AudioReverbZone>();
                }

                zone.reverbPreset = marker.Danger > 0.5f ? AudioReverbPreset.Cave : AudioReverbPreset.Room;
                zone.minDistance = 2f;
                zone.maxDistance = 9f;
                AudioSource source = ambience.GetComponent<AudioSource>();
                if (source == null)
                {
                    source = ambience.AddComponent<AudioSource>();
                }

                source.spatialBlend = 1f;
                source.loop = true;
                source.playOnAwake = false;
                source.volume = 0.15f;

                SetUsed(markers, i, "audio", ambience.name);
                spawns.Add(SpawnRecord("audio", markers[i], "audio", ambience.name));
            }
        }

        private static PopulationSpawnRecord SpawnRecord(string phase, PopulationMarkerUsage marker, string spawnKind, string spawnedName)
        {
            return new PopulationSpawnRecord
            {
                Phase = phase,
                NodeId = marker.NodeId,
                MarkerId = marker.MarkerId,
                SpawnKind = spawnKind,
                SpawnedObjectName = spawnedName,
                WorldPosition = marker.WorldPosition
            };
        }

        private static bool IsDeadEndMarker(PopulationMarkerUsage marker)
        {
            return marker.MarkerId.IndexOf("dead", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static Dictionary<int, PlacedTile> BuildPlacedByNode(ResolvedFacilityLayout layout)
        {
            var map = new Dictionary<int, PlacedTile>();
            for (int i = 0; i < layout.Tiles.Count; i++)
            {
                map[layout.Tiles[i].NodeId] = layout.Tiles[i];
            }

            return map;
        }

        private static string NormalizeMarkerId(SpawnMarker marker, int nodeId, int markerIndex)
        {
            if (marker == null)
            {
                return $"node_{nodeId}_marker_{markerIndex}";
            }

            if (!string.IsNullOrWhiteSpace(marker.MarkerId))
            {
                return marker.MarkerId.Trim();
            }

            return $"{marker.Kind}_{nodeId}_{markerIndex}";
        }

        private static float ComputeDanger(FacilityGraphNode node, IReadOnlyDictionary<int, int> mainPathIndex, int mainPathLength)
        {
            float baseDanger = 0.2f;
            if (mainPathIndex.TryGetValue(node.Id, out int index))
            {
                float denominator = Mathf.Max(1f, mainPathLength - 1f);
                baseDanger = Mathf.Clamp01(index / denominator);
            }
            else
            {
                baseDanger = 0.35f + Mathf.Clamp01(node.Depth * 0.17f);
                if (node.Role == FacilityGraphNodeRole.DeadEnd)
                {
                    baseDanger += 0.1f;
                }
                else if (node.Role == FacilityGraphNodeRole.FireExit)
                {
                    baseDanger += 0.2f;
                }
            }

            return Mathf.Clamp01(baseDanger);
        }

        private int ChooseMarker(
            IList<PopulationMarkerUsage> markers,
            SpawnMarkerKind kind,
            SeededRandom random,
            Func<PopulationMarkerUsage, bool> extraPredicate)
        {
            var candidates = new List<int>();
            float totalWeight = 0f;
            for (int i = 0; i < markers.Count; i++)
            {
                PopulationMarkerUsage marker = markers[i];
                if (marker.Kind != kind || marker.Status != PopulationMarkerStatus.Unused)
                {
                    continue;
                }

                if (extraPredicate != null && !extraPredicate(marker))
                {
                    continue;
                }

                float weight = Mathf.Max(0.001f, 0.6f + marker.Danger);
                totalWeight += weight;
                candidates.Add(i);
            }

            if (candidates.Count == 0)
            {
                return -1;
            }

            float pick = random.Value01() * totalWeight;
            float cumulative = 0f;
            for (int i = 0; i < candidates.Count; i++)
            {
                int markerIndex = candidates[i];
                float weight = Mathf.Max(0.001f, 0.6f + markers[markerIndex].Danger);
                cumulative += weight;
                if (pick <= cumulative)
                {
                    return markerIndex;
                }
            }

            return candidates[candidates.Count - 1];
        }

        private static void SetUsed(IList<PopulationMarkerUsage> markers, int index, string reason, string spawnedObjectName)
        {
            PopulationMarkerUsage marker = markers[index];
            marker.Status = PopulationMarkerStatus.Used;
            marker.Reason = reason;
            marker.SpawnedObjectName = spawnedObjectName ?? string.Empty;
            markers[index] = marker;
        }

        private GameObject SpawnGameplayPrefab(Transform root, GameObject prefab, PopulationMarkerUsage marker, string fallbackName)
        {
            if (prefab == null)
            {
                return null;
            }

            GameObject instance = UnityEngine.Object.Instantiate(prefab, marker.WorldPosition, Quaternion.identity, root);
            if (instance != null && string.IsNullOrWhiteSpace(instance.name))
            {
                instance.name = fallbackName;
            }

            return instance;
        }

        private static GameObject SpawnHazard(Transform root, PopulationMarkerUsage marker, GameObject hazardPrefab, string hazardId)
        {
            GameObject hazard = hazardPrefab != null
                ? UnityEngine.Object.Instantiate(hazardPrefab, marker.WorldPosition, Quaternion.identity, root)
                : new GameObject(hazardId);

            if (hazardPrefab == null)
            {
                hazard.transform.SetParent(root, true);
                hazard.transform.position = marker.WorldPosition;
            }

            SphereCollider collider = hazard.GetComponent<SphereCollider>();
            if (collider == null)
            {
                collider = hazard.AddComponent<SphereCollider>();
            }

            collider.radius = Mathf.Max(0.35f, collider.radius);
            collider.isTrigger = true;
            if (hazard.GetComponent<ProcGenHazardTrigger>() == null)
            {
                hazard.AddComponent<ProcGenHazardTrigger>();
            }

            return hazard;
        }

        private static bool IsBlockedByDoorway(PostLayoutConnectionMetadata metadata, Vector3 worldPosition)
        {
            if (metadata == null)
            {
                return false;
            }

            for (int i = 0; i < metadata.Doorways.Count; i++)
            {
                ResolvedDoorwayMetadata doorway = metadata.Doorways[i];
                if (doorway.ResolutionKind != DoorwayResolutionKind.Blocked)
                {
                    continue;
                }

                if (Vector3.Distance(doorway.WorldPosition, worldPosition) <= 1.05f)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool LooksLikeBlocker(Vector3 worldPosition, float radius)
        {
            Collider[] hits = Physics.OverlapSphere(worldPosition, radius, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < hits.Length; i++)
            {
                Collider hit = hits[i];
                if (hit == null)
                {
                    continue;
                }

                string name = hit.transform.name;
                if (name.IndexOf("blocker", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static void TryConfigureItemPickup(GameObject target, string itemId, string displayName)
        {
            if (target == null || ItemPickupType == null)
            {
                return;
            }

            Component itemPickup = target.GetComponent(ItemPickupType);
            if (itemPickup == null)
            {
                return;
            }

            ItemIdField?.SetValue(itemPickup, itemId);
            ItemDisplayNameField?.SetValue(itemPickup, displayName);
        }

        private static void TryAddFlicker(GameObject lightRoot)
        {
            if (lightRoot == null || FlickeringFluorescentType == null)
            {
                return;
            }

            if (lightRoot.GetComponent(FlickeringFluorescentType) == null)
            {
                lightRoot.AddComponent(FlickeringFluorescentType);
            }
        }
    }
}
