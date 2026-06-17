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
using System.Reflection;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace EscapeBlock9.ProcGen.Runtime
{
    [Serializable]
    public sealed class FacilityRuntimeLightingSettings
    {
        public Color AmbientColor = new Color(0.009f, 0.011f, 0.015f, 1f);
        [Range(0f, 2f)] public float AmbientIntensity = 0.025f;
        [Range(0f, 2f)] public float ReflectionIntensity = 0.015f;
        [Range(0f, 2f)] public float DirectionalLightIntensity = 0.01f;
    }

    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-950)]
    public sealed class FacilityRuntimeGenerator : MonoBehaviour
    {
        private static readonly Type FirstPersonControllerType = Type.GetType("FirstPersonController, Assembly-CSharp");
        private static readonly Type MultiplayerRuntimeType = Type.GetType("EscapeBlock9MultiplayerRuntime, Assembly-CSharp");
        private static readonly PropertyInfo MultiplayerControlsProceduralGenerationProperty =
            MultiplayerRuntimeType?.GetProperty("ControlsProceduralGeneration", BindingFlags.Public | BindingFlags.Static);
        private static readonly MethodInfo MultiplayerTryGetDesiredSpawnSlotMethod =
            MultiplayerRuntimeType?.GetMethod("TryGetDesiredSpawnSlot", BindingFlags.Public | BindingFlags.Static);
        private const string DefaultCatalogPath = "Assets/ProcGen/Catalogs/InitialBlock9TileCatalog.asset";
        private const string DefaultLootPrefabPath = "Assets/arhitektura/KeyItem.prefab";
        private const string DefaultEnemyPrefabPath = "Assets/enteties/StickNPC_01.prefab";
        private const string DefaultLightFixturePrefabPath = "Assets/GeneratedLighting/ProcGenFlickeringFluorescent.prefab";

        [Header("Generation Assets")]
        [SerializeField] private TileCatalog tileCatalog;
        [SerializeField] private FacilityRunConfig runConfig;
        [SerializeField] private bool usePreviewConnectedRoomLayout = true;
        [Range(1, 12)]
        [SerializeField] private int previewConnectedRoomCount = 12;
        [SerializeField] private int fallbackSeed = 24680;
        [SerializeField] private int currentSeed = 24680;
        [SerializeField] private bool useRandomSeedOnStart = true;

        [Header("Runtime Flow")]
        [SerializeField] private bool generateOnStart = true;
        [SerializeField] private bool clearPreviousGeneration = true;
        [SerializeField] private string generatedRootName = "GeneratedFacilityRuntime";
        [Min(1)]
        [SerializeField] private int runtimeSeedRetryAttempts = 128;
        [SerializeField] private bool autoTagPlayerAsPlayer = true;
        [SerializeField] private bool enablePortalLinks;
        [SerializeField] private bool logVerbose = true;
        [SerializeField] private bool showSeedReplayHud;

        [Header("Population")]
        [SerializeField] private FacilityPopulationSettings populationSettings = new FacilityPopulationSettings();

        [Header("Lighting")]
        [SerializeField] private FacilityRuntimeLightingSettings lightingSettings = new FacilityRuntimeLightingSettings();

        [Header("Navigation")]
        [SerializeField] private RuntimeNavMeshBuilder navMeshBuilder;

        private bool isGenerating;
        private GameObject generatedRoot;
        private float lastGenerationDurationSeconds;
        private string lastFailureSummary = string.Empty;
        private IReadOnlyList<FailedSeedEntry> recentFailedSeeds = new List<FailedSeedEntry>();
        private readonly List<PlayerSpawnCandidate> cachedMultiplayerSpawnPoints = new List<PlayerSpawnCandidate>();

        public event Action<FacilityRuntimeGenerator, bool> GenerationCompleted;
        public bool IsGenerating => isGenerating;
        public GameObject GeneratedRoot => generatedRoot;
        public int CurrentSeed => currentSeed;

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
            if (ShouldDeferToMultiplayerRuntime())
            {
                yield break;
            }

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

                ApplyDarkGeneratedLighting(lightingSettings);
                if (!TryBuildValidRuntimeLayout(out FacilityGraph graph, out ResolvedFacilityLayout layout, out string diagnostics))
                {
                    errors.Add($"Runtime generation failed: no valid connected facility after {Mathf.Max(1, runtimeSeedRetryAttempts)} seed attempt(s).");
                    errors.Add(diagnostics);
                    yield break;
                }

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
                SpawnEscapeExitTriggers(generatedRoot.transform, graph, instanceTiles, connectionMetadata);
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
                CacheMultiplayerSpawnPoints(layout, instanceTiles);
                PlacePlayer(populationReport, graph, layout, instanceTiles, errors);
                FacilityHorrorVisualPass.Apply(generatedRoot.transform, instanceTiles, layout.Seed);

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

                GenerationCompleted?.Invoke(this, errors.Count == 0);
            }
        }

        private FacilityGraphPlanConfig BuildGraphPlan(int seed)
        {
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
            fallback.FireExitChance = 1f;
            fallback.FireExitCountRange = new IntRange(2, 2);
            fallback.AllowFireExitNearStart = false;
            fallback.MinimumMainPathDistanceForFireExit = 3;
            fallback.PortalChance = enablePortalLinks ? 0.3f : 0f;
            fallback.LoopChance = 0.2f;
            fallback.MaxAttempts = 6;
            return fallback.Normalized();
        }

        private static void SpawnEscapeExitTriggers(
            Transform root,
            FacilityGraph graph,
            IReadOnlyDictionary<int, Tile> instanceTiles,
            PostLayoutConnectionMetadata connectionMetadata)
        {
            if (root == null || graph == null)
            {
                return;
            }

            var spawned = new HashSet<int>();
            for (int i = 0; i < graph.Nodes.Count; i++)
            {
                FacilityGraphNode node = graph.Nodes[i];
                if (node == null || node.Role != FacilityGraphNodeRole.FireExit || !spawned.Add(node.Id))
                {
                    continue;
                }

                if (TryAttachEscapeInteractionToExitDoor(instanceTiles, node.Id))
                {
                    continue;
                }

                if (TryResolveAuthoredFireExitDoorway(instanceTiles, node.Id, out Vector3 position, out Vector3 forward))
                {
                    SpawnEscapeExitTrigger(root, node.Id, position, forward);
                    continue;
                }

                if (TryResolveFireExitMetadata(connectionMetadata, node.Id, out position, out forward))
                {
                    SpawnEscapeExitTrigger(root, node.Id, position, forward);
                }
            }
        }

        private static bool TryAttachEscapeInteractionToExitDoor(
            IReadOnlyDictionary<int, Tile> instanceTiles,
            int nodeId)
        {
            if (instanceTiles == null || !instanceTiles.TryGetValue(nodeId, out Tile tile) || tile == null)
            {
                return false;
            }

            Transform exitDoor = FindChildRecursive(tile.transform, "ExitDoor");
            if (exitDoor == null)
            {
                return false;
            }

            DisableOrdinaryDoorController(exitDoor.gameObject);
            if (exitDoor.GetComponent<FacilityEscapeExitTrigger>() == null)
            {
                exitDoor.gameObject.AddComponent<FacilityEscapeExitTrigger>();
            }

            BoxCollider collider = exitDoor.GetComponent<BoxCollider>();
            if (collider == null)
            {
                collider = exitDoor.gameObject.AddComponent<BoxCollider>();
                collider.size = new Vector3(1.8f, 2.4f, 0.45f);
                collider.center = new Vector3(0f, 1.15f, 0f);
            }

            return true;
        }

        private static Transform FindChildRecursive(Transform root, string childName)
        {
            if (root == null || string.IsNullOrWhiteSpace(childName))
            {
                return null;
            }

            if (string.Equals(root.name, childName, StringComparison.OrdinalIgnoreCase))
            {
                return root;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindChildRecursive(root.GetChild(i), childName);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private static void DisableOrdinaryDoorController(GameObject doorObject)
        {
            if (doorObject == null)
            {
                return;
            }

            Component[] components = doorObject.GetComponents<Component>();
            for (int i = 0; i < components.Length; i++)
            {
                Component component = components[i];
                if (component == null ||
                    !string.Equals(component.GetType().Name, "DoorController", StringComparison.Ordinal))
                {
                    continue;
                }

                if (component is Behaviour behaviour)
                {
                    behaviour.enabled = false;
                }
            }
        }

        private static bool TryResolveAuthoredFireExitDoorway(
            IReadOnlyDictionary<int, Tile> instanceTiles,
            int nodeId,
            out Vector3 position,
            out Vector3 forward)
        {
            position = Vector3.zero;
            forward = Vector3.forward;
            if (instanceTiles == null || !instanceTiles.TryGetValue(nodeId, out Tile tile) || tile == null)
            {
                return false;
            }

            Doorway[] doorways = tile.GetDoorways();
            for (int i = 0; i < doorways.Length; i++)
            {
                Doorway doorway = doorways[i];
                if (doorway == null)
                {
                    continue;
                }

                if (doorway.ConnectorKind == ConnectorKind.FireExit ||
                    string.Equals(doorway.ConnectorId, "fire_exit", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(doorway.SocketName, "fire_exit", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(doorway.name, "fire_exit", StringComparison.OrdinalIgnoreCase))
                {
                    position = doorway.transform.position;
                    forward = doorway.transform.forward;
                    return true;
                }
            }

            return false;
        }

        private static bool TryResolveFireExitMetadata(
            PostLayoutConnectionMetadata connectionMetadata,
            int nodeId,
            out Vector3 position,
            out Vector3 forward)
        {
            position = Vector3.zero;
            forward = Vector3.forward;
            if (connectionMetadata == null)
            {
                return false;
            }

            for (int i = 0; i < connectionMetadata.FireExits.Count; i++)
            {
                FireExitRuntimeMetadata fireExit = connectionMetadata.FireExits[i];
                if (fireExit.NodeId != nodeId)
                {
                    continue;
                }

                position = fireExit.WorldPosition;
                for (int doorwayIndex = 0; doorwayIndex < connectionMetadata.Doorways.Count; doorwayIndex++)
                {
                    ResolvedDoorwayMetadata doorway = connectionMetadata.Doorways[doorwayIndex];
                    if (doorway.NodeId == fireExit.NodeId && doorway.DoorwayIndex == fireExit.DoorwayIndex)
                    {
                        forward = doorway.WorldForward;
                        break;
                    }
                }

                return true;
            }

            return false;
        }

        private static void SpawnEscapeExitTrigger(Transform root, int nodeId, Vector3 position, Vector3 forward)
        {
            Vector3 flattenedForward = Vector3.ProjectOnPlane(forward, Vector3.up);
            if (flattenedForward.sqrMagnitude <= 0.0001f)
            {
                flattenedForward = Vector3.forward;
            }

            flattenedForward.Normalize();

            GameObject triggerObject = new GameObject($"EscapeExit_{nodeId}");
            triggerObject.transform.SetParent(root, true);
            triggerObject.transform.position = position + Vector3.up * 1.1f + flattenedForward * 0.55f;
            triggerObject.transform.rotation = Quaternion.LookRotation(flattenedForward, Vector3.up);

            BoxCollider collider = triggerObject.AddComponent<BoxCollider>();
            collider.isTrigger = true;
            collider.size = new Vector3(3f, 2.6f, 2.4f);

            triggerObject.AddComponent<FacilityEscapeExitTrigger>();
        }

        public void RegenerateCurrentSeed()
        {
            if (!isGenerating)
            {
                StartCoroutine(GenerateAsync());
            }
        }

        public void ConfigurePreviewConnectedRoomLayout(int roomCount)
        {
            usePreviewConnectedRoomLayout = true;
            previewConnectedRoomCount = Mathf.Clamp(roomCount, 1, 12);
        }

        public void GenerateWithSeed(int seed)
        {
            currentSeed = seed != 0 ? seed : fallbackSeed;
            RegenerateCurrentSeed();
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
            Quaternion targetRotation = playerTransform.rotation;
            if (TryResolveMultiplayerSpawn(out Vector3 multiplayerTarget, out Quaternion multiplayerRotation))
            {
                target = multiplayerTarget;
                targetRotation = multiplayerRotation;
                foundMarker = true;
            }
            else if (TryResolveRandomRoomSpawn(layout, instanceTiles, out Vector3 roomTarget, out Quaternion roomRotation))
            {
                target = roomTarget;
                targetRotation = roomRotation;
                foundMarker = true;
            }

            for (int i = 0; i < populationReport.MarkerUsage.Count; i++)
            {
                if (foundMarker)
                {
                    break;
                }

                PopulationMarkerUsage marker = populationReport.MarkerUsage[i];
                if (marker.Kind == SpawnMarkerKind.PlayerStart && marker.Status == PopulationMarkerStatus.Used)
                {
                    target = marker.WorldPosition;
                    targetRotation = Quaternion.identity;
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
                            targetRotation = ResolveSpawnRotation(markers[i].transform.forward, startTile.transform.rotation);
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
            playerTransform.rotation = targetRotation;

            if (playerControllerBehaviour != null)
            {
                playerControllerBehaviour.enabled = true;
            }
        }

        public bool TryGetSpawnPointForSlot(int slot, out Vector3 target, out Quaternion rotation)
        {
            target = Vector3.zero;
            rotation = Quaternion.identity;
            if (slot < 0 || cachedMultiplayerSpawnPoints.Count == 0)
            {
                return false;
            }

            PlayerSpawnCandidate candidate = cachedMultiplayerSpawnPoints[slot % cachedMultiplayerSpawnPoints.Count];
            target = candidate.Position;
            rotation = candidate.Rotation;
            return true;
        }

        private bool TryResolveMultiplayerSpawn(out Vector3 target, out Quaternion rotation)
        {
            if (TryGetMultiplayerSpawnSlot(out int slot))
            {
                return TryGetSpawnPointForSlot(slot, out target, out rotation);
            }

            target = Vector3.zero;
            rotation = Quaternion.identity;
            return false;
        }

        private static bool TryResolveRandomRoomSpawn(
            ResolvedFacilityLayout layout,
            IReadOnlyDictionary<int, Tile> instanceTiles,
            out Vector3 target,
            out Quaternion rotation)
        {
            target = Vector3.zero;
            rotation = Quaternion.identity;
            if (layout == null || instanceTiles == null)
            {
                return false;
            }

            var random = new NamedRandomStreams(layout.Seed).Stream("runtime/player-random-room-spawn");
            var preferredCandidates = new List<PlayerSpawnCandidate>();
            var fallbackCandidates = new List<PlayerSpawnCandidate>();
            CollectSpawnCandidates(layout, instanceTiles, preferredCandidates, fallbackCandidates);

            if (TryPickSpawnCandidate(preferredCandidates, random, out target, out rotation))
            {
                return true;
            }

            return TryPickSpawnCandidate(fallbackCandidates, random, out target, out rotation);
        }

        private void CacheMultiplayerSpawnPoints(ResolvedFacilityLayout layout, IReadOnlyDictionary<int, Tile> instanceTiles)
        {
            cachedMultiplayerSpawnPoints.Clear();
            if (layout == null || instanceTiles == null)
            {
                return;
            }

            if (TryBuildSharedRoomSpawnCluster(layout, instanceTiles, cachedMultiplayerSpawnPoints))
            {
                return;
            }

            var preferredCandidates = new List<PlayerSpawnCandidate>();
            var fallbackCandidates = new List<PlayerSpawnCandidate>();
            CollectSpawnCandidates(layout, instanceTiles, preferredCandidates, fallbackCandidates);
            List<PlayerSpawnCandidate> source = preferredCandidates.Count > 0 ? preferredCandidates : fallbackCandidates;
            if (source.Count == 0)
            {
                return;
            }

            var ordered = new List<PlayerSpawnCandidate>(source);
            var random = new NamedRandomStreams(layout.Seed).Stream("runtime/player-multiplayer-spawns");
            for (int i = ordered.Count - 1; i > 0; i--)
            {
                int swapIndex = random.RangeInclusive(0, i);
                (ordered[i], ordered[swapIndex]) = (ordered[swapIndex], ordered[i]);
            }

            int spawnCount = Mathf.Min(4, ordered.Count);
            for (int i = 0; i < spawnCount; i++)
            {
                cachedMultiplayerSpawnPoints.Add(ordered[i]);
            }

            if (cachedMultiplayerSpawnPoints.Count == 1)
            {
                cachedMultiplayerSpawnPoints.Add(cachedMultiplayerSpawnPoints[0]);
            }
        }

        private bool TryBuildValidRuntimeLayout(out FacilityGraph graph, out ResolvedFacilityLayout layout, out string diagnostics)
        {
            graph = null;
            layout = null;
            diagnostics = string.Empty;

            int baseSeed = currentSeed != 0 ? currentSeed : fallbackSeed;
            int attemptCount = Mathf.Max(1, runtimeSeedRetryAttempts);
            var failures = new List<string>();

            for (int attempt = 0; attempt < attemptCount; attempt++)
            {
                int seed = ComputeRetrySeed(baseSeed, attempt);
                try
                {
                    if (!TryBuildRuntimeLayout(seed, out FacilityGraph candidateGraph, out ResolvedFacilityLayout candidateLayout, out string buildDiagnostics))
                    {
                        AddRuntimeSeedFailure(failures, seed, buildDiagnostics);
                        continue;
                    }

                    if (!ValidateRuntimeLayoutCandidate(candidateGraph, candidateLayout, out string validationDiagnostics))
                    {
                        AddRuntimeSeedFailure(failures, seed, validationDiagnostics);
                        continue;
                    }

                    currentSeed = seed;
                    graph = candidateGraph;
                    layout = candidateLayout;
                    diagnostics = failures.Count > 0
                        ? $"Succeeded with seed {seed} after {attempt + 1} attempt(s). Earlier failures: {string.Join(" || ", failures)}"
                        : string.Empty;
                    return true;
                }
                catch (Exception ex)
                {
                    AddRuntimeSeedFailure(failures, seed, $"{ex.GetType().Name}: {ex.Message}");
                }
            }

            diagnostics = failures.Count > 0 ? string.Join(" || ", failures) : "No seed attempts were run.";
            return false;
        }

        private bool TryBuildRuntimeLayout(int seed, out FacilityGraph graph, out ResolvedFacilityLayout layout, out string diagnostics)
        {
            diagnostics = string.Empty;
            if (usePreviewConnectedRoomLayout)
            {
                return FacilityMapLikeLayoutBuilder.TryBuild(
                    tileCatalog,
                    Mathf.Clamp(previewConnectedRoomCount, 1, 12),
                    seed,
                    out graph,
                    out layout,
                    out diagnostics);
            }

            FacilityGraphPlanConfig graphPlan = BuildGraphPlan(seed);
            graph = new FacilityGraphPlanner().Plan(graphPlan);
            layout = new CustomFacilityLayoutSolver().Solve(graph, tileCatalog, graphPlan.MasterSeed);
            return true;
        }

        private static bool ValidateRuntimeLayoutCandidate(
            FacilityGraph graph,
            ResolvedFacilityLayout layout,
            out string diagnostics)
        {
            diagnostics = string.Empty;
            if (graph == null || layout == null)
            {
                diagnostics = "layout builder returned null graph/layout.";
                return false;
            }

            if (layout.Tiles.Count != graph.Nodes.Count)
            {
                diagnostics = "layout solver did not place all graph nodes. " + layout.Diagnostics.ToDebugString();
                return false;
            }

            if (!ResolvedFacilityLayoutValidator.ValidateConnected(graph, layout, layout.Diagnostics))
            {
                diagnostics = "resolved layout is not fully connected. " + layout.Diagnostics.ToDebugString();
                return false;
            }

            if (OccupancyValidator.AnyOverlap(layout.Tiles, new FacilityPlacementSettings().OverlapTolerance, out string overlap))
            {
                diagnostics = "resolved layout overlaps: " + overlap;
                return false;
            }

            return true;
        }

        private static int ComputeRetrySeed(int baseSeed, int attempt)
        {
            int mixed = unchecked(baseSeed + attempt * 7919);
            if (mixed == int.MinValue)
            {
                return int.MaxValue;
            }

            if (mixed < 0)
            {
                mixed = -mixed;
            }

            return mixed != 0 ? mixed : attempt + 1;
        }

        private static void AddRuntimeSeedFailure(ICollection<string> failures, int seed, string reason)
        {
            if (failures == null || failures.Count >= 8)
            {
                return;
            }

            failures.Add($"seed {seed}: {reason}");
        }

        private static bool TryBuildSharedRoomSpawnCluster(
            ResolvedFacilityLayout layout,
            IReadOnlyDictionary<int, Tile> instanceTiles,
            ICollection<PlayerSpawnCandidate> results)
        {
            if (results == null || layout == null || instanceTiles == null)
            {
                return false;
            }

            if (!TryPickSharedSpawnTile(layout, instanceTiles, out PlacedTile placedTile, out Tile tile))
            {
                return false;
            }

            AddSharedRoomSpawnCandidates(results, tile, placedTile, 4);
            return results.Count > 0;
        }

        private static bool TryPickSharedSpawnTile(
            ResolvedFacilityLayout layout,
            IReadOnlyDictionary<int, Tile> instanceTiles,
            out PlacedTile placedTile,
            out Tile tile)
        {
            placedTile = null;
            tile = null;

            var preferredChoices = new List<SpawnTileChoice>();
            var fallbackChoices = new List<SpawnTileChoice>();
            for (int i = 0; i < layout.Tiles.Count; i++)
            {
                PlacedTile candidate = layout.Tiles[i];
                if (!instanceTiles.TryGetValue(candidate.NodeId, out Tile instanceTile) || instanceTile == null)
                {
                    continue;
                }

                float weight = 1f + CountPlayerStartMarkers(instanceTile);
                TileCategory category = instanceTile.Category;
                if (category == TileCategory.Room || category == TileCategory.Special)
                {
                    preferredChoices.Add(new SpawnTileChoice(candidate, instanceTile, weight));
                }
                else if (category != TileCategory.Corridor)
                {
                    fallbackChoices.Add(new SpawnTileChoice(candidate, instanceTile, weight));
                }
            }

            IReadOnlyList<SpawnTileChoice> source = preferredChoices.Count > 0 ? preferredChoices : fallbackChoices;
            if (source.Count == 0)
            {
                return false;
            }

            var random = new NamedRandomStreams(layout.Seed).Stream("runtime/player-shared-room");
            if (!TryPickSpawnTileChoice(source, random, out SpawnTileChoice selected))
            {
                return false;
            }

            placedTile = selected.PlacedTile;
            tile = selected.Tile;
            return true;
        }

        private static bool TryPickSpawnTileChoice(
            IReadOnlyList<SpawnTileChoice> choices,
            SeededRandom random,
            out SpawnTileChoice selected)
        {
            selected = default;
            if (choices == null || choices.Count == 0 || random == null)
            {
                return false;
            }

            float totalWeight = 0f;
            for (int i = 0; i < choices.Count; i++)
            {
                totalWeight += Mathf.Max(0.001f, choices[i].Weight);
            }

            float pick = random.Value01() * totalWeight;
            float cumulative = 0f;
            for (int i = 0; i < choices.Count; i++)
            {
                SpawnTileChoice choice = choices[i];
                cumulative += Mathf.Max(0.001f, choice.Weight);
                if (pick <= cumulative)
                {
                    selected = choice;
                    return true;
                }
            }

            selected = choices[choices.Count - 1];
            return true;
        }

        private static int CountPlayerStartMarkers(Tile tile)
        {
            if (tile == null)
            {
                return 0;
            }

            int count = 0;
            SpawnMarker[] markers = tile.GetSpawnMarkers();
            for (int i = 0; i < markers.Length; i++)
            {
                if (markers[i] != null && markers[i].Kind == SpawnMarkerKind.PlayerStart)
                {
                    count++;
                }
            }

            return count;
        }

        private static void AddSharedRoomSpawnCandidates(
            ICollection<PlayerSpawnCandidate> candidates,
            Tile tile,
            PlacedTile placedTile,
            int desiredCount)
        {
            if (candidates == null || tile == null || placedTile == null || desiredCount <= 0)
            {
                return;
            }

            SpawnMarker[] markers = tile.GetSpawnMarkers();
            Quaternion baseRotation = ResolveSpawnRotation(tile.transform.forward, tile.transform.rotation);
            for (int i = 0; i < markers.Length && candidates.Count < desiredCount; i++)
            {
                SpawnMarker marker = markers[i];
                if (marker == null || marker.Kind != SpawnMarkerKind.PlayerStart)
                {
                    continue;
                }

                baseRotation = ResolveSpawnRotation(marker.transform.forward, tile.transform.rotation);
                candidates.Add(new PlayerSpawnCandidate(
                    marker.transform.position,
                    baseRotation,
                    Mathf.Max(0.001f, marker.Weight * 2f)));
            }

            Bounds bounds = BuildNodeBounds(placedTile);
            Vector3 center = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            if (NavMesh.SamplePosition(center, out NavMeshHit centerHit, 2.5f, NavMesh.AllAreas))
            {
                center = centerHit.position;
            }

            Vector3 right = Vector3.ProjectOnPlane(baseRotation * Vector3.right, Vector3.up);
            if (right.sqrMagnitude <= 0.0001f)
            {
                right = Vector3.right;
            }
            else
            {
                right.Normalize();
            }

            Vector3 forward = Vector3.ProjectOnPlane(baseRotation * Vector3.forward, Vector3.up);
            if (forward.sqrMagnitude <= 0.0001f)
            {
                forward = Vector3.forward;
            }
            else
            {
                forward.Normalize();
            }

            float lateralExtent = Mathf.Max(0.75f, Mathf.Min(bounds.extents.x, bounds.extents.z) - 0.75f);
            Vector2[] offsets =
            {
                Vector2.zero,
                new Vector2(0.45f, 0f),
                new Vector2(-0.45f, 0f),
                new Vector2(0f, 0.45f)
            };

            for (int i = 0; i < offsets.Length && candidates.Count < desiredCount; i++)
            {
                Vector2 offset = offsets[i];
                Vector3 desired = center + right * (offset.x * lateralExtent) + forward * (offset.y * lateralExtent);
                if (NavMesh.SamplePosition(desired, out NavMeshHit hit, 1.5f, NavMesh.AllAreas))
                {
                    desired = hit.position;
                }

                candidates.Add(new PlayerSpawnCandidate(desired, baseRotation, 1f));
            }
        }

        private static void CollectSpawnCandidates(
            ResolvedFacilityLayout layout,
            IReadOnlyDictionary<int, Tile> instanceTiles,
            ICollection<PlayerSpawnCandidate> preferredCandidates,
            ICollection<PlayerSpawnCandidate> fallbackCandidates)
        {
            if (layout == null || instanceTiles == null)
            {
                return;
            }

            for (int i = 0; i < layout.Tiles.Count; i++)
            {
                PlacedTile placedTile = layout.Tiles[i];
                if (!instanceTiles.TryGetValue(placedTile.NodeId, out Tile tile) || tile == null)
                {
                    continue;
                }

                TileCategory category = tile.Category;
                if (category == TileCategory.Room || category == TileCategory.Special)
                {
                    AddSpawnCandidates(preferredCandidates, tile, placedTile);
                }
                else if (category != TileCategory.Corridor)
                {
                    AddSpawnCandidates(fallbackCandidates, tile, placedTile);
                }
            }
        }

        private static void AddSpawnCandidates(ICollection<PlayerSpawnCandidate> candidates, Tile tile, PlacedTile placedTile)
        {
            if (candidates == null || tile == null || placedTile == null)
            {
                return;
            }

            SpawnMarker[] markers = tile.GetSpawnMarkers();
            for (int i = 0; i < markers.Length; i++)
            {
                SpawnMarker marker = markers[i];
                if (marker == null || marker.Kind != SpawnMarkerKind.PlayerStart)
                {
                    continue;
                }

                candidates.Add(new PlayerSpawnCandidate(
                    marker.transform.position,
                    ResolveSpawnRotation(marker.transform.forward, tile.transform.rotation),
                    Mathf.Max(0.001f, marker.Weight * 2f)));
            }

            Bounds bounds = BuildNodeBounds(placedTile);
            Vector3 center = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            if (NavMesh.SamplePosition(center, out NavMeshHit hit, 2.5f, NavMesh.AllAreas))
            {
                center = hit.position;
            }

            candidates.Add(new PlayerSpawnCandidate(
                center,
                ResolveSpawnRotation(tile.transform.forward, tile.transform.rotation),
                1f));
        }

        private static bool TryPickSpawnCandidate(
            IReadOnlyList<PlayerSpawnCandidate> candidates,
            SeededRandom random,
            out Vector3 target,
            out Quaternion rotation)
        {
            target = Vector3.zero;
            rotation = Quaternion.identity;
            if (candidates == null || candidates.Count == 0 || random == null)
            {
                return false;
            }

            float totalWeight = 0f;
            for (int i = 0; i < candidates.Count; i++)
            {
                totalWeight += Mathf.Max(0.001f, candidates[i].Weight);
            }

            float pick = random.Value01() * totalWeight;
            float cumulative = 0f;
            for (int i = 0; i < candidates.Count; i++)
            {
                PlayerSpawnCandidate candidate = candidates[i];
                cumulative += Mathf.Max(0.001f, candidate.Weight);
                if (pick <= cumulative)
                {
                    target = candidate.Position;
                    rotation = candidate.Rotation;
                    return true;
                }
            }

            PlayerSpawnCandidate fallback = candidates[candidates.Count - 1];
            target = fallback.Position;
            rotation = fallback.Rotation;
            return true;
        }

        private static Quaternion ResolveSpawnRotation(Vector3 forward, Quaternion fallbackRotation)
        {
            Vector3 flattenedForward = Vector3.ProjectOnPlane(forward, Vector3.up);
            if (flattenedForward.sqrMagnitude <= 0.0001f)
            {
                return fallbackRotation;
            }

            return Quaternion.LookRotation(flattenedForward.normalized, Vector3.up);
        }

        private readonly struct PlayerSpawnCandidate
        {
            public PlayerSpawnCandidate(Vector3 position, Quaternion rotation, float weight)
            {
                Position = position;
                Rotation = rotation;
                Weight = weight;
            }

            public Vector3 Position { get; }
            public Quaternion Rotation { get; }
            public float Weight { get; }
        }

        private readonly struct SpawnTileChoice
        {
            public SpawnTileChoice(PlacedTile placedTile, Tile tile, float weight)
            {
                PlacedTile = placedTile;
                Tile = tile;
                Weight = weight;
            }

            public PlacedTile PlacedTile { get; }
            public Tile Tile { get; }
            public float Weight { get; }
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

        private static bool ShouldDeferToMultiplayerRuntime()
        {
            if (MultiplayerControlsProceduralGenerationProperty == null)
            {
                return false;
            }

            try
            {
                return MultiplayerControlsProceduralGenerationProperty.GetValue(null) is bool value && value;
            }
            catch (TargetInvocationException)
            {
                return false;
            }
            catch (TargetException)
            {
                return false;
            }
        }

        private static bool TryGetMultiplayerSpawnSlot(out int slot)
        {
            slot = 0;
            if (MultiplayerTryGetDesiredSpawnSlotMethod == null)
            {
                return false;
            }

            object[] args = { 0 };
            try
            {
                bool hasSlot = MultiplayerTryGetDesiredSpawnSlotMethod.Invoke(null, args) is bool result && result;
                if (!hasSlot || args[0] is not int resolvedSlot)
                {
                    return false;
                }

                slot = resolvedSlot;
                return true;
            }
            catch (TargetInvocationException)
            {
                return false;
            }
            catch (TargetException)
            {
                return false;
            }
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

        private static string TruncateForHud(string value, int maxLength = 700)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
            {
                return value;
            }

            return value.Substring(0, maxLength) + "...";
        }

        private static void ApplyDarkGeneratedLighting(FacilityRuntimeLightingSettings settings)
        {
            settings ??= new FacilityRuntimeLightingSettings();
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = settings.AmbientColor;
            RenderSettings.ambientIntensity = Mathf.Clamp(settings.AmbientIntensity, 0f, 2f);
            RenderSettings.reflectionIntensity = Mathf.Clamp(settings.ReflectionIntensity, 0f, 2f);

            Light[] lights = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsInactive.Include);
            for (int i = 0; i < lights.Length; i++)
            {
                Light light = lights[i];
                if (light != null && light.type == LightType.Directional)
                {
                    light.intensity = Mathf.Clamp(settings.DirectionalLightIntensity, 0f, 2f);
                }
            }
        }

#if UNITY_EDITOR
        private void Reset()
        {
            PersistEditorDefaults();
        }

        private void OnValidate()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            PersistEditorDefaults();
        }

        private void PersistEditorDefaults()
        {
            if (!AutoAssignDefaultsInEditor())
            {
                return;
            }

            EditorUtility.SetDirty(this);
            if (gameObject.scene.IsValid())
            {
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
            }
        }

        private bool AutoAssignDefaultsInEditor()
        {
            bool changed = false;
            if (tileCatalog == null)
            {
                tileCatalog = AssetDatabase.LoadAssetAtPath<TileCatalog>(DefaultCatalogPath);
                changed |= tileCatalog != null;
            }

            if (populationSettings == null)
            {
                populationSettings = new FacilityPopulationSettings();
                changed = true;
            }

            if (populationSettings.LootPrefab == null)
            {
                populationSettings.LootPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DefaultLootPrefabPath);
                changed |= populationSettings.LootPrefab != null;
            }

            if (populationSettings.ObjectivePrefab == null)
            {
                populationSettings.ObjectivePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DefaultLootPrefabPath);
                changed |= populationSettings.ObjectivePrefab != null;
            }

            if (populationSettings.EnemyPrefab == null)
            {
                populationSettings.EnemyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DefaultEnemyPrefabPath);
                changed |= populationSettings.EnemyPrefab != null;
            }

            if (populationSettings.LightFixturePrefab == null)
            {
                populationSettings.LightFixturePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DefaultLightFixturePrefabPath);
                changed |= populationSettings.LightFixturePrefab != null;
            }

            return changed;
        }
#endif

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void OnGUI()
        {
            if (!showSeedReplayHud || Application.isPlaying)
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
                GUILayout.Label($"Last Failure: {TruncateForHud(lastFailureSummary)}");
            }

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
