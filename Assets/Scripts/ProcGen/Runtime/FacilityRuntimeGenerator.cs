using System.Collections;
using System.Collections.Generic;
using EscapeBlock9.ProcGen.Authoring;
using EscapeBlock9.ProcGen.Data;
using EscapeBlock9.ProcGen.Debugging;
using EscapeBlock9.ProcGen.Navigation;
using EscapeBlock9.ProcGen.Placement;
using EscapeBlock9.ProcGen.Planning;
using EscapeBlock9.ProcGen.Population;
using System;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace EscapeBlock9.ProcGen.Runtime
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-950)]
    public sealed class FacilityRuntimeGenerator : MonoBehaviour
    {
        private static readonly Type FirstPersonControllerType = Type.GetType("FirstPersonController, Assembly-CSharp");
        private const string DefaultCatalogPath = "Assets/ProcGen/Catalogs/InitialBlock9TileCatalog.asset";
        private const string DefaultLootPrefabPath = "Assets/arhitektura/KeyItem.prefab";
        private const string DefaultEnemyPrefabPath = "Assets/enteties/StickNPC_01.prefab";

        [Header("Generation Assets")]
        [SerializeField] private TileCatalog tileCatalog;
        [SerializeField] private FacilityRunConfig runConfig;
        [SerializeField] private int fallbackSeed = 24680;
        [SerializeField] private int currentSeed = 24680;
        [SerializeField] private bool useRandomSeedOnStart;

        [Header("Runtime Flow")]
        [SerializeField] private bool generateOnStart = true;
        [SerializeField] private bool clearPreviousGeneration = true;
        [SerializeField] private string generatedRootName = "GeneratedFacilityRuntime";
        [SerializeField] private bool autoTagPlayerAsPlayer = true;
        [SerializeField] private bool enablePortalLinks;
        [SerializeField] private bool logVerbose = true;
        [SerializeField] private bool showSeedReplayHud = true;

        [Header("Population")]
        [SerializeField] private FacilityPopulationSettings populationSettings = new FacilityPopulationSettings();

        [Header("Navigation")]
        [SerializeField] private RuntimeNavMeshBuilder navMeshBuilder;

        private bool isGenerating;
        private GameObject generatedRoot;
        private float lastGenerationDurationSeconds;
        private string lastFailureSummary = string.Empty;
        private IReadOnlyList<FailedSeedEntry> recentFailedSeeds = new List<FailedSeedEntry>();

        private void Awake()
        {
#if UNITY_EDITOR
            AutoAssignDefaultsInEditor();
#endif
            if (currentSeed == 0)
            {
                currentSeed = fallbackSeed;
            }

            recentFailedSeeds = FacilitySeedHistory.ReadRecent();
            navMeshBuilder ??= GetComponent<RuntimeNavMeshBuilder>();
            if (navMeshBuilder == null)
            {
                navMeshBuilder = gameObject.AddComponent<RuntimeNavMeshBuilder>();
            }
        }

        private IEnumerator Start()
        {
            if (!generateOnStart)
            {
                yield break;
            }

            if (useRandomSeedOnStart)
            {
                currentSeed = UnityEngine.Random.Range(1, int.MaxValue);
            }

            yield return GenerateAsync();
        }

        [ContextMenu("Generate Facility Runtime")]
        public void GenerateNow()
        {
            if (!isGenerating)
            {
                StartCoroutine(GenerateAsync());
            }
        }

        public IEnumerator GenerateAsync()
        {
            if (isGenerating)
            {
                yield break;
            }

            isGenerating = true;
            float startTimestamp = Time.realtimeSinceStartup;
            var errors = new List<string>();
            try
            {
                if (tileCatalog == null)
                {
                    errors.Add("Runtime generation failed: TileCatalog is not assigned.");
                    yield break;
                }

                if (clearPreviousGeneration)
                {
                    ClearPreviousRoot();
                }

                FacilityGraphPlanConfig graphPlan = BuildGraphPlan();
                FacilityGraph graph = new FacilityGraphPlanner().Plan(graphPlan);
                ResolvedFacilityLayout layout = new CustomFacilityLayoutSolver().Solve(graph, tileCatalog, graphPlan.MasterSeed);
                if (layout.Tiles.Count != graph.Nodes.Count)
                {
                    errors.Add("Runtime generation failed: layout solver did not place all graph nodes.");
                    errors.Add(layout.Diagnostics.ToDebugString());
                    yield break;
                }

                if (!ResolvedFacilityLayoutValidator.ValidateConnected(graph, layout, layout.Diagnostics))
                {
                    errors.Add("Runtime generation failed: resolved layout is not fully connected.");
                    errors.Add(layout.Diagnostics.ToDebugString());
                    yield break;
                }

                generatedRoot = new GameObject(generatedRootName);
                var instanceTiles = new Dictionary<int, Tile>();
                for (int i = 0; i < layout.Tiles.Count; i++)
                {
                    PlacedTile placed = layout.Tiles[i];
                    GameObject instance = Instantiate(placed.Definition.Prefab, generatedRoot.transform);
                    instance.name = $"Node_{placed.NodeId:00}_{placed.ModuleId}";
                    instance.transform.SetPositionAndRotation(placed.Position, placed.Rotation);
                    GeneratedDoorVisualStripper.RemoveDoorVisuals(instance);
                    Tile tile = instance.GetComponent<Tile>();
                    if (tile != null)
                    {
                        instanceTiles[placed.NodeId] = tile;
                    }

                    if ((i % 4) == 0)
                    {
                        yield return null;
                    }
                }

                PostLayoutConnectionResolution connectionResolution = new PostLayoutConnectionResolver().Resolve(layout, graph, instanceTiles);
                PostLayoutConnectionMetadata connectionMetadata = generatedRoot.AddComponent<PostLayoutConnectionMetadata>();
                connectionMetadata.Apply(connectionResolution);
                generatedRoot.AddComponent<FacilityConnectionDebugOverlay>();
                yield return null;

                Transform populationRoot = new GameObject("Population").transform;
                populationRoot.SetParent(generatedRoot.transform, false);
                FacilityPopulationReport populationReport = new FacilityPopulationPipeline(populationSettings)
                    .Populate(populationRoot, graph, layout, instanceTiles, connectionMetadata);
                FacilityPopulationMetadata populationMetadata = generatedRoot.AddComponent<FacilityPopulationMetadata>();
                populationMetadata.Apply(populationReport);
                generatedRoot.AddComponent<FacilityPopulationDebugOverlay>();
                yield return null;

                List<RuntimeNavLinkRequest> linkRequests = RuntimeNavLinkPlanner.Build(
                    graph,
                    layout,
                    instanceTiles,
                    connectionMetadata,
                    enablePortalLinks);

                RuntimeNavMeshBuildReport navReport = null;
                yield return navMeshBuilder.RebuildAsync(generatedRoot.transform, linkRequests, report => navReport = report);
                if (navReport != null && navReport.Errors.Count > 0)
                {
                    errors.AddRange(navReport.Errors);
                }

                ValidateEnemySpawns(populationReport, errors);
                PlacePlayer(populationReport, graph, layout, instanceTiles, errors);

                float durationSeconds = Time.realtimeSinceStartup - startTimestamp;
                lastGenerationDurationSeconds = durationSeconds;
                var stats = BuildStatistics(graph, layout, durationSeconds);
                var moduleUsage = BuildModuleUsage(layout);
                var failureReasonCounts = BuildFailureReasonCounts(layout.Diagnostics);
                var debugNodes = BuildNodeRecords(graph, layout);
                var debugEdges = BuildEdgeRecords(graph, layout);
                var debugOccupancy = BuildOccupancyRecords(layout);

                FacilityGenerationDebugData debugData = generatedRoot.AddComponent<FacilityGenerationDebugData>();
                debugData.Apply(stats, moduleUsage, failureReasonCounts, debugNodes, debugEdges, debugOccupancy, lastFailureSummary);
                generatedRoot.AddComponent<FacilityGenerationDebugOverlay>();

                if (logVerbose)
                {
                    Debug.Log(layout.ToDebugString());
                    Debug.Log(connectionResolution.ToDebugString());
                    Debug.Log(populationReport.ToDebugString());
                    if (navReport != null)
                    {
                        Debug.Log($"Runtime NavMesh: sources={navReport.SourceCount}, links={navReport.LinkCount}, errors={navReport.Errors.Count}");
                    }
                }
            }
            finally
            {
                isGenerating = false;
                if (errors.Count > 0)
                {
                    lastFailureSummary = string.Join(" | ", errors);
                    int seed = currentSeed;
                    FacilitySeedHistory.AppendFailedSeed(seed, "runtime-generation", lastFailureSummary);
                    recentFailedSeeds = FacilitySeedHistory.ReadRecent();
                    for (int i = 0; i < errors.Count; i++)
                    {
                        Debug.LogError(errors[i]);
                    }
                }
                else
                {
                    lastFailureSummary = string.Empty;
                    recentFailedSeeds = FacilitySeedHistory.ReadRecent();
                }
            }
        }

        private FacilityGraphPlanConfig BuildGraphPlan()
        {
            int seed = currentSeed != 0 ? currentSeed : fallbackSeed;
            if (runConfig != null && runConfig.GraphPlan != null)
            {
                FacilityGraphPlanConfig copy = runConfig.GraphPlan.Normalized();
                copy.MasterSeed = seed;
                copy.PortalChance = enablePortalLinks ? copy.PortalChance : 0f;
                return copy;
            }

            var fallback = FacilityGraphPlanConfig.CreateDefault(seed);
            fallback.MainPathLengthRange = new IntRange(7, 10);
            fallback.BranchCountRange = new IntRange(1, 2);
            fallback.BranchLengthRange = new IntRange(1, 2);
            fallback.FireExitChance = 0.9f;
            fallback.FireExitCountRange = new IntRange(1, 1);
            fallback.AllowFireExitNearStart = false;
            fallback.MinimumMainPathDistanceForFireExit = 3;
            fallback.PortalChance = enablePortalLinks ? 0.3f : 0f;
            fallback.LoopChance = 0.2f;
            fallback.MaxAttempts = 6;
            return fallback.Normalized();
        }

        public void RegenerateCurrentSeed()
        {
            if (!isGenerating)
            {
                StartCoroutine(GenerateAsync());
            }
        }

        public void RandomizeSeedAndGenerate()
        {
            currentSeed = UnityEngine.Random.Range(1, int.MaxValue);
            RegenerateCurrentSeed();
        }

        public void CopyCurrentSeed()
        {
            GUIUtility.systemCopyBuffer = currentSeed.ToString();
        }

        private void PlacePlayer(
            FacilityPopulationReport populationReport,
            FacilityGraph graph,
            ResolvedFacilityLayout layout,
            IReadOnlyDictionary<int, Tile> instanceTiles,
            ICollection<string> errors)
        {
            Transform playerTransform = ResolvePlayerTransform(out Behaviour playerControllerBehaviour);
            if (playerTransform == null)
            {
                errors.Add("Player spawn failed: no FirstPersonController found in scene.");
                return;
            }

            if (autoTagPlayerAsPlayer && playerTransform.gameObject.tag != "Player")
            {
                playerTransform.gameObject.tag = "Player";
            }

            bool foundMarker = false;
            Vector3 target = Vector3.zero;
            for (int i = 0; i < populationReport.MarkerUsage.Count; i++)
            {
                PopulationMarkerUsage marker = populationReport.MarkerUsage[i];
                if (marker.Kind == SpawnMarkerKind.PlayerStart && marker.Status == PopulationMarkerStatus.Used)
                {
                    target = marker.WorldPosition;
                    foundMarker = true;
                    break;
                }
            }

            if (!foundMarker)
            {
                int startNodeId = graph.MainPathNodeIds.Count > 0 ? graph.MainPathNodeIds[0] : -1;
                if (startNodeId >= 0 &&
                    instanceTiles.TryGetValue(startNodeId, out Tile startTile) &&
                    startTile != null)
                {
                    SpawnMarker[] markers = startTile.GetSpawnMarkers();
                    for (int i = 0; i < markers.Length; i++)
                    {
                        if (markers[i] != null && markers[i].Kind == SpawnMarkerKind.PlayerStart)
                        {
                            target = markers[i].transform.position;
                            foundMarker = true;
                            break;
                        }
                    }
                }
            }

            if (!foundMarker)
            {
                errors.Add("Player spawn failed: no valid PlayerStart marker found.");
                return;
            }

            if (playerControllerBehaviour != null)
            {
                playerControllerBehaviour.enabled = false;
            }

            playerTransform.position = target + Vector3.up * 0.1f;
            playerTransform.rotation = Quaternion.identity;

            if (playerControllerBehaviour != null)
            {
                playerControllerBehaviour.enabled = true;
            }
        }

        private static Transform ResolvePlayerTransform(out Behaviour controllerBehaviour)
        {
            controllerBehaviour = null;
            if (FirstPersonControllerType != null)
            {
                Component component = UnityEngine.Object.FindAnyObjectByType(FirstPersonControllerType) as Component;
                if (component != null)
                {
                    controllerBehaviour = component as Behaviour;
                    return component.transform;
                }
            }

            GameObject taggedPlayer = GameObject.FindGameObjectWithTag("Player");
            if (taggedPlayer != null)
            {
                return taggedPlayer.transform;
            }

            return null;
        }

        private static void ValidateEnemySpawns(FacilityPopulationReport report, ICollection<string> errors)
        {
            for (int i = 0; i < report.Spawns.Count; i++)
            {
                PopulationSpawnRecord spawn = report.Spawns[i];
                if (!spawn.SpawnKind.Equals("enemy"))
                {
                    continue;
                }

                if (!NavMesh.SamplePosition(spawn.WorldPosition, out _, 1.8f, NavMesh.AllAreas))
                {
                    errors.Add($"Enemy spawn not on navigable surface: marker={spawn.MarkerId} node={spawn.NodeId} pos={spawn.WorldPosition}");
                }
            }
        }

        private void ClearPreviousRoot()
        {
            GameObject[] existing = FindObjectsByType<GameObject>(FindObjectsInactive.Include);
            for (int i = 0; i < existing.Length; i++)
            {
                GameObject candidate = existing[i];
                if (candidate != null && candidate.name == generatedRootName)
                {
                    Destroy(candidate);
                }
            }
        }

        private static GenerationStatistics BuildStatistics(FacilityGraph graph, ResolvedFacilityLayout layout, float durationSeconds)
        {
            int deadEnds = 0;
            for (int i = 0; i < graph.Nodes.Count; i++)
            {
                if (graph.Nodes[i].Role == FacilityGraphNodeRole.DeadEnd)
                {
                    deadEnds++;
                }
            }

            int loops = 0;
            int fireExits = 0;
            int portals = 0;
            for (int i = 0; i < graph.Edges.Count; i++)
            {
                switch (graph.Edges[i].Role)
                {
                    case FacilityGraphEdgeRole.Loop:
                        loops++;
                        break;
                    case FacilityGraphEdgeRole.FireExit:
                        fireExits++;
                        break;
                    case FacilityGraphEdgeRole.Portal:
                        portals++;
                        break;
                }
            }

            return new GenerationStatistics
            {
                Seed = layout.Seed,
                BranchCount = graph.Branches.Count,
                DeadEndCount = deadEnds,
                LoopCount = loops,
                FireExitCount = fireExits,
                PortalCount = portals,
                PlacementAttempts = layout.PlacementAttempts,
                GenerationDurationSeconds = durationSeconds
            };
        }

        private static List<ModuleUsageCount> BuildModuleUsage(ResolvedFacilityLayout layout)
        {
            var counts = new Dictionary<string, int>();
            for (int i = 0; i < layout.Tiles.Count; i++)
            {
                string moduleId = layout.Tiles[i].ModuleId;
                counts[moduleId] = counts.TryGetValue(moduleId, out int current) ? current + 1 : 1;
            }

            var usage = new List<ModuleUsageCount>(counts.Count);
            foreach (KeyValuePair<string, int> pair in counts)
            {
                usage.Add(new ModuleUsageCount { ModuleId = pair.Key, Count = pair.Value });
            }

            usage.Sort((a, b) => b.Count.CompareTo(a.Count));
            return usage;
        }

        private static List<FailureReasonCount> BuildFailureReasonCounts(PlacementFailureDiagnostics diagnostics)
        {
            var counts = new Dictionary<PlacementFailureReason, int>();
            if (diagnostics != null)
            {
                for (int i = 0; i < diagnostics.Failures.Count; i++)
                {
                    PlacementFailureReason reason = diagnostics.Failures[i].Reason;
                    counts[reason] = counts.TryGetValue(reason, out int current) ? current + 1 : 1;
                }
            }

            var result = new List<FailureReasonCount>(counts.Count);
            foreach (KeyValuePair<PlacementFailureReason, int> pair in counts)
            {
                result.Add(new FailureReasonCount { Reason = pair.Key, Count = pair.Value });
            }

            return result;
        }

        private static List<DebugNodeRecord> BuildNodeRecords(FacilityGraph graph, ResolvedFacilityLayout layout)
        {
            var mainPathLookup = new Dictionary<int, int>();
            for (int i = 0; i < graph.MainPathNodeIds.Count; i++)
            {
                mainPathLookup[graph.MainPathNodeIds[i]] = i;
            }

            var nodes = new List<DebugNodeRecord>(layout.Tiles.Count);
            for (int i = 0; i < layout.Tiles.Count; i++)
            {
                PlacedTile tile = layout.Tiles[i];
                FacilityGraphNode node = graph.GetNode(tile.NodeId);
                Bounds bounds = BuildNodeBounds(tile);
                nodes.Add(new DebugNodeRecord
                {
                    NodeId = tile.NodeId,
                    ModuleId = tile.ModuleId,
                    Role = node.Role,
                    IsMainPath = mainPathLookup.ContainsKey(tile.NodeId),
                    MainPathIndex = mainPathLookup.TryGetValue(tile.NodeId, out int idx) ? idx : -1,
                    WorldPosition = bounds.center,
                    Size = bounds.size
                });
            }

            return nodes;
        }

        private static List<DebugEdgeRecord> BuildEdgeRecords(FacilityGraph graph, ResolvedFacilityLayout layout)
        {
            var connectedEdgeIds = new HashSet<int>();
            for (int i = 0; i < layout.Connections.Count; i++)
            {
                connectedEdgeIds.Add(layout.Connections[i].EdgeId);
            }

            var edges = new List<DebugEdgeRecord>(graph.Edges.Count);
            for (int i = 0; i < graph.Edges.Count; i++)
            {
                FacilityGraphEdge edge = graph.Edges[i];
                edges.Add(new DebugEdgeRecord
                {
                    EdgeId = edge.Id,
                    FromNodeId = edge.FromNodeId,
                    ToNodeId = edge.ToNodeId,
                    Role = edge.Role,
                    Connected = connectedEdgeIds.Contains(edge.Id)
                });
            }

            return edges;
        }

        private static List<DebugOccupancyRecord> BuildOccupancyRecords(ResolvedFacilityLayout layout)
        {
            var occupancy = new List<DebugOccupancyRecord>();
            for (int i = 0; i < layout.Tiles.Count; i++)
            {
                PlacedTile tile = layout.Tiles[i];
                for (int j = 0; j < tile.OccupancyBoxes.Count; j++)
                {
                    occupancy.Add(new DebugOccupancyRecord
                    {
                        NodeId = tile.NodeId,
                        Bounds = tile.OccupancyBoxes[j].Bounds
                    });
                }
            }

            return occupancy;
        }

        private static Bounds BuildNodeBounds(PlacedTile tile)
        {
            if (tile.OccupancyBoxes.Count > 0)
            {
                Bounds bounds = tile.OccupancyBoxes[0].Bounds;
                for (int i = 1; i < tile.OccupancyBoxes.Count; i++)
                {
                    bounds.Encapsulate(tile.OccupancyBoxes[i].Bounds);
                }

                return bounds;
            }

            return new Bounds(tile.Position, Vector3.one * 2f);
        }

#if UNITY_EDITOR
        private void AutoAssignDefaultsInEditor()
        {
            if (tileCatalog == null)
            {
                tileCatalog = AssetDatabase.LoadAssetAtPath<TileCatalog>(DefaultCatalogPath);
            }

            if (populationSettings == null)
            {
                populationSettings = new FacilityPopulationSettings();
            }

            if (populationSettings.LootPrefab == null)
            {
                populationSettings.LootPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DefaultLootPrefabPath);
            }

            if (populationSettings.ObjectivePrefab == null)
            {
                populationSettings.ObjectivePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DefaultLootPrefabPath);
            }

            if (populationSettings.EnemyPrefab == null)
            {
                populationSettings.EnemyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DefaultEnemyPrefabPath);
            }
        }
#endif

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void OnGUI()
        {
            if (!showSeedReplayHud)
            {
                return;
            }

            GUILayout.BeginArea(new Rect(12f, 12f, 430f, 280f), GUI.skin.box);
            GUILayout.Label("ProcGen Seed Replay");
            GUILayout.Label($"Current Seed: {currentSeed}");
            GUILayout.Label($"Generating: {isGenerating}");
            GUILayout.Label($"Last Duration: {lastGenerationDurationSeconds:0.###}s");
            if (!string.IsNullOrWhiteSpace(lastFailureSummary))
            {
                GUILayout.Label($"Last Failure: {lastFailureSummary}");
            }

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Random Seed"))
            {
                RandomizeSeedAndGenerate();
            }

            if (GUILayout.Button("Regenerate Seed"))
            {
                RegenerateCurrentSeed();
            }

            if (GUILayout.Button("Copy Seed"))
            {
                CopyCurrentSeed();
            }
            GUILayout.EndHorizontal();

            if (recentFailedSeeds != null && recentFailedSeeds.Count > 0)
            {
                GUILayout.Space(8f);
                GUILayout.Label("Recent Failed Seeds:");
                int max = Mathf.Min(5, recentFailedSeeds.Count);
                for (int i = recentFailedSeeds.Count - max; i < recentFailedSeeds.Count; i++)
                {
                    FailedSeedEntry entry = recentFailedSeeds[i];
                    GUILayout.Label($"{entry.Seed} | {entry.Context}");
                }
            }

            GUILayout.EndArea();
        }
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoBootstrapRuntimeGenerator()
        {
            if (FindAnyObjectByType<FacilityRuntimeGenerator>() != null)
            {
                return;
            }

            Scene scene = SceneManager.GetActiveScene();
            if (scene.name != "SampleScene" && scene.name != "procedural")
            {
                return;
            }

            var bootstrap = new GameObject("FacilityRuntimeGenerator");
            bootstrap.AddComponent<FacilityRuntimeGenerator>();
        }
    }
}
