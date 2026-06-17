using System.Collections.Generic;
using EscapeBlock9.ProcGen.Authoring;
using EscapeBlock9.ProcGen.Placement;
using EscapeBlock9.ProcGen.Planning;
using UnityEngine;

namespace EscapeBlock9.ProcGen.Navigation
{
    public static class RuntimeNavLinkPlanner
    {
        public static List<RuntimeNavLinkRequest> Build(
            FacilityGraph graph,
            ResolvedFacilityLayout layout,
            IReadOnlyDictionary<int, Tile> instantiatedTilesByNode,
            PostLayoutConnectionMetadata connectionMetadata,
            bool enablePortalLinks)
        {
            var requests = new List<RuntimeNavLinkRequest>();
            if (graph == null || layout == null || instantiatedTilesByNode == null)
            {
                return requests;
            }

            var edgeById = new Dictionary<int, FacilityGraphEdge>();
            for (int i = 0; i < graph.Edges.Count; i++)
            {
                edgeById[graph.Edges[i].Id] = graph.Edges[i];
            }

            for (int i = 0; i < layout.Connections.Count; i++)
            {
                PlacedDoorwayConnection connection = layout.Connections[i];
                if (!edgeById.TryGetValue(connection.EdgeId, out FacilityGraphEdge edge))
                {
                    continue;
                }

                if (edge.Role != FacilityGraphEdgeRole.Stair && edge.Role != FacilityGraphEdgeRole.Portal)
                {
                    continue;
                }

                if (edge.Role == FacilityGraphEdgeRole.Portal && !enablePortalLinks)
                {
                    continue;
                }

                if (!TryResolveDoorwayPosition(layout, instantiatedTilesByNode, connection.FromNodeId, connection.FromDoorwayIndex, out Vector3 start))
                {
                    continue;
                }

                if (!TryResolveDoorwayPosition(layout, instantiatedTilesByNode, connection.ToNodeId, connection.ToDoorwayIndex, out Vector3 end))
                {
                    continue;
                }

                requests.Add(new RuntimeNavLinkRequest
                {
                    Start = start + Vector3.up * 0.15f,
                    End = end + Vector3.up * 0.15f,
                    Width = 1.1f,
                    Bidirectional = edge.Bidirectional,
                    Reason = edge.Role.ToString()
                });
            }

            // Optional authored special links for catwalk gaps or custom transitions.
            foreach (KeyValuePair<int, Tile> pair in instantiatedTilesByNode)
            {
                if (pair.Value == null)
                {
                    continue;
                }

                SpawnMarker[] markers = pair.Value.GetSpawnMarkers();
                for (int i = 0; i < markers.Length; i++)
                {
                    SpawnMarker first = markers[i];
                    if (first == null || !HasSpecialLinkTag(first))
                    {
                        continue;
                    }

                    for (int j = i + 1; j < markers.Length; j++)
                    {
                        SpawnMarker second = markers[j];
                        if (second == null || !HasSpecialLinkTag(second))
                        {
                            continue;
                        }

                        if (Vector3.Distance(first.transform.position, second.transform.position) < 1.5f)
                        {
                            continue;
                        }

                        requests.Add(new RuntimeNavLinkRequest
                        {
                            Start = first.transform.position + Vector3.up * 0.15f,
                            End = second.transform.position + Vector3.up * 0.15f,
                            Width = 1.1f,
                            Bidirectional = true,
                            Reason = "SpecialTransition"
                        });
                    }
                }
            }

            if (enablePortalLinks && connectionMetadata != null)
            {
                for (int i = 0; i < connectionMetadata.PortalPairs.Count; i++)
                {
                    PortalPairMetadata portal = connectionMetadata.PortalPairs[i];
                    if (!portal.HasResolvedDoorways)
                    {
                        continue;
                    }

                    requests.Add(new RuntimeNavLinkRequest
                    {
                        Start = portal.FromWorldPosition + Vector3.up * 0.15f,
                        End = portal.ToWorldPosition + Vector3.up * 0.15f,
                        Width = 1.1f,
                        Bidirectional = true,
                        Reason = "PortalPair"
                    });
                }
            }

            return requests;
        }

        private static bool TryResolveDoorwayPosition(
            ResolvedFacilityLayout layout,
            IReadOnlyDictionary<int, Tile> instantiatedTilesByNode,
            int nodeId,
            int doorwayIndex,
            out Vector3 worldPosition)
        {
            worldPosition = Vector3.zero;
            PlacedTile placed = layout.GetTile(nodeId);
            if (placed == null)
            {
                return false;
            }

            if (!instantiatedTilesByNode.TryGetValue(nodeId, out Tile tile) || tile == null)
            {
                return false;
            }

            Doorway[] doorways = tile.GetDoorways();
            if (doorways == null || doorwayIndex < 0 || doorwayIndex >= doorways.Length || doorways[doorwayIndex] == null)
            {
                return false;
            }

            worldPosition = placed.DoorwayPosition(doorways[doorwayIndex]);
            return true;
        }

        private static bool HasSpecialLinkTag(SpawnMarker marker)
        {
            string[] tags = marker.Tags;
            if (tags == null)
            {
                return false;
            }

            for (int i = 0; i < tags.Length; i++)
            {
                string tag = tags[i];
                if (string.IsNullOrWhiteSpace(tag))
                {
                    continue;
                }

                if (tag.Equals("catwalk-gap", System.StringComparison.OrdinalIgnoreCase) ||
                    tag.Equals("special-transition", System.StringComparison.OrdinalIgnoreCase) ||
                    tag.Equals("nav-link", System.StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
