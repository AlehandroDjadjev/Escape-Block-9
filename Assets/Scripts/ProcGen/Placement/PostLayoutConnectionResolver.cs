using System;
using System.Collections.Generic;
using System.Text;
using EscapeBlock9.ProcGen.Authoring;
using EscapeBlock9.ProcGen.Data;
using EscapeBlock9.ProcGen.Planning;
using UnityEngine;

namespace EscapeBlock9.ProcGen.Placement
{
    public enum DoorwayResolutionKind
    {
        Connected,
        Blocked,
        FireExit,
        Portal
    }

    [Serializable]
    public struct ResolvedDoorwayMetadata
    {
        public int NodeId;
        public int DoorwayIndex;
        public string ConnectorId;
        public string SocketName;
        public ConnectorKind ConnectorKind;
        public FacilityGraphEdgeRole EdgeRole;
        public DoorwayResolutionKind ResolutionKind;
        public bool IsLocked;
        public Vector3 WorldPosition;
        public Vector3 WorldForward;
    }

    [Serializable]
    public struct FireExitRuntimeMetadata
    {
        public int EdgeId;
        public int NodeId;
        public int DoorwayIndex;
        public int MainPathDistanceFromStart;
        public bool SuppressedNearStart;
        public Vector3 WorldPosition;
    }

    [Serializable]
    public struct PortalPairMetadata
    {
        public int EdgeId;
        public int FromNodeId;
        public int FromDoorwayIndex;
        public int ToNodeId;
        public int ToDoorwayIndex;
        public bool VisualsEnabled;
        public bool HasResolvedDoorways;
        public Vector3 FromWorldPosition;
        public Vector3 ToWorldPosition;
    }

    public sealed class PostLayoutConnectionResolverOptions
    {
        public bool EnablePortalVisuals { get; set; }
        public bool EnableLockedDoorMetadata { get; set; } = true;
        public float BranchLockChance { get; set; } = 0.2f;
        public int LockSeedOffset { get; set; } = 4099;
        public int MinimumFireExitDistanceFromStart { get; set; } = 2;
    }

    public sealed class PostLayoutConnectionResolution
    {
        public PostLayoutConnectionResolution(
            IReadOnlyList<ResolvedDoorwayMetadata> doorways,
            IReadOnlyList<FireExitRuntimeMetadata> fireExits,
            IReadOnlyList<PortalPairMetadata> portalPairs)
        {
            Doorways = doorways;
            FireExits = fireExits;
            PortalPairs = portalPairs;
        }

        public IReadOnlyList<ResolvedDoorwayMetadata> Doorways { get; }
        public IReadOnlyList<FireExitRuntimeMetadata> FireExits { get; }
        public IReadOnlyList<PortalPairMetadata> PortalPairs { get; }

        public string ToDebugString()
        {
            int connected = 0;
            int blocked = 0;
            int fireExit = 0;
            int portal = 0;
            int locked = 0;
            for (int i = 0; i < Doorways.Count; i++)
            {
                ResolvedDoorwayMetadata doorway = Doorways[i];
                switch (doorway.ResolutionKind)
                {
                    case DoorwayResolutionKind.Blocked:
                        blocked++;
                        break;
                    case DoorwayResolutionKind.FireExit:
                        fireExit++;
                        break;
                    case DoorwayResolutionKind.Portal:
                        portal++;
                        break;
                    default:
                        connected++;
                        break;
                }

                if (doorway.IsLocked)
                {
                    locked++;
                }
            }

            var builder = new StringBuilder();
            builder.AppendLine("Post-layout connection resolution:");
            builder.AppendLine($"  doorways={Doorways.Count} connected={connected} blocked={blocked} fireExit={fireExit} portal={portal} locked={locked}");
            builder.AppendLine($"  fireExitMetadata={FireExits.Count} portalPairs={PortalPairs.Count}");
            return builder.ToString();
        }
    }

    [DisallowMultipleComponent]
    public sealed class PostLayoutConnectionMetadata : MonoBehaviour
    {
        [SerializeField] private List<ResolvedDoorwayMetadata> doorways = new List<ResolvedDoorwayMetadata>();
        [SerializeField] private List<FireExitRuntimeMetadata> fireExits = new List<FireExitRuntimeMetadata>();
        [SerializeField] private List<PortalPairMetadata> portalPairs = new List<PortalPairMetadata>();

        public IReadOnlyList<ResolvedDoorwayMetadata> Doorways => doorways;
        public IReadOnlyList<FireExitRuntimeMetadata> FireExits => fireExits;
        public IReadOnlyList<PortalPairMetadata> PortalPairs => portalPairs;

        public void Apply(PostLayoutConnectionResolution resolution)
        {
            doorways.Clear();
            fireExits.Clear();
            portalPairs.Clear();
            if (resolution == null)
            {
                return;
            }

            doorways.AddRange(resolution.Doorways);
            fireExits.AddRange(resolution.FireExits);
            portalPairs.AddRange(resolution.PortalPairs);
        }
    }

    [ExecuteAlways]
    [RequireComponent(typeof(PostLayoutConnectionMetadata))]
    public sealed class FacilityConnectionDebugOverlay : MonoBehaviour
    {
        [SerializeField] private bool drawConnectedDoorways = true;
        [SerializeField] private bool drawBlockedDoorways = true;
        [SerializeField] private bool drawFireExits = true;
        [SerializeField] private bool drawPortalPairs = true;
        [SerializeField] private float markerSize = 0.15f;
        [SerializeField] private float forwardLength = 0.9f;
        [SerializeField] private Color connectedColor = new Color(0.2f, 0.85f, 0.35f, 1f);
        [SerializeField] private Color blockedColor = new Color(0.9f, 0.2f, 0.2f, 1f);
        [SerializeField] private Color fireExitColor = new Color(1f, 0.5f, 0.12f, 1f);
        [SerializeField] private Color portalColor = new Color(0.7f, 0.35f, 1f, 1f);

        private PostLayoutConnectionMetadata metadata;

        private void OnValidate()
        {
            markerSize = Mathf.Max(0.02f, markerSize);
            forwardLength = Mathf.Max(0.1f, forwardLength);
            metadata = GetComponent<PostLayoutConnectionMetadata>();
        }

        private void OnDrawGizmos()
        {
            if (metadata == null)
            {
                metadata = GetComponent<PostLayoutConnectionMetadata>();
            }

            if (metadata == null)
            {
                return;
            }

            if (drawConnectedDoorways || drawBlockedDoorways || drawFireExits)
            {
                DrawDoorways(metadata.Doorways);
            }

            if (drawPortalPairs)
            {
                DrawPortalPairs(metadata.PortalPairs);
            }
        }

        private void DrawDoorways(IReadOnlyList<ResolvedDoorwayMetadata> doorways)
        {
            for (int i = 0; i < doorways.Count; i++)
            {
                ResolvedDoorwayMetadata doorway = doorways[i];
                if (doorway.ResolutionKind == DoorwayResolutionKind.Blocked)
                {
                    if (!drawBlockedDoorways)
                    {
                        continue;
                    }

                    Gizmos.color = blockedColor;
                    Gizmos.DrawCube(doorway.WorldPosition, Vector3.one * markerSize);
                    continue;
                }

                if (doorway.ResolutionKind == DoorwayResolutionKind.FireExit)
                {
                    if (!drawFireExits)
                    {
                        continue;
                    }

                    Gizmos.color = fireExitColor;
                    Gizmos.DrawSphere(doorway.WorldPosition, markerSize * 0.65f);
                    DrawDirection(doorway.WorldPosition, doorway.WorldForward, fireExitColor);
                    continue;
                }

                if (doorway.ResolutionKind == DoorwayResolutionKind.Portal)
                {
                    if (!drawPortalPairs)
                    {
                        continue;
                    }

                    Gizmos.color = portalColor;
                    Gizmos.DrawSphere(doorway.WorldPosition, markerSize * 0.65f);
                    DrawDirection(doorway.WorldPosition, doorway.WorldForward, portalColor);
                    continue;
                }

                if (!drawConnectedDoorways)
                {
                    continue;
                }

                Gizmos.color = connectedColor;
                Gizmos.DrawSphere(doorway.WorldPosition, markerSize * 0.5f);
                DrawDirection(doorway.WorldPosition, doorway.WorldForward, connectedColor);
            }
        }

        private void DrawPortalPairs(IReadOnlyList<PortalPairMetadata> portalPairs)
        {
            for (int i = 0; i < portalPairs.Count; i++)
            {
                PortalPairMetadata pair = portalPairs[i];
                if (!pair.HasResolvedDoorways || !pair.VisualsEnabled)
                {
                    continue;
                }

                Gizmos.color = portalColor;
                Gizmos.DrawLine(pair.FromWorldPosition, pair.ToWorldPosition);
                Gizmos.DrawSphere(pair.FromWorldPosition, markerSize * 0.35f);
                Gizmos.DrawSphere(pair.ToWorldPosition, markerSize * 0.35f);
            }
        }

        private void DrawDirection(Vector3 origin, Vector3 forward, Color color)
        {
            Vector3 direction = forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
            Vector3 end = origin + direction * forwardLength;
            Gizmos.color = color;
            Gizmos.DrawLine(origin, end);
        }
    }

    public sealed class PostLayoutConnectionResolver
    {
        private readonly PostLayoutConnectionResolverOptions options;

        public PostLayoutConnectionResolver(PostLayoutConnectionResolverOptions options = null)
        {
            this.options = options ?? new PostLayoutConnectionResolverOptions();
        }

        public PostLayoutConnectionResolution Resolve(
            ResolvedFacilityLayout layout,
            FacilityGraph graph,
            IReadOnlyDictionary<int, Tile> instantiatedTilesByNode = null)
        {
            if (layout == null)
            {
                throw new ArgumentNullException(nameof(layout));
            }

            if (graph == null)
            {
                throw new ArgumentNullException(nameof(graph));
            }

            var edgeById = new Dictionary<int, FacilityGraphEdge>();
            for (int i = 0; i < graph.Edges.Count; i++)
            {
                FacilityGraphEdge edge = graph.Edges[i];
                edgeById[edge.Id] = edge;
            }

            var mainPathIndexByNodeId = new Dictionary<int, int>();
            for (int i = 0; i < graph.MainPathNodeIds.Count; i++)
            {
                mainPathIndexByNodeId[graph.MainPathNodeIds[i]] = i;
            }

            var usedByDoorway = new Dictionary<string, ConnectionEndpoint>();
            for (int i = 0; i < layout.Connections.Count; i++)
            {
                PlacedDoorwayConnection connection = layout.Connections[i];
                FacilityGraphEdgeRole role = edgeById.TryGetValue(connection.EdgeId, out FacilityGraphEdge edge)
                    ? edge.Role
                    : FacilityGraphEdgeRole.MainPath;
                usedByDoorway[DoorwayKey(connection.FromNodeId, connection.FromDoorwayIndex)] =
                    new ConnectionEndpoint(connection.EdgeId, role, connection.FromNodeId, connection.FromDoorwayIndex);
                usedByDoorway[DoorwayKey(connection.ToNodeId, connection.ToDoorwayIndex)] =
                    new ConnectionEndpoint(connection.EdgeId, role, connection.ToNodeId, connection.ToDoorwayIndex);
            }

            var doorwayEntries = new List<ResolvedDoorwayMetadata>();
            var doorwayIndexByKey = new Dictionary<string, int>();
            var fireExitEntries = new List<FireExitRuntimeMetadata>();
            var portalPairs = new List<PortalPairMetadata>();
            var placedByNode = BuildPlacedByNode(layout);
            var random = new System.Random(layout.Seed + options.LockSeedOffset);

            for (int i = 0; i < layout.Tiles.Count; i++)
            {
                PlacedTile placedTile = layout.Tiles[i];
                if (!TryGetDoorwaySet(placedTile, instantiatedTilesByNode, out Doorway[] doorways))
                {
                    continue;
                }

                for (int doorwayIndex = 0; doorwayIndex < doorways.Length; doorwayIndex++)
                {
                    Doorway doorway = doorways[doorwayIndex];
                    if (doorway == null)
                    {
                        continue;
                    }

                    string key = DoorwayKey(placedTile.NodeId, doorwayIndex);
                    bool isUsed = usedByDoorway.TryGetValue(key, out ConnectionEndpoint endpoint);
                    bool isFireExit = isUsed && (endpoint.EdgeRole == FacilityGraphEdgeRole.FireExit || doorway.ConnectorKind == ConnectorKind.FireExit);
                    bool isLocked = isUsed && ShouldMarkLocked(endpoint.EdgeRole, random);
                    Vector3 worldPosition = placedTile.DoorwayPosition(doorway);
                    Vector3 worldForward = placedTile.DoorwayForward(doorway);

                    if (isUsed)
                    {
                        ToggleDoorwayVisual(doorway, useConnector: true);
                        doorwayEntries.Add(new ResolvedDoorwayMetadata
                        {
                            NodeId = placedTile.NodeId,
                            DoorwayIndex = doorwayIndex,
                            ConnectorId = NormalizedConnectorId(doorway),
                            SocketName = doorway.SocketName,
                            ConnectorKind = doorway.ConnectorKind,
                            EdgeRole = endpoint.EdgeRole,
                            ResolutionKind = DoorwayResolutionKind.Connected,
                            IsLocked = isLocked,
                            WorldPosition = worldPosition,
                            WorldForward = worldForward
                        });

                        if (isFireExit)
                        {
                            int distance = mainPathIndexByNodeId.TryGetValue(placedTile.NodeId, out int index) ? index : int.MaxValue;
                            bool suppressedNearStart = distance < options.MinimumFireExitDistanceFromStart;
                            fireExitEntries.Add(new FireExitRuntimeMetadata
                            {
                                EdgeId = endpoint.EdgeId,
                                NodeId = placedTile.NodeId,
                                DoorwayIndex = doorwayIndex,
                                MainPathDistanceFromStart = distance,
                                SuppressedNearStart = suppressedNearStart,
                                WorldPosition = worldPosition
                            });
                        }
                    }
                    else
                    {
                        ToggleDoorwayVisual(doorway, useConnector: false);
                        doorwayEntries.Add(new ResolvedDoorwayMetadata
                        {
                            NodeId = placedTile.NodeId,
                            DoorwayIndex = doorwayIndex,
                            ConnectorId = NormalizedConnectorId(doorway),
                            SocketName = doorway.SocketName,
                            ConnectorKind = doorway.ConnectorKind,
                            EdgeRole = FacilityGraphEdgeRole.MainPath,
                            ResolutionKind = DoorwayResolutionKind.Blocked,
                            IsLocked = false,
                            WorldPosition = worldPosition,
                            WorldForward = worldForward
                        });
                    }

                    doorwayIndexByKey[key] = doorwayEntries.Count - 1;
                }
            }

            for (int i = 0; i < graph.Edges.Count; i++)
            {
                FacilityGraphEdge edge = graph.Edges[i];
                if (edge.Role != FacilityGraphEdgeRole.Portal)
                {
                    continue;
                }

                bool fromFound = TryFindPortalDoorway(edge.FromNodeId, placedByNode, instantiatedTilesByNode, usedByDoorway, out int fromDoorwayIndex);
                bool toFound = TryFindPortalDoorway(edge.ToNodeId, placedByNode, instantiatedTilesByNode, usedByDoorway, out int toDoorwayIndex);
                bool hasDoorways = fromFound && toFound;
                bool visualsEnabled = options.EnablePortalVisuals && hasDoorways;

                Vector3 fromPosition = Vector3.zero;
                Vector3 toPosition = Vector3.zero;

                if (hasDoorways)
                {
                    if (TryGetDoorwayPosition(edge.FromNodeId, fromDoorwayIndex, placedByNode, instantiatedTilesByNode, out fromPosition) &&
                        TryGetDoorwayPosition(edge.ToNodeId, toDoorwayIndex, placedByNode, instantiatedTilesByNode, out toPosition))
                    {
                        if (visualsEnabled)
                        {
                            MarkDoorwayAsPortal(edge.FromNodeId, fromDoorwayIndex, doorwayIndexByKey, doorwayEntries);
                            MarkDoorwayAsPortal(edge.ToNodeId, toDoorwayIndex, doorwayIndexByKey, doorwayEntries);
                            ToggleDoorwayVisual(FindDoorway(edge.FromNodeId, fromDoorwayIndex, instantiatedTilesByNode, placedByNode), useConnector: true);
                            ToggleDoorwayVisual(FindDoorway(edge.ToNodeId, toDoorwayIndex, instantiatedTilesByNode, placedByNode), useConnector: true);
                        }
                    }
                }

                portalPairs.Add(new PortalPairMetadata
                {
                    EdgeId = edge.Id,
                    FromNodeId = edge.FromNodeId,
                    FromDoorwayIndex = hasDoorways ? fromDoorwayIndex : -1,
                    ToNodeId = edge.ToNodeId,
                    ToDoorwayIndex = hasDoorways ? toDoorwayIndex : -1,
                    VisualsEnabled = visualsEnabled,
                    HasResolvedDoorways = hasDoorways,
                    FromWorldPosition = fromPosition,
                    ToWorldPosition = toPosition
                });
            }

            return new PostLayoutConnectionResolution(doorwayEntries, fireExitEntries, portalPairs);
        }

        private bool ShouldMarkLocked(FacilityGraphEdgeRole edgeRole, System.Random random)
        {
            if (!options.EnableLockedDoorMetadata)
            {
                return false;
            }

            if (edgeRole != FacilityGraphEdgeRole.Branch && edgeRole != FacilityGraphEdgeRole.DeadEnd)
            {
                return false;
            }

            float roll = (float)random.NextDouble();
            float chance = Mathf.Clamp01(options.BranchLockChance);
            return roll <= chance;
        }

        private static Dictionary<int, PlacedTile> BuildPlacedByNode(ResolvedFacilityLayout layout)
        {
            var map = new Dictionary<int, PlacedTile>();
            for (int i = 0; i < layout.Tiles.Count; i++)
            {
                PlacedTile tile = layout.Tiles[i];
                map[tile.NodeId] = tile;
            }

            return map;
        }

        private static string DoorwayKey(int nodeId, int doorwayIndex)
        {
            return $"{nodeId}:{doorwayIndex}";
        }

        private static string NormalizedConnectorId(Doorway doorway)
        {
            if (doorway == null)
            {
                return string.Empty;
            }

            return string.IsNullOrWhiteSpace(doorway.ConnectorId) ? doorway.name : doorway.ConnectorId.Trim();
        }

        private static bool TryGetDoorwaySet(
            PlacedTile placedTile,
            IReadOnlyDictionary<int, Tile> instantiatedTilesByNode,
            out Doorway[] doorways)
        {
            if (instantiatedTilesByNode != null &&
                instantiatedTilesByNode.TryGetValue(placedTile.NodeId, out Tile instanceTile) &&
                instanceTile != null)
            {
                doorways = instanceTile.GetDoorways();
                return doorways != null && doorways.Length > 0;
            }

            Tile prefabTile = placedTile.Definition != null ? placedTile.Definition.TilePrefab : null;
            if (prefabTile == null)
            {
                doorways = Array.Empty<Doorway>();
                return false;
            }

            doorways = prefabTile.GetDoorways();
            return doorways != null && doorways.Length > 0;
        }

        private static void ToggleDoorwayVisual(Doorway doorway, bool useConnector)
        {
            if (doorway == null || !doorway.gameObject.scene.IsValid())
            {
                return;
            }

            if (IsDoorVisualSuppressed(doorway))
            {
                EnsureVisualActive(doorway.ConnectorObject, false);
                if (useConnector)
                {
                    EnsureVisualActive(doorway.BlockerObject, false);
                    return;
                }
            }

            if (useConnector)
            {
                EnsureVisualActive(doorway.ConnectorObject, true);
                EnsureVisualActive(doorway.BlockerObject, false);
                if (doorway.ConnectorObject == null && doorway.ConnectorPrefab != null)
                {
                    InstantiateVisual(doorway.ConnectorPrefab, doorway.transform, $"{doorway.name}_ConnectorRuntime");
                }
            }
            else
            {
                EnsureVisualActive(doorway.ConnectorObject, false);
                EnsureVisualActive(doorway.BlockerObject, true);
                if (doorway.BlockerObject == null && doorway.BlockerPrefab != null)
                {
                    InstantiateVisual(doorway.BlockerPrefab, doorway.transform, $"{doorway.name}_BlockerRuntime");
                }
            }
        }

        private static void EnsureVisualActive(GameObject target, bool active)
        {
            if (target != null && target.activeSelf != active)
            {
                target.SetActive(active);
            }
        }

        private static void InstantiateVisual(GameObject prefab, Transform parent, string runtimeName)
        {
            GameObject instance = UnityEngine.Object.Instantiate(prefab, parent);
            instance.name = runtimeName;
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;
            instance.SetActive(true);
        }

        private static bool IsDoorVisualSuppressed(Doorway doorway)
        {
            return doorway.ConnectorKind == ConnectorKind.Door || doorway.ConnectorKind == ConnectorKind.FireExit;
        }

        private static bool TryFindPortalDoorway(
            int nodeId,
            IReadOnlyDictionary<int, PlacedTile> placedByNode,
            IReadOnlyDictionary<int, Tile> instantiatedTilesByNode,
            IReadOnlyDictionary<string, ConnectionEndpoint> usedDoorways,
            out int doorwayIndex)
        {
            doorwayIndex = -1;
            if (!placedByNode.TryGetValue(nodeId, out PlacedTile placed))
            {
                return false;
            }

            if (!TryGetDoorwaySet(placed, instantiatedTilesByNode, out Doorway[] doorways))
            {
                return false;
            }

            for (int i = 0; i < doorways.Length; i++)
            {
                string key = DoorwayKey(nodeId, i);
                if (usedDoorways.ContainsKey(key))
                {
                    continue;
                }

                if (doorways[i] != null && doorways[i].ConnectorKind == ConnectorKind.Portal)
                {
                    doorwayIndex = i;
                    return true;
                }
            }

            for (int i = 0; i < doorways.Length; i++)
            {
                string key = DoorwayKey(nodeId, i);
                if (usedDoorways.ContainsKey(key))
                {
                    continue;
                }

                if (doorways[i] != null)
                {
                    doorwayIndex = i;
                    return true;
                }
            }

            return false;
        }

        private static bool TryGetDoorwayPosition(
            int nodeId,
            int doorwayIndex,
            IReadOnlyDictionary<int, PlacedTile> placedByNode,
            IReadOnlyDictionary<int, Tile> instantiatedTilesByNode,
            out Vector3 position)
        {
            position = Vector3.zero;
            if (!placedByNode.TryGetValue(nodeId, out PlacedTile placed))
            {
                return false;
            }

            Doorway doorway = FindDoorway(nodeId, doorwayIndex, instantiatedTilesByNode, placedByNode);
            if (doorway == null)
            {
                return false;
            }

            position = placed.DoorwayPosition(doorway);
            return true;
        }

        private static Doorway FindDoorway(
            int nodeId,
            int doorwayIndex,
            IReadOnlyDictionary<int, Tile> instantiatedTilesByNode,
            IReadOnlyDictionary<int, PlacedTile> placedByNode)
        {
            if (instantiatedTilesByNode != null &&
                instantiatedTilesByNode.TryGetValue(nodeId, out Tile instanceTile) &&
                instanceTile != null)
            {
                Doorway[] doorways = instanceTile.GetDoorways();
                if (doorwayIndex >= 0 && doorwayIndex < doorways.Length)
                {
                    return doorways[doorwayIndex];
                }
            }

            if (!placedByNode.TryGetValue(nodeId, out PlacedTile placed) || placed.Definition == null)
            {
                return null;
            }

            Tile prefabTile = placed.Definition.TilePrefab;
            if (prefabTile == null)
            {
                return null;
            }

            Doorway[] prefabDoorways = prefabTile.GetDoorways();
            if (doorwayIndex < 0 || doorwayIndex >= prefabDoorways.Length)
            {
                return null;
            }

            return prefabDoorways[doorwayIndex];
        }

        private static void MarkDoorwayAsPortal(
            int nodeId,
            int doorwayIndex,
            IReadOnlyDictionary<string, int> doorwayIndexByKey,
            IList<ResolvedDoorwayMetadata> entries)
        {
            string key = DoorwayKey(nodeId, doorwayIndex);
            if (!doorwayIndexByKey.TryGetValue(key, out int index) || index < 0 || index >= entries.Count)
            {
                return;
            }

            ResolvedDoorwayMetadata value = entries[index];
            value.ResolutionKind = DoorwayResolutionKind.Portal;
            value.EdgeRole = FacilityGraphEdgeRole.Portal;
            entries[index] = value;
        }

        private readonly struct ConnectionEndpoint
        {
            public ConnectionEndpoint(int edgeId, FacilityGraphEdgeRole edgeRole, int nodeId, int doorwayIndex)
            {
                EdgeId = edgeId;
                EdgeRole = edgeRole;
                NodeId = nodeId;
                DoorwayIndex = doorwayIndex;
            }

            public int EdgeId { get; }
            public FacilityGraphEdgeRole EdgeRole { get; }
            public int NodeId { get; }
            public int DoorwayIndex { get; }
        }
    }
}
