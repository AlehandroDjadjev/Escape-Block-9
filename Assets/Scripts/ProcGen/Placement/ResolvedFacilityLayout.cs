using System.Collections.Generic;
using System.Text;
using EscapeBlock9.ProcGen.Authoring;
using EscapeBlock9.ProcGen.Data;
using UnityEngine;

namespace EscapeBlock9.ProcGen.Placement
{
    public readonly struct PlacedOccupancyBox
    {
        public PlacedOccupancyBox(Bounds bounds)
        {
            Bounds = bounds;
        }

        public Bounds Bounds { get; }
    }

    public sealed class PlacedTile
    {
        private readonly List<PlacedOccupancyBox> occupancyBoxes;

        public PlacedTile(int nodeId, TileDefinition definition, Vector3 position, Quaternion rotation, IReadOnlyList<PlacedOccupancyBox> occupancyBoxes)
        {
            NodeId = nodeId;
            Definition = definition;
            Position = position;
            Rotation = rotation;
            this.occupancyBoxes = new List<PlacedOccupancyBox>(occupancyBoxes);
        }

        public int NodeId { get; }
        public TileDefinition Definition { get; }
        public string ModuleId => Definition != null ? Definition.ModuleId : string.Empty;
        public Vector3 Position { get; }
        public Quaternion Rotation { get; }
        public IReadOnlyList<PlacedOccupancyBox> OccupancyBoxes => occupancyBoxes;

        public Matrix4x4 LocalToWorld => Matrix4x4.TRS(Position, Rotation, Vector3.one);

        public Vector3 DoorwayPosition(Doorway doorway)
        {
            return LocalToWorld.MultiplyPoint3x4(doorway.transform.localPosition);
        }

        public Vector3 DoorwayForward(Doorway doorway)
        {
            return (Rotation * doorway.transform.localRotation * Vector3.forward).normalized;
        }
    }

    public sealed class PlacedDoorwayConnection
    {
        public PlacedDoorwayConnection(int edgeId, int fromNodeId, int fromDoorwayIndex, int toNodeId, int toDoorwayIndex)
        {
            EdgeId = edgeId;
            FromNodeId = fromNodeId;
            FromDoorwayIndex = fromDoorwayIndex;
            ToNodeId = toNodeId;
            ToDoorwayIndex = toDoorwayIndex;
        }

        public int EdgeId { get; }
        public int FromNodeId { get; }
        public int FromDoorwayIndex { get; }
        public int ToNodeId { get; }
        public int ToDoorwayIndex { get; }
    }

    public sealed class ResolvedFacilityLayout
    {
        private readonly List<PlacedTile> tiles;
        private readonly List<PlacedDoorwayConnection> connections;

        public ResolvedFacilityLayout(
            int seed,
            IReadOnlyList<PlacedTile> tiles,
            IReadOnlyList<PlacedDoorwayConnection> connections,
            PlacementFailureDiagnostics diagnostics,
            int placementAttempts = 0)
        {
            Seed = seed;
            this.tiles = new List<PlacedTile>(tiles);
            this.connections = new List<PlacedDoorwayConnection>(connections);
            Diagnostics = diagnostics;
            PlacementAttempts = placementAttempts;
        }

        public int Seed { get; }
        public IReadOnlyList<PlacedTile> Tiles => tiles;
        public IReadOnlyList<PlacedDoorwayConnection> Connections => connections;
        public PlacementFailureDiagnostics Diagnostics { get; }
        public int PlacementAttempts { get; }

        public PlacedTile GetTile(int nodeId)
        {
            for (int i = 0; i < tiles.Count; i++)
            {
                if (tiles[i].NodeId == nodeId)
                {
                    return tiles[i];
                }
            }

            return null;
        }

        public string ToDebugString()
        {
            var builder = new StringBuilder();
            builder.AppendLine($"ResolvedFacilityLayout seed={Seed} tiles={tiles.Count} connections={connections.Count} attempts={PlacementAttempts}");
            for (int i = 0; i < tiles.Count; i++)
            {
                PlacedTile tile = tiles[i];
                Vector3 euler = tile.Rotation.eulerAngles;
                builder.AppendLine($"  node={tile.NodeId} module={tile.ModuleId} pos={Format(tile.Position)} yaw={euler.y:0.###}");
            }

            for (int i = 0; i < connections.Count; i++)
            {
                PlacedDoorwayConnection connection = connections[i];
                builder.AppendLine($"  edge={connection.EdgeId} {connection.FromNodeId}:{connection.FromDoorwayIndex} -> {connection.ToNodeId}:{connection.ToDoorwayIndex}");
            }

            return builder.ToString();
        }

        private static string Format(Vector3 value)
        {
            return $"({value.x:0.###},{value.y:0.###},{value.z:0.###})";
        }
    }
}
