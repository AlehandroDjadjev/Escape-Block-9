using EscapeBlock9.ProcGen.Placement;
using EscapeBlock9.ProcGen.Planning;
using EscapeBlock9.ProcGen.Population;
using UnityEngine;

namespace EscapeBlock9.ProcGen.Debugging
{
    [ExecuteAlways]
    [RequireComponent(typeof(FacilityGenerationDebugData))]
    public sealed class FacilityGenerationDebugOverlay : MonoBehaviour
    {
        [Header("Visibility")]
        [SerializeField] private bool drawModuleBounds = true;
        [SerializeField] private bool drawOccupancyBoxes = true;
        [SerializeField] private bool drawGraphEdges = true;
        [SerializeField] private bool drawConnectors = true;
        [SerializeField] private bool drawSockets = true;
        [SerializeField] private bool drawPopulationMarkers = true;
        [SerializeField] private bool drawBlockedConnectors = true;

        [Header("Style")]
        [SerializeField] private float nodeRadius = 0.14f;
        [SerializeField] private float connectorLength = 0.8f;
        [SerializeField] private Color moduleBoundsColor = new Color(0.2f, 0.8f, 1f, 0.8f);
        [SerializeField] private Color occupancyColor = new Color(1f, 0.72f, 0.22f, 0.25f);
        [SerializeField] private Color mainPathColor = new Color(0.15f, 1f, 0.35f, 1f);
        [SerializeField] private Color branchColor = new Color(0.22f, 0.68f, 1f, 1f);
        [SerializeField] private Color loopColor = new Color(0.95f, 0.15f, 0.95f, 1f);
        [SerializeField] private Color fireExitColor = new Color(1f, 0.5f, 0.12f, 1f);
        [SerializeField] private Color portalColor = new Color(0.75f, 0.35f, 1f, 1f);
        [SerializeField] private Color connectorColor = new Color(0.3f, 1f, 0.6f, 1f);
        [SerializeField] private Color blockedConnectorColor = new Color(1f, 0.2f, 0.2f, 1f);
        [SerializeField] private Color populationUsedColor = new Color(0.2f, 1f, 0.3f, 1f);
        [SerializeField] private Color populationUnusedColor = new Color(1f, 0.2f, 0.2f, 1f);

        private FacilityGenerationDebugData debugData;
        private PostLayoutConnectionMetadata connectionMetadata;
        private FacilityPopulationMetadata populationMetadata;

        private void OnValidate()
        {
            nodeRadius = Mathf.Max(0.02f, nodeRadius);
            connectorLength = Mathf.Max(0.1f, connectorLength);
            CacheComponents();
        }

        private void OnDrawGizmos()
        {
            CacheComponents();
            if (debugData == null)
            {
                return;
            }

            if (drawModuleBounds)
            {
                DrawModuleBounds();
            }

            if (drawOccupancyBoxes)
            {
                DrawOccupancy();
            }

            if (drawGraphEdges)
            {
                DrawGraphEdges();
            }

            if (drawConnectors || drawBlockedConnectors || drawSockets)
            {
                DrawConnectorsAndSockets();
            }

            if (drawPopulationMarkers)
            {
                DrawPopulationMarkers();
            }
        }

        private void CacheComponents()
        {
            if (debugData == null)
            {
                debugData = GetComponent<FacilityGenerationDebugData>();
            }

            if (connectionMetadata == null)
            {
                connectionMetadata = GetComponent<PostLayoutConnectionMetadata>();
            }

            if (populationMetadata == null)
            {
                populationMetadata = GetComponent<FacilityPopulationMetadata>();
            }
        }

        private void DrawModuleBounds()
        {
            for (int i = 0; i < debugData.Nodes.Count; i++)
            {
                DebugNodeRecord node = debugData.Nodes[i];
                Color color = node.IsMainPath ? mainPathColor : moduleBoundsColor;
                if (node.Role == FacilityGraphNodeRole.FireExit)
                {
                    color = fireExitColor;
                }
                else if (node.Role == FacilityGraphNodeRole.Portal)
                {
                    color = portalColor;
                }

                Gizmos.color = color;
                Gizmos.DrawWireCube(node.WorldPosition, node.Size);
                Gizmos.DrawSphere(node.WorldPosition, nodeRadius * 0.45f);
            }
        }

        private void DrawOccupancy()
        {
            for (int i = 0; i < debugData.Occupancy.Count; i++)
            {
                DebugOccupancyRecord record = debugData.Occupancy[i];
                Gizmos.color = occupancyColor;
                Gizmos.DrawCube(record.Bounds.center, record.Bounds.size);
                Gizmos.color = new Color(occupancyColor.r, occupancyColor.g, occupancyColor.b, 1f);
                Gizmos.DrawWireCube(record.Bounds.center, record.Bounds.size);
            }
        }

        private void DrawGraphEdges()
        {
            for (int i = 0; i < debugData.Edges.Count; i++)
            {
                DebugEdgeRecord edge = debugData.Edges[i];
                if (!TryGetNodePosition(edge.FromNodeId, out Vector3 from) ||
                    !TryGetNodePosition(edge.ToNodeId, out Vector3 to))
                {
                    continue;
                }

                Color color = Color.white;
                switch (edge.Role)
                {
                    case FacilityGraphEdgeRole.MainPath:
                        color = mainPathColor;
                        break;
                    case FacilityGraphEdgeRole.Branch:
                    case FacilityGraphEdgeRole.DeadEnd:
                        color = branchColor;
                        break;
                    case FacilityGraphEdgeRole.Loop:
                        color = loopColor;
                        break;
                    case FacilityGraphEdgeRole.FireExit:
                        color = fireExitColor;
                        break;
                    case FacilityGraphEdgeRole.Portal:
                        color = portalColor;
                        break;
                }

                if (!edge.Connected)
                {
                    color = new Color(color.r, color.g, color.b, 0.35f);
                }

                Gizmos.color = color;
                Gizmos.DrawLine(from, to);
            }
        }

        private void DrawConnectorsAndSockets()
        {
            if (connectionMetadata == null)
            {
                return;
            }

            for (int i = 0; i < connectionMetadata.Doorways.Count; i++)
            {
                ResolvedDoorwayMetadata doorway = connectionMetadata.Doorways[i];
                bool blocked = doorway.ResolutionKind == DoorwayResolutionKind.Blocked;
                if (blocked && !drawBlockedConnectors)
                {
                    continue;
                }

                if (!blocked && !drawConnectors)
                {
                    continue;
                }

                Color color = blocked ? blockedConnectorColor : connectorColor;
                if (doorway.ResolutionKind == DoorwayResolutionKind.FireExit)
                {
                    color = fireExitColor;
                }
                else if (doorway.ResolutionKind == DoorwayResolutionKind.Portal)
                {
                    color = portalColor;
                }

                Vector3 end = doorway.WorldPosition + doorway.WorldForward * connectorLength;
                Gizmos.color = color;
                Gizmos.DrawLine(doorway.WorldPosition, end);
                Gizmos.DrawSphere(doorway.WorldPosition, nodeRadius * 0.35f);

#if UNITY_EDITOR
                if (drawSockets)
                {
                    UnityEditor.Handles.color = color;
                    string socket = string.IsNullOrWhiteSpace(doorway.SocketName) ? "<socket?>" : doorway.SocketName;
                    UnityEditor.Handles.Label(end + Vector3.up * 0.15f, $"{doorway.ConnectorId} | {socket}");
                }
#endif
            }
        }

        private void DrawPopulationMarkers()
        {
            if (populationMetadata == null)
            {
                return;
            }

            for (int i = 0; i < populationMetadata.MarkerUsage.Count; i++)
            {
                PopulationMarkerUsage marker = populationMetadata.MarkerUsage[i];
                bool used = marker.Status == PopulationMarkerStatus.Used;
                Gizmos.color = used ? populationUsedColor : populationUnusedColor;
                Gizmos.DrawSphere(marker.WorldPosition, nodeRadius * 0.4f);
            }
        }

        private bool TryGetNodePosition(int nodeId, out Vector3 position)
        {
            for (int i = 0; i < debugData.Nodes.Count; i++)
            {
                if (debugData.Nodes[i].NodeId == nodeId)
                {
                    position = debugData.Nodes[i].WorldPosition;
                    return true;
                }
            }

            position = Vector3.zero;
            return false;
        }
    }
}
