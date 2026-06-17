using System;
using System.Collections.Generic;
using EscapeBlock9.ProcGen.Authoring;
using EscapeBlock9.ProcGen.Data;
using EscapeBlock9.ProcGen.Planning;
using EscapeBlock9.ProcGen.Runtime;
using UnityEngine;

namespace EscapeBlock9.ProcGen.Placement
{
    public sealed class CustomFacilityLayoutSolver
    {
        private const float DoorwayPositionTolerance = 0.05f;
        private readonly FacilityPlacementSettings settings;

        public CustomFacilityLayoutSolver(FacilityPlacementSettings settings = null)
        {
            this.settings = settings ?? new FacilityPlacementSettings();
        }

        public ResolvedFacilityLayout Solve(FacilityGraph graph, TileCatalog catalog, int seed)
        {
            if (graph == null)
            {
                throw new ArgumentNullException(nameof(graph));
            }

            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }

            var diagnostics = new PlacementFailureDiagnostics();
            List<TileModuleInfo> modules = LoadModules(catalog, diagnostics);
            var state = new PlacementState(graph, modules, seed, settings, diagnostics);

            if (!PlaceStart(state) || !Backtrack(state))
            {
                return new ResolvedFacilityLayout(seed, state.PlacedTiles, state.Connections, diagnostics, state.Steps);
            }

            ResolveOptionalEdges(state);
            return new ResolvedFacilityLayout(seed, state.PlacedTiles, state.Connections, diagnostics, state.Steps);
        }

        private static List<TileModuleInfo> LoadModules(TileCatalog catalog, PlacementFailureDiagnostics diagnostics)
        {
            var modules = new List<TileModuleInfo>();
            IReadOnlyList<TileDefinition> definitions = catalog.Definitions;
            for (int i = 0; i < definitions.Count; i++)
            {
                TileDefinition definition = definitions[i];
                if (definition == null || definition.Prefab == null)
                {
                    diagnostics.Add(PlacementFailureReason.MissingPrefab, -1, definition != null ? definition.ModuleId : null, "Catalog definition has no prefab.");
                    continue;
                }

                Tile tile = definition.Prefab.GetComponent<Tile>();
                if (tile == null)
                {
                    diagnostics.Add(PlacementFailureReason.MissingPrefab, -1, definition.ModuleId, "Prefab has no Tile component.");
                    continue;
                }

                Doorway[] doorways = tile.GetDoorways();
                if (doorways == null || doorways.Length == 0)
                {
                    diagnostics.Add(PlacementFailureReason.MissingDoorways, -1, definition.ModuleId, "Tile has no authored doorways.");
                    continue;
                }

                modules.Add(new TileModuleInfo(definition, tile, doorways));
            }

            return modules;
        }

        private static bool PlaceStart(PlacementState state)
        {
            FacilityGraphNode start = state.Graph.GetNode(state.Graph.MainPathNodeIds[0]);
            List<TileModuleInfo> candidates = state.OrderCandidates(FilterCandidates(state, start, null), start.Id);
            if (candidates.Count == 0)
            {
                state.Diagnostics.Add(PlacementFailureReason.NoCandidates, start.Id, null, "No start candidates.");
                return false;
            }

            for (int i = 0; i < candidates.Count; i++)
            {
                TileModuleInfo module = candidates[i];
                if (!state.CanUse(module, start.Id))
                {
                    continue;
                }

                List<PlacedOccupancyBox> boxes = OccupancyValidator.BuildBoxes(module.Tile, Vector3.zero, Quaternion.identity, state.Settings.OccupancyPadding);
                if (OccupancyValidator.WouldOverlap(boxes, state.PlacedTiles, state.Settings.OverlapTolerance, out string overlap))
                {
                    state.Diagnostics.Add(PlacementFailureReason.Overlap, start.Id, module.ModuleId, overlap);
                    continue;
                }

                state.PushPlacement(start.Id, module, Vector3.zero, Quaternion.identity, boxes);
                return true;
            }

            return false;
        }

        private static bool Backtrack(PlacementState state)
        {
            if (state.Steps++ > state.Settings.MaxBacktrackingSteps)
            {
                state.Diagnostics.Add(PlacementFailureReason.BacktrackingLimitReached, -1, null, "Exceeded placement search budget.");
                return false;
            }

            if (state.PlacedTiles.Count == state.Graph.Nodes.Count)
            {
                return true;
            }

            FacilityGraphEdge edge = state.NextFrontierEdge();
            if (edge == null)
            {
                state.Diagnostics.Add(PlacementFailureReason.ConnectorUnavailable, -1, null, "No frontier edge from placed nodes to unplaced nodes.");
                return false;
            }

            bool fromPlaced = state.IsPlaced(edge.FromNodeId);
            int parentNodeId = fromPlaced ? edge.FromNodeId : edge.ToNodeId;
            int childNodeId = fromPlaced ? edge.ToNodeId : edge.FromNodeId;
            PlacedTile parentTile = state.GetPlaced(parentNodeId);
            TileModuleInfo parentModule = state.GetModule(parentTile.ModuleId);
            FacilityGraphNode childNode = state.Graph.GetNode(childNodeId);

            List<TileModuleInfo> candidates = state.OrderCandidates(FilterCandidates(state, childNode, edge), childNodeId);
            if (candidates.Count == 0)
            {
                state.Diagnostics.Add(PlacementFailureReason.NoCandidates, childNodeId, null, $"No compatible candidates for edge {edge.Id} role {edge.Role}.");
                return false;
            }

            for (int parentDoorwayIndex = 0; parentDoorwayIndex < parentModule.Doorways.Length; parentDoorwayIndex++)
            {
                if (state.IsDoorwayUsed(parentNodeId, parentDoorwayIndex))
                {
                    continue;
                }

                Doorway openDoorway = parentModule.Doorways[parentDoorwayIndex];
                for (int candidateIndex = 0; candidateIndex < candidates.Count; candidateIndex++)
                {
                    TileModuleInfo candidate = candidates[candidateIndex];
                    if (!state.CanUse(candidate, childNodeId))
                    {
                        continue;
                    }

                    for (int candidateDoorwayIndex = 0; candidateDoorwayIndex < candidate.Doorways.Length; candidateDoorwayIndex++)
                    {
                        Doorway candidateDoorway = candidate.Doorways[candidateDoorwayIndex];
                        if (!DoorwaysCanConnect(openDoorway, candidateDoorway, edge.Role, out string mismatch))
                        {
                            state.Diagnostics.Add(PlacementFailureReason.SocketMismatch, childNodeId, candidate.ModuleId, mismatch);
                            continue;
                        }

                        SnapTransformResult snap = SnapTransformSolver.Solve(openDoorway, parentTile, candidateDoorway);
                        if (!SnapTransformSolver.IsYawAllowed(snap.YawDegrees, candidate.Definition.AllowedYawRotations))
                        {
                            state.Diagnostics.Add(PlacementFailureReason.RotationNotAllowed, childNodeId, candidate.ModuleId, $"Yaw {snap.YawDegrees:0.##} is not allowed.");
                            continue;
                        }

                        List<PlacedOccupancyBox> boxes = OccupancyValidator.BuildBoxes(candidate.Tile, snap.Position, snap.Rotation, state.Settings.OccupancyPadding);
                        if (OccupancyValidator.WouldOverlap(boxes, state.PlacedTiles, state.Settings.OverlapTolerance, out string overlap))
                        {
                            state.Diagnostics.Add(PlacementFailureReason.Overlap, childNodeId, candidate.ModuleId, overlap);
                            continue;
                        }

                        state.PushPlacement(childNodeId, candidate, snap.Position, snap.Rotation, boxes);
                        state.UseConnection(edge.Id, parentNodeId, parentDoorwayIndex, childNodeId, candidateDoorwayIndex);
                        if (Backtrack(state))
                        {
                            return true;
                        }

                        state.PopPlacement(childNodeId, candidate.ModuleId);
                    }
                }
            }

            state.Diagnostics.Add(PlacementFailureReason.ConnectorUnavailable, childNodeId, null, $"No available connector could satisfy edge {edge.Id}.");
            return false;
        }

        private static List<TileModuleInfo> FilterCandidates(PlacementState state, FacilityGraphNode node, FacilityGraphEdge incomingEdge)
        {
            var candidates = new List<TileModuleInfo>();
            for (int i = 0; i < state.Modules.Count; i++)
            {
                TileModuleInfo module = state.Modules[i];
                if (!RoleAllowsModule(node.Role, incomingEdge != null ? incomingEdge.Role : FacilityGraphEdgeRole.MainPath, module, out string detail))
                {
                    state.Diagnostics.Add(PlacementFailureReason.CategoryMismatch, node.Id, module.ModuleId, detail);
                    continue;
                }

                if (!state.CanUse(module, node.Id))
                {
                    continue;
                }

                candidates.Add(module);
            }

            return candidates;
        }

        private static bool RoleAllowsModule(FacilityGraphNodeRole nodeRole, FacilityGraphEdgeRole edgeRole, TileModuleInfo module, out string detail)
        {
            TileCategory category = module.Definition.Category;
            switch (nodeRole)
            {
                case FacilityGraphNodeRole.Start:
                    if (category == TileCategory.Exit && module.HasTag("start"))
                    {
                        detail = string.Empty;
                        return true;
                    }
                    break;
                case FacilityGraphNodeRole.MainPath:
                    if (category == TileCategory.Corridor || category == TileCategory.Room)
                    {
                        detail = string.Empty;
                        return true;
                    }
                    break;
                case FacilityGraphNodeRole.Branch:
                    if (category == TileCategory.Room || category == TileCategory.Special || category == TileCategory.Corridor)
                    {
                        detail = string.Empty;
                        return true;
                    }
                    break;
                case FacilityGraphNodeRole.DeadEnd:
                    if (module.HasTag("dead-end") || category == TileCategory.Room || category == TileCategory.Special)
                    {
                        detail = string.Empty;
                        return true;
                    }
                    break;
                case FacilityGraphNodeRole.Stair:
                    if (category == TileCategory.Stair)
                    {
                        detail = string.Empty;
                        return true;
                    }
                    break;
                case FacilityGraphNodeRole.FireExit:
                    if (category == TileCategory.Exit && module.HasTag("fire-exit"))
                    {
                        detail = string.Empty;
                        return true;
                    }
                    break;
                case FacilityGraphNodeRole.Portal:
                    if (category == TileCategory.Portal || edgeRole == FacilityGraphEdgeRole.Portal)
                    {
                        detail = string.Empty;
                        return true;
                    }
                    break;
            }

            detail = $"Node role {nodeRole} rejects category {category} tags [{string.Join(",", module.Tags)}].";
            return false;
        }

        private static bool DoorwaysCanConnect(Doorway first, Doorway second, FacilityGraphEdgeRole edgeRole, out string detail)
        {
            if (!ConnectorKindsCompatible(first.ConnectorKind, second.ConnectorKind, edgeRole))
            {
                detail = $"Connector kind mismatch: {first.ConnectorKind} cannot connect to {second.ConnectorKind} for {edgeRole}.";
                return false;
            }

            if (!SocketsCompatible(first, second))
            {
                detail = $"Socket mismatch: {first.SocketName} cannot connect to {second.SocketName}.";
                return false;
            }

            detail = string.Empty;
            return true;
        }

        private static bool SocketsCompatible(Doorway first, Doorway second)
        {
            bool firstAllowsSecond = first.Socket != null
                ? first.Socket.IsCompatibleWith(second.Socket, second.SocketName)
                : string.Equals(first.SocketName, second.SocketName, StringComparison.OrdinalIgnoreCase);
            bool secondAllowsFirst = second.Socket != null
                ? second.Socket.IsCompatibleWith(first.Socket, first.SocketName)
                : string.Equals(first.SocketName, second.SocketName, StringComparison.OrdinalIgnoreCase);
            return firstAllowsSecond && secondAllowsFirst;
        }

        private static bool ConnectorKindsCompatible(ConnectorKind first, ConnectorKind second, FacilityGraphEdgeRole edgeRole)
        {
            if (edgeRole == FacilityGraphEdgeRole.Stair)
            {
                return first == ConnectorKind.Stair && second == ConnectorKind.Stair;
            }

            if (edgeRole == FacilityGraphEdgeRole.FireExit)
            {
                return first == ConnectorKind.FireExit || second == ConnectorKind.FireExit || first == ConnectorKind.Door || second == ConnectorKind.Door;
            }

            if (edgeRole == FacilityGraphEdgeRole.Portal)
            {
                return first == ConnectorKind.Portal && second == ConnectorKind.Portal;
            }

            return first != ConnectorKind.None && second != ConnectorKind.None &&
                   first != ConnectorKind.Sealed && second != ConnectorKind.Sealed &&
                   first != ConnectorKind.Stair && second != ConnectorKind.Stair &&
                   first != ConnectorKind.Portal && second != ConnectorKind.Portal &&
                   first != ConnectorKind.FireExit && second != ConnectorKind.FireExit;
        }

        private static void ResolveOptionalEdges(PlacementState state)
        {
            for (int i = 0; i < state.Graph.Edges.Count; i++)
            {
                FacilityGraphEdge edge = state.Graph.Edges[i];
                if (edge.Role != FacilityGraphEdgeRole.Loop && edge.Role != FacilityGraphEdgeRole.Portal)
                {
                    continue;
                }

                if (state.HasConnectionForEdge(edge.Id))
                {
                    continue;
                }

                state.Diagnostics.Add(PlacementFailureReason.OptionalEdgeUnresolved, edge.ToNodeId, null, $"Optional {edge.Role} edge {edge.Id} was not physically connected in this placement pass.");
            }
        }

        private sealed class TileModuleInfo
        {
            public TileModuleInfo(TileDefinition definition, Tile tile, Doorway[] doorways)
            {
                Definition = definition;
                Tile = tile;
                Doorways = doorways;
                Tags = definition.Tags ?? Array.Empty<string>();
            }

            public TileDefinition Definition { get; }
            public Tile Tile { get; }
            public Doorway[] Doorways { get; }
            public IReadOnlyList<string> Tags { get; }
            public string ModuleId => Definition.ModuleId;

            public bool HasTag(string tag)
            {
                for (int i = 0; i < Tags.Count; i++)
                {
                    if (string.Equals(Tags[i], tag, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        private sealed class PlacementState
        {
            private readonly Dictionary<int, PlacedTile> placedByNode = new Dictionary<int, PlacedTile>();
            private readonly Dictionary<string, int> useCounts = new Dictionary<string, int>();
            private readonly HashSet<string> usedDoorways = new HashSet<string>();
            private readonly List<PlacedTile> placedTiles = new List<PlacedTile>();
            private readonly List<PlacedDoorwayConnection> connections = new List<PlacedDoorwayConnection>();
            private readonly NamedRandomStreams streams;

            public PlacementState(FacilityGraph graph, List<TileModuleInfo> modules, int seed, FacilityPlacementSettings settings, PlacementFailureDiagnostics diagnostics)
            {
                Graph = graph;
                Modules = modules;
                Seed = seed;
                Settings = settings;
                Diagnostics = diagnostics;
                streams = new NamedRandomStreams(seed);
            }

            public FacilityGraph Graph { get; }
            public List<TileModuleInfo> Modules { get; }
            public int Seed { get; }
            public FacilityPlacementSettings Settings { get; }
            public PlacementFailureDiagnostics Diagnostics { get; }
            public int Steps { get; set; }
            public IReadOnlyList<PlacedTile> PlacedTiles => placedTiles;
            public IReadOnlyList<PlacedDoorwayConnection> Connections => connections;

            public bool IsPlaced(int nodeId)
            {
                return placedByNode.ContainsKey(nodeId);
            }

            public PlacedTile GetPlaced(int nodeId)
            {
                return placedByNode[nodeId];
            }

            public TileModuleInfo GetModule(string moduleId)
            {
                for (int i = 0; i < Modules.Count; i++)
                {
                    if (Modules[i].ModuleId == moduleId)
                    {
                        return Modules[i];
                    }
                }

                return null;
            }

            public bool CanUse(TileModuleInfo module, int nodeId)
            {
                int count = useCounts.TryGetValue(module.ModuleId, out int current) ? current : 0;
                if (module.Definition.Unique && count > 0)
                {
                    Diagnostics.Add(PlacementFailureReason.MaxUseExceeded, nodeId, module.ModuleId, "Unique module already used.");
                    return false;
                }

                if (module.Definition.MaxUseCount >= 0 && count >= module.Definition.MaxUseCount)
                {
                    Diagnostics.Add(PlacementFailureReason.MaxUseExceeded, nodeId, module.ModuleId, $"Max use {module.Definition.MaxUseCount} exceeded.");
                    return false;
                }

                return true;
            }

            public List<TileModuleInfo> OrderCandidates(List<TileModuleInfo> candidates, int nodeId)
            {
                SeededRandom random = streams.Stream($"placement/candidates/{nodeId}");
                var ordered = new List<CandidateOrder>();
                for (int i = 0; i < candidates.Count; i++)
                {
                    TileModuleInfo candidate = candidates[i];
                    float weight = Mathf.Max(0.0001f, candidate.Definition.SelectionWeight);
                    ordered.Add(new CandidateOrder(candidate, random.Value01() / weight));
                }

                ordered.Sort((a, b) => a.SortKey.CompareTo(b.SortKey));
                candidates.Clear();
                for (int i = 0; i < ordered.Count; i++)
                {
                    candidates.Add(ordered[i].Module);
                }

                return candidates;
            }

            public FacilityGraphEdge NextFrontierEdge()
            {
                for (int i = 0; i < Graph.Edges.Count; i++)
                {
                    FacilityGraphEdge edge = Graph.Edges[i];
                    if (edge.Role == FacilityGraphEdgeRole.Loop || edge.Role == FacilityGraphEdgeRole.Portal)
                    {
                        continue;
                    }

                    bool fromPlaced = IsPlaced(edge.FromNodeId);
                    bool toPlaced = IsPlaced(edge.ToNodeId);
                    if (fromPlaced != toPlaced)
                    {
                        return edge;
                    }
                }

                return null;
            }

            public void PushPlacement(int nodeId, TileModuleInfo module, Vector3 position, Quaternion rotation, List<PlacedOccupancyBox> boxes)
            {
                var placed = new PlacedTile(nodeId, module.Definition, position, rotation, boxes);
                placedByNode[nodeId] = placed;
                placedTiles.Add(placed);
                useCounts[module.ModuleId] = useCounts.TryGetValue(module.ModuleId, out int current) ? current + 1 : 1;
            }

            public void UseConnection(int edgeId, int fromNodeId, int fromDoorwayIndex, int toNodeId, int toDoorwayIndex)
            {
                usedDoorways.Add(Key(fromNodeId, fromDoorwayIndex));
                usedDoorways.Add(Key(toNodeId, toDoorwayIndex));
                connections.Add(new PlacedDoorwayConnection(edgeId, fromNodeId, fromDoorwayIndex, toNodeId, toDoorwayIndex));
            }

            public void PopPlacement(int nodeId, string moduleId)
            {
                placedByNode.Remove(nodeId);
                for (int i = placedTiles.Count - 1; i >= 0; i--)
                {
                    if (placedTiles[i].NodeId == nodeId)
                    {
                        placedTiles.RemoveAt(i);
                        break;
                    }
                }

                if (useCounts.TryGetValue(moduleId, out int current))
                {
                    if (current <= 1)
                    {
                        useCounts.Remove(moduleId);
                    }
                    else
                    {
                        useCounts[moduleId] = current - 1;
                    }
                }

                for (int i = connections.Count - 1; i >= 0; i--)
                {
                    PlacedDoorwayConnection connection = connections[i];
                    if (connection.FromNodeId == nodeId || connection.ToNodeId == nodeId)
                    {
                        usedDoorways.Remove(Key(connection.FromNodeId, connection.FromDoorwayIndex));
                        usedDoorways.Remove(Key(connection.ToNodeId, connection.ToDoorwayIndex));
                        connections.RemoveAt(i);
                    }
                }
            }

            public bool IsDoorwayUsed(int nodeId, int doorwayIndex)
            {
                return usedDoorways.Contains(Key(nodeId, doorwayIndex));
            }

            public bool HasConnectionForEdge(int edgeId)
            {
                for (int i = 0; i < connections.Count; i++)
                {
                    if (connections[i].EdgeId == edgeId)
                    {
                        return true;
                    }
                }

                return false;
            }

            private static string Key(int nodeId, int doorwayIndex)
            {
                return $"{nodeId}:{doorwayIndex}";
            }
        }

        private readonly struct CandidateOrder
        {
            public CandidateOrder(TileModuleInfo module, float sortKey)
            {
                Module = module;
                SortKey = sortKey;
            }

            public TileModuleInfo Module { get; }
            public float SortKey { get; }
        }
    }
}
