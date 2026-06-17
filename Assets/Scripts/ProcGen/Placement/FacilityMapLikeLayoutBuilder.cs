using System;
using System.Collections.Generic;
using EscapeBlock9.ProcGen.Authoring;
using EscapeBlock9.ProcGen.Data;
using EscapeBlock9.ProcGen.Planning;
using UnityEngine;

namespace EscapeBlock9.ProcGen.Placement
{
    public static class FacilityMapLikeLayoutBuilder
    {
        private static readonly GridDirection[] CardinalDirections =
        {
            GridDirection.North,
            GridDirection.South,
            GridDirection.East,
            GridDirection.West
        };

        public static bool TryBuild(
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

            if (catalog == null)
            {
                diagnostics = "Map-like layout requires a TileCatalog.";
                return false;
            }

            roomCount = Mathf.Clamp(roomCount, 1, 12);

            TileDefinition start = FindDefinition(catalog, "start_exit_lobby");
            TileDefinition junction = FindDefinition(catalog, "corridor_cross_junction_3m");
            TileDefinition straight = FindDefinition(catalog, "corridor_straight_8m");
            TileDefinition deadEnd = FindDefinition(catalog, "corridor_dead_end");
            TileDefinition fireExit = FindDefinition(catalog, "fire_exit_lobby");
            List<TileDefinition> rooms = FindRoomDefinitions(catalog);

            if (start == null || junction == null || straight == null || deadEnd == null || fireExit == null || rooms.Count == 0)
            {
                diagnostics = "Map-like layout requires start_exit_lobby, corridor_cross_junction_3m, corridor_straight_8m, corridor_dead_end, fire_exit_lobby, and at least one room/special room definition.";
                return false;
            }

            var random = new System.Random(seed);
            var settings = new FacilityPlacementSettings();
            var tiles = new List<PlacedTile>();
            var manualConnections = new List<PlacedDoorwayConnection>();
            var manualEdgeIds = new HashSet<int>();
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

            graph.AddEdge(entryStraightNode.Id, cellToNode[Vector2Int.zero], FacilityGraphEdgeRole.MainPath);

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

                    graph.AddEdge(cellToNode[cell], straightNode.Id, FacilityGraphEdgeRole.MainPath);
                    graph.AddEdge(straightNode.Id, cellToNode[neighbor], FacilityGraphEdgeRole.MainPath);
                }
            }

            if (!TryPlaceMapRooms(
                    roomCount,
                    rooms,
                    junction,
                    straight,
                    deadEnd,
                    random,
                    settings,
                    graph,
                    tiles,
                    placedByNode,
                    definitionByNode,
                    roomUseCounts,
                    cellToNode,
                    corridorAdjacency,
                    usedRoomDirections,
                    manualConnections,
                    manualEdgeIds,
                    out diagnostics))
            {
                return false;
            }

            if (!TryPlaceFireExitRooms(
                    2,
                    fireExit,
                    junction,
                    random,
                    settings,
                    graph,
                    tiles,
                    placedByNode,
                    definitionByNode,
                    cellToNode,
                    corridorAdjacency,
                    usedRoomDirections,
                    manualConnections,
                    manualEdgeIds,
                    out diagnostics))
            {
                return false;
            }

            var connections = new List<PlacedDoorwayConnection>(manualConnections);
            if (!TryBuildMatchedConnections(graph, placedByNode, definitionByNode, connections, manualEdgeIds, out diagnostics))
            {
                return false;
            }

            AddMatchedLoopConnections(graph, placedByNode, definitionByNode, connections);

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
            TileDefinition straight,
            TileDefinition deadEnd,
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
            List<PlacedDoorwayConnection> manualConnections,
            HashSet<int> manualEdgeIds,
            out string diagnostics)
        {
            diagnostics = string.Empty;

            for (int roomIndex = 0; roomIndex < roomCount; roomIndex++)
            {
                TileDefinition roomDefinition = SelectRoomDefinition(rooms, random, roomUseCounts);
                List<RoomDoorSlot> slots = BuildRoomDoorSlots(roomDefinition, random);
                RoomDoorSlot primarySlot = FindPrimaryActiveSlot(slots);
                List<RoomSocketCandidate> candidates = BuildRoomSocketCandidates(cellToNode, corridorAdjacency, usedRoomDirections, random);
                bool placedRoom = false;

                for (int i = 0; i < candidates.Count; i++)
                {
                    RoomSocketCandidate candidate = candidates[i];
                    int corridorNodeId = cellToNode[candidate.Cell];
                    PlacedTile corridorTile = placedByNode[corridorNodeId];
                    string doorwayId = DirectionToDoorwayId(candidate.Direction);
                    int corridorDoorwayIndex = FindDoorwayIndex(junction, doorwayId);
                    SnapTransformResult roomSnap = SnapToDoorway(corridorTile, junction, corridorDoorwayIndex, roomDefinition, primarySlot.ConnectorId);
                    List<PlacedOccupancyBox> boxes = OccupancyValidator.BuildBoxes(roomDefinition.TilePrefab, roomSnap.Position, roomSnap.Rotation, settings.OccupancyPadding);
                    if (OccupancyValidator.WouldOverlap(boxes, tiles, settings.OverlapTolerance, out _))
                    {
                        continue;
                    }

                    var roomProbe = new PlacedTile(-1, roomDefinition, roomSnap.Position, roomSnap.Rotation, boxes);
                    if (!TryPlanExtraRoomDoorBranches(roomProbe, slots, primarySlot, straight, junction, deadEnd, random, settings, tiles, out List<PlannedBranchTile> plannedBranchTiles))
                    {
                        continue;
                    }

                    FacilityGraphNode roomNode = graph.AddNode(FacilityGraphNodeRole.DeadEnd, -1, roomIndex, 1, 0);
                    var placed = new PlacedTile(roomNode.Id, roomDefinition, roomSnap.Position, roomSnap.Rotation, boxes);
                    tiles.Add(placed);
                    placedByNode[roomNode.Id] = placed;
                    definitionByNode[roomNode.Id] = roomDefinition;

                    FacilityGraphEdge roomEdge = graph.AddEdge(corridorNodeId, roomNode.Id, FacilityGraphEdgeRole.DeadEnd);
                    manualConnections.Add(new PlacedDoorwayConnection(
                        roomEdge.Id,
                        corridorNodeId,
                        corridorDoorwayIndex,
                        roomNode.Id,
                        FindDoorwayIndex(roomDefinition, primarySlot.ConnectorId)));
                    manualEdgeIds.Add(roomEdge.Id);

                    var branchNodeIds = new List<int> { roomNode.Id };
                    var plannedNodeIds = new List<int>(plannedBranchTiles.Count);
                    for (int branchTileIndex = 0; branchTileIndex < plannedBranchTiles.Count; branchTileIndex++)
                    {
                        PlannedBranchTile plannedBranchTile = plannedBranchTiles[branchTileIndex];
                        FacilityGraphNode branchNode = graph.AddNode(plannedBranchTile.NodeRole, -1, roomIndex, 2 + branchTileIndex, 0);
                        plannedNodeIds.Add(branchNode.Id);
                        var branchPlaced = new PlacedTile(branchNode.Id, plannedBranchTile.Definition, plannedBranchTile.Position, plannedBranchTile.Rotation, plannedBranchTile.OccupancyBoxes);
                        tiles.Add(branchPlaced);
                        placedByNode[branchNode.Id] = branchPlaced;
                        definitionByNode[branchNode.Id] = plannedBranchTile.Definition;

                        int fromNodeId = plannedBranchTile.ConnectFromPlanIndex < 0
                            ? roomNode.Id
                            : plannedNodeIds[plannedBranchTile.ConnectFromPlanIndex];
                        TileDefinition fromDefinition = plannedBranchTile.ConnectFromPlanIndex < 0
                            ? roomDefinition
                            : plannedBranchTiles[plannedBranchTile.ConnectFromPlanIndex].Definition;

                        FacilityGraphEdge branchEdge = graph.AddEdge(fromNodeId, branchNode.Id, plannedBranchTile.EdgeRole);
                        manualConnections.Add(new PlacedDoorwayConnection(
                            branchEdge.Id,
                            fromNodeId,
                            FindDoorwayIndex(fromDefinition, plannedBranchTile.ConnectFromDoorwayId),
                            branchNode.Id,
                            FindDoorwayIndex(plannedBranchTile.Definition, plannedBranchTile.ConnectToDoorwayId)));
                        manualEdgeIds.Add(branchEdge.Id);
                        branchNodeIds.Add(branchNode.Id);
                    }

                    graph.AddBranch(new FacilityGraphBranch(roomIndex, corridorNodeId, branchNodeIds, FacilityGraphEdgeRole.Branch));
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

        private static bool TryPlaceFireExitRooms(
            int exitCount,
            TileDefinition fireExit,
            TileDefinition junction,
            System.Random random,
            FacilityPlacementSettings settings,
            FacilityGraph graph,
            List<PlacedTile> tiles,
            Dictionary<int, PlacedTile> placedByNode,
            Dictionary<int, TileDefinition> definitionByNode,
            Dictionary<Vector2Int, int> cellToNode,
            Dictionary<Vector2Int, HashSet<GridDirection>> corridorAdjacency,
            Dictionary<Vector2Int, HashSet<GridDirection>> usedRoomDirections,
            List<PlacedDoorwayConnection> manualConnections,
            HashSet<int> manualEdgeIds,
            out string diagnostics)
        {
            diagnostics = string.Empty;

            for (int exitIndex = 0; exitIndex < exitCount; exitIndex++)
            {
                List<RoomSocketCandidate> candidates = BuildRoomSocketCandidates(cellToNode, corridorAdjacency, usedRoomDirections, random);
                bool placedExit = false;
                for (int i = 0; i < candidates.Count; i++)
                {
                    RoomSocketCandidate candidate = candidates[i];
                    int corridorNodeId = cellToNode[candidate.Cell];
                    PlacedTile corridorTile = placedByNode[corridorNodeId];
                    int corridorDoorwayIndex = FindDoorwayIndex(junction, DirectionToDoorwayId(candidate.Direction));
                    SnapTransformResult exitSnap = SnapToDoorway(corridorTile, junction, corridorDoorwayIndex, fireExit, "interior_door");
                    List<PlacedOccupancyBox> boxes = OccupancyValidator.BuildBoxes(fireExit.TilePrefab, exitSnap.Position, exitSnap.Rotation, settings.OccupancyPadding);
                    if (OccupancyValidator.WouldOverlap(boxes, tiles, settings.OverlapTolerance, out _))
                    {
                        continue;
                    }

                    FacilityGraphNode exitNode = graph.AddNode(FacilityGraphNodeRole.FireExit, -1, graph.Branches.Count, 1, 0);
                    var placed = new PlacedTile(exitNode.Id, fireExit, exitSnap.Position, exitSnap.Rotation, boxes);
                    tiles.Add(placed);
                    placedByNode[exitNode.Id] = placed;
                    definitionByNode[exitNode.Id] = fireExit;

                    FacilityGraphEdge exitEdge = graph.AddEdge(corridorNodeId, exitNode.Id, FacilityGraphEdgeRole.FireExit);
                    manualConnections.Add(new PlacedDoorwayConnection(
                        exitEdge.Id,
                        corridorNodeId,
                        corridorDoorwayIndex,
                        exitNode.Id,
                        FindDoorwayIndex(fireExit, "interior_door")));
                    manualEdgeIds.Add(exitEdge.Id);

                    graph.AddBranch(new FacilityGraphBranch(graph.Branches.Count, corridorNodeId, new[] { exitNode.Id }, FacilityGraphEdgeRole.FireExit));
                    usedRoomDirections[candidate.Cell].Add(candidate.Direction);
                    placedExit = true;
                    break;
                }

                if (!placedExit)
                {
                    diagnostics = $"Could not place fire exit {exitIndex + 1}/{exitCount} without overlap.";
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

        private static List<RoomDoorSlot> BuildRoomDoorSlots(TileDefinition roomDefinition, System.Random random)
        {
            RoomDoorSlot[] candidates = GetRoomDoorSlotCandidates(roomDefinition);
            var order = new List<int>(candidates.Length);
            for (int i = 0; i < candidates.Length; i++)
            {
                order.Add(i);
            }

            Shuffle(order, random);
            var activeSlots = new HashSet<int> { order[0] };
            if (order.Count > 1 && random.NextDouble() <= 0.66d)
            {
                activeSlots.Add(order[1]);
            }

            if (order.Count > 2 && random.NextDouble() <= 0.33d)
            {
                activeSlots.Add(order[2]);
            }

            var slots = new List<RoomDoorSlot>(candidates.Length);
            for (int i = 0; i < candidates.Length; i++)
            {
                RoomDoorSlot slot = candidates[i];
                slots.Add(new RoomDoorSlot(
                    slot.SlotIndex,
                    slot.ConnectorId,
                    slot.LocalPosition,
                    slot.LocalForward,
                    activeSlots.Contains(i),
                    order[0] == i));
            }

            return slots;
        }

        private static RoomDoorSlot[] GetRoomDoorSlotCandidates(TileDefinition roomDefinition)
        {
            string moduleId = roomDefinition != null ? roomDefinition.ModuleId : string.Empty;
            if (string.Equals(moduleId, "room_bathroom", StringComparison.OrdinalIgnoreCase))
            {
                return new[]
                {
                    new RoomDoorSlot(0, "room_door", new Vector3(4f, 1.1f, 7.05f), Vector3.forward, false, false),
                    new RoomDoorSlot(1, "room_door_right", new Vector3(8.1f, 1.1f, 5.1f), Vector3.right, false, false)
                };
            }

            if (string.Equals(moduleId, "room_shop_special", StringComparison.OrdinalIgnoreCase))
            {
                return new[]
                {
                    new RoomDoorSlot(0, "room_door", new Vector3(4f, 1.1f, 7.05f), Vector3.forward, false, false),
                    new RoomDoorSlot(1, "room_door_left", new Vector3(-0.1f, 1.1f, 3.5f), Vector3.left, false, false)
                };
            }

            return new[]
            {
                new RoomDoorSlot(0, "room_door", new Vector3(4f, 1.1f, 7.05f), Vector3.forward, false, false),
                new RoomDoorSlot(1, "room_door_left", new Vector3(-0.1f, 1.1f, 3.5f), Vector3.left, false, false),
                new RoomDoorSlot(2, "room_door_right", new Vector3(8.1f, 1.1f, 3.5f), Vector3.right, false, false)
            };
        }

        private static RoomDoorSlot FindPrimaryActiveSlot(IReadOnlyList<RoomDoorSlot> slots)
        {
            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i].IsPrimary)
                {
                    return slots[i];
                }
            }

            return slots[0];
        }

        private static bool TryPlanExtraRoomDoorBranches(
            PlacedTile roomProbe,
            IReadOnlyList<RoomDoorSlot> slots,
            RoomDoorSlot primarySlot,
            TileDefinition straight,
            TileDefinition junction,
            TileDefinition deadEnd,
            System.Random random,
            FacilityPlacementSettings settings,
            IReadOnlyList<PlacedTile> existingTiles,
            out List<PlannedBranchTile> plannedBranchTiles)
        {
            plannedBranchTiles = new List<PlannedBranchTile>();
            var validationTiles = new List<PlacedTile>(existingTiles) { roomProbe };

            for (int i = 0; i < slots.Count; i++)
            {
                RoomDoorSlot slot = slots[i];
                if (!slot.IsActive || slot.SlotIndex == primarySlot.SlotIndex)
                {
                    continue;
                }

                int planStart = plannedBranchTiles.Count;
                int validationStart = validationTiles.Count;
                bool branchFits = true;
                int lastPlanIndex = -1;
                string lastDoorwayId = slot.ConnectorId;
                int straightCount = random.Next(1, 3);
                for (int step = 0; step < straightCount; step++)
                {
                    if (!TryAppendBranchTile(
                            roomProbe,
                            plannedBranchTiles,
                            validationTiles,
                            settings,
                            lastPlanIndex,
                            lastDoorwayId,
                            straight,
                            "south",
                            FacilityGraphNodeRole.Branch,
                            FacilityGraphEdgeRole.Branch,
                            out lastPlanIndex))
                    {
                        branchFits = false;
                        break;
                    }

                    lastDoorwayId = "north";
                }

                if (branchFits && TryAppendJunctionBranchNetwork(plannedBranchTiles, validationTiles, settings, lastPlanIndex, lastDoorwayId, straight, junction, deadEnd, random))
                {
                    continue;
                }

                if (branchFits && !TryAppendBranchTermination(roomProbe, plannedBranchTiles, validationTiles, settings, lastPlanIndex, lastDoorwayId, straight, deadEnd))
                {
                    branchFits = false;
                }

                if (!branchFits)
                {
                    plannedBranchTiles.RemoveRange(planStart, plannedBranchTiles.Count - planStart);
                    validationTiles.RemoveRange(validationStart, validationTiles.Count - validationStart);
                }
            }

            return true;
        }

        private static bool TryAppendJunctionBranchNetwork(
            List<PlannedBranchTile> plannedBranchTiles,
            List<PlacedTile> validationTiles,
            FacilityPlacementSettings settings,
            int fromPlanIndex,
            string fromDoorwayId,
            TileDefinition straight,
            TileDefinition junction,
            TileDefinition deadEnd,
            System.Random random)
        {
            int planStart = plannedBranchTiles.Count;
            int validationStart = validationTiles.Count;
            if (!TryAppendBranchTile(validationTiles[validationTiles.Count - plannedBranchTiles.Count - 1], plannedBranchTiles, validationTiles, settings, fromPlanIndex, fromDoorwayId, junction, "south", FacilityGraphNodeRole.Branch, FacilityGraphEdgeRole.Branch, out int junctionPlanIndex))
            {
                return false;
            }

            GridDirection[] exits = { GridDirection.North, GridDirection.East, GridDirection.West };
            Shuffle(exits, random);
            int connectedExits = 0;
            for (int i = 0; i < exits.Length; i++)
            {
                if (TryAppendBranchTermination(validationTiles[validationTiles.Count - plannedBranchTiles.Count - 1], plannedBranchTiles, validationTiles, settings, junctionPlanIndex, DirectionToDoorwayId(exits[i]), straight, deadEnd))
                {
                    connectedExits++;
                }
            }

            if (connectedExits > 0)
            {
                return true;
            }

            plannedBranchTiles.RemoveRange(planStart, plannedBranchTiles.Count - planStart);
            validationTiles.RemoveRange(validationStart, validationTiles.Count - validationStart);
            return false;
        }

        private static bool TryAppendBranchTermination(
            PlacedTile roomProbe,
            List<PlannedBranchTile> plannedBranchTiles,
            List<PlacedTile> validationTiles,
            FacilityPlacementSettings settings,
            int fromPlanIndex,
            string fromDoorwayId,
            TileDefinition straight,
            TileDefinition deadEnd)
        {
            int terminalFromIndex = fromPlanIndex;
            string terminalFromDoorway = fromDoorwayId;
            if (TryAppendBranchTile(roomProbe, plannedBranchTiles, validationTiles, settings, fromPlanIndex, fromDoorwayId, straight, "south", FacilityGraphNodeRole.Branch, FacilityGraphEdgeRole.Branch, out int straightPlanIndex))
            {
                terminalFromIndex = straightPlanIndex;
                terminalFromDoorway = "north";
            }

            return TryAppendBranchTile(roomProbe, plannedBranchTiles, validationTiles, settings, terminalFromIndex, terminalFromDoorway, deadEnd, "south", FacilityGraphNodeRole.DeadEnd, FacilityGraphEdgeRole.DeadEnd, out _);
        }

        private static bool TryAppendBranchTile(
            PlacedTile roomProbe,
            List<PlannedBranchTile> plannedBranchTiles,
            List<PlacedTile> validationTiles,
            FacilityPlacementSettings settings,
            int fromPlanIndex,
            string fromDoorwayId,
            TileDefinition candidateDefinition,
            string candidateDoorwayId,
            FacilityGraphNodeRole nodeRole,
            FacilityGraphEdgeRole edgeRole,
            out int appendedPlanIndex)
        {
            appendedPlanIndex = -1;
            PlacedTile openTile = fromPlanIndex < 0
                ? roomProbe
                : validationTiles[validationTiles.Count - plannedBranchTiles.Count + fromPlanIndex];
            TileDefinition openDefinition = fromPlanIndex < 0
                ? roomProbe.Definition
                : plannedBranchTiles[fromPlanIndex].Definition;
            int openDoorwayIndex = FindDoorwayIndex(openDefinition, fromDoorwayId);
            if (openDoorwayIndex < 0)
            {
                return false;
            }

            SnapTransformResult snap = SnapToDoorway(openTile, openDefinition, openDoorwayIndex, candidateDefinition, candidateDoorwayId);
            List<PlacedOccupancyBox> boxes = OccupancyValidator.BuildBoxes(candidateDefinition.TilePrefab, snap.Position, snap.Rotation, settings.OccupancyPadding);
            if (OccupancyValidator.WouldOverlap(boxes, validationTiles, settings.OverlapTolerance, out _))
            {
                return false;
            }

            var planned = new PlannedBranchTile(
                candidateDefinition,
                snap.Position,
                snap.Rotation,
                boxes,
                fromPlanIndex,
                fromDoorwayId,
                candidateDoorwayId,
                nodeRole,
                edgeRole);
            plannedBranchTiles.Add(planned);
            validationTiles.Add(new PlacedTile(-1, candidateDefinition, snap.Position, snap.Rotation, boxes));
            appendedPlanIndex = plannedBranchTiles.Count - 1;
            return true;
        }

        private static bool TryBuildMatchedConnections(
            FacilityGraph graph,
            IReadOnlyDictionary<int, PlacedTile> placedByNode,
            IReadOnlyDictionary<int, TileDefinition> definitionByNode,
            List<PlacedDoorwayConnection> connections,
            HashSet<int> skippedEdgeIds,
            out string diagnostics)
        {
            diagnostics = string.Empty;
            for (int i = 0; i < graph.Edges.Count; i++)
            {
                FacilityGraphEdge edge = graph.Edges[i];
                if (skippedEdgeIds != null && skippedEdgeIds.Contains(edge.Id))
                {
                    continue;
                }

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

        private static void AddMatchedLoopConnections(
            FacilityGraph graph,
            IReadOnlyDictionary<int, PlacedTile> placedByNode,
            IReadOnlyDictionary<int, TileDefinition> definitionByNode,
            List<PlacedDoorwayConnection> connections)
        {
            var usedDoorways = new HashSet<string>();
            var connectedPairs = new HashSet<string>();
            for (int i = 0; i < connections.Count; i++)
            {
                PlacedDoorwayConnection connection = connections[i];
                usedDoorways.Add(DoorwayKey(connection.FromNodeId, connection.FromDoorwayIndex));
                usedDoorways.Add(DoorwayKey(connection.ToNodeId, connection.ToDoorwayIndex));
                connectedPairs.Add(NodePairKey(connection.FromNodeId, connection.ToNodeId));
            }

            var placed = new List<PlacedTile>(placedByNode.Count);
            foreach (KeyValuePair<int, PlacedTile> pair in placedByNode)
            {
                placed.Add(pair.Value);
            }

            placed.Sort((a, b) => a.NodeId.CompareTo(b.NodeId));
            for (int a = 0; a < placed.Count; a++)
            {
                PlacedTile firstTile = placed[a];
                if (!definitionByNode.TryGetValue(firstTile.NodeId, out TileDefinition firstDefinition))
                {
                    continue;
                }

                Doorway[] firstDoorways = firstDefinition.TilePrefab.GetDoorways();
                for (int b = a + 1; b < placed.Count; b++)
                {
                    PlacedTile secondTile = placed[b];
                    if (connectedPairs.Contains(NodePairKey(firstTile.NodeId, secondTile.NodeId)) ||
                        !definitionByNode.TryGetValue(secondTile.NodeId, out TileDefinition secondDefinition))
                    {
                        continue;
                    }

                    Doorway[] secondDoorways = secondDefinition.TilePrefab.GetDoorways();
                    for (int firstIndex = 0; firstIndex < firstDoorways.Length; firstIndex++)
                    {
                        string firstKey = DoorwayKey(firstTile.NodeId, firstIndex);
                        if (usedDoorways.Contains(firstKey))
                        {
                            continue;
                        }

                        Vector3 firstPosition = firstTile.DoorwayPosition(firstDoorways[firstIndex]);
                        Vector3 firstForward = firstTile.DoorwayForward(firstDoorways[firstIndex]);
                        for (int secondIndex = 0; secondIndex < secondDoorways.Length; secondIndex++)
                        {
                            string secondKey = DoorwayKey(secondTile.NodeId, secondIndex);
                            if (usedDoorways.Contains(secondKey))
                            {
                                continue;
                            }

                            if (Vector3.Distance(firstPosition, secondTile.DoorwayPosition(secondDoorways[secondIndex])) > 0.06f)
                            {
                                continue;
                            }

                            float dot = Vector3.Dot(firstForward, secondTile.DoorwayForward(secondDoorways[secondIndex]));
                            if (dot > -0.98f)
                            {
                                continue;
                            }

                            FacilityGraphEdge loop = graph.AddEdge(firstTile.NodeId, secondTile.NodeId, FacilityGraphEdgeRole.Loop);
                            connections.Add(new PlacedDoorwayConnection(loop.Id, firstTile.NodeId, firstIndex, secondTile.NodeId, secondIndex));
                            usedDoorways.Add(firstKey);
                            usedDoorways.Add(secondKey);
                            connectedPairs.Add(NodePairKey(firstTile.NodeId, secondTile.NodeId));
                            break;
                        }
                    }
                }
            }
        }

        private static string DoorwayKey(int nodeId, int doorwayIndex)
        {
            return $"{nodeId}:{doorwayIndex}";
        }

        private static string NodePairKey(int firstNodeId, int secondNodeId)
        {
            if (firstNodeId > secondNodeId)
            {
                int temp = firstNodeId;
                firstNodeId = secondNodeId;
                secondNodeId = temp;
            }

            return $"{firstNodeId}:{secondNodeId}";
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

        private readonly struct RoomDoorSlot
        {
            public RoomDoorSlot(
                int slotIndex,
                string connectorId,
                Vector3 localPosition,
                Vector3 localForward,
                bool isActive,
                bool isPrimary)
            {
                SlotIndex = slotIndex;
                ConnectorId = connectorId;
                LocalPosition = localPosition;
                LocalForward = localForward.sqrMagnitude > 0.0001f ? localForward.normalized : Vector3.forward;
                IsActive = isActive;
                IsPrimary = isPrimary;
            }

            public int SlotIndex { get; }
            public string ConnectorId { get; }
            public Vector3 LocalPosition { get; }
            public Vector3 LocalForward { get; }
            public bool IsActive { get; }
            public bool IsPrimary { get; }
        }

        private readonly struct PlannedBranchTile
        {
            public PlannedBranchTile(
                TileDefinition definition,
                Vector3 position,
                Quaternion rotation,
                IReadOnlyList<PlacedOccupancyBox> occupancyBoxes,
                int connectFromPlanIndex,
                string connectFromDoorwayId,
                string connectToDoorwayId,
                FacilityGraphNodeRole nodeRole,
                FacilityGraphEdgeRole edgeRole)
            {
                Definition = definition;
                Position = position;
                Rotation = rotation;
                OccupancyBoxes = new List<PlacedOccupancyBox>(occupancyBoxes);
                ConnectFromPlanIndex = connectFromPlanIndex;
                ConnectFromDoorwayId = connectFromDoorwayId;
                ConnectToDoorwayId = connectToDoorwayId;
                NodeRole = nodeRole;
                EdgeRole = edgeRole;
            }

            public TileDefinition Definition { get; }
            public Vector3 Position { get; }
            public Quaternion Rotation { get; }
            public IReadOnlyList<PlacedOccupancyBox> OccupancyBoxes { get; }
            public int ConnectFromPlanIndex { get; }
            public string ConnectFromDoorwayId { get; }
            public string ConnectToDoorwayId { get; }
            public FacilityGraphNodeRole NodeRole { get; }
            public FacilityGraphEdgeRole EdgeRole { get; }
        }
    }
}
