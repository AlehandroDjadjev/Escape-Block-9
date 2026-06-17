using System.Collections.Generic;
using EscapeBlock9.ProcGen.Authoring;
using EscapeBlock9.ProcGen.Planning;
using UnityEngine;

namespace EscapeBlock9.ProcGen.Placement
{
    public static class ResolvedFacilityLayoutValidator
    {
        public static bool ValidateConnected(
            FacilityGraph graph,
            ResolvedFacilityLayout layout,
            PlacementFailureDiagnostics diagnostics,
            float doorwayPositionTolerance = 0.05f,
            float oppositeForwardDotThreshold = -0.98f)
        {
            if (graph == null || layout == null)
            {
                return false;
            }

            var placedByNode = BuildPlacedMap(layout);
            if (placedByNode.Count != graph.Nodes.Count)
            {
                diagnostics?.Add(
                    PlacementFailureReason.LayoutDisconnected,
                    -1,
                    null,
                    $"Placed {placedByNode.Count} of {graph.Nodes.Count} graph nodes.");
                return false;
            }

            var connectionByEdge = BuildConnectionMap(layout);
            var adjacency = new Dictionary<int, List<int>>();
            for (int i = 0; i < graph.Nodes.Count; i++)
            {
                adjacency[graph.Nodes[i].Id] = new List<int>();
            }

            for (int i = 0; i < layout.Connections.Count; i++)
            {
                PlacedDoorwayConnection connection = layout.Connections[i];
                if (!placedByNode.ContainsKey(connection.FromNodeId) || !placedByNode.ContainsKey(connection.ToNodeId))
                {
                    diagnostics?.Add(
                        PlacementFailureReason.LayoutDisconnected,
                        -1,
                        null,
                        $"Connection {connection.EdgeId} references unplaced node {connection.FromNodeId}->{connection.ToNodeId}.");
                    return false;
                }

                adjacency[connection.FromNodeId].Add(connection.ToNodeId);
                adjacency[connection.ToNodeId].Add(connection.FromNodeId);

                if (!DoorwayPairAligned(placedByNode, connection, doorwayPositionTolerance, oppositeForwardDotThreshold, out string detail))
                {
                    diagnostics?.Add(PlacementFailureReason.DoorwayMisaligned, connection.ToNodeId, null, detail);
                    return false;
                }
            }

            for (int i = 0; i < graph.Edges.Count; i++)
            {
                FacilityGraphEdge edge = graph.Edges[i];
                if (edge.Role == FacilityGraphEdgeRole.Loop || edge.Role == FacilityGraphEdgeRole.Portal)
                {
                    continue;
                }

                if (!connectionByEdge.ContainsKey(edge.Id))
                {
                    diagnostics?.Add(
                        PlacementFailureReason.RequiredEdgeUnresolved,
                        edge.ToNodeId,
                        null,
                        $"Required {edge.Role} edge {edge.Id} {edge.FromNodeId}->{edge.ToNodeId} has no physical doorway connection.");
                    return false;
                }
            }

            int startNodeId = graph.MainPathNodeIds.Count > 0 ? graph.MainPathNodeIds[0] : graph.Nodes[0].Id;
            int reached = CountReachable(startNodeId, adjacency);
            if (reached != graph.Nodes.Count)
            {
                diagnostics?.Add(
                    PlacementFailureReason.LayoutDisconnected,
                    startNodeId,
                    null,
                    $"Only {reached} of {graph.Nodes.Count} nodes are reachable from the start.");
                return false;
            }

            return true;
        }

        private static Dictionary<int, PlacedTile> BuildPlacedMap(ResolvedFacilityLayout layout)
        {
            var placedByNode = new Dictionary<int, PlacedTile>();
            for (int i = 0; i < layout.Tiles.Count; i++)
            {
                placedByNode[layout.Tiles[i].NodeId] = layout.Tiles[i];
            }

            return placedByNode;
        }

        private static Dictionary<int, PlacedDoorwayConnection> BuildConnectionMap(ResolvedFacilityLayout layout)
        {
            var connectionByEdge = new Dictionary<int, PlacedDoorwayConnection>();
            for (int i = 0; i < layout.Connections.Count; i++)
            {
                connectionByEdge[layout.Connections[i].EdgeId] = layout.Connections[i];
            }

            return connectionByEdge;
        }

        private static bool DoorwayPairAligned(
            IReadOnlyDictionary<int, PlacedTile> placedByNode,
            PlacedDoorwayConnection connection,
            float positionTolerance,
            float oppositeForwardDotThreshold,
            out string detail)
        {
            PlacedTile fromTile = placedByNode[connection.FromNodeId];
            PlacedTile toTile = placedByNode[connection.ToNodeId];
            Doorway[] fromDoorways = fromTile.Definition.TilePrefab.GetDoorways();
            Doorway[] toDoorways = toTile.Definition.TilePrefab.GetDoorways();

            if (connection.FromDoorwayIndex < 0 || connection.FromDoorwayIndex >= fromDoorways.Length ||
                connection.ToDoorwayIndex < 0 || connection.ToDoorwayIndex >= toDoorways.Length)
            {
                detail = $"Connection {connection.EdgeId} has invalid doorway indices.";
                return false;
            }

            Doorway fromDoorway = fromDoorways[connection.FromDoorwayIndex];
            Doorway toDoorway = toDoorways[connection.ToDoorwayIndex];
            Vector3 fromPosition = fromTile.DoorwayPosition(fromDoorway);
            Vector3 toPosition = toTile.DoorwayPosition(toDoorway);
            float distance = Vector3.Distance(fromPosition, toPosition);
            if (distance > positionTolerance)
            {
                detail = $"Connection {connection.EdgeId} doorway positions differ by {distance:0.###}m.";
                return false;
            }

            Vector3 fromForward = fromTile.DoorwayForward(fromDoorway);
            Vector3 toForward = toTile.DoorwayForward(toDoorway);
            float dot = Vector3.Dot(fromForward, toForward);
            if (dot > oppositeForwardDotThreshold)
            {
                detail = $"Connection {connection.EdgeId} doorway forwards are not opposite enough, dot={dot:0.###}.";
                return false;
            }

            detail = string.Empty;
            return true;
        }

        private static int CountReachable(int startNodeId, IReadOnlyDictionary<int, List<int>> adjacency)
        {
            var visited = new HashSet<int>();
            var stack = new Stack<int>();
            stack.Push(startNodeId);
            while (stack.Count > 0)
            {
                int current = stack.Pop();
                if (!visited.Add(current))
                {
                    continue;
                }

                if (!adjacency.TryGetValue(current, out List<int> neighbors))
                {
                    continue;
                }

                for (int i = 0; i < neighbors.Count; i++)
                {
                    stack.Push(neighbors[i]);
                }
            }

            return visited.Count;
        }
    }
}
