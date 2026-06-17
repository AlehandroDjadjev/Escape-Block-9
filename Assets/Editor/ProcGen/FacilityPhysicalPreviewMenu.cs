using EscapeBlock9.ProcGen.Authoring;
using EscapeBlock9.ProcGen.Data;
using EscapeBlock9.ProcGen.Debugging;
using EscapeBlock9.ProcGen.Population;
using EscapeBlock9.ProcGen.Placement;
using EscapeBlock9.ProcGen.Planning;
using EscapeBlock9.ProcGen.Runtime;
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace EscapeBlock9.ProcGen.Editor
{
    public static class FacilityPhysicalPreviewMenu
    {
        private const string CatalogPath = "Assets/ProcGen/Catalogs/InitialBlock9TileCatalog.asset";
        private const string PreviewRootName = "GeneratedFacilityPreview";
        private const int RandomPreviewRetries = 40;
        private const int RandomPreviewMaxPlacementAttempts = 256;

        [MenuItem("Tools/ProcGen/Generate Physical Layout Preview")]
        public static void GeneratePhysicalLayoutPreview()
        {
            GeneratePreview(BuildSmokeConfig(), "Generate Facility Layout Preview");
        }

        [MenuItem("Tools/ProcGen/Generate Random Physical Layout Preview")]
        public static void GenerateRandomPhysicalLayoutPreview()
        {
            if (!TryGenerateRandomPreview(out int seed, out string diagnostics))
            {
                Debug.LogError("Random physical layout preview failed.");
                Debug.LogError(diagnostics);
                return;
            }

            Debug.Log($"Generated random physical layout preview with seed {seed}.");
        }

        [MenuItem("Tools/ProcGen/Generate Connected 4-Room Building Preview")]
        public static void GenerateConnectedFourRoomBuildingPreview()
        {
            if (!TryGenerateConnectedRoomPreview(4, out int seed, out string diagnostics))
            {
                Debug.LogError("Connected room building preview failed.");
                Debug.LogError(diagnostics);
                return;
            }

            Debug.Log($"Generated connected 4-room building preview with seed {seed}.");
        }

        [MenuItem("Tools/ProcGen/Random Facility Preview Window")]
        public static void OpenRandomPreviewWindow()
        {
            FacilityRandomPreviewWindow.Open();
        }

        public static bool TryGenerateRandomPreview(out int seed, out string diagnostics)
        {
            int baseSeed = MakeRandomSeed();
            diagnostics = string.Empty;
            seed = baseSeed;

            for (int attempt = 0; attempt < RandomPreviewRetries; attempt++)
            {
                seed = unchecked(baseSeed + attempt * 7919);
                FacilityGraphPlanConfig config = BuildRandomConfig(seed);
                if (TryGeneratePreview(config, $"Generate Random Facility Layout Preview ({seed})", out diagnostics, RandomPreviewMaxPlacementAttempts))
                {
                    return true;
                }
            }

            return false;
        }

        public static bool TryGenerateConnectedRoomPreview(int roomCount, out int seed, out string diagnostics)
        {
            seed = MakeRandomSeed();
            roomCount = Mathf.Clamp(roomCount, 1, 12);
            return TryGenerateConnectedRoomPreview(roomCount, seed, out diagnostics);
        }

        public static bool TryGenerateConnectedRoomPreview(int roomCount, int seed, out string diagnostics)
        {
            TileCatalog catalog = AssetDatabase.LoadAssetAtPath<TileCatalog>(CatalogPath);
            if (catalog == null)
            {
                diagnostics = $"Missing TileCatalog at {CatalogPath}. Run Tools/ProcGen/Build Initial Module Catalog first.";
                return false;
            }

            if (!TryBuildMapLikeRoomLayout(catalog, Mathf.Clamp(roomCount, 1, 12), seed, out FacilityGraph graph, out ResolvedFacilityLayout layout, out diagnostics))
            {
                return false;
            }

            if (!ResolvedFacilityLayoutValidator.ValidateConnected(graph, layout, layout.Diagnostics))
            {
                diagnostics = layout.Diagnostics.ToDebugString();
                return false;
            }

            if (OccupancyValidator.AnyOverlap(layout.Tiles, new FacilityPlacementSettings().OverlapTolerance, out string overlap))
            {
                diagnostics = $"Connected room layout overlaps: {overlap}";
                return false;
            }

            InstantiatePreview(graph, layout, $"Generate Connected {roomCount}-Room Facility Preview ({seed})");
            Debug.Log(layout.ToDebugString() + layout.Diagnostics.ToDebugString());
            diagnostics = string.Empty;
            return true;
        }

        private static void GeneratePreview(FacilityGraphPlanConfig graphConfig, string undoName)
        {
            if (!TryGeneratePreview(graphConfig, undoName, out string diagnostics))
            {
                Debug.LogError("Physical layout preview failed.");
                Debug.LogError(diagnostics);
            }
        }

        private static bool TryGeneratePreview(
            FacilityGraphPlanConfig graphConfig,
            string undoName,
            out string diagnostics,
            int maxPlacementAttempts = int.MaxValue)
        {
            TileCatalog catalog = AssetDatabase.LoadAssetAtPath<TileCatalog>(CatalogPath);
            if (catalog == null)
            {
                diagnostics = $"Missing TileCatalog at {CatalogPath}. Run Tools/ProcGen/Build Initial Module Catalog first.";
                return false;
            }

            FacilityGraph graph = new FacilityGraphPlanner().Plan(graphConfig);
            ResolvedFacilityLayout layout = new CustomFacilityLayoutSolver().Solve(graph, catalog, graphConfig.MasterSeed);
            if (layout.Tiles.Count != graph.Nodes.Count)
            {
                diagnostics = layout.Diagnostics.ToDebugString();
                return false;
            }

            if (!ResolvedFacilityLayoutValidator.ValidateConnected(graph, layout, layout.Diagnostics))
            {
                diagnostics = layout.Diagnostics.ToDebugString();
                return false;
            }

            if (layout.PlacementAttempts > maxPlacementAttempts)
            {
                diagnostics = $"Rejected seed {graphConfig.MasterSeed}: layout solved in {layout.PlacementAttempts} placement attempts, above random preview limit {maxPlacementAttempts}.";
                return false;
            }

            InstantiatePreview(graph, layout, undoName);
            diagnostics = string.Empty;
            return true;
        }

        private static void InstantiatePreview(FacilityGraph graph, ResolvedFacilityLayout layout, string undoName)
        {
            GameObject[] existing = UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include);
            for (int i = 0; i < existing.Length; i++)
            {
                GameObject candidate = existing[i];
                if (candidate == null)
                {
                    continue;
                }

                if (candidate.name == PreviewRootName)
                {
                    Undo.DestroyObjectImmediate(candidate);
                }
            }

            GameObject root = new GameObject(PreviewRootName);
            Undo.RegisterCreatedObjectUndo(root, undoName);
            var instanceTiles = new Dictionary<int, EscapeBlock9.ProcGen.Authoring.Tile>();

            for (int i = 0; i < layout.Tiles.Count; i++)
            {
                PlacedTile placed = layout.Tiles[i];
                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(placed.Definition.Prefab, root.transform);
                instance.name = $"Node_{placed.NodeId:00}_{placed.ModuleId}";
                Undo.RegisterCreatedObjectUndo(instance, undoName);
                instance.transform.SetPositionAndRotation(placed.Position, placed.Rotation);
                GeneratedDoorVisualStripper.RemoveDoorVisuals(instance);
                EscapeBlock9.ProcGen.Authoring.Tile tile = instance.GetComponent<EscapeBlock9.ProcGen.Authoring.Tile>();
                if (tile != null)
                {
                    instanceTiles[placed.NodeId] = tile;
                }
            }

            PostLayoutConnectionResolution resolution = new PostLayoutConnectionResolver().Resolve(layout, graph, instanceTiles);
            PostLayoutConnectionMetadata metadata = root.AddComponent<PostLayoutConnectionMetadata>();
            metadata.Apply(resolution);
            root.AddComponent<FacilityConnectionDebugOverlay>();

            var populationSettings = new FacilityPopulationSettings
            {
                LootPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/arhitektura/KeyItem.prefab"),
                ObjectivePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/arhitektura/KeyItem.prefab"),
                EnemyPrefab = null,
                EnableVerbosePopulationLogs = true
            };

            Transform populationRoot = new GameObject("Population").transform;
            populationRoot.SetParent(root.transform, false);
            FacilityPopulationReport populationReport = new FacilityPopulationPipeline(populationSettings)
                .Populate(populationRoot, graph, layout, instanceTiles, metadata);
            FacilityPopulationMetadata populationMetadata = root.AddComponent<FacilityPopulationMetadata>();
            populationMetadata.Apply(populationReport);
            root.AddComponent<FacilityPopulationDebugOverlay>();
            var generationData = root.AddComponent<FacilityGenerationDebugData>();
            generationData.Apply(
                BuildStatistics(graph, layout),
                BuildModuleUsage(layout),
                BuildFailureReasonCounts(layout.Diagnostics),
                BuildNodeRecords(graph, layout),
                BuildEdgeRecords(graph, layout),
                BuildOccupancy(layout),
                string.Empty);
            root.AddComponent<FacilityGenerationDebugOverlay>();

            Debug.Log(layout.ToDebugString() + layout.Diagnostics.ToDebugString() + resolution.ToDebugString() + populationReport.ToDebugString());
        }

        private static FacilityGraphPlanConfig BuildSmokeConfig()
        {
            return new FacilityGraphPlanConfig
            {
                MasterSeed = 13579,
                MainPathLengthRange = new IntRange(4, 4),
                BranchCountRange = new IntRange(2, 2),
                BranchLengthRange = new IntRange(1, 1),
                LoopChance = 0f,
                FireExitCountRange = new IntRange(1, 1),
                FireExitChance = 1f,
                VerticalTransitionChance = 0f,
                PortalChance = 0f,
                MaxAttempts = 4,
            };
        }

        private static FacilityGraphPlanConfig BuildRandomConfig(int seed)
        {
            return new FacilityGraphPlanConfig
            {
                MasterSeed = seed,
                MainPathLengthRange = new IntRange(4, 6),
                BranchCountRange = new IntRange(1, 2),
                BranchLengthRange = new IntRange(1, 1),
                LoopChance = 0f,
                FireExitCountRange = new IntRange(0, 1),
                FireExitChance = 0.5f,
                VerticalTransitionChance = 0f,
                PortalChance = 0f,
                MaxAttempts = 8,
            };
        }

        private static bool TryBuildConnectedRoomLayout(
            TileCatalog catalog,
            int roomCount,
            int seed,
            out FacilityGraph graph,
            out ResolvedFacilityLayout layout,
            out string diagnostics)
        {
            graph = new FacilityGraph(seed, 0);
            layout = null;
            diagnostics = string.Empty;

            TileDefinition start = FindDefinition(catalog, "start_exit_lobby");
            TileDefinition junction = FindDefinition(catalog, "corridor_cross_junction_3m");
            TileDefinition straight = FindDefinition(catalog, "corridor_straight_8m");
            TileDefinition deadEnd = FindDefinition(catalog, "corridor_dead_end");
            List<TileDefinition> rooms = FindRoomDefinitions(catalog);

            if (start == null || junction == null || straight == null || deadEnd == null || rooms.Count == 0)
            {
                diagnostics = "Connected room layout requires start_exit_lobby, corridor_cross_junction_3m, corridor_straight_8m, corridor_dead_end, and at least one room/special room definition.";
                return false;
            }

            var random = new System.Random(seed);
            var settings = new FacilityPlacementSettings();
            var tiles = new List<PlacedTile>();
            var connections = new List<PlacedDoorwayConnection>();
            var placedByNode = new Dictionary<int, PlacedTile>();
            var definitionByNode = new Dictionary<int, TileDefinition>();

            FacilityGraphNode startNode = graph.AddNode(FacilityGraphNodeRole.Start, 0, -1, 0, 0);
            PlacedTile startTile = AddPlacedTile(startNode.Id, start, Vector3.zero, Quaternion.identity, settings, tiles, placedByNode, definitionByNode);

            int mainPathIndex = 1;

            FacilityGraphNode entryStraightNode = graph.AddNode(FacilityGraphNodeRole.MainPath, mainPathIndex++, -1, mainPathIndex, 0);
            SnapTransformResult entryStraightSnap = SnapToDoorway(startTile, start, FindDoorwayIndex(start, "interior_door"), straight, "south");
            PlacedTile entryStraightTile = AddPlacedTile(entryStraightNode.Id, straight, entryStraightSnap.Position, entryStraightSnap.Rotation, settings, tiles, placedByNode, definitionByNode);
            FacilityGraphEdge entryEdge = graph.AddEdge(startNode.Id, entryStraightNode.Id, FacilityGraphEdgeRole.MainPath);
            connections.Add(new PlacedDoorwayConnection(entryEdge.Id, startNode.Id, FindDoorwayIndex(start, "interior_door"), entryStraightNode.Id, FindDoorwayIndex(straight, "south")));

            PlacedTile previousSpineTile = entryStraightTile;
            TileDefinition previousSpineDefinition = straight;
            int previousExitDoorway = FindDoorwayIndex(straight, "north");
            var roomUseCounts = new Dictionary<string, int>();

            for (int roomIndex = 0; roomIndex < roomCount; roomIndex++)
            {
                FacilityGraphNode junctionNode = graph.AddNode(FacilityGraphNodeRole.MainPath, mainPathIndex++, -1, mainPathIndex, 0);
                SnapTransformResult junctionSnap = SnapToDoorway(previousSpineTile, previousSpineDefinition, previousExitDoorway, junction, "south");
                PlacedTile junctionTile = AddPlacedTile(junctionNode.Id, junction, junctionSnap.Position, junctionSnap.Rotation, settings, tiles, placedByNode, definitionByNode);
                FacilityGraphEdge spineEdge = graph.AddEdge(previousSpineTile.NodeId, junctionNode.Id, FacilityGraphEdgeRole.MainPath);
                connections.Add(new PlacedDoorwayConnection(spineEdge.Id, previousSpineTile.NodeId, previousExitDoorway, junctionNode.Id, FindDoorwayIndex(junction, "south")));

                bool useEast = random.NextDouble() >= 0.5d;
                string branchDoorway = useEast ? "east" : "west";
                TileDefinition roomDefinition = SelectRoomDefinition(rooms, random, roomUseCounts);
                FacilityGraphNode roomNode = graph.AddNode(FacilityGraphNodeRole.DeadEnd, -1, roomIndex, 1, 0);
                SnapTransformResult roomSnap = SnapToDoorway(junctionTile, junction, FindDoorwayIndex(junction, branchDoorway), roomDefinition, "room_door");
                PlacedTile roomTile = AddPlacedTile(roomNode.Id, roomDefinition, roomSnap.Position, roomSnap.Rotation, settings, tiles, placedByNode, definitionByNode);
                FacilityGraphEdge roomEdge = graph.AddEdge(junctionNode.Id, roomNode.Id, FacilityGraphEdgeRole.DeadEnd);
                connections.Add(new PlacedDoorwayConnection(roomEdge.Id, junctionNode.Id, FindDoorwayIndex(junction, branchDoorway), roomNode.Id, FindDoorwayIndex(roomDefinition, "room_door")));
                graph.AddBranch(new FacilityGraphBranch(roomIndex, junctionNode.Id, new[] { roomNode.Id }, FacilityGraphEdgeRole.DeadEnd));

                previousSpineTile = junctionTile;
                previousSpineDefinition = junction;
                previousExitDoorway = FindDoorwayIndex(junction, "north");

                if (roomIndex < roomCount - 1)
                {
                    FacilityGraphNode straightNode = graph.AddNode(FacilityGraphNodeRole.MainPath, mainPathIndex++, -1, mainPathIndex, 0);
                    SnapTransformResult straightSnap = SnapToDoorway(previousSpineTile, previousSpineDefinition, previousExitDoorway, straight, "south");
                    PlacedTile straightTile = AddPlacedTile(straightNode.Id, straight, straightSnap.Position, straightSnap.Rotation, settings, tiles, placedByNode, definitionByNode);
                    FacilityGraphEdge straightEdge = graph.AddEdge(previousSpineTile.NodeId, straightNode.Id, FacilityGraphEdgeRole.MainPath);
                    connections.Add(new PlacedDoorwayConnection(straightEdge.Id, previousSpineTile.NodeId, previousExitDoorway, straightNode.Id, FindDoorwayIndex(straight, "south")));

                    previousSpineTile = straightTile;
                    previousSpineDefinition = straight;
                    previousExitDoorway = FindDoorwayIndex(straight, "north");
                }
            }

            FacilityGraphNode capNode = graph.AddNode(FacilityGraphNodeRole.DeadEnd, -1, roomCount, 1, 0);
            SnapTransformResult capSnap = SnapToDoorway(previousSpineTile, previousSpineDefinition, previousExitDoorway, deadEnd, "south");
            AddPlacedTile(capNode.Id, deadEnd, capSnap.Position, capSnap.Rotation, settings, tiles, placedByNode, definitionByNode);
            FacilityGraphEdge capEdge = graph.AddEdge(previousSpineTile.NodeId, capNode.Id, FacilityGraphEdgeRole.DeadEnd);
            connections.Add(new PlacedDoorwayConnection(capEdge.Id, previousSpineTile.NodeId, previousExitDoorway, capNode.Id, FindDoorwayIndex(deadEnd, "south")));

            var placementDiagnostics = new PlacementFailureDiagnostics();
            layout = new ResolvedFacilityLayout(seed, tiles, connections, placementDiagnostics, 0);
            if (OccupancyValidator.AnyOverlap(layout.Tiles, settings.OverlapTolerance, out string overlap))
            {
                diagnostics = $"Connected room layout overlaps: {overlap}";
                return false;
            }

            return true;
        }

        private static bool TryBuildMapLikeRoomLayout(
            TileCatalog catalog,
            int roomCount,
            int seed,
            out FacilityGraph graph,
            out ResolvedFacilityLayout layout,
            out string diagnostics)
        {
            graph = new FacilityGraph(seed, 0);
            layout = null;
            diagnostics = string.Empty;

            TileDefinition start = FindDefinition(catalog, "start_exit_lobby");
            TileDefinition junction = FindDefinition(catalog, "corridor_cross_junction_3m");
            TileDefinition straight = FindDefinition(catalog, "corridor_straight_8m");
            List<TileDefinition> rooms = FindRoomDefinitions(catalog);

            if (start == null || junction == null || straight == null || rooms.Count == 0)
            {
                diagnostics = "Map-like layout requires start_exit_lobby, corridor_cross_junction_3m, corridor_straight_8m, and at least one room/special room definition.";
                return false;
            }

            var random = new System.Random(seed);
            var settings = new FacilityPlacementSettings();
            var tiles = new List<PlacedTile>();
            var placedByNode = new Dictionary<int, PlacedTile>();
            var definitionByNode = new Dictionary<int, TileDefinition>();
            var roomUseCounts = new Dictionary<string, int>();
            var cellToNode = new Dictionary<Vector2Int, int>();
            var corridorAdjacency = new Dictionary<Vector2Int, HashSet<GridDirection>>();
            var usedRoomDirections = new Dictionary<Vector2Int, HashSet<GridDirection>>();

            List<Vector2Int> cells = BuildCorridorCellGraph(roomCount, random, corridorAdjacency);

            FacilityGraphNode startNode = graph.AddNode(FacilityGraphNodeRole.Start, 0, -1, 0, 0);
            PlacedTile startTile = AddPlacedTile(startNode.Id, start, Vector3.zero, Quaternion.identity, settings, tiles, placedByNode, definitionByNode);

            FacilityGraphNode entryStraightNode = graph.AddNode(FacilityGraphNodeRole.MainPath, 1, -1, 1, 0);
            SnapTransformResult entryStraightSnap = SnapToDoorway(startTile, start, FindDoorwayIndex(start, "interior_door"), straight, "south");
            AddPlacedTile(entryStraightNode.Id, straight, entryStraightSnap.Position, entryStraightSnap.Rotation, settings, tiles, placedByNode, definitionByNode);
            graph.AddEdge(startNode.Id, entryStraightNode.Id, FacilityGraphEdgeRole.MainPath);

            for (int i = 0; i < cells.Count; i++)
            {
                Vector2Int cell = cells[i];
                FacilityGraphNode node = graph.AddNode(FacilityGraphNodeRole.MainPath, i + 2, -1, i + 2, 0);
                Vector3 position = CellToWorld(cell);
                AddPlacedTile(node.Id, junction, position, Quaternion.identity, settings, tiles, placedByNode, definitionByNode);
                cellToNode[cell] = node.Id;
                usedRoomDirections[cell] = new HashSet<GridDirection>();
            }

            FacilityGraphEdge entryEdge = graph.AddEdge(entryStraightNode.Id, cellToNode[Vector2Int.zero], FacilityGraphEdgeRole.MainPath);

            var edgeKeys = new HashSet<string>();
            for (int i = 0; i < cells.Count; i++)
            {
                Vector2Int cell = cells[i];
                foreach (GridDirection direction in corridorAdjacency[cell])
                {
                    Vector2Int neighbor = cell + DirectionToVector(direction);
                    if (!cellToNode.ContainsKey(neighbor))
                    {
                        continue;
                    }

                    string key = EdgeKey(cell, neighbor);
                    if (!edgeKeys.Add(key))
                    {
                        continue;
                    }

                    Vector3 cellWorld = CellToWorld(cell);
                    Vector3 neighborWorld = CellToWorld(neighbor);
                    Vector3 straightPosition = (cellWorld + neighborWorld) * 0.5f;
                    Quaternion straightRotation = IsHorizontal(direction) ? Quaternion.Euler(0f, 90f, 0f) : Quaternion.identity;
                    FacilityGraphNode straightNode = graph.AddNode(FacilityGraphNodeRole.MainPath, graph.MainPathNodeIds.Count, -1, graph.MainPathNodeIds.Count, 0);
                    AddPlacedTile(straightNode.Id, straight, straightPosition, straightRotation, settings, tiles, placedByNode, definitionByNode);

                    FacilityGraphEdge first = graph.AddEdge(cellToNode[cell], straightNode.Id, FacilityGraphEdgeRole.MainPath);
                    FacilityGraphEdge second = graph.AddEdge(straightNode.Id, cellToNode[neighbor], FacilityGraphEdgeRole.MainPath);
                }
            }

            if (!TryPlaceMapRooms(roomCount, rooms, junction, random, settings, graph, tiles, placedByNode, definitionByNode, roomUseCounts, cellToNode, corridorAdjacency, usedRoomDirections, out diagnostics))
            {
                return false;
            }

            var connections = new List<PlacedDoorwayConnection>();
            if (!TryBuildMatchedConnections(graph, placedByNode, definitionByNode, connections, out diagnostics))
            {
                return false;
            }

            layout = new ResolvedFacilityLayout(seed, tiles, connections, new PlacementFailureDiagnostics(), 0);
            if (OccupancyValidator.AnyOverlap(layout.Tiles, settings.OverlapTolerance, out string overlap))
            {
                diagnostics = $"Map-like room layout overlaps: {overlap}";
                return false;
            }

            return true;
        }

        private static List<Vector2Int> BuildCorridorCellGraph(
            int roomCount,
            System.Random random,
            Dictionary<Vector2Int, HashSet<GridDirection>> adjacency)
        {
            int targetCells = Mathf.Clamp(roomCount + 4, 6, 24);
            int horizontalLimit = Mathf.Clamp(2 + roomCount / 3, 3, 5);
            int northLimit = Mathf.Clamp(3 + roomCount / 2, 4, 9);
            var cells = new List<Vector2Int> { Vector2Int.zero };
            var occupied = new HashSet<Vector2Int> { Vector2Int.zero };
            adjacency[Vector2Int.zero] = new HashSet<GridDirection>();

            for (int attempts = 0; cells.Count < targetCells && attempts < 500; attempts++)
            {
                Vector2Int from = cells[random.Next(cells.Count)];
                GridDirection[] directions = ShuffledDirections(random);
                for (int i = 0; i < directions.Length; i++)
                {
                    GridDirection direction = directions[i];
                    if (from == Vector2Int.zero && direction == GridDirection.South)
                    {
                        continue;
                    }

                    Vector2Int to = from + DirectionToVector(direction);
                    if (occupied.Contains(to) || to.y < 0 || to.y > northLimit || Mathf.Abs(to.x) > horizontalLimit)
                    {
                        continue;
                    }

                    occupied.Add(to);
                    cells.Add(to);
                    adjacency[to] = new HashSet<GridDirection>();
                    adjacency[from].Add(direction);
                    adjacency[to].Add(Opposite(direction));
                    break;
                }
            }

            for (int i = 0; i < cells.Count; i++)
            {
                Vector2Int cell = cells[i];
                GridDirection[] directions = ShuffledDirections(random);
                for (int d = 0; d < directions.Length; d++)
                {
                    if (random.NextDouble() > 0.22d)
                    {
                        continue;
                    }

                    GridDirection direction = directions[d];
                    Vector2Int to = cell + DirectionToVector(direction);
                    if (!occupied.Contains(to) || adjacency[cell].Contains(direction))
                    {
                        continue;
                    }

                    adjacency[cell].Add(direction);
                    adjacency[to].Add(Opposite(direction));
                }
            }

            return cells;
        }

        private static bool TryPlaceMapRooms(
            int roomCount,
            IReadOnlyList<TileDefinition> rooms,
            TileDefinition junction,
            System.Random random,
            FacilityPlacementSettings settings,
            FacilityGraph graph,
            List<PlacedTile> tiles,
            Dictionary<int, PlacedTile> placedByNode,
            Dictionary<int, TileDefinition> definitionByNode,
            Dictionary<string, int> roomUseCounts,
            Dictionary<Vector2Int, int> cellToNode,
            Dictionary<Vector2Int, HashSet<GridDirection>> corridorAdjacency,
            Dictionary<Vector2Int, HashSet<GridDirection>> usedRoomDirections,
            out string diagnostics)
        {
            diagnostics = string.Empty;

            for (int roomIndex = 0; roomIndex < roomCount; roomIndex++)
            {
                TileDefinition roomDefinition = SelectRoomDefinition(rooms, random, roomUseCounts);
                List<RoomSocketCandidate> candidates = BuildRoomSocketCandidates(cellToNode, corridorAdjacency, usedRoomDirections, random);
                bool placedRoom = false;

                for (int i = 0; i < candidates.Count; i++)
                {
                    RoomSocketCandidate candidate = candidates[i];
                    int corridorNodeId = cellToNode[candidate.Cell];
                    PlacedTile corridorTile = placedByNode[corridorNodeId];
                    string doorwayId = DirectionToDoorwayId(candidate.Direction);
                    SnapTransformResult roomSnap = SnapToDoorway(corridorTile, junction, FindDoorwayIndex(junction, doorwayId), roomDefinition, "room_door");
                    List<PlacedOccupancyBox> boxes = OccupancyValidator.BuildBoxes(roomDefinition.TilePrefab, roomSnap.Position, roomSnap.Rotation, settings.OccupancyPadding);
                    if (OccupancyValidator.WouldOverlap(boxes, tiles, settings.OverlapTolerance, out _))
                    {
                        continue;
                    }

                    FacilityGraphNode roomNode = graph.AddNode(FacilityGraphNodeRole.DeadEnd, -1, roomIndex, 1, 0);
                    var placed = new PlacedTile(roomNode.Id, roomDefinition, roomSnap.Position, roomSnap.Rotation, boxes);
                    tiles.Add(placed);
                    placedByNode[roomNode.Id] = placed;
                    definitionByNode[roomNode.Id] = roomDefinition;
                    graph.AddEdge(corridorNodeId, roomNode.Id, FacilityGraphEdgeRole.DeadEnd);
                    graph.AddBranch(new FacilityGraphBranch(roomIndex, corridorNodeId, new[] { roomNode.Id }, FacilityGraphEdgeRole.DeadEnd));
                    usedRoomDirections[candidate.Cell].Add(candidate.Direction);
                    placedRoom = true;
                    break;
                }

                if (!placedRoom)
                {
                    diagnostics = $"Could not place room {roomIndex + 1}/{roomCount} without overlap.";
                    return false;
                }
            }

            return true;
        }

        private static List<RoomSocketCandidate> BuildRoomSocketCandidates(
            Dictionary<Vector2Int, int> cellToNode,
            Dictionary<Vector2Int, HashSet<GridDirection>> corridorAdjacency,
            Dictionary<Vector2Int, HashSet<GridDirection>> usedRoomDirections,
            System.Random random)
        {
            var candidates = new List<RoomSocketCandidate>();
            foreach (KeyValuePair<Vector2Int, int> pair in cellToNode)
            {
                Vector2Int cell = pair.Key;
                for (int i = 0; i < CardinalDirections.Length; i++)
                {
                    GridDirection direction = CardinalDirections[i];
                    if (cell == Vector2Int.zero && direction == GridDirection.South)
                    {
                        continue;
                    }

                    if (corridorAdjacency[cell].Contains(direction) || usedRoomDirections[cell].Contains(direction))
                    {
                        continue;
                    }

                    candidates.Add(new RoomSocketCandidate(cell, direction));
                }
            }

            Shuffle(candidates, random);
            return candidates;
        }

        private static bool TryBuildMatchedConnections(
            FacilityGraph graph,
            IReadOnlyDictionary<int, PlacedTile> placedByNode,
            IReadOnlyDictionary<int, TileDefinition> definitionByNode,
            List<PlacedDoorwayConnection> connections,
            out string diagnostics)
        {
            diagnostics = string.Empty;
            for (int i = 0; i < graph.Edges.Count; i++)
            {
                FacilityGraphEdge edge = graph.Edges[i];
                if (!placedByNode.TryGetValue(edge.FromNodeId, out PlacedTile fromTile) ||
                    !placedByNode.TryGetValue(edge.ToNodeId, out PlacedTile toTile) ||
                    !definitionByNode.TryGetValue(edge.FromNodeId, out TileDefinition fromDefinition) ||
                    !definitionByNode.TryGetValue(edge.ToNodeId, out TileDefinition toDefinition))
                {
                    diagnostics = $"Missing placed tile for edge {edge.Id}.";
                    return false;
                }

                Doorway[] fromDoorways = fromDefinition.TilePrefab.GetDoorways();
                Doorway[] toDoorways = toDefinition.TilePrefab.GetDoorways();
                int bestFrom = -1;
                int bestTo = -1;
                float bestDistance = float.MaxValue;
                for (int fromIndex = 0; fromIndex < fromDoorways.Length; fromIndex++)
                {
                    Vector3 fromPosition = fromTile.DoorwayPosition(fromDoorways[fromIndex]);
                    Vector3 fromForward = fromTile.DoorwayForward(fromDoorways[fromIndex]);
                    for (int toIndex = 0; toIndex < toDoorways.Length; toIndex++)
                    {
                        Vector3 toPosition = toTile.DoorwayPosition(toDoorways[toIndex]);
                        float distance = Vector3.Distance(fromPosition, toPosition);
                        if (distance > 0.06f || distance >= bestDistance)
                        {
                            continue;
                        }

                        float dot = Vector3.Dot(fromForward, toTile.DoorwayForward(toDoorways[toIndex]));
                        if (dot > -0.98f)
                        {
                            continue;
                        }

                        bestDistance = distance;
                        bestFrom = fromIndex;
                        bestTo = toIndex;
                    }
                }

                if (bestFrom < 0 || bestTo < 0)
                {
                    diagnostics = $"Could not match physical doorway pair for edge {edge.Id} {edge.FromNodeId}->{edge.ToNodeId}.";
                    return false;
                }

                connections.Add(new PlacedDoorwayConnection(edge.Id, edge.FromNodeId, bestFrom, edge.ToNodeId, bestTo));
            }

            return true;
        }

        private static Vector3 CellToWorld(Vector2Int cell)
        {
            return new Vector3(4f + cell.x * 14f, 0f, 18.05f + cell.y * 14f);
        }

        private static string EdgeKey(Vector2Int a, Vector2Int b)
        {
            if (a.x > b.x || (a.x == b.x && a.y > b.y))
            {
                Vector2Int temp = a;
                a = b;
                b = temp;
            }

            return $"{a.x},{a.y}:{b.x},{b.y}";
        }

        private static bool IsHorizontal(GridDirection direction)
        {
            return direction == GridDirection.East || direction == GridDirection.West;
        }

        private static string DirectionToDoorwayId(GridDirection direction)
        {
            switch (direction)
            {
                case GridDirection.North:
                    return "north";
                case GridDirection.South:
                    return "south";
                case GridDirection.East:
                    return "east";
                default:
                    return "west";
            }
        }

        private static Vector2Int DirectionToVector(GridDirection direction)
        {
            switch (direction)
            {
                case GridDirection.North:
                    return Vector2Int.up;
                case GridDirection.South:
                    return Vector2Int.down;
                case GridDirection.East:
                    return Vector2Int.right;
                default:
                    return Vector2Int.left;
            }
        }

        private static GridDirection Opposite(GridDirection direction)
        {
            switch (direction)
            {
                case GridDirection.North:
                    return GridDirection.South;
                case GridDirection.South:
                    return GridDirection.North;
                case GridDirection.East:
                    return GridDirection.West;
                default:
                    return GridDirection.East;
            }
        }

        private static GridDirection[] ShuffledDirections(System.Random random)
        {
            GridDirection[] directions = (GridDirection[])CardinalDirections.Clone();
            for (int i = directions.Length - 1; i > 0; i--)
            {
                int swap = random.Next(i + 1);
                GridDirection temp = directions[i];
                directions[i] = directions[swap];
                directions[swap] = temp;
            }

            return directions;
        }

        private static void Shuffle<T>(IList<T> list, System.Random random)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int swap = random.Next(i + 1);
                T temp = list[i];
                list[i] = list[swap];
                list[swap] = temp;
            }
        }

        private enum GridDirection
        {
            North,
            South,
            East,
            West
        }

        private readonly struct RoomSocketCandidate
        {
            public RoomSocketCandidate(Vector2Int cell, GridDirection direction)
            {
                Cell = cell;
                Direction = direction;
            }

            public Vector2Int Cell { get; }
            public GridDirection Direction { get; }
        }

        private static readonly GridDirection[] CardinalDirections =
        {
            GridDirection.North,
            GridDirection.South,
            GridDirection.East,
            GridDirection.West
        };

        private static PlacedTile AddPlacedTile(
            int nodeId,
            TileDefinition definition,
            Vector3 position,
            Quaternion rotation,
            FacilityPlacementSettings settings,
            List<PlacedTile> tiles,
            Dictionary<int, PlacedTile> placedByNode,
            Dictionary<int, TileDefinition> definitionByNode)
        {
            List<PlacedOccupancyBox> boxes = OccupancyValidator.BuildBoxes(definition.TilePrefab, position, rotation, settings.OccupancyPadding);
            var placed = new PlacedTile(nodeId, definition, position, rotation, boxes);
            tiles.Add(placed);
            placedByNode[nodeId] = placed;
            definitionByNode[nodeId] = definition;
            return placed;
        }

        private static SnapTransformResult SnapToDoorway(
            PlacedTile openTile,
            TileDefinition openDefinition,
            int openDoorwayIndex,
            TileDefinition candidateDefinition,
            string candidateDoorwayId)
        {
            Doorway[] openDoorways = openDefinition.TilePrefab.GetDoorways();
            Doorway openDoorway = openDoorways[openDoorwayIndex];
            Doorway candidateDoorway = candidateDefinition.TilePrefab.GetDoorways()[FindDoorwayIndex(candidateDefinition, candidateDoorwayId)];
            return SnapTransformSolver.Solve(openDoorway, openTile, candidateDoorway);
        }

        private static int FindDoorwayIndex(TileDefinition definition, string connectorIdOrName)
        {
            Doorway[] doorways = definition.TilePrefab.GetDoorways();
            for (int i = 0; i < doorways.Length; i++)
            {
                Doorway doorway = doorways[i];
                if (string.Equals(doorway.ConnectorId, connectorIdOrName, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(doorway.name, connectorIdOrName, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }

            return -1;
        }

        private static TileDefinition FindDefinition(TileCatalog catalog, string moduleId)
        {
            for (int i = 0; i < catalog.Definitions.Count; i++)
            {
                TileDefinition definition = catalog.Definitions[i];
                if (definition != null && string.Equals(definition.ModuleId, moduleId, StringComparison.OrdinalIgnoreCase))
                {
                    return definition;
                }
            }

            return null;
        }

        private static List<TileDefinition> FindRoomDefinitions(TileCatalog catalog)
        {
            var rooms = new List<TileDefinition>();
            for (int i = 0; i < catalog.Definitions.Count; i++)
            {
                TileDefinition definition = catalog.Definitions[i];
                if (definition == null || definition.TilePrefab == null)
                {
                    continue;
                }

                if ((definition.Category == TileCategory.Room || definition.Category == TileCategory.Special) &&
                    HasTag(definition, "room"))
                {
                    rooms.Add(definition);
                }
            }

            return rooms;
        }

        private static TileDefinition SelectRoomDefinition(
            IReadOnlyList<TileDefinition> rooms,
            System.Random random,
            Dictionary<string, int> useCounts)
        {
            var candidates = new List<TileDefinition>();
            for (int i = 0; i < rooms.Count; i++)
            {
                TileDefinition room = rooms[i];
                int current = useCounts.TryGetValue(room.ModuleId, out int count) ? count : 0;
                int maxUse = room.Unique ? 1 : room.MaxUseCount;
                if (maxUse >= 0 && current >= maxUse)
                {
                    continue;
                }

                candidates.Add(room);
            }

            if (candidates.Count == 0)
            {
                for (int i = 0; i < rooms.Count; i++)
                {
                    if (!rooms[i].Unique)
                    {
                        candidates.Add(rooms[i]);
                    }
                }
            }

            if (candidates.Count == 0)
            {
                candidates.Add(rooms[0]);
            }

            TileDefinition selected = candidates[random.Next(candidates.Count)];
            useCounts[selected.ModuleId] = useCounts.TryGetValue(selected.ModuleId, out int selectedCount) ? selectedCount + 1 : 1;
            return selected;
        }

        private static bool HasTag(TileDefinition definition, string tag)
        {
            string[] tags = definition.Tags;
            if (tags == null)
            {
                return false;
            }

            for (int i = 0; i < tags.Length; i++)
            {
                if (string.Equals(tags[i], tag, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static int MakeRandomSeed()
        {
            unchecked
            {
                int ticks = (int)DateTime.UtcNow.Ticks;
                int frame = Time.frameCount * 397;
                int editor = Environment.TickCount * 31;
                return ticks ^ frame ^ editor;
            }
        }

        private static GenerationStatistics BuildStatistics(FacilityGraph graph, ResolvedFacilityLayout layout)
        {
            int deadEnds = 0;
            int loops = 0;
            int fireExits = 0;
            int portals = 0;

            for (int i = 0; i < graph.Nodes.Count; i++)
            {
                if (graph.Nodes[i].Role == FacilityGraphNodeRole.DeadEnd)
                {
                    deadEnds++;
                }
            }

            for (int i = 0; i < graph.Edges.Count; i++)
            {
                FacilityGraphEdgeRole role = graph.Edges[i].Role;
                if (role == FacilityGraphEdgeRole.Loop)
                {
                    loops++;
                }
                else if (role == FacilityGraphEdgeRole.FireExit)
                {
                    fireExits++;
                }
                else if (role == FacilityGraphEdgeRole.Portal)
                {
                    portals++;
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
                GenerationDurationSeconds = 0f
            };
        }

        private static List<ModuleUsageCount> BuildModuleUsage(ResolvedFacilityLayout layout)
        {
            var map = new Dictionary<string, int>();
            for (int i = 0; i < layout.Tiles.Count; i++)
            {
                string moduleId = layout.Tiles[i].ModuleId;
                map[moduleId] = map.TryGetValue(moduleId, out int count) ? count + 1 : 1;
            }

            var result = new List<ModuleUsageCount>(map.Count);
            foreach (KeyValuePair<string, int> pair in map)
            {
                result.Add(new ModuleUsageCount { ModuleId = pair.Key, Count = pair.Value });
            }

            return result;
        }

        private static List<FailureReasonCount> BuildFailureReasonCounts(PlacementFailureDiagnostics diagnostics)
        {
            var map = new Dictionary<PlacementFailureReason, int>();
            if (diagnostics != null)
            {
                for (int i = 0; i < diagnostics.Failures.Count; i++)
                {
                    PlacementFailureReason reason = diagnostics.Failures[i].Reason;
                    map[reason] = map.TryGetValue(reason, out int count) ? count + 1 : 1;
                }
            }

            var result = new List<FailureReasonCount>(map.Count);
            foreach (KeyValuePair<PlacementFailureReason, int> pair in map)
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
                Bounds bounds = BuildBounds(tile);
                FacilityGraphNode node = graph.GetNode(tile.NodeId);
                nodes.Add(new DebugNodeRecord
                {
                    NodeId = tile.NodeId,
                    ModuleId = tile.ModuleId,
                    Role = node.Role,
                    IsMainPath = mainPathLookup.ContainsKey(tile.NodeId),
                    MainPathIndex = mainPathLookup.TryGetValue(tile.NodeId, out int index) ? index : -1,
                    WorldPosition = bounds.center,
                    Size = bounds.size
                });
            }

            return nodes;
        }

        private static List<DebugEdgeRecord> BuildEdgeRecords(FacilityGraph graph, ResolvedFacilityLayout layout)
        {
            var connected = new HashSet<int>();
            for (int i = 0; i < layout.Connections.Count; i++)
            {
                connected.Add(layout.Connections[i].EdgeId);
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
                    Connected = connected.Contains(edge.Id)
                });
            }

            return edges;
        }

        private static List<DebugOccupancyRecord> BuildOccupancy(ResolvedFacilityLayout layout)
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

        private static Bounds BuildBounds(PlacedTile tile)
        {
            if (tile.OccupancyBoxes.Count == 0)
            {
                return new Bounds(tile.Position, Vector3.one * 2f);
            }

            Bounds bounds = tile.OccupancyBoxes[0].Bounds;
            for (int i = 1; i < tile.OccupancyBoxes.Count; i++)
            {
                bounds.Encapsulate(tile.OccupancyBoxes[i].Bounds);
            }

            return bounds;
        }
    }

    public sealed class FacilityRandomPreviewWindow : EditorWindow
    {
        private string lastSeedText = "No random preview generated yet.";
        private int connectedRoomCount = 4;
        private Vector2 scroll;

        public static void Open()
        {
            GetWindow<FacilityRandomPreviewWindow>("Random Facility");
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Random Facility Preview", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            if (GUILayout.Button("Generate Random Building", GUILayout.Height(38f)))
            {
                if (FacilityPhysicalPreviewMenu.TryGenerateRandomPreview(out int seed, out string diagnostics))
                {
                    lastSeedText = $"Generated seed: {seed}";
                }
                else
                {
                    lastSeedText = diagnostics;
                }
            }

            EditorGUILayout.Space();
            connectedRoomCount = EditorGUILayout.IntSlider("Rooms", connectedRoomCount, 1, 12);
            if (GUILayout.Button("Generate Connected Room Building", GUILayout.Height(38f)))
            {
                if (FacilityPhysicalPreviewMenu.TryGenerateConnectedRoomPreview(connectedRoomCount, out int seed, out string diagnostics))
                {
                    lastSeedText = $"Generated connected {connectedRoomCount}-room seed: {seed}";
                }
                else
                {
                    lastSeedText = diagnostics;
                }
            }

            EditorGUILayout.Space();
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.HelpBox(lastSeedText, MessageType.Info);
            EditorGUILayout.EndScrollView();
        }
    }
}
